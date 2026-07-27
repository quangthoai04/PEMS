namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>
/// Subjects/bodies for the notifications this slice sends (spec §12.12). Two rules shape every body:
///
///  • the OLD address gets a strictly neutral notice — it may belong to an uninvolved person reached by
///    a typo, so it reveals no name, no role, no department/campus and above all not the NEW address;
///  • the NEW address gets a snapshot built from the DATABASE entity, never from the request, with
///    every interpolated value HTML-encoded.
/// </summary>
public static class DepartmentPersonnelEmails
{
    // ── Delivery outcome vocabulary returned to the client (spec §12.12). ──
    public const string StatusSent = "SENT";
    public const string StatusPartial = "PARTIAL";
    public const string StatusFailed = "FAILED";
    public const string StatusSkipped = "SKIPPED";
    public const string StatusNotRequired = "NOT_REQUIRED";

    public const string OldAddressSubject = "Địa chỉ email này đã được gỡ khỏi một tài khoản PEMS";
    public const string NewAddressSubject = "Email đăng nhập tài khoản PEMS của bạn đã được cập nhật";
    public const string DisabledSubject = "Tài khoản PEMS của bạn đã bị vô hiệu hóa";
    public const string EnabledSubject = "Tài khoản PEMS của bạn đã được kích hoạt lại";
    public const string LeadershipGrantedSubject = "Bạn đã được bổ nhiệm làm Trưởng phòng";
    public const string LeadershipHandedOverSubject = "Bạn đã bàn giao vai trò Trưởng phòng";

    /// <summary>
    /// Notice for the address that was just unlinked. Deliberately anonymous — no holder name, no new
    /// address, no role/campus/department, no "changed from A to B" phrasing, and no token.
    /// </summary>
    public static string BuildOldAddressNotice() =>
        "<p>Xin chào,</p>" +
        "<p>Địa chỉ email này không còn được sử dụng để đăng nhập vào tài khoản PEMS đã liên kết trước đó.</p>" +
        "<p>Mọi phiên đăng nhập đang hoạt động bằng địa chỉ email này đã được thu hồi. " +
        "Bạn không cần thực hiện thêm thao tác nào.</p>" +
        "<p>Nếu bạn không biết về tài khoản này hoặc cho rằng đây là sự nhầm lẫn, vui lòng liên hệ " +
        "Trưởng phòng phụ trách hoặc bộ phận quản trị hệ thống PEMS để được hỗ trợ.</p>" +
        "<p>Trân trọng,<br/>PEMS System</p>";

    /// <summary>
    /// Snapshot for the new address of an already-provisioned account (ACTIVE / INACTIVE / LOCKED).
    /// States plainly that the account status is unchanged, because changing the login address does not
    /// activate, unlock or deactivate anything (spec §12.1).
    /// </summary>
    public static string BuildNewAddressNotice(
        string fullName, string newEmail, string departmentName, string campusName, string accountStatus)
    {
        var statusLine = accountStatus switch
        {
            PEMS.Domain.Constants.UserStatuses.Active =>
                "<p>Tài khoản của bạn vẫn đang hoạt động. Từ bây giờ vui lòng dùng địa chỉ email trên để đăng nhập.</p>",
            PEMS.Domain.Constants.UserStatuses.Inactive =>
                "<p>Tài khoản của bạn hiện đang bị vô hiệu hóa và sẽ chưa thể đăng nhập cho tới khi được kích hoạt lại.</p>",
            PEMS.Domain.Constants.UserStatuses.Locked =>
                "<p>Tài khoản của bạn hiện đang bị khóa vì lý do bảo mật. Việc đổi email không mở khóa tài khoản; " +
                "vui lòng liên hệ bộ phận quản trị hệ thống để được hỗ trợ mở khóa.</p>",
            _ => string.Empty,
        };

        return
            $"<p>Xin chào {Encode(fullName)},</p>" +
            "<p>Email đăng nhập của tài khoản PEMS của bạn đã được cập nhật.</p>" +
            "<p><strong>Thông tin tài khoản hiện tại:</strong></p>" +
            "<ul>" +
            $"<li>Email đăng nhập: <strong>{Encode(newEmail)}</strong></li>" +
            $"<li>Phòng ban: <strong>{Encode(departmentName)}</strong></li>" +
            $"<li>Cơ sở: <strong>{Encode(campusName)}</strong></li>" +
            "</ul>" +
            statusLine +
            "<p>Các phiên đăng nhập trước đó đã được thu hồi. Trong lần đăng nhập tiếp theo bằng email mới, " +
            "hệ thống sẽ liên kết lại phương thức đăng nhập với tài khoản của bạn.</p>" +
            "<p>Nếu bạn không mong đợi thay đổi này, vui lòng liên hệ Trưởng phòng phụ trách hoặc bộ phận " +
            "quản trị hệ thống PEMS để được hỗ trợ.</p>" +
            "<p>Trân trọng,<br/>PEMS System</p>";
    }

