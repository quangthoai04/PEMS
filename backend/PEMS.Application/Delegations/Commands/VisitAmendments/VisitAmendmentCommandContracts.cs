using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Validation;

namespace PEMS.Application.Delegations.Commands.VisitAmendments;

// ── Safe edit (apply-now fields, plan §16.6) ────────────────────────────────────

public sealed record SubmitVisitSafeEditCommand(ulong VisitRequestId, VisitRequestSafeEditDto Patch)
    : IRequest<VisitRequestSafeEditResponse>;

public sealed class SubmitVisitSafeEditCommandValidator : AbstractValidator<SubmitVisitSafeEditCommand>
{
    public SubmitVisitSafeEditCommandValidator()
    {
        RuleFor(x => x.Patch).NotNull();
        When(x => x.Patch?.Registrant is not null, () =>
        {
            RuleFor(x => x.Patch.Registrant!.FullName).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Patch.Registrant!.Organization).MaximumLength(200);
            RuleFor(x => x.Patch.Registrant!.JobTitle).MaximumLength(150);
            RuleFor(x => x.Patch.Registrant!.Phone)
                .MustBeAPhoneNumber("Số điện thoại người đăng ký không hợp lệ.");
        });
        RuleForEach(x => x.Patch.Instances).ChildRules(i =>
        {
            // Same-person operational-contact correction (plan CanhIter3FixBug §4/§8.1) — validated only
            // when present; a sparse patch that doesn't touch the contact never reaches these rules.
            // Organization has no NotEmpty rule here deliberately: VisitSafeEditService trims and
            // manually requires it non-blank, mirroring the Nationality pattern already in this file, so
            // an omitted-vs-blank distinction never needs to exist at the FluentValidation layer.
            i.When(p => p.OperationalContact is not null, () =>
            {
                i.RuleFor(p => p.OperationalContact!.FullName).NotEmpty().MaximumLength(150);
                i.RuleFor(p => p.OperationalContact!.JobTitle).NotEmpty().MaximumLength(150);
                i.RuleFor(p => p.OperationalContact!.Organization).MaximumLength(200);
                i.RuleFor(p => p.OperationalContact!.Phone)
                    .MustBeAPhoneNumber("Số điện thoại đầu mối vận hành không hợp lệ.");
                i.RuleFor(p => p.OperationalContact!.Email).NotEmpty().MaximumLength(150);
            });
            i.RuleFor(p => p.TransportationNote)
                .MaximumLength(2000)
                .Must(n => string.IsNullOrEmpty(n) || (!n.Contains('<') && !n.Contains('>')))
                .WithMessage("Nhận diện phương tiện di chuyển không được chứa HTML/script.");
            i.RuleFor(p => p.Notes).MaximumLength(2000);
            // null is legitimate — the field is simply not part of this sparse patch.
            i.RuleFor(p => p.MediaConsentStatus)
                .Must(s => s is null or "AGREED" or "DECLINED")
                .WithMessage("Trạng thái truyền thông không hợp lệ.");
        }).When(x => x.Patch?.Instances is not null);
    }
}

// ── Amendment lifecycle ─────────────────────────────────────────────────────────

public sealed record SubmitVisitAmendmentCommand(
    ulong VisitRequestId, ulong VisitInstanceId, VisitAmendmentProposalDto Proposal)
    : IRequest<VisitAmendmentDto>;

