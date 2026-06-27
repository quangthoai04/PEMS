using MediatR;

namespace PEMS.Application.EmailActions;

/// <summary>
/// Read-only lookup for the public email-action landing page (the GET a recipient lands on when
/// clicking Accept/Decline in their email). Does NOT consume the token — it just reports whether the
/// link is still actionable and the context to render a confirm page. Consuming happens on POST via
/// <see cref="ExecuteEmailActionCommand"/>, so a mail-scanner prefetch of the GET cannot burn the token.
/// </summary>
public sealed record GetEmailActionInfoQuery(string RawToken) : IRequest<EmailActionInfoResult>;

/// <summary>View states the public pages render. VALID = ready to confirm; the rest are terminal,
/// except REASON_REQUIRED which re-renders the decline form with a validation error (token untouched).</summary>
public static class EmailActionViewStatuses
{
    public const string Valid = "VALID";
    public const string Success = "SUCCESS";
    public const string AlreadyResponded = "ALREADY_RESPONDED";
    public const string Expired = "EXPIRED";
    public const string Invalid = "INVALID";
    public const string ReasonRequired = "REASON_REQUIRED";
}

public sealed class EmailActionInfoResult
{
    public string Status { get; set; } = EmailActionViewStatuses.Invalid;
    /// <summary>ACCEPT | DECLINE (the action this specific token performs).</summary>
    public string? Action { get; set; }
    public string? RecipientName { get; set; }
    public string? DelegationName { get; set; }
    public string? CampusName { get; set; }
    public string? PlannedTimeText { get; set; }
    public string? ParticipantRoleLabel { get; set; }
    /// <summary>When already responded: ACCEPTED | DECLINED (for the message).</summary>
    public string? CurrentResponse { get; set; }
}
