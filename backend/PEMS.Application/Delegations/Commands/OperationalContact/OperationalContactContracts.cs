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
    /// <summary>
    /// Whether the reader must sign in before they may answer. FALSE for an ordinary invitation link:
    /// the token itself is single-use, action-bound and address-bound, and demanding a Google account
    /// from an external guest before they may say yes or no is why invitations went unanswered.
    /// Signing in remains possible and is the path an invitee who is already in PEMS will take.
    /// </summary>
    bool RequiresGoogleLoginEmailMatch,
    /// <summary>
    /// Which answer THIS link carries — ACCEPT or DECLINE. The email offers one link per answer, so
    /// the page renders the single action the reader chose rather than both buttons, one of which
    /// their link cannot perform.
    /// </summary>
    string? IntendedAction = null);

// ── Invited-side accept / decline (authenticated; email must match) ───────────

/// <param name="ActingUserId">
/// INTERNAL ONLY — never bound from a route, a query string or a body. It is set by the PUBLIC
/// (no-login) handlers, which have already proved the answerer's identity from the single-use token
/// and resolved it to a real account, so this command can run the one canonical accept without a
/// signed-in session. Null on every authenticated call, where the session decides the actor.
/// </param>
/// <param name="Token">
/// The single-use link. NULL when a signed-in invitee answers from inside the product ("Lời mời đầu
/// mối của tôi") instead of from their email: they have no link, and demanding one would tell a
/// person who is already authenticated as the invited address to go and find an email. Then
/// <paramref name="IdentityChangeId"/> names the invitation and the session proves the identity —
/// the same two facts a token carries, established more strongly.
/// </param>
/// <param name="IdentityChangeId">Required when <paramref name="Token"/> is null; ignored otherwise.</param>
public sealed record AcceptOperationalContactConfirmationCommand(
    string? Token, ulong? ActingUserId = null, ulong? IdentityChangeId = null)
    : IRequest<OperationalContactActionResponse>;

/// <param name="Token">See <see cref="AcceptOperationalContactConfirmationCommand.Token"/>.</param>
/// <param name="ActingUserId">See <see cref="AcceptOperationalContactConfirmationCommand.ActingUserId"/>.</param>
public sealed record DeclineOperationalContactConfirmationCommand(
    string? Token, string? Reason, ulong? ActingUserId = null, ulong? IdentityChangeId = null)
    : IRequest<OperationalContactActionResponse>;

// ── The signed-in invitee's own surface ──────────────────────────────────────

/// <summary>
/// "Lời mời đầu mối của tôi" — the invitations addressed to the signed-in account's own address.
///
/// <para>
/// A pending invitee is NOT yet the operational contact of anything, so this deliberately does not
/// widen the request read scope: <c>VisitFormReadService</c> still refuses them the full request, and
/// matching on an email address is not evidence of a relation the system has granted. What they get
/// is a limited summary of what they are being asked to take on — enough to decide, and nothing that
/// belongs to a campus they have not accepted.
/// </para>
/// </summary>
public sealed record GetMyOperationalContactInvitationsQuery
    : IRequest<IReadOnlyList<MyOperationalContactInvitationDto>>;

/// <summary>One outstanding invitation, as the invited person may see it before deciding.</summary>
public sealed record MyOperationalContactInvitationDto(
    ulong IdentityChangeId,
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string Kind,                   // INITIAL_CONFIRMATION | TRANSFER
    string? RequestCode,
    string? CampusName,
    string? DelegationName,
    DateTime? PlannedStartAt,
    DateTime? PlannedEndAt,
    string? RegistrantFullName,
    string? RegistrantOrganization,
    DateTime ExpiresAt);

// ── Public accept / decline from the invitation email (NO login) ─────────────
//
// Same two answers, reachable by somebody who has never signed in. The invited person is usually an
// external guest with no PEMS account and no reason to create one before deciding whether they will
// take the role at all — requiring a Google sign-in first turned a one-click answer into an account
// setup, and the invitations simply went unanswered.
//
// Both are POST and both are reached from a confirmation PAGE, never from the email link directly.
// A GET that mutates is answered by whatever prefetches URLs — Outlook, Gmail, Defender, corporate
// scanners — so the email's links only ever OPEN the page, and the page posts when a human clicks.
//
// The token is the whole authorization, which is why it is single-use, short-lived, bound to one
// intended action and to one address that was chosen by the registrant rather than by the caller.

/// <summary>
/// Accepts the invitation without a signed-in session. The address still ends up owning the campus
/// through a real <c>users</c> row — the relation is an account id, never an email string — so the
/// handler links the existing account for that address, or provisions the Visitor account the public
/// visit-request flow would have created. A later Google sign-in with the same address resolves to
/// that same user.
/// </summary>
public sealed record PublicAcceptOperationalContactConfirmationCommand(string Token)
    : IRequest<OperationalContactActionResponse>;

/// <summary>
/// Declines the invitation without a signed-in session. No account is provisioned: somebody who is
/// not taking the role has no reason to acquire an account in order to say so.
/// </summary>
public sealed record PublicDeclineOperationalContactConfirmationCommand(string Token, string? Reason)
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
/// Sends a FRESH confirmation invitation to the address this campus already names, when there is no
/// live invitation left to resend.
///
/// <para>
/// RESEND and REINVITE answer different situations and must not be the same command:
/// </para>
/// <list type="bullet">
/// <item><b>Resend</b> — the invitation is still PENDING; the person just needs the mail again. It
/// reissues the token on the EXISTING <c>VisitRequestIdentityChange</c> and counts against the resend
/// cap.</item>
/// <item><b>Reinvite</b> — the invitation is CANCELLED, DECLINED or EXPIRED, so there is nothing to
/// resend. It opens a NEW identity change with a new token and a fresh expiry.</item>
/// </list>
///
/// <para>
/// Without this the registrant was stuck. Cancelling an INITIAL_CONFIRMATION leaves the campus's
/// snapshot email exactly as it was, so re-saving the contact form with that same address is
/// classified by <c>SaveOperationalContactCommandHandler</c> as an unchanged address — a profile
/// update, which by design mints no token and sends no mail. The only escape was to save a fake
/// address and then change it back, which supersedes invitations and writes two false entries into
/// the campus's identity history to achieve what this command does in one honest step.
/// </para>
/// <para>
/// It never changes WHO the contact is — that is replace (undecided) or transfer (decided). It only
/// re-opens the confirmation for the address already on the campus, so it is refused when the campus
/// already has a confirmed contact or already has an invitation in flight.
/// </para>
/// </summary>
public sealed record ReinviteOperationalContactConfirmationCommand(
    ulong VisitRequestId, ulong VisitInstanceId)
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
        RuleFor(x => x.Organization)
            .NotEmpty().WithMessage("Đơn vị công tác đầu mối vận hành không được để trống.")
            .MaximumLength(200);
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
        RuleFor(x => x.Organization)
            .NotEmpty().WithMessage("Đơn vị công tác đầu mối vận hành không được để trống.")
            .MaximumLength(200);
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
        RuleFor(x => x.Organization)
            .NotEmpty().WithMessage("Đơn vị công tác đầu mối vận hành không được để trống.")
            .MaximumLength(200);
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
        RuleFor(x => x.Organization)
            .NotEmpty().WithMessage("Đơn vị công tác đầu mối vận hành mới không được để trống.")
            .MaximumLength(200);
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
