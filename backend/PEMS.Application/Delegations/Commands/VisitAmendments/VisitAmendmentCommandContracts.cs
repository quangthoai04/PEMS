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
            i.RuleFor(p => p.MediaConsentStatus)
                .Must(s => s is "AGREED" or "DECLINED")
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

/// <summary>One scoped timeline entry. Metadata only — no snapshot JSON, no tokens, no IP/UA, no full
/// emails (identity entries carry the masked email in <c>Detail</c>).</summary>
public sealed record VisitHistoryEntryDto(
    DateTime At,
    string Kind,            // REQUEST_REVISION | INSTANCE_REVISION | AMENDMENT | AMENDMENT_DECISION | IDENTITY | DECISION
    ulong? VisitInstanceId,
    string Title,
    string? Detail,
    string? ActorName);

public sealed record VisitRequestHistoryResponse(
    ulong VisitRequestId,
    string RequestCode,
    IReadOnlyList<VisitHistoryEntryDto> Entries);
