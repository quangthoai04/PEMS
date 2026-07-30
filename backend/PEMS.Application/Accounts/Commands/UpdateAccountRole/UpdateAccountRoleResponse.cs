namespace PEMS.Application.Accounts.Commands.UpdateAccountRole;

public sealed class UpdateAccountRoleResponse
{
    public ulong UserId { get; init; }
    public string RoleCode { get; init; } = default!;
    public ulong? PrimaryCampusId { get; init; }
    public int RevokedSessions { get; init; }

    /// <summary>True when this request changed the account's login email.</summary>
    public bool EmailChanged { get; init; }

    /// <summary>
    /// True when this request moved the account's role or sub-role — the one fact
    /// <c>ACCOUNT_ROLE_CHANGED</c> reports. A department/campus move, a rename or an MSSV correction
    /// is NOT a role change and leaves this false, so the caller never announces a role change that
    /// did not happen.
    /// </summary>
    public bool RoleChanged { get; init; }

    /// <summary>
    /// True when the account was still PENDING_EMAIL_CONFIRMATION when this request arrived — so it is
    /// STILL pending afterwards, whatever else changed. Nothing in this flow activates an account: only
    /// the holder clicking a confirmation link does. The caller needs this to say the right thing —
    /// "đã cập nhật" and "đã kích hoạt" are not the same outcome, and only one of them is true here.
    /// </summary>
    public bool RequiresEmailConfirmation { get; init; }

    /// <summary>
    /// What actually happened to the message(s) sent BECAUSE THE ADDRESS MOVED, never what was
    /// attempted: <c>NOT_REQUIRED</c> (the email did not change) · <c>SENT</c> · <c>SKIPPED</c> (mail
    /// is off in this environment) · <c>FAILED</c> · <c>PARTIAL</c> (several messages, not all of them
    /// landed). The account change is committed in every one of these cases.
    /// </summary>
    public string EmailNotificationStatus { get; init; } = "NOT_REQUIRED";

    /// <summary>
    /// Delivery outcome of the ACTIVATION link mailed to a still-pending account's new address —
    /// <c>NOT_REQUIRED</c> unless the account was pending AND its address moved. Reported separately
    /// from <see cref="RoleChangeEmailNotificationStatus"/> because the two mails carry different
    /// consequences: this one decides whether the account can ever be activated, so its failure is
    /// what "Gửi lại email xác nhận" exists for, while a failed role notice costs the holder nothing
    /// but information.
    /// </summary>
    public string ConfirmationEmailNotificationStatus { get; init; } = "NOT_REQUIRED";

    /// <summary>
    /// Delivery outcome of <c>ACCOUNT_ROLE_CHANGED</c> — <c>NOT_REQUIRED</c> when the role did not
    /// move. Independent of the two fields above in both directions: a failed confirmation must not
    /// suppress this mail, and a failed role notice must not be reported as a failed confirmation.
    /// </summary>
    public string RoleChangeEmailNotificationStatus { get; init; } = "NOT_REQUIRED";

    public string Message { get; init; } =
        "Role updated successfully. The user must sign in again via the internal portal and select the correct campus.";
}
