using MediatR;

namespace PEMS.Application.EmailActions;

/// <summary>
/// Consumes a one-time participation-response token (the POST behind the public confirm page).
/// Applies ACCEPT/DECLINE to the participant, marks the token (and its sibling) used, notifies the
/// host, and audits — all idempotently: a second click reports ALREADY_RESPONDED and never mutates
/// twice. Never throws for token problems (returns a status) so the public page can render cleanly.
/// </summary>
public sealed record ExecuteEmailActionCommand(string RawToken, string? Ip, string? UserAgent)
    : IRequest<EmailActionExecuteResult>;

public sealed class EmailActionExecuteResult
{
    /// <summary>SUCCESS | ALREADY_RESPONDED | EXPIRED | INVALID.</summary>
    public string Status { get; set; } = EmailActionViewStatuses.Invalid;
    /// <summary>ACCEPT | DECLINE.</summary>
    public string? Action { get; set; }
    public string? RecipientName { get; set; }
    public string? DelegationName { get; set; }
    public string Message { get; set; } = "Liên kết không hợp lệ.";
}