public sealed class SubmitVisitAmendmentCommandValidator : AbstractValidator<SubmitVisitAmendmentCommand>
{
    public SubmitVisitAmendmentCommandValidator()
    {
        RuleFor(x => x.Proposal).NotNull();
        When(x => x.Proposal is not null, () =>
        {
            RuleFor(x => x.Proposal.DelegationName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Proposal.VisitType)
                .NotEmpty()
                .Must(t => t is "CAMPUS_TOUR" or "MEETING" or "WORKSHOP" or "SIGNING_CEREMONY" or "EXCHANGE" or "OTHER");
            RuleFor(x => x.Proposal.VisitTypeOther)
                .NotEmpty().MaximumLength(200)
                .When(x => x.Proposal.VisitType == "OTHER");
            RuleFor(x => x.Proposal.Purpose).NotEmpty().MaximumLength(2000);
            // Same per-campus content contract as create-v2: working content is required and every
            // member row must be filled in. An amendment REPLACES a campus's content, so it cannot be
            // looser than the original create — EXCEPT the operational contact, which an amendment
            // always targets against an ALREADY-EXISTING campus and can never redescribe (the profile
            // has exactly one door, "Manage the contact role" — see BuildChangeRows'
            // ContactProfileNotAmendable guard). The proposal carries the contact only so an unchanged
            // snapshot round-trips; requiring Create-level completeness on that replay would refuse an
            // amendment to an unrelated field purely because the campus's contact snapshot predates the
            // Organization-required rule — the same trap the edit/resubmit paths had.
            RuleFor(x => x.Proposal.WorkingContent)
                .NotEmpty().WithMessage("Nội dung làm việc không được để trống.").MaximumLength(4000);
            RuleFor(x => x.Proposal.WorkingLanguage).Must(l => l is "EN" or "VI");
            RuleFor(x => x.Proposal.OperationalContact).NotNull();
            RuleFor(x => x.Proposal.OperationalContact!)
                .SetValidator(new CreateVisitRequestV2.OperationalContactReplayV2Validator())
                .When(x => x.Proposal.OperationalContact is not null);
            RuleFor(x => x.Proposal.Reason).MaximumLength(500);
            // An approved amendment REPLACES the campus's guest list, so accepting an empty one here
            // would reintroduce the zero-guest campus that create/edit/resubmit now refuse.
            RuleFor(x => x.Proposal.Visitors)
                .NotNull()
                .NotEmpty().WithMessage("Mỗi cơ sở phải có ít nhất 1 khách.")
                .Must(v => v is null || v.Count <= 200).WithMessage("Tối đa 200 khách mỗi cơ sở.");
            RuleForEach(x => x.Proposal.Visitors).SetValidator(new CreateVisitRequestV2.VisitorV2Validator());
            RuleFor(x => x.Proposal.ExternalSupportMembers)
                .NotNull().Must(s => s.Count <= 200).WithMessage("Tối đa 200 nhân sự hỗ trợ mỗi cơ sở.");
            RuleForEach(x => x.Proposal.ExternalSupportMembers)
                .SetValidator(new CreateVisitRequestV2.SupportTeamMemberV2Validator());
        });
    }
}

public sealed record GetActiveVisitAmendmentQuery(ulong VisitRequestId, ulong VisitInstanceId)
    : IRequest<VisitAmendmentDto?>;

public sealed record ApproveVisitAmendmentCommand(ulong VisitInstanceId, ulong AmendmentId, string? Note)
    : IRequest<VisitAmendmentDecisionResponse>;

public sealed record RejectVisitAmendmentCommand(ulong VisitInstanceId, ulong AmendmentId, string Note)
    : IRequest<VisitAmendmentDecisionResponse>;

public sealed record WithdrawVisitAmendmentCommand(ulong VisitRequestId, ulong VisitInstanceId, ulong AmendmentId)
    : IRequest<VisitAmendmentDecisionResponse>;

// ── History timeline (scoped, masked) ───────────────────────────────────────────

public sealed record GetVisitRequestHistoryQuery(ulong VisitRequestId)
    : IRequest<VisitRequestHistoryResponse>;

/// <summary>
/// One scoped timeline entry. Metadata only — no snapshot JSON, no tokens, no IP/UA, no full emails
/// (identity entries carry the MASKED email only).
///
/// The entry is STRUCTURED rather than pre-formatted: the backend states WHAT happened
/// (<see cref="EventCode"/> plus the few facts that event carries) and the frontend decides how to say
/// it. The previous shape shipped assembled Vietnamese titles with audit fragments glued on
/// ("source=CREATE;approvalRevision=1", "Cơ sở: REJECTED", "email=***;PENDING→APPLIED"), which was
/// untranslatable, leaked internal enum names to whoever opened the page, and made two campuses of the
/// same request produce identical lines.
/// </summary>
public sealed record VisitHistoryEntryDto(
    DateTime At,
    string EventCode,
    /// <summary>
    /// Opaque handle for GET …/history/{eventId}. Null when the event has no detail worth opening
    /// (a campus decision or a cancellation says everything it has to say on the timeline line
    /// itself) — the client hides the eye button rather than offering an empty drawer.
    /// </summary>
    string? EventId,
    ulong? VisitInstanceId,
    /// <summary>Campus of this entry — without it, multi-campus requests emit indistinguishable rows.</summary>
    string? CampusName,
    string? ActorName,
    uint? FormRevision,
    uint? ApprovalRevision,
    uint? AmendmentNo,
    /// <summary>Business status this event settled on (campus decision / amendment outcome).</summary>
    string? StatusCode,
    /// <summary>Why a revision was written (CREATE / SAFE_EDIT / AMENDMENT …).</summary>
    string? SourceType,
    string? Reason,
    string? MaskedEmail,
    string? FromStatus,
    string? ToStatus);

