namespace PEMS.Application.Delegations.Commands.InviteVisitParticipant;

public sealed record InviteVisitParticipantResponse(
    ulong ParticipantId,
    ulong UserId,
    string ParticipantRole,
    string Status,
    /// <summary>True when the invitation email was actually sent; false when it was queued/failed
    /// (the participant row still exists — the host can resend later).</summary>
    bool EmailQueued,
    string EmailRecipient,
    string Message,
    /// <summary>SENT | FAILED — explicit email delivery outcome for the toast.</summary>
    string EmailStatus,
    ulong SentEmailId);
