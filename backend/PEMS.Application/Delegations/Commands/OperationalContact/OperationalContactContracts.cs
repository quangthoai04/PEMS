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

/// <summary>
/// Corrects the DETAILS of the person who is already this campus's operational contact — their name,
/// organization, job title, phone. Never their address.
///
/// <para>
/// It exists because the two things a contact form can express are not the same act. Fixing a typo in
/// a name is an ordinary data correction; pointing the campus at a different address is a change of
/// identity that somebody has to accept. Routing the first through the second — which is what happens
/// when the only available command is <see cref="ReplaceOperationalContactCommand"/> — supersedes a
/// live invitation, drops the campus's confirmed contact, re-closes the global gate for every campus
/// on the request and sends a confirmation email, all to correct a spelling.
/// </para>
/// <para>
/// So this command writes four columns and nothing else: no identity-change row, no token, no email,
/// no <c>operational_contact_user_id</c>, no status move, no approval effect, and no registration lead
/// time — that rule is about when a VISIT may be scheduled and has nothing to say about a phone number.
/// </para>
/// </summary>
/// <param name="Email">
/// The address as the caller believes it to be. Carried so the handler can PROVE the caller is not
/// trying to change it — a metadata command that silently ignored a changed address would apply half
/// of what the user typed.
/// </param>
/// <param name="ExpectedRowVersion">
/// The campus row version the form was opened on. Optional: a caller with no version to offer (an
/// internal caller, an older client) is not blocked, but a stale modal that HAS one cannot overwrite
/// newer contact information with what it read minutes ago.
/// </param>
public sealed record UpdateOperationalContactProfileCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string FullName,
    string? Organization,
    string JobTitle,
    string? Phone,
    string Email,
    int? ExpectedRowVersion = null) : IRequest<OperationalContactManageResponse>;

/// <summary>
/// The ONE contact-management save the detail screen calls, for ONE campus.
///
/// <para>
/// The user fills in five fields and presses save; which of the three canonical workflows that means is
/// decided HERE, from the data, rather than by asking the user to classify their own edit in advance.
/// The address decides: unchanged (normalised) is a profile update, changed is an identity change, and
/// the campus's own state then decides whether that identity change is a pre-decision replace or a
/// post-decision transfer.
/// </para>
/// <para>
/// It implements no workflow of its own. Each branch dispatches the existing command, so the guards,
/// the invitation lifecycle, the audit rows and the emails all stay in exactly one place.
/// </para>
/// </summary>
public sealed record SaveOperationalContactCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string FullName,
    string? Organization,
    string JobTitle,
    string? Phone,
    string Email,
    string? Reason = null,
    int? ExpectedRowVersion = null) : IRequest<OperationalContactManageResponse>;

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
    uint TokenVersion,

    /// <summary>
    /// Set ONLY when the caller is the account this campus's contact snapshot describes, and only when
    /// something the account actually owns differs from it (plan v10 §6.1).
    ///
    /// <para>
    /// Null for everybody else — a registrant looking at their contact's card, a Staff Leader, a sibling
    /// campus's holder. Offering one person's profile to another to "tidy up" is how a self-service
    /// action turns into an edit of somebody else's identity.
    /// </para>
    /// </summary>
    OperationalContactProfileDifference? ProfileDifference = null);

/// <summary>
/// What the signed-in contact's PEMS profile says versus what this visit's snapshot says about them.
///
/// <para>
/// Both are legitimate. The snapshot is how this campus described them at this moment; the account is
/// who they are across every request. They are allowed to differ — a title translated for one delegation,
/// a desk number that only applies to one visit — so this is an OFFER to reconcile, never a correction
/// and never automatic (plan v10 §6.4, §6.6).
/// </para>
/// <para>
/// Only the two fields the account schema actually owns are compared. The users table has no
/// organization and no job title column, and email is the account's identity rather than a profile
/// field, so none of those three can be — or are — offered here.
/// </para>
/// </summary>
public sealed record OperationalContactProfileDifference(
    bool FullNameDiffers,
    bool PhoneDiffers,
    string? AccountFullName,
    string? AccountPhone,
    string? SnapshotFullName,
    string? SnapshotPhone);

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

public sealed class UpdateOperationalContactProfileCommandValidator
    : AbstractValidator<UpdateOperationalContactProfileCommand>
{
    public UpdateOperationalContactProfileCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên đầu mối vận hành không được để trống.").MaximumLength(150);
        RuleFor(x => x.Organization).MaximumLength(200);
        RuleFor(x => x.JobTitle)
            .NotEmpty().WithMessage("Chức vụ đầu mối vận hành không được để trống.").MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email đầu mối vận hành không được để trống.")
            .EmailAddress().WithMessage("Email đầu mối vận hành không đúng định dạng.")
            .MaximumLength(150);
    }
}

/// <summary>
/// Structural only. Which workflow the payload means is a business decision the handler makes from the
/// stored address, so nothing here may depend on it — in particular <c>Reason</c> is accepted on every
/// save and simply goes unused on the two branches that have no use for it.
/// </summary>
public sealed class SaveOperationalContactCommandValidator
    : AbstractValidator<SaveOperationalContactCommand>
{
    public SaveOperationalContactCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên đầu mối vận hành không được để trống.").MaximumLength(150);
        RuleFor(x => x.Organization).MaximumLength(200);
        RuleFor(x => x.JobTitle)
            .NotEmpty().WithMessage("Chức vụ đầu mối vận hành không được để trống.").MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email đầu mối vận hành không được để trống.")
            .EmailAddress().WithMessage("Email đầu mối vận hành không đúng định dạng.")
            .MaximumLength(150);
        RuleFor(x => x.Reason).MaximumLength(500);
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
