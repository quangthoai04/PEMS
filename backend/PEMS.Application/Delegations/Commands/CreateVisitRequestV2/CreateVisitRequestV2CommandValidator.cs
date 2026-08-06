using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Validation;
using PEMS.Domain.Constants;
using static PEMS.Application.Delegations.Commands.CreateVisitRequestV2.LengthMessages;

namespace PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

/// <summary>
/// STRUCTURAL (payload-shape) validation for authenticated create-v2. Runs in the MediatR
/// <c>ValidationBehaviour</c> pipeline BEFORE the handler, so a malformed payload never reaches the
/// service and never opens a transaction. This is a boundary guard only — <see cref="PEMS.Infrastructure.Services.VisitRequestV2CreateService"/>
/// STILL revalidates every DB/clock-dependent rule (campus existence/ACTIVE, Staff-Leader routing,
/// not-in-the-past, partner, min-duration) inside the transaction; the validator never replaces it.
///
/// Every create carries a per-campus payload: one entry per campus, each with its own form content, which
/// the service turns into that campus's detail row. There is no schema discriminator and no alternative
/// request-level shape to choose between.
///
/// System-derived fields (visitScope, hasMixedCampusDetails, status/revision, coordinator/approval state,
/// visitorUserId) are NOT part of <see cref="VisitRequestFormDataV2"/>, so "the client cannot send them"
/// is enforced by the DTO shape itself — there is nothing to reject here.
/// </summary>
public sealed class CreateVisitRequestV2CommandValidator : AbstractValidator<CreateVisitRequestV2Command>
{
    public CreateVisitRequestV2CommandValidator()
    {
        RuleFor(x => x.Form).NotNull().WithMessage("Thiếu dữ liệu biểu mẫu.");
        RuleFor(x => x.Form).SetValidator(new VisitRequestFormDataV2Validator()!).When(x => x.Form is not null);
    }
}

/// <summary>Structural rules for the whole v2 payload (request-level fields + the campus collection).</summary>
public sealed class VisitRequestFormDataV2Validator : AbstractValidator<VisitRequestFormDataV2>
{
    // Structural collection ceilings — defence-in-depth against oversized payloads; the DB has its own
    // per-request campus uniqueness. Kept generous so legitimate delegations are never blocked.
    private const int MaxCampuses = 10;

    public VisitRequestFormDataV2Validator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Thiếu submissionId.")
            .MaximumLength(100);

        // ── Registrant — the SAME shared child validator used by pending-edit-v2 and resubmit-v2,
        //    so the three write paths cannot drift. The contact is validated per campus below:
        //    there is no request-level one to check here. ──
        RuleFor(x => x.Registrant).NotNull().WithMessage("Thiếu thông tin người đăng ký.");
        RuleFor(x => x.Registrant!).SetValidator(new RegistrantInputV2Validator()).When(x => x.Registrant is not null);

        // ── Campus collection ──
        RuleFor(x => x.CampusVisits)
            .NotEmpty().WithMessage("Phải có ít nhất 1 cơ sở.");
        RuleFor(x => x.CampusVisits)
            .Must(cs => cs is null || cs.Count <= MaxCampuses)
            .WithMessage($"Không được vượt quá {MaxCampuses} cơ sở trong một đơn.");
        RuleFor(x => x.CampusVisits)
            .Must(NoDuplicateCampus).WithMessage("Không được chọn trùng cơ sở.")
            .When(x => x.CampusVisits is { Count: > 0 });

        RuleForEach(x => x.CampusVisits).SetValidator(new CampusVisitFormDtoValidator());
    }

    private static bool NoDuplicateCampus(IList<CampusVisitFormDto>? campuses)
    {
        if (campuses is null) return true;
        var codes = campuses
            .Where(c => !string.IsNullOrWhiteSpace(c.CampusId))
            .Select(c => c.CampusId.Trim().ToUpperInvariant())
            .ToList();
        return codes.Count == codes.Distinct().Count();
    }
}

/// <summary>Structural rules for ONE fully-resolved campus snapshot (v2 sends every campus independently).</summary>
public sealed class CampusVisitFormDtoValidator : AbstractValidator<CampusVisitFormDto>
{
    // Must match VisitRequestV2CreateService.MinDurationMinutes — the validator is not stricter than the service.
    private const int MinDurationMinutes = 30;
    private const int MaxMembers = 200;

