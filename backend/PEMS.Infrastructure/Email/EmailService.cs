using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// SMTP email sender.
///
/// Delivery status is TRUTHFUL and environment-aware:
///   • <c>Smtp:Enabled=false</c> in a NON-production environment → <see cref="EmailDeliveryStatus.Skipped"/>
///     (nothing sent; metadata-only log). It is never reported as "sent".
///   • <c>Smtp:Enabled=false</c> or misconfigured in Production → <see cref="EmailDeliveryStatus.Failed"/>
///     (fail-closed: email is a required feature and must not be silently dropped).
///   • provider accepted → <see cref="EmailDeliveryStatus.Sent"/>; provider error → Failed.
///
/// It never logs the OTP, action token, confirmation URL or the message body; the recipient is reduced to
/// its domain in metadata logs.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IHostEnvironment _environment;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IHostEnvironment environment)
    {
        _configuration = configuration;
        _logger        = logger;
        _environment   = environment;
    }

    // ── Generic send ──────────────────────────────────────────────────────────

    public async Task SendAsync(string toEmail, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage { Subject = subject, Body = htmlBody, IsBodyHtml = true };
        message.To.Add(toEmail);
        ThrowIfFailed(await SendCoreAsync(message, cancellationToken));
    }

    /// <summary>Truthful send: returns Sent/Skipped/Failed without throwing on a delivery outcome.</summary>
    public async Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage { Subject = subject, Body = htmlBody, IsBodyHtml = true };
        message.To.Add(toEmail);
        return await SendCoreAsync(message, cancellationToken);
    }

    // ── Rich send: real MIME with attachments + inline (cid) images ─────────────

    public async Task SendAsync(OutboundEmail email, CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage { Subject = email.Subject };
        message.To.Add(email.ToEmail);

        var inline = email.Attachments.Where(a => a.IsInline && !string.IsNullOrWhiteSpace(a.ContentId)).ToList();
        var files  = email.Attachments.Where(a => !a.IsInline || string.IsNullOrWhiteSpace(a.ContentId)).ToList();

        if (email.IsHtml)
        {
            // HTML alternate view carries the inline images as linked resources so <img src="cid:..">
            // resolves in Gmail/Outlook. A MemoryStream per resource — disposed with the MailMessage.
            var htmlView = AlternateView.CreateAlternateViewFromString(
                email.Body ?? string.Empty, null, MediaTypeNames.Text.Html);
            foreach (var img in inline)
            {
                var lr = new LinkedResource(new MemoryStream(img.Content), img.ContentType ?? "application/octet-stream")
                {
                    ContentId = img.ContentId,
                    TransferEncoding = TransferEncoding.Base64,
                };
                lr.ContentType.Name = img.FileName;
                htmlView.LinkedResources.Add(lr);
            }
            message.AlternateViews.Add(htmlView);
        }
        else
        {
            message.Body = email.Body;
            message.IsBodyHtml = false;
        }

        foreach (var f in files)
        {
            var att = new Attachment(new MemoryStream(f.Content), f.FileName, f.ContentType ?? "application/octet-stream");
            message.Attachments.Add(att);
        }

        ThrowIfFailed(await SendCoreAsync(message, cancellationToken));
    }

    /// <summary>
    /// Resolves SMTP config and dispatches the message, returning the TRUTHFUL outcome. Disabled/misconfigured
    /// is Skipped in non-production but Failed (fail-closed) in Production; provider errors are Failed.
    /// </summary>
    private async Task<EmailDeliveryResult> SendCoreAsync(MailMessage message, CancellationToken cancellationToken)
    {
        var smtp    = _configuration.GetSection("Smtp");
        var enabled = bool.TryParse(smtp["Enabled"], out var e) && e;

        var fromEmail = smtp["FromEmail"] ?? smtp["User"] ?? "no-reply@pems.local";
        var fromName  = smtp["FromName"] ?? "PEMS";
        message.From = new MailAddress(fromEmail, fromName);

        var replyToEmail = smtp["ReplyToEmail"];
        var replyToName = smtp["ReplyToName"] ?? "PEMS";
        if (!string.IsNullOrWhiteSpace(replyToEmail))
        {
            message.ReplyToList.Add(new MailAddress(replyToEmail, replyToName));
        }

        var to        = message.To.Count > 0 ? message.To[0].Address : "(none)";
        var host      = smtp["Host"];
        var inlineCount = message.AlternateViews.Count > 0 ? message.AlternateViews[0].LinkedResources.Count : 0;

        // ── SMTP not usable (disabled OR no host) ────────────────────────────────
        if (!enabled || string.IsNullOrWhiteSpace(host))
        {
            var reason = !enabled ? "SMTP_DISABLED" : "SMTP_MISCONFIGURED";

            // Metadata ONLY — the body may carry OTP codes, action tokens or confirmation URLs, so it must
            // never reach the logs; the recipient is reduced to its domain (no address local-part persisted).
            _logger.LogInformation(
                "[EmailService] {Reason} — email NOT sent. To:{ToDomain} Subject:{Subject} Attachments:{Att} Inline:{Inline} Env:{Env}",
                reason, MaskEmail(to), message.Subject, message.Attachments.Count, inlineCount, _environment.EnvironmentName);

            if (_environment.IsProduction())
            {
                // Fail-closed: in Production, email is a required feature and must not be silently dropped.
                _logger.LogError(
                    "[EmailService] {Reason} in Production — failing closed (email is required). Subject:{Subject}",
                    reason, message.Subject);
                return EmailDeliveryResult.Failed(reason, "Email service is not configured.");
            }

            // Non-production: an intentional skip, never "sent".
            return EmailDeliveryResult.Skipped(reason, "SMTP is not enabled in this environment.");
        }

        var port      = int.TryParse(smtp["Port"], out var p) ? p : 587;
        var user      = smtp["User"];
        var password  = smtp["Password"];
        var enableSsl = !bool.TryParse(smtp["EnableSsl"], out var ssl) || ssl;
        var config    = new SmtpConfig(host, port, user, password, enableSsl);

        try
        {
            await DispatchAsync(message, config, cancellationToken);
            _logger.LogInformation(
                "Sent email to {ToDomain} (subject: {Subject}, attachments: {Att}).",
                MaskEmail(to), message.Subject, message.Attachments.Count);
            return EmailDeliveryResult.Sent();
        }
        catch (Exception ex)
        {
            // Log the failure WITHOUT the body/recipient local-part/secret. The SmtpClient exception text is
            // operational (host/status), never a user secret, but the safe message returned stays generic.
            _logger.LogError(ex,
                "[EmailService] SMTP send FAILED. To:{ToDomain} Subject:{Subject}", MaskEmail(to), message.Subject);
            return EmailDeliveryResult.Failed("SMTP_SEND_FAILED", "Email delivery failed.");
        }
    }

    /// <summary>
    /// Dispatches the built message over SMTP. Virtual so tests can simulate provider success/failure
    /// deterministically without any network I/O.
    /// </summary>
    protected virtual async Task DispatchAsync(MailMessage message, SmtpConfig config, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(config.Host, config.Port) { EnableSsl = config.EnableSsl };
        client.UseDefaultCredentials = false;
        if (!string.IsNullOrEmpty(config.User))
            client.Credentials = new NetworkCredential(config.User, config.Password);

        await client.SendMailAsync(message, cancellationToken);
    }

    /// <summary>Void send contract: a hard Failed outcome throws so fire-and-forget callers still observe it.</summary>
    private static void ThrowIfFailed(EmailDeliveryResult result)
    {
        if (result.Status == EmailDeliveryStatus.Failed)
            throw new EmailDeliveryException(result.Code ?? "EMAIL_SEND_FAILED", result.SafeMessage ?? "Email delivery failed.");
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
      <p style=""color:#374151;font-size:14px"">Xin chào <strong>{HE(contactFullName)}</strong>,</p>
      <p style=""color:#374151;font-size:14px"">
        Yêu cầu tham quan của đoàn <strong>{HE(delegationName)}</strong> đã được hệ thống PEMS ghi nhận thành công.
      </p>
      
      <div style=""background:#f0f7ff;border-left:4px solid #004c91;border-radius:8px;padding:16px 20px;margin:20px 0"">
        <p style=""margin:0 0 8px;color:#374151;font-size:14px""><strong>Thông tin yêu cầu:</strong></p>
        <ul style=""margin:0;padding-left:20px;color:#374151;font-size:13px;line-height:1.6"">
          <li><strong>Mã yêu cầu:</strong> <span style=""color:#004c91;font-weight:bold"">{HE(requestCode)}</span></li>
          <li><strong>Trạng thái:</strong> <span style=""color:#d97706;font-weight:bold"">Chờ duyệt</span></li>
          <li><strong>Phạm vi tham quan:</strong> {HE(visitScopeDisplay)}</li>
          <li><strong>Thời gian dự kiến:</strong> {HE(plannedTime)}</li>
        </ul>
      </div>

      <p style=""color:#374151;font-size:14px"">
        Email <strong>{HE(toEmail)}</strong> đã được sử dụng làm tài khoản VISITOR để theo dõi yêu cầu này.
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
      <p style=""color:#374151;font-size:14px"">Xin chào <strong>{HE(registrantFullName)}</strong>,</p>
      <p style=""color:#374151;font-size:14px"">
        Bạn đã gửi yêu cầu tham quan cho đoàn <strong>{HE(delegationName)}</strong> thành công với mã yêu cầu là <strong>{HE(requestCode)}</strong>.
      </p>
      
      <div style=""background:#f0f7ff;border-left:4px solid #004c91;border-radius:8px;padding:16px 20px;margin:20px 0"">
        <p style=""margin:0 0 8px;color:#374151;font-size:13px""><strong>Thông tin đầu mối liên hệ được ghi nhận:</strong></p>
        <ul style=""margin:0;padding-left:20px;color:#374151;font-size:13px"">
          <li><strong>Họ và tên:</strong> {HE(contactFullName)}</li>
          <li><strong>Email:</strong> {HE(contactEmail)}</li>
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

    /// <summary>
    /// Reduce an address to a non-PII <c>***@domain</c> form for safe metadata logging, so the local-part
    /// (which may identify a person) is never persisted to logs.
    /// </summary>
    private static string MaskEmail(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || address == "(none)") return "(none)";
        var at = address.LastIndexOf('@');
        return at > 0 ? "***@" + address[(at + 1)..] : "***";
    }
}

/// <summary>Resolved SMTP connection settings passed to <see cref="EmailService.DispatchAsync"/>.</summary>
public readonly record struct SmtpConfig(string? Host, int Port, string? User, string? Password, bool EnableSsl);

/// <summary>
/// Thrown by the void send contract when delivery hard-fails (provider error, or fail-closed in
/// Production). Carries only a machine code + a human-safe message — never a secret.
/// </summary>
public sealed class EmailDeliveryException : Exception
{
    public string Code { get; }

    public EmailDeliveryException(string code, string safeMessage) : base(safeMessage) => Code = code;
}
