using System.Net;

namespace PEMS.Application.Delegations.Commands.InviteVisitParticipant;

/// <summary>Builds the participant-invitation email (subject + HTML). Staff/Student get Accept +
/// Decline; a Department Leader additionally gets "Gán nhân sự" (a login-required internal link, not
/// a public token). Mirrors the branded card style of the other PEMS transactional emails.</summary>
public static class ParticipantInvitationEmailBuilder
{
    public sealed record Result(string Subject, string HtmlBody);

    public static string BuildSubject(bool isDept, string delegationName)
        => isDept
            ? $"[PEMS] Yêu cầu phòng ban hỗ trợ tiếp khách — {delegationName}"
            : $"[PEMS] Lời mời tham gia hỗ trợ tiếp khách — {delegationName}";

    public static Result Build(
        string recipientName,
        string participantRoleLabel,
        string delegationName,
        string campusName,
        string plannedTimeText,
        string hostName,
        string acceptUrl,
        string declineUrl,
        string? assignStaffUrl,
        string? message)
    {
        var isDept = assignStaffUrl != null;
        var subject = BuildSubject(isDept, delegationName);

        var intro = isDept
            ? $"Phòng ban của bạn được mời hỗ trợ đoàn <strong>{HE(delegationName)}</strong> tại <strong>{HE(campusName)}</strong>."
            : $"Bạn được mời tham gia hỗ trợ đoàn <strong>{HE(delegationName)}</strong> tại <strong>{HE(campusName)}</strong>.";

        var messageBlock = string.IsNullOrWhiteSpace(message)
            ? string.Empty
            : $@"<div style=""background:#fafafa;border:1px solid #e5e7eb;border-radius:8px;padding:12px 16px;margin:16px 0"">
                  <p style=""margin:0;color:#374151;font-size:13px""><strong>Lời nhắn từ Host:</strong> {HE(message)}</p>
                </div>";

        var assignButton = isDept
            ? $@"<a href=""{HE(assignStaffUrl!)}"" style=""display:inline-block;background:#004c91;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Gán nhân sự</a>"
            : string.Empty;

        var assignNote = isDept
            ? @"<p style=""color:#6b7280;font-size:12px;margin-top:8px"">Lưu ý: thao tác <strong>Gán nhân sự</strong> yêu cầu đăng nhập hệ thống.</p>"
            : string.Empty;

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
      <p style=""color:#374151;font-size:14px"">Xin chào <strong>{HE(recipientName)}</strong>,</p>
      <p style=""color:#374151;font-size:14px"">{intro}</p>

      <div style=""background:#f0f7ff;border-left:4px solid #004c91;border-radius:8px;padding:16px 20px;margin:20px 0"">
        <ul style=""margin:0;padding-left:20px;color:#374151;font-size:13px;line-height:1.7"">
          <li><strong>Đoàn khách:</strong> {HE(delegationName)}</li>
          <li><strong>Cơ sở:</strong> {HE(campusName)}</li>
          <li><strong>Thời gian:</strong> {HE(plannedTimeText)}</li>
          <li><strong>Vai trò:</strong> {HE(participantRoleLabel)}</li>
          <li><strong>Host chính:</strong> {HE(hostName)}</li>
        </ul>
      </div>

      {messageBlock}

      <p style=""color:#374151;font-size:14px"">Vui lòng phản hồi bằng một trong các nút dưới đây:</p>
      <div style=""text-align:center;margin:20px 0"">
        <a href=""{HE(acceptUrl)}"" style=""display:inline-block;background:#10b981;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Chấp nhận</a>
        <a href=""{HE(declineUrl)}"" style=""display:inline-block;background:#ef4444;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Từ chối</a>
        {assignButton}
      </div>
      {assignNote}

      <p style=""color:#9ca3af;font-size:12px;margin-top:16px"">Liên kết phản hồi sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p>
      <p style=""color:#6b7280;font-size:12px;margin-top:12px"">Trân trọng,<br/>PEMS - FPT University</p>
    </div>
    <div style=""background:#f9fafb;padding:16px 32px;text-align:center"">
      <p style=""color:#9ca3af;font-size:11px;margin:0"">© 2026 PEMS — FPT University. Không trả lời email này.</p>
    </div>
  </div>
</body>
</html>";

        return new Result(subject, body);
    }

    private static string HE(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
