namespace PEMS.Application.Accounts.Common;

/// <summary>Shared subject/body for the account email-confirmation link (create / resend / edit-email).</summary>
public static class AccountConfirmationEmail
{
    public const string Subject = "PEMS — Xác nhận email để kích hoạt tài khoản";

    public static string BuildHtml(string fullName, string confirmUrl)
    {
        var name = System.Net.WebUtility.HtmlEncode(fullName);
        var url = System.Net.WebUtility.HtmlEncode(confirmUrl);
        return
            $"<p>Xin chào {name},</p>" +
            "<p>Vui lòng xác nhận địa chỉ email này để kích hoạt tài khoản nội bộ PEMS của bạn. " +
            "Trước khi xác nhận, tài khoản chưa thể đăng nhập.</p>" +
            $"<p><a href=\"{url}\" style=\"display:inline-block;background:#004c91;color:#fff;text-decoration:none;padding:12px 24px;border-radius:6px;font-weight:bold\">Xác nhận email &amp; kích hoạt tài khoản</a></p>" +
            $"<p>Hoặc mở liên kết:<br/>{url}</p>" +
            "<p>Liên kết có hiệu lực trong <strong>24 giờ</strong>. Nếu bạn không yêu cầu tài khoản này, vui lòng bỏ qua email.</p>" +
            "<p>Trân trọng,<br/>PEMS System</p>";
    }

    /// <summary>Neutral notice sent to the OLD address after a pending account's email is changed.</summary>
    public const string EmailChangedNoticeSubject = "PEMS — Thông báo thay đổi email đăng ký";

    public static string BuildEmailChangedNoticeHtml() =>
        "<p>Xin chào,</p>" +
        "<p>Một tài khoản nội bộ PEMS trước đây được đăng ký với địa chỉ email này đã được cập nhật sang một " +
        "địa chỉ khác trước khi kích hoạt. Nếu đây không phải là bạn, bạn không cần làm gì thêm — địa chỉ email " +
        "này sẽ không được sử dụng để đăng nhập.</p>" +
        "<p>Trân trọng,<br/>PEMS System</p>";

    /// <summary>Maps a delivery outcome to the truthful string status the API returns.</summary>
    public static string MapStatus(PEMS.Application.Common.Interfaces.EmailDeliveryStatus status) => status switch
    {
        PEMS.Application.Common.Interfaces.EmailDeliveryStatus.Sent => "SENT",
        PEMS.Application.Common.Interfaces.EmailDeliveryStatus.Skipped => "SKIPPED",
        _ => "FAILED",
    };
}
