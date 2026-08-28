using MediatR;

namespace PEMS.Application.EmailActions;

/// <summary>
/// Consumes a one-time participation-response token (the POST behind the public confirm page).
/// Applies ACCEPT/DECLINE to the participant, marks the token (and its sibling) used, notifies the
/// host, and audits — all idempotently: a second click reports ALREADY_RESPONDED and never mutates
/// twice. Never throws for token problems (returns a status) so the public page can render cleanly.
/// A DECLINE must carry <see cref="DeclineReason"/> (5–1000 chars after trim); a missing/invalid
/// reason returns REASON_REQUIRED and leaves the token untouched so the user can retry.
/// <see cref="Note"/> is the ACCEPT counterpart: optional, applied only when the token's intended
/// action is ACCEPT, and ignored otherwise.
/// </summary>
public sealed record ExecuteEmailActionCommand(
    string RawToken, string? Ip, string? UserAgent, string? DeclineReason = null, string? Note = null)
    : IRequest<EmailActionExecuteResult>;

public sealed class EmailActionExecuteResult
{
    /// <summary>SUCCESS | ALREADY_RESPONDED | EXPIRED | INVALID | REASON_REQUIRED.</summary>
    public string Status { get; set; } = EmailActionViewStatuses.Invalid;
    /// <summary>ACCEPT | DECLINE.</summary>
    public string? Action { get; set; }
    /// <summary>The token's action_context (e.g. PARTICIPATION_RESPONSE | LOGISTICS_REQUEST_RESPONSE) so
    /// the public pages can render context-appropriate wording.</summary>
    public string? Context { get; set; }
    public string? RecipientName { get; set; }
    public string? DelegationName { get; set; }
    public string Message { get; set; } = "Liên kết không hợp lệ.";
    /// <summary>On REASON_REQUIRED: the reason the user just submitted, echoed back to re-fill the form.</summary>
    public string? SubmittedReason { get; set; }
}
