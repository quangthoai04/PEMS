using FluentValidation;
using MediatR;

namespace PEMS.Application.Delegations.Commands.VisitContactTransfer;

/// <summary>
/// Stable error codes for the 24h primary-contact TRANSFER workflow (plan §16.4/§4.4, handoff §6.7).
/// The link-token codes are shared with the claim workflow (<c>CONTACT_CLAIM_TOKEN_INVALID</c>) — the
/// "your link is dead" semantics are identical.
/// </summary>
public static class VisitContactTransferErrorCodes
{
    /// <summary>Another identity change (claim or transfer) is already PENDING on this request.</summary>
    public const string AlreadyPending = "IDENTITY_CHANGE_ALREADY_PENDING";
    /// <summary>The proposed contact email equals the current contact email.</summary>
    public const string EmailUnchanged = "IDENTITY_CHANGE_EMAIL_UNCHANGED";
    /// <summary>The proposed target account may not become a visit contact (e.g. inactive visitor).</summary>
    public const string TargetNotAllowed = "IDENTITY_CHANGE_TARGET_NOT_ALLOWED";
    /// <summary>The transfer window (24h) has passed.</summary>
    public const string Expired = "IDENTITY_CHANGE_EXPIRED";
    /// <summary>The transfer is no longer in a state where this action applies (applied/declined/cancelled,
    /// owner changed outside the workflow, or the request moved on).</summary>
    public const string Conflict = "IDENTITY_CHANGE_CONFLICT";
    /// <summary>The logged-in Google account's email does not match the invited email.</summary>
    public const string GoogleEmailMismatch = "IDENTITY_GOOGLE_EMAIL_MISMATCH";
    /// <summary>The transfer was superseded by a newer invitation.</summary>
    public const string Superseded = "IDENTITY_CHANGE_SUPERSEDED";
    /// <summary>The proposed email belongs to an internal (non-VISITOR) account.</summary>
    public const string InternalAccountConflict = "CONTACT_EMAIL_INTERNAL_ACCOUNT_CONFLICT";
    /// <summary>The current primary contact is not ACTIVE — transfer needs an established owner
    /// (an unclaimed contact is INITIAL_CLAIM territory: resend/replace, not transfer).</summary>
    public const string ContactNotActive = "CONTACT_ACCOUNT_NOT_ACTIVE";
    /// <summary>No PENDING transfer exists on this request.</summary>
    public const string NonePending = "IDENTITY_TRANSFER_NONE_PENDING";
    /// <summary>Resend limit reached for this transfer invitation.</summary>
    public const string ResendLimit = "IDENTITY_TRANSFER_RESEND_LIMIT";
}

// ── Initiate (registrant or current ACTIVE primary contact) ────────────────────

public sealed record InitiateVisitContactTransferCommand(
    ulong VisitRequestId,
    string FullName,
    string Organization,
    string Phone,
    string Email,
    string? Reason) : IRequest<VisitContactTransferManageResponse>;

/// <summary>Structural validation for the initiate payload (business rules re-checked in the handler).</summary>
public sealed class InitiateVisitContactTransferCommandValidator
    : AbstractValidator<InitiateVisitContactTransferCommand>
{
    public InitiateVisitContactTransferCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên đầu mối liên hệ mới không được để trống.").MaximumLength(150);
        RuleFor(x => x.Organization).MaximumLength(200);
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Số điện thoại đầu mối liên hệ mới không được để trống.").MaximumLength(50);
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email đầu mối liên hệ mới không được để trống.")
            .EmailAddress().WithMessage("Email đầu mối liên hệ mới không đúng định dạng.")
            .MaximumLength(150);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

// ── Registrant/owner-side management ────────────────────────────────────────────

public sealed record GetActiveVisitContactTransferQuery(ulong VisitRequestId)
    : IRequest<VisitContactTransferStateResponse>;

public sealed record ResendVisitContactTransferCommand(ulong VisitRequestId)
    : IRequest<VisitContactTransferManageResponse>;

public sealed record CancelVisitContactTransferCommand(ulong VisitRequestId, string? Reason)
    : IRequest<VisitContactTransferManageResponse>;

/// <summary>Owner-side view of the transfer state (authenticated; masked email only).</summary>
public sealed record VisitContactTransferStateResponse(
    ulong VisitRequestId,
    bool HasPendingTransfer,
    ulong? IdentityChangeId,
    string? Status,
    string? NewEmailMasked,
    System.DateTime? ExpiresAt,
    uint ResendCount);

public sealed record VisitContactTransferManageResponse(
    ulong VisitRequestId,
    string? TransferStatus,     // PENDING | CANCELLED | null
    string? NewEmailMasked,
    System.DateTime? ExpiresAt,
    uint ResendCount,
    string Message);

// ── Public masked landing + invited-side accept/decline ────────────────────────

public sealed record GetVisitContactTransferInfoQuery(string Token)
    : IRequest<VisitContactTransferInfoResponse>;

/// <summary>Anonymous landing summary. Masked-only; never mutates and never enumerates accounts.</summary>
public sealed record VisitContactTransferInfoResponse(
    string Status,                // PENDING | APPLIED | DECLINED | EXPIRED | CANCELLED | SUPERSEDED | INVALID
    bool Actionable,
    string? MaskedEmail,
    string? DelegationName,
    string? RequestCode,
    string? RequestedByName,      // display name of the initiator (registrant/owner) — not an email
    System.DateTime? ExpiresAt,
    bool RequiresGoogleLoginEmailMatch);

public sealed record AcceptVisitContactTransferCommand(string Token)
    : IRequest<VisitContactTransferActionResponse>;

public sealed record DeclineVisitContactTransferCommand(string Token, string? Reason)
    : IRequest<VisitContactTransferActionResponse>;

public sealed record VisitContactTransferActionResponse(
    ulong VisitRequestId,
    string RequestCode,
    string TransferStatus,              // APPLIED | DECLINED
    string PrimaryContactAccessStatus,  // stays ACTIVE throughout
    bool Idempotent,
    string Message);