    private static readonly HashSet<string> AllowedVisitTypes = new(StringComparer.Ordinal)
    {
        "CAMPUS_TOUR", "MEETING", "WORKSHOP", "SIGNING_CEREMONY", "EXCHANGE", "OTHER",
    };

    public CampusVisitFormDtoValidator()
    {
        RuleFor(c => c.CampusId).NotEmpty().WithMessage("Vui lòng chọn cơ sở.").MaximumLength(50);

        // Clock-independent schedule shape only; the service revalidates "not in the past" against VietnamNow.
        RuleFor(c => c)
            .Must(c => c.PlannedEndAt > c.PlannedStartAt)
            .WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");
        RuleFor(c => c)
            .Must(c => (c.PlannedEndAt - c.PlannedStartAt).TotalMinutes >= MinDurationMinutes)
            .WithMessage("Mỗi buổi thăm phải kéo dài tối thiểu 30 phút.");

        // ── Per-campus visit content ──
        RuleFor(c => c.DelegationName)
            .NotEmpty().WithMessage("Tên đoàn không được để trống.")
            .MaximumLength(200).WithMessage(TooLong("Tên đoàn", 200));
        RuleFor(c => c.VisitType)
            .NotEmpty().WithMessage("Loại hình tham quan không được để trống.")
            .Must(t => AllowedVisitTypes.Contains(t)).WithMessage("Loại hình tham quan không hợp lệ.");
        RuleFor(c => c.VisitTypeOther)
            .NotEmpty().WithMessage("Vui lòng ghi rõ loại hình tham quan khác.")
            .MaximumLength(200).WithMessage(TooLong("Loại hình tham quan khác", 200))
            .When(c => c.VisitType == "OTHER");
        RuleFor(c => c.Purpose)
            .NotEmpty().WithMessage("Mục đích thăm không được để trống.")
            .MaximumLength(2000).WithMessage(TooLong("Mục đích thăm", 2000));
        RuleFor(c => c.WorkingContent)
            .NotEmpty().WithMessage("Nội dung làm việc không được để trống.")
            .MaximumLength(4000).WithMessage(TooLong("Nội dung làm việc", 4000));

        // ── Per-campus operational (working) contact — a snapshot, never a login ──
        RuleFor(c => c.OperationalContact).NotNull().WithMessage("Thiếu đầu mối phối hợp của cơ sở.");
        RuleFor(c => c.OperationalContact!).SetValidator(new OperationalContactV2Validator())
            .When(c => c.OperationalContact is not null);

        // ── Additional per-campus requirements ──
        RuleFor(c => c.WorkingLanguage)
            .Must(l => l is "EN" or "VI").WithMessage("Ngôn ngữ làm việc phải là EN hoặc VI.");
        RuleFor(c => c.TransportationNote)
            .MaximumLength(2000).WithMessage(TooLong("Nhận diện phương tiện di chuyển", 2000))
            .Must(note => string.IsNullOrEmpty(note) || (!note.Contains('<') && !note.Contains('>')))
            .WithMessage("Nhận diện phương tiện di chuyển không được chứa HTML/script.");
        RuleFor(c => c.MediaConsentStatus)
            .NotEmpty().WithMessage("Trạng thái truyền thông không được để trống.")
            .Must(s => s is "AGREED" or "DECLINED").WithMessage("Trạng thái truyền thông không hợp lệ.");
        // The form has always capped this at 2000; the server never did, so a direct call could
        // store a note the edit screen would then refuse to save back.
        RuleFor(c => c.MediaConsentNote)
            .MaximumLength(2000).WithMessage(TooLong("Ghi chú truyền thông", 2000));

        // ── People (per-campus, independent) ──
        RuleFor(c => c.Visitors)
            .NotNull().WithMessage("Danh sách khách không hợp lệ.")
            .Must(v => v is null || v.Count <= MaxMembers).WithMessage($"Tối đa {MaxMembers} khách mỗi cơ sở.");
        RuleForEach(c => c.Visitors).SetValidator(new VisitorV2Validator());

        RuleFor(c => c.ExternalSupportMembers)
            .NotNull().WithMessage("Danh sách nhân sự hỗ trợ không hợp lệ.")
            .Must(s => s is null || s.Count <= MaxMembers).WithMessage($"Tối đa {MaxMembers} nhân sự hỗ trợ mỗi cơ sở.");
        RuleForEach(c => c.ExternalSupportMembers).SetValidator(new SupportTeamMemberV2Validator());
    }
}
