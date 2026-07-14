namespace PEMS.Application.Accounts.Commands.UpdateBasicAccountInfo;

public sealed class UpdateBasicAccountInfoResponse
{
    public ulong UserId { get; init; }
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;

    /// <summary>True when the login email actually changed (drives provider/session reset).</summary>
    public bool EmailChanged { get; init; }

    /// <summary>How many active sessions were revoked (only when the email changed).</summary>
    public int RevokedSessions { get; init; }

    /// <summary>NOT_REQUIRED (no email change) | SENT | FAILED | PARTIAL (only one of the two mails sent).</summary>
    public string EmailNotificationStatus { get; init; } = "NOT_REQUIRED";

    public string Message { get; init; } = "Cập nhật thông tin tài khoản thành công.";
}
