using System;
using FluentValidation;
using MediatR;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  Per-campus operational-contact confirmation and transfer (plan §3.2, §3.3, §5.2).
//
//  Every mutation names BOTH the request and the campus, and the handler proves the campus belongs to
//  that request before touching anything. There is no request-wide contact action any more: the old
//  workflow could hand one person authority over campuses they were never invited to, which is the
//  hole this cutover closes.
//
//  Accept and decline are ONE pair for both kinds. The token resolves to an invitation that already
//  knows whether it is an INITIAL_CONFIRMATION or a TRANSFER, so the invited person answers one link
//  and the kind decides the effect — not the URL they happened to receive.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// ── Public landing (anonymous, masked, never mutates) ─────────────────────────

public sealed record GetOperationalContactConfirmationInfoQuery(string Token)
    : IRequest<OperationalContactConfirmationInfoResponse>;

/// <summary>
/// What an anonymous holder of the link may see: the invitation's state, the masked target address and
/// the public header of the ONE campus involved. Never the full email, never the sibling campuses,
/// never form content. An unknown token returns the same shape as an expired one, so the endpoint
/// cannot be used to test whether an address was ever invited.
/// </summary>
public sealed record OperationalContactConfirmationInfoResponse(
    string Status,                 // PENDING | APPLIED | DECLINED | EXPIRED | CANCELLED | SUPERSEDED | INVALID
    bool Actionable,
    string? Kind,                  // INITIAL_CONFIRMATION | TRANSFER
    string? MaskedEmail,
    string? RequestCode,
    string? CampusName,
    string? DelegationName,
    DateTime? PlannedStartAt,
    DateTime? PlannedEndAt,
    DateTime? ExpiresAt,
    bool RequiresGoogleLoginEmailMatch);

// ── Invited-side accept / decline (authenticated; email must match) ───────────

public sealed record AcceptOperationalContactConfirmationCommand(string Token)
    : IRequest<OperationalContactActionResponse>;

public sealed record DeclineOperationalContactConfirmationCommand(string Token, string? Reason)
    : IRequest<OperationalContactActionResponse>;

/// <summary>
/// The outcome for the ONE campus that was answered. <paramref name="RequestStatus"/> is included
/// because accepting the last outstanding campus is what opens the global gate — the invited person
/// should see that the request moved on, not just their own row.
/// </summary>
public sealed record OperationalContactActionResponse(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string RequestCode,
    string Kind,
    string ChangeStatus,           // APPLIED | DECLINED
    string CampusStatus,
    string RequestStatus,
    bool Idempotent,
    string Message);

// ── Registrant / contact-side management, always for ONE campus ──────────────

public sealed record GetOperationalContactStateQuery(ulong VisitRequestId, ulong VisitInstanceId)
    : IRequest<OperationalContactStateResponse>;

public sealed record ResendOperationalContactConfirmationCommand(ulong VisitRequestId, ulong VisitInstanceId)
    : IRequest<OperationalContactManageResponse>;

/// <summary>
/// Replaces the operational contact of ONE campus BEFORE that campus has been decided (plan §3.3).
/// Rewrites the campus's contact snapshot and then either links the registrant immediately (the new
/// address is their own verified one) or clears the campus relation and invites the new address —
/// which closes the global gate again and stops every Staff Leader, on every campus, until it is
/// answered. Once the campus has a decision this command is refused: that case is a transfer.
/// </summary>
public sealed record ReplaceOperationalContactCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string FullName,
    string? Organization,
    string JobTitle,
    string? Phone,
    string Email) : IRequest<OperationalContactManageResponse>;

/// <summary>
/// Hands ONE campus's operational-contact role to a new address AFTER that campus has been decided.
/// Nothing moves until the invited person accepts: the current contact keeps every right, the campus
/// decision, host and schedule are untouched, and sibling campuses never notice.
/// </summary>
public sealed record InitiateOperationalContactTransferCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string FullName,
    string? Organization,
    string JobTitle,
    string? Phone,
    string Email,
    string? Reason) : IRequest<OperationalContactManageResponse>;

/// <summary>Closes the campus's in-flight invitation without changing who holds the campus.</summary>
public sealed record CancelOperationalContactChangeCommand(
    ulong VisitRequestId, ulong VisitInstanceId, string? Reason)
    : IRequest<OperationalContactManageResponse>;

/// <summary>Owner-side view of ONE campus's contact state. Masked address only — even the registrant
/// never reads an invited address back out of the API.</summary>
public sealed record OperationalContactStateResponse(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string CampusStatus,
    bool ContactConfirmed,
    string? ConfirmedEmailMasked,
    DateTime? ConfirmedAt,
    string? ConfirmationSource,
    string? PendingChangeKind,
    string? PendingChangeStatus,
    string? PendingEmailMasked,
    DateTime? ExpiresAt,
    uint ResendCount,
    uint TokenVersion);

public sealed record OperationalContactManageResponse(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string CampusStatus,
    string RequestStatus,
    bool ContactConfirmed,
    string? PendingChangeKind,
    string? PendingChangeStatus,
    string? PendingEmailMasked,
    DateTime? ExpiresAt,
    uint ResendCount,
    uint TokenVersion,
    string Message);

// ── Structural validation (business rules are re-checked in the handlers) ─────

public sealed class ReplaceOperationalContactCommandValidator
    : AbstractValidator<ReplaceOperationalContactCommand>
{
    public ReplaceOperationalContactCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên đầu mối vận hành không được để trống.").MaximumLength(150);
        RuleFor(x => x.Organization).MaximumLength(200);
        RuleFor(x => x.JobTitle)
            .NotEmpty().WithMessage("Chức vụ đầu mối vận hành không được để trống.").MaximumLength(150);
        // Phone is OPTIONAL — an email is what an invitation binds to.
        RuleFor(x => x.Phone)
            .MaximumLength(50);
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email đầu mối vận hành không được để trống.")
            .EmailAddress().WithMessage("Email đầu mối vận hành không đúng định dạng.")
            .MaximumLength(150);
    }
}

public sealed class InitiateOperationalContactTransferCommandValidator
    : AbstractValidator<InitiateOperationalContactTransferCommand>
{
    public InitiateOperationalContactTransferCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên đầu mối vận hành mới không được để trống.").MaximumLength(150);
        RuleFor(x => x.Organization).MaximumLength(200);
        RuleFor(x => x.JobTitle)
            .NotEmpty().WithMessage("Chức vụ đầu mối vận hành mới không được để trống.").MaximumLength(150);
        // Phone is OPTIONAL — an email is what an invitation binds to.
        RuleFor(x => x.Phone)
            .MaximumLength(50);
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email đầu mối vận hành mới không được để trống.")
            .EmailAddress().WithMessage("Email đầu mối vận hành mới không đúng định dạng.")
            .MaximumLength(150);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
