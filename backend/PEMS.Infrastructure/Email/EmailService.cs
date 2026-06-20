using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// SMTP email sender. When <c>Smtp:Enabled</c> is false (default for dev) it logs
/// the message instead of sending, so auth/OTP flows keep working without a server.
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger        = logger;
    }

    // ── Generic send ──────────────────────────────────────────────────────────

    public async Task SendAsync(string toEmail, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var smtp    = _configuration.GetSection("Smtp");
        var enabled = bool.TryParse(smtp["Enabled"], out var e) && e;

        if (!enabled)
        {
            _logger.LogInformation(
                "[EmailService-DEV] To:{To} Subject:{Subject}\n{Body}", toEmail, subject, htmlBody);
            return;
        }

        var host      = smtp["Host"];
        var port      = int.TryParse(smtp["Port"], out var p) ? p : 587;
        var user      = smtp["User"];
        var password  = smtp["Password"];
        var fromEmail = smtp["FromEmail"] ?? user ?? "no-reply@pems.local";
        var fromName  = smtp["FromName"] ?? "PEMS";
        var enableSsl = !bool.TryParse(smtp["EnableSsl"], out var ssl) || ssl;

        using var message = new MailMessage
        {
            From      = new MailAddress(fromEmail, fromName),
            Subject   = subject,
            Body      = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(host, port) { EnableSsl = enableSsl };
        if (!string.IsNullOrEmpty(user))
            client.Credentials = new NetworkCredential(user, password);

        await client.SendMailAsync(message, cancellationToken);
        _logger.LogInformation("Sent email to {To} (subject: {Subject}).", toEmail, subject);
    }

    // ── Password reset ────────────────────────────────────────────────────────

    public Task SendPasswordResetAsync(string toEmail, string fullName, string code,
        CancellationToken cancellationToken = default)
    {
        const string subject = "PEMS — Mã đặt lại mật khẩu";
        var body =
            $"<p>Xin chào {HE(fullName)},</p>" +
            $"<p>Mã đặt lại mật khẩu của bạn là: <strong style=\"font-size:20px;letter-spacing:4px\">{HE(code)}</strong></p>" +
            "<p>Mã này sẽ hết hạn sau 15 phút. Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.</p>" +
            "<p>— PEMS System</p>";

        return SendAsync(toEmail, subject, body, cancellationToken);
    }

    // ── Visit request OTP ─────────────────────────────────────────────────────

    public Task SendVisitRequestOtpAsync(string toEmail, string fullName, string code,
        CancellationToken cancellationToken = default)
    {
        const string subject = "PEMS — Xác thực đăng ký tham quan";
        var body = $@"
<!DOCTYPE html>
<html lang=""vi"">
<head><meta charset=""UTF-8""></head>
<body style=""font-family:Arial,sans-serif;background:#f4f6f9;margin:0;padding:20px"">
  <div style=""max-width:520px;margin:auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08)"">
    <div style=""background:linear-gradient(135deg,#004c91,#013565);padding:28px 32px"">
      <h1 style=""color:#fff;margin:0;font-size:22px"">PEMS — Campus Visit</h1>
      <p style=""color:#b3c8e8;margin:6px 0 0;font-size:13px"">FPT University</p>
    </div>
    <div style=""padding:32px"">
      <p style=""color:#374151;font-size:15px"">Xin chào <strong>{HE(fullName)}</strong>,</p>
      <p style=""color:#374151;font-size:14px"">
        Bạn đang đăng ký tham quan FPT University. Vui lòng nhập mã xác thực bên dưới để hoàn tất đơn đăng ký.
      </p>
      <div style=""text-align:center;margin:28px 0"">
        <div style=""display:inline-block;background:#f0f7ff;border:2px dashed #004c91;border-radius:12px;padding:16px 40px"">
          <span style=""font-size:36px;font-weight:900;letter-spacing:10px;color:#004c91"">{HE(code)}</span>
        </div>
        <p style=""color:#9ca3af;font-size:12px;margin-top:10px"">Mã có hiệu lực trong <strong>5 phút</strong></p>
      </div>
      <p style=""color:#6b7280;font-size:12px"">
        Nếu bạn không thực hiện thao tác này, vui lòng bỏ qua email này.
      </p>
    </div>
    <div style=""background:#f9fafb;padding:16px 32px;text-align:center"">
      <p style=""color:#9ca3af;font-size:11px;margin:0"">© 2026 PEMS — FPT University. Không trả lời email này.</p>
    </div>
  </div>
</body>
</html>";

        return SendAsync(toEmail, subject, body, cancellationToken);
    }

    // ── Visit request confirmation ────────────────────────────────────────────

    public Task SendVisitorAccountCreatedOrLinkedEmailAsync(
        string toEmail, string contactFullName, string delegationName,
        string requestCode, string visitScope, string plannedTime,
        CancellationToken cancellationToken = default)
    {
        const string subject = "PEMS — Yêu cầu tham quan của bạn đã được ghi nhận";
        string visitScopeDisplay = visitScope == "MULTI_CAMPUS" ? "Liên cơ sở" : "Đơn cơ sở";
        
        var body = $@"
<!DOCTYPE html>
<html lang=""vi"">
<head><meta charset=""UTF-8""></head>
<body style=""font-family:Arial,sans-serif;background:#f4f6f9;margin:0;padding:20px"">
  <div style=""max-width:560px;margin:auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08)"">
    <div style=""background:linear-gradient(135deg,#004c91,#013565);padding:28px 32px"">
      <h1 style=""color:#fff;margin:0;font-size:22px"">PEMS — Campus Visit</h1>
      <p style=""color:#b3c8e8;margin:6px 0 0;font-size:13px"">FPT University</p>
    </div>
    <div style=""padding:32px"">
      <div style=""text-align:center;margin-bottom:24px"">
        <div style=""width:56px;height:56px;background:#d1fae5;border-radius:50%;display:inline-flex;align-items:center;justify-content:center;font-size:28px"">✓</div>
      </div>
      <h2 style=""text-align:center;color:#065f46;font-size:18px;margin:0 0 16px"">Đơn đăng ký đã được ghi nhận!</h2>
      <p style=""color:#374151;font-size:14px"">Xin chào <strong>{{HE(contactFullName)}}</strong>,</p>
      <p style=""color:#374151;font-size:14px"">
        Yêu cầu tham quan của đoàn <strong>{{HE(delegationName)}}</strong> đã được hệ thống PEMS ghi nhận thành công.
      </p>
      
      <div style=""background:#f0f7ff;border-left:4px solid #004c91;border-radius:8px;padding:16px 20px;margin:20px 0"">
        <p style=""margin:0 0 8px;color:#374151;font-size:14px""><strong>Thông tin yêu cầu:</strong></p>
        <ul style=""margin:0;padding-left:20px;color:#374151;font-size:13px;line-height:1.6"">
          <li><strong>Mã yêu cầu:</strong> <span style=""color:#004c91;font-weight:bold"">{{HE(requestCode)}}</span></li>
          <li><strong>Trạng thái:</strong> <span style=""color:#d97706;font-weight:bold"">Chờ duyệt</span></li>
          <li><strong>Phạm vi tham quan:</strong> {{HE(visitScopeDisplay)}}</li>
          <li><strong>Thời gian dự kiến:</strong> {{HE(plannedTime)}}</li>
        </ul>
      </div>

      <p style=""color:#374151;font-size:14px"">
        Email <strong>{{HE(toEmail)}}</strong> đã được sử dụng làm tài khoản VISITOR để theo dõi yêu cầu này.
      </p>
      
      <div style=""background:#fafafa;border:1px solid #e5e7eb;border-radius:8px;padding:14px 18px;margin:16px 0"">
        <p style=""margin:0;color:#374151;font-size:13px"">
          Sử dụng nút <strong>Đăng nhập bằng Google</strong> với tài khoản email này tại cổng VISITOR của PEMS để xem trạng thái xử lý trong những lần sau.
        </p>
        <p style=""margin:8px 0 0;color:#dc2626;font-size:12px;font-style:italic"">
          Lưu ý: Hệ thống không tạo hoặc gửi mật khẩu. Vui lòng sử dụng đăng nhập Google.
        </p>
      </div>
      
      <p style=""color:#6b7280;font-size:12px;margin-top:20px"">
        Trân trọng,<br/>PEMS - FPT University
      </p>
    </div>
    <div style=""background:#f9fafb;padding:16px 32px;text-align:center"">
      <p style=""color:#9ca3af;font-size:11px;margin:0"">© 2026 PEMS — FPT University. Không trả lời email này.</p>
    </div>
  </div>
</body>
</html>";

        return SendAsync(toEmail, subject, body, cancellationToken);
    }

    public Task SendRegistrantConfirmationAsync(
        string toEmail, string registrantFullName, string contactFullName, 
        string contactEmail, string delegationName, string requestCode,
        CancellationToken cancellationToken = default)
    {
        const string subject = "PEMS — Gửi yêu cầu tham quan thành công";
        var body = $@"
<!DOCTYPE html>
<html lang=""vi"">
<head><meta charset=""UTF-8""></head>
<body style=""font-family:Arial,sans-serif;background:#f4f6f9;margin:0;padding:20px"">
  <div style=""max-width:560px;margin:auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08)"">
    <div style=""background:linear-gradient(135deg,#004c91,#013565);padding:28px 32px"">
      <h1 style=""color:#fff;margin:0;font-size:22px"">PEMS — Campus Visit</h1>
      <p style=""color:#b3c8e8;margin:6px 0 0;font-size:13px"">FPT University</p>
    </div>
    <div style=""padding:32px"">
      <p style=""color:#374151;font-size:14px"">Xin chào <strong>{{HE(registrantFullName)}}</strong>,</p>
      <p style=""color:#374151;font-size:14px"">
        Bạn đã gửi yêu cầu tham quan cho đoàn <strong>{{HE(delegationName)}}</strong> thành công với mã yêu cầu là <strong>{{HE(requestCode)}}</strong>.
      </p>
      
      <div style=""background:#f0f7ff;border-left:4px solid #004c91;border-radius:8px;padding:16px 20px;margin:20px 0"">
        <p style=""margin:0 0 8px;color:#374151;font-size:13px""><strong>Thông tin đầu mối liên hệ được ghi nhận:</strong></p>
        <ul style=""margin:0;padding-left:20px;color:#374151;font-size:13px"">
          <li><strong>Họ và tên:</strong> {{HE(contactFullName)}}</li>
          <li><strong>Email:</strong> {{HE(contactEmail)}}</li>
        </ul>
      </div>

      <p style=""color:#374151;font-size:14px"">
        Thông tin tài khoản để theo dõi yêu cầu đã được gửi đến email của đầu mối liên hệ trên.
      </p>
      
      <p style=""color:#6b7280;font-size:12px;margin-top:20px"">
        Trân trọng,<br/>PEMS - FPT University
      </p>
    </div>
    <div style=""background:#f9fafb;padding:16px 32px;text-align:center"">
      <p style=""color:#9ca3af;font-size:11px;margin:0"">© 2026 PEMS — FPT University. Không trả lời email này.</p>
    </div>
  </div>
</body>
</html>";
        return SendAsync(toEmail, subject, body, cancellationToken);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>HTML-encode a value for safe inclusion in email body.</summary>
    private static string HE(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
