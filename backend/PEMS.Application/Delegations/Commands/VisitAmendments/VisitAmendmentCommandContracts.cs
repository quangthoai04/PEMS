using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using PEMS.Application.Common.DTOs;

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
            RuleFor(x => x.Patch.Registrant!.Phone).MaximumLength(50);
        });
        When(x => x.Patch?.Contact is not null, () =>
        {
            RuleFor(x => x.Patch.Contact!.FullName).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Patch.Contact!.Organization).MaximumLength(200);
            RuleFor(x => x.Patch.Contact!.Phone).NotEmpty().MaximumLength(50);
        });
        RuleForEach(x => x.Patch.Instances).ChildRules(i =>
        {
            i.RuleFor(p => p.TransportationNote)
                .MaximumLength(2000)
                .Must(n => string.IsNullOrEmpty(n) || (!n.Contains('<') && !n.Contains('>')))
                .WithMessage("Nhận diện phương tiện di chuyển không được chứa HTML/script.");
            i.RuleFor(p => p.NoteToFptu).MaximumLength(2000);
            i.RuleFor(p => p.MediaConsentNote).MaximumLength(2000);
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
            // Same per-campus content contract as create-v2: working content is required, the
            // operational contact must be complete, and every member row must be filled in. An
            // amendment REPLACES a campus's content, so it cannot be looser than the original create.
            RuleFor(x => x.Proposal.WorkingContent)
                .NotEmpty().WithMessage("Nội dung làm việc không được để trống.").MaximumLength(4000);
            RuleFor(x => x.Proposal.WorkingLanguage).Must(l => l is "EN" or "VI");
            RuleFor(x => x.Proposal.OperationalContact).NotNull();
            RuleFor(x => x.Proposal.OperationalContact!)
                .SetValidator(new CreateVisitRequestV2.OperationalContactV2Validator())
                .When(x => x.Proposal.OperationalContact is not null);
            RuleFor(x => x.Proposal.Reason).MaximumLength(500);
            RuleFor(x => x.Proposal.Visitors)
                .NotNull().Must(v => v.Count <= 200).WithMessage("Tối đa 200 khách mỗi cơ sở.");
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
    public const string InstanceCancelled = "INSTANCE_CANCELLED";
    public const string InstanceClosed = "INSTANCE_CLOSED";
    public const string InstanceDecided = "INSTANCE_DECIDED";
    public const string AmendmentSubmitted = "AMENDMENT_SUBMITTED";
    public const string AmendmentApproved = "AMENDMENT_APPROVED";
    public const string AmendmentRejected = "AMENDMENT_REJECTED";
    public const string AmendmentWithdrawn = "AMENDMENT_WITHDRAWN";
    public const string AmendmentDecided = "AMENDMENT_DECIDED";
    public const string ContactIdentityChanged = "CONTACT_IDENTITY_CHANGED";
}

public sealed record VisitRequestHistoryResponse(
    ulong VisitRequestId,
    string RequestCode,
    IReadOnlyList<VisitHistoryEntryDto> Entries);