    /// <summary>Account was deactivated by the Department Leader. The reason is operator-entered free text.</summary>
    public static string BuildDisabledNotice(string fullName, string departmentName, string? reason)
    {
        var reasonLine = string.IsNullOrWhiteSpace(reason)
            ? string.Empty
            : $"<p>Lý do: <strong>{Encode(reason)}</strong></p>";

        return
            $"<p>Xin chào {Encode(fullName)},</p>" +
            $"<p>Tài khoản PEMS của bạn thuộc phòng ban <strong>{Encode(departmentName)}</strong> đã được vô hiệu hóa. " +
            "Bạn sẽ không thể đăng nhập cho tới khi tài khoản được kích hoạt lại.</p>" +
            reasonLine +
            "<p>Mọi phiên đăng nhập đang hoạt động đã được thu hồi.</p>" +
            "<p>Nếu bạn cho rằng đây là sự nhầm lẫn, vui lòng liên hệ Trưởng phòng phụ trách.</p>" +
            "<p>Trân trọng,<br/>PEMS System</p>";
    }

    /// <summary>Account was re-enabled. It does NOT restore sessions — the user signs in again.</summary>
    public static string BuildEnabledNotice(string fullName, string departmentName) =>
        $"<p>Xin chào {Encode(fullName)},</p>" +
        $"<p>Tài khoản PEMS của bạn thuộc phòng ban <strong>{Encode(departmentName)}</strong> đã được kích hoạt lại.</p>" +
        "<p>Vui lòng đăng nhập lại để tiếp tục sử dụng hệ thống.</p>" +
        "<p>Trân trọng,<br/>PEMS System</p>";

    /// <summary>Sent to the incoming head after a committed leadership transfer.</summary>
    public static string BuildLeadershipGrantedNotice(string fullName, string departmentName) =>
        $"<p>Xin chào {Encode(fullName)},</p>" +
        $"<p>Bạn đã được bổ nhiệm làm <strong>Trưởng phòng</strong> của phòng ban " +
        $"<strong>{Encode(departmentName)}</strong>.</p>" +
        "<p>Các phiên đăng nhập hiện tại đã được thu hồi. Vui lòng đăng nhập lại để nhận quyền quản lý mới.</p>" +
        "<p>Trân trọng,<br/>PEMS System</p>";

    /// <summary>Sent to the outgoing head after a committed leadership transfer.</summary>
    public static string BuildLeadershipHandedOverNotice(string fullName, string departmentName) =>
        $"<p>Xin chào {Encode(fullName)},</p>" +
        $"<p>Bạn đã bàn giao vai trò Trưởng phòng của phòng ban <strong>{Encode(departmentName)}</strong> " +
        "và hiện là nhân viên của phòng ban này.</p>" +
        "<p>Các phiên đăng nhập hiện tại đã được thu hồi. Vui lòng đăng nhập lại để hệ thống áp dụng quyền mới.</p>" +
        "<p>Trân trọng,<br/>PEMS System</p>";

    /// <summary>
    /// Escapes the five characters that could break out of HTML element text while leaving Vietnamese
    /// diacritics intact — every value here lands in element content, so this is enough to stop
    /// injection without turning accented letters into numeric entities.
    /// </summary>
    private static string Encode(string? value) => (value ?? string.Empty)
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#39;");
}
