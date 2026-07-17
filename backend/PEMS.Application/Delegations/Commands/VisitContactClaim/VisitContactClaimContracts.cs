using MediatR;

namespace PEMS.Application.Delegations.Commands.VisitContactClaim;

/// <summary>Stable error codes for the v2 primary-contact claim workflow (plan §16.4).</summary>
public static class VisitContactClaimErrorCodes
{
    /// <summary>The link's token is unknown, already used, superseded or expired.</summary>
    public const string TokenInvalid = "CONTACT_CLAIM_TOKEN_INVALID";
    /// <summary>The identity change is no longer PENDING (applied/declined/expired/cancelled/superseded).</summary>
    public const string NotPending = "CONTACT_CLAIM_NOT_PENDING";
    /// <summary>The authenticated account's email does not match the invited contact email.</summary>
    public const string EmailMismatch = "CONTACT_CLAIM_EMAIL_MISMATCH";
    /// <summary>Resend limit reached for this invitation.</summary>
    public const string ResendLimit = "CONTACT_CLAIM_RESEND_LIMIT";
    /// <summary>No PENDING initial claim exists on this request.</summary>
    public const string NoPendingClaim = "CONTACT_CLAIM_NONE_PENDING";
}

// ── Public landing info (masked; no login required) ────────────────────────────

public sealed record GetVisitContactClaimInfoQuery(string Token) : IRequest<VisitContactClaimInfoResponse>;

/// <summary>
/// Masked landing summary for the claim page. NEVER exposes the full contact email or any form PII —
/// the viewer is anonymous. <c>Actionable</c> is true only for a live PENDING claim with an unused token.
/// </summary>
public sealed record VisitContactClaimInfoResponse(
    string Status,                // PENDING | APPLIED | DECLINED | EXPIRED | CANCELLED | SUPERSEDED | INVALID
    bool Actionable,
    string? MaskedEmail,
    string? DelegationName,
    string? RequestCode,
    string? RegistrantFullName,
    System.DateTime? ExpiresAt,
    // The claim can only be accepted after logging in with the Google account of the invited email.
    bool RequiresGoogleLoginEmailMatch);

// ── Accept / decline (authenticated; email must match the claim) ───────────────

public sealed record AcceptVisitContactClaimCommand(string Token) : IRequest<VisitContactClaimActionResponse>;

public sealed record DeclineVisitContactClaimCommand(string Token, string? Reason)
    : IRequest<VisitContactClaimActionResponse>;

public sealed record VisitContactClaimActionResponse(
    ulong VisitRequestId,
    string RequestCode,
    string ClaimStatus,                 // APPLIED | DECLINED
    string PrimaryContactAccessStatus,  // ACTIVE after accept; PENDING_CONFIRMATION after decline
    string Message);

// ── Registrant-side management of the pending invitation ───────────────────────

public sealed record ResendVisitContactClaimCommand(ulong VisitRequestId)
    : IRequest<VisitContactClaimManageResponse>;

/// <summary>
/// Replaces the still-unclaimed pending contact (the "typo fix", plan §16.4 "nhập lại email đúng"):
/// supersedes the old claim + tokens, updates the request-level contact snapshot and either creates a fresh
/// PENDING claim (different email) or links the registrant immediately (new email == registrant email).
/// Only while the initial claim is unresolved — once the contact is ACTIVE this is a TRANSFER (out of scope).
/// </summary>
public sealed record ReplacePendingVisitContactCommand(
    ulong VisitRequestId,
    string FullName,
    string Organization,
    string Phone,
    string Email) : IRequest<VisitContactClaimManageResponse>;

public sealed record VisitContactClaimManageResponse(
    ulong VisitRequestId,
    string PrimaryContactAccessStatus,
    string? ClaimStatus,       // status of the (new/current) claim, null when none is needed
    uint ResendCount,
    string Message);