/// <summary>
/// The vocabulary the timeline speaks. Every value maps to one frontend sentence; a value the client
/// has not been taught renders as a neutral "other change" rather than as a raw code.
/// </summary>
public static class VisitHistoryEventCodes
{
    public const string RequestRevision = "REQUEST_REVISION";
    public const string RequestCreated = "REQUEST_CREATED";
    /// <summary>Request-level safe edit ("sửa nhanh") — the shared registrant/contact block changed.</summary>
    public const string RequestSafeEditApplied = "REQUEST_SAFE_EDIT_APPLIED";
    /// <summary>
    /// Request-level full edit of a still-pending request ("sửa đơn"). Distinct from the safe edit
    /// above: it happens before any campus has decided and is not limited to the safe field subset.
    /// </summary>
    public const string RequestPendingEditApplied = "REQUEST_PENDING_EDIT_APPLIED";
    /// <summary>The whole request was sent in again after a rejection.</summary>
    public const string RequestResubmitted = "REQUEST_RESUBMITTED";
    /// <summary>The whole request was cancelled (who, when, why).</summary>
    public const string RequestCancelled = "REQUEST_CANCELLED";
    public const string InstanceContentCreated = "INSTANCE_CONTENT_CREATED";
    public const string InstanceContentRevised = "INSTANCE_CONTENT_REVISED";
    /// <summary>Per-campus safe edit — the campus content changed without an amendment.</summary>
    public const string InstanceSafeEditApplied = "INSTANCE_SAFE_EDIT_APPLIED";
    /// <summary>Per-campus content rewritten by a full pending edit, before any campus decided.</summary>
    public const string InstancePendingEditApplied = "INSTANCE_PENDING_EDIT_APPLIED";
    /// <summary>Per-campus content re-sent as part of a request resubmit.</summary>
    public const string InstanceContentResubmitted = "INSTANCE_CONTENT_RESUBMITTED";
    /// <summary>An approved amendment was written into the campus's active content.</summary>
    public const string InstanceAmendmentApplied = "INSTANCE_AMENDMENT_APPLIED";
    public const string InstanceApproved = "INSTANCE_APPROVED";
    public const string InstanceRejected = "INSTANCE_REJECTED";
    /// <summary>
    /// A preauthorized host proposal auto-applied the moment the request's contact-confirmation gate
    /// opened (ProposedHostActivationService.ActivateAsync → CampusDecisionAudit.HostProposalActivated).
    /// Same decision fields as an approval (decided_by/decided_at/status → ASSIGNED, a host lands on
    /// the campus) but a DIFFERENT event: the review happened earlier, when the proposal was
    /// authorized, not by a Staff Leader looking at the campus live at this moment. Deliberately not
    /// <see cref="InstanceApproved"/> — collapsing the two would misattribute who reviewed the campus
    /// and when.
    /// </summary>
    public const string HostProposalActivated = "PROPOSED_HOST_ACTIVATED";
    public const string InstanceCancelled = "INSTANCE_CANCELLED";
    /// <summary>
    /// ASSIGNED → BEFORE_VISIT — the campus's Host opened the preparation stage. Read from
    /// StartVisitPreparationCommandHandler's immutable START_VISIT_PREPARATION audit. Distinct from
    /// <see cref="VisitStarted"/> below, which is the NEXT transition (BEFORE_VISIT → DURING_VISIT) —
    /// the two must never collapse into one event.
    /// </summary>
    public const string VisitPreparationStarted = "VISIT_PREPARATION_STARTED";
    /// <summary>
    /// BEFORE_VISIT → DURING_VISIT (Commit 4, Fix Group F) — the Host declared preparation complete
    /// and the visit itself began. Read from CompleteVisitStageCommandHandler's immutable
    /// COMPLETE_BEFORE_VISIT audit, never from the campus's current status.
    /// </summary>
    public const string VisitStarted = "VISIT_STARTED";
    /// <summary>
    /// DURING_VISIT → AFTER_VISIT (Commit 4, Fix Group F) — the Host declared the visit itself
    /// complete. Read from the immutable COMPLETE_DURING_VISIT audit.
    /// </summary>
    public const string VisitCompleted = "VISIT_COMPLETED";
    /// <summary>
    /// AFTER_VISIT → CLOSED (Commit 4, Fix Group F) — the Host closed the instance's record. Read
    /// from the immutable CLOSE_VISIT_INSTANCE audit. This constant predates Commit 4 but was never
    /// constructed anywhere until now.
    /// </summary>
    public const string InstanceClosed = "INSTANCE_CLOSED";
    public const string InstanceDecided = "INSTANCE_DECIDED";
    public const string AmendmentSubmitted = "AMENDMENT_SUBMITTED";
    public const string AmendmentApproved = "AMENDMENT_APPROVED";
    public const string AmendmentRejected = "AMENDMENT_REJECTED";
    public const string AmendmentWithdrawn = "AMENDMENT_WITHDRAWN";
    public const string AmendmentDecided = "AMENDMENT_DECIDED";
    /// <summary>
    /// Fallback for an identity transition the vocabulary below has no word for. Everything the
    /// workflow actually emits has its own code — rolling them all into this one told a reader that
    /// "vai trò đầu mối có thay đổi" whether an invitation had just been sent, resent, cancelled,
    /// accepted or had expired, which are five different things to do next about.
    /// </summary>
    public const string ContactIdentityChanged = "CONTACT_IDENTITY_CHANGED";
    /// <summary>An invitation went out to a campus that has no confirmed contact yet.</summary>
    public const string ContactInitialConfirmationCreated = "CONTACT_INITIAL_CONFIRMATION_CREATED";
    /// <summary>A confirmed contact asked somebody else to take the campus over.</summary>
    public const string ContactTransferRequested = "CONTACT_TRANSFER_REQUESTED";
    public const string ContactInvitationResent = "CONTACT_INVITATION_RESENT";
    public const string ContactInvitationCancelled = "CONTACT_INVITATION_CANCELLED";
    /// <summary>An older invitation was dropped because a newer one replaced it.</summary>
    public const string ContactInvitationSuperseded = "CONTACT_INVITATION_SUPERSEDED";
    /// <summary>The invited person accepted a FIRST confirmation — the campus now has its contact.</summary>
    public const string ContactConfirmed = "CONTACT_CONFIRMED";
    /// <summary>The invited person accepted a handover — the role moved.</summary>
    public const string ContactTransferAccepted = "CONTACT_TRANSFER_ACCEPTED";
    public const string ContactConfirmationDeclined = "CONTACT_CONFIRMATION_DECLINED";
    public const string ContactTransferDeclined = "CONTACT_TRANSFER_DECLINED";
    public const string ContactInvitationExpired = "CONTACT_INVITATION_EXPIRED";
    /// <summary>
    /// The SAME contact was asked again after their previous invitation lapsed WITHOUT an answer
    /// (cancelled, declined, or expired) — a brand new VisitRequestIdentityChange row, TokenVersion
    /// reset to 1. Distinct from ContactInvitationResent (same row, same TokenVersion chain, still
    /// live) and from ContactInitialConfirmationCreated (a genuinely new/different contact, via
    /// Replace or the original submit) — all three read as different sentences to a reader and must
    /// never collapse into one.
    /// </summary>
    public const string ContactReinvited = "CONTACT_REINVITED";
    /// <summary>
    /// A campus's operational-contact DETAILS (name/organization/title/phone) were corrected. The
    /// address, and so the person holding the role, did not change — never confuse this with a
    /// Contact* identity/role transition above.
    /// </summary>
    public const string ContactProfileUpdated = "CONTACT_PROFILE_UPDATED";
    /// <summary>
    /// A campus's contact's link to a delegation member changed (null→A / A→null / A→B) via Safe Edit's
    /// direct relation update (plan CanhIter3FixBug) — never an amendment.
    /// </summary>
    public const string ContactRelationUpdated = "CONTACT_RELATION_UPDATED";
    /// <summary>
    /// The operational contact was replaced by an address that immediately self-matched the
    /// registrant's own verified account — linked at once, no invitation, no separate confirmation
    /// event exists for this outcome (unlike the external-address branch, which is told in full by
    /// ContactInitialConfirmationCreated / ContactInvitationSuperseded above).
    /// </summary>
    public const string ContactReplacedWithRegistrant = "CONTACT_REPLACED_WITH_REGISTRANT";
    /// <summary>One campus's Host role was handed to a different user after approval.</summary>
    public const string HostTransferred = "HOST_TRANSFERRED";
}

public sealed record VisitRequestHistoryResponse(
    ulong VisitRequestId,
    string RequestCode,
    IReadOnlyList<VisitHistoryEntryDto> Entries);
