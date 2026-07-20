using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Validation;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

/// <summary>
/// STRUCTURAL (payload-shape) validation for authenticated create-v2. Runs in the MediatR
/// <c>ValidationBehaviour</c> pipeline BEFORE the handler, so a malformed payload never reaches the
/// service and never opens a transaction. This is a boundary guard only — <see cref="PEMS.Infrastructure.Services.VisitRequestV2CreateService"/>
/// STILL revalidates every DB/clock-dependent rule (campus existence/ACTIVE, Staff-Leader routing,
/// not-in-the-past, partner, min-duration) inside the transaction; the validator never replaces it.
///
/// System-derived fields (visitScope, hasMixedCampusDetails, formSchemaVersion, status/revision,
/// coordinator/approval state, visitorUserId) are NOT part of <see cref="VisitRequestFormDataV2"/>, so
/// "the client cannot send them" is enforced by the DTO shape itself — there is nothing to reject here.
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

        // ── Registrant (submitter) ──
        RuleFor(x => x.Registrant).NotNull().WithMessage("Thiếu thông tin người đăng ký.");
        When(x => x.Registrant is not null, () =>
        {
            RuleFor(x => x.Registrant.FullName)
                .NotEmpty().WithMessage("Họ tên người đăng ký không được để trống.").MaximumLength(150);
            RuleFor(x => x.Registrant.Organization)
                .NotEmpty().WithMessage("Đơn vị công tác không được để trống.").MaximumLength(200);
            RuleFor(x => x.Registrant.JobTitle)
                .NotEmpty().WithMessage("Chức vụ người đăng ký không được để trống.").MaximumLength(150);
            RuleFor(x => x.Registrant.Phone)
                .NotEmpty().WithMessage("Số điện thoại người đăng ký không được để trống.")
                .MustBeAPhoneNumber("Số điện thoại người đăng ký không hợp lệ.");
            RuleFor(x => x.Registrant.Nationality)
                .NotEmpty().WithMessage("Quốc tịch người đăng ký không được để trống.").MaximumLength(100);
            RuleFor(x => x.Registrant.Email)
                .NotEmpty().WithMessage("Email người đăng ký không được để trống.")
                .EmailAddress().WithMessage("Email người đăng ký không đúng định dạng.")
                .MaximumLength(150);
        });

        // ── Request-level primary contact (may be a different visitor → INITIAL_CLAIM) ──
        RuleFor(x => x.PrimaryContact).NotNull().WithMessage("Thiếu thông tin đầu mối liên hệ.");
        When(x => x.PrimaryContact is not null, () =>
        {
            RuleFor(x => x.PrimaryContact.FullName)
                .NotEmpty().WithMessage("Họ tên đầu mối liên hệ không được để trống.").MaximumLength(150);
            RuleFor(x => x.PrimaryContact.Email)
                .NotEmpty().WithMessage("Email đầu mối liên hệ không được để trống.")
                .EmailAddress().WithMessage("Email đầu mối liên hệ không đúng định dạng.")
                .MaximumLength(150);
            RuleFor(x => x.PrimaryContact.Phone)
                .NotEmpty().WithMessage("Số điện thoại đầu mối liên hệ không được để trống.")
                .MustBeAPhoneNumber("Số điện thoại đầu mối liên hệ không hợp lệ.");
            RuleFor(x => x.PrimaryContact.Organization)
                .NotEmpty().WithMessage("Đơn vị công tác đầu mối liên hệ không được để trống.").MaximumLength(200);
        });

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
            .NotEmpty().WithMessage("Tên đoàn không được để trống.").MaximumLength(200);
        RuleFor(c => c.VisitType)
            .NotEmpty().WithMessage("Loại hình tham quan không được để trống.")
            .Must(t => AllowedVisitTypes.Contains(t)).WithMessage("Loại hình tham quan không hợp lệ.");
        RuleFor(c => c.VisitTypeOther)
            .NotEmpty().WithMessage("Vui lòng ghi rõ loại hình tham quan khác.").MaximumLength(200)
            .When(c => c.VisitType == "OTHER");
        RuleFor(c => c.Purpose)
            .NotEmpty().WithMessage("Mục đích thăm không được để trống.").MaximumLength(2000);
        RuleFor(c => c.WorkingContent)
            .NotEmpty().WithMessage("Nội dung làm việc không được để trống.").MaximumLength(4000);

        // ── Per-campus operational (working) contact — a snapshot, never a login ──
        RuleFor(c => c.OperationalContact).NotNull().WithMessage("Thiếu đầu mối phối hợp của cơ sở.");
        When(c => c.OperationalContact is not null, () =>
        {
            RuleFor(c => c.OperationalContact.FullName)
                .NotEmpty().WithMessage("Họ tên đầu mối phối hợp không được để trống.").MaximumLength(150);
            RuleFor(c => c.OperationalContact.Organization)
                .NotEmpty().WithMessage("Đơn vị công tác đầu mối phối hợp không được để trống.").MaximumLength(200);
            RuleFor(c => c.OperationalContact.Phone)
                .NotEmpty().WithMessage("Số điện thoại đầu mối phối hợp không được để trống.")
                .MustBeAPhoneNumber("Số điện thoại đầu mối phối hợp không hợp lệ.");
            RuleFor(c => c.OperationalContact.Email)
                .NotEmpty().WithMessage("Email đầu mối phối hợp không được để trống.")
                .EmailAddress().WithMessage("Email đầu mối phối hợp không đúng định dạng.").MaximumLength(150);
        });

        // ── Additional per-campus requirements ──
        RuleFor(c => c.WorkingLanguage)
            .Must(l => l is "EN" or "VI").WithMessage("Ngôn ngữ làm việc phải là EN hoặc VI.");
        RuleFor(c => c.TransportationNote)
            .MaximumLength(2000).WithMessage("Nhận diện phương tiện di chuyển tối đa 2000 ký tự.")
            .Must(note => string.IsNullOrEmpty(note) || (!note.Contains('<') && !note.Contains('>')))
            .WithMessage("Nhận diện phương tiện di chuyển không được chứa HTML/script.");
        RuleFor(c => c.MediaConsentStatus)
            .NotEmpty().WithMessage("Trạng thái truyền thông không được để trống.")
            .Must(s => s is "AGREED" or "DECLINED").WithMessage("Trạng thái truyền thông không hợp lệ.");
        RuleFor(c => c.Notes).MaximumLength(2000);

        // ── People (per-campus, independent) ──
        RuleFor(c => c.Visitors)
            .NotNull().WithMessage("Danh sách khách không hợp lệ.")
            .Must(v => v is null || v.Count <= MaxMembers).WithMessage($"Tối đa {MaxMembers} khách mỗi cơ sở.");
        RuleForEach(c => c.Visitors).ChildRules(g =>
        {
            g.RuleFor(x => x.FullName).NotEmpty().WithMessage("Họ tên khách không được để trống.").MaximumLength(150);
            g.RuleFor(x => x.Nationality).NotEmpty().WithMessage("Quốc tịch khách không được để trống.").MaximumLength(100);
            g.RuleFor(x => x.Organization).NotEmpty().WithMessage("Đơn vị công tác khách không được để trống.").MaximumLength(200);
            g.RuleFor(x => x.JobTitle).NotEmpty().WithMessage("Chức vụ khách không được để trống.").MaximumLength(150);
        });

        RuleFor(c => c.ExternalSupportMembers)
            .NotNull().WithMessage("Danh sách nhân sự hỗ trợ không hợp lệ.")
            .Must(s => s is null || s.Count <= MaxMembers).WithMessage($"Tối đa {MaxMembers} nhân sự hỗ trợ mỗi cơ sở.");
        RuleForEach(c => c.ExternalSupportMembers).ChildRules(s =>
        {
            // The support list may be EMPTY, but a row that exists must be complete: the columns are
            // NOT NULL in visit_guest_members, so a half-filled row is a 500 at insert time, not a
            // validation message.
            s.RuleFor(x => x.FullName).NotEmpty().WithMessage("Họ tên nhân sự hỗ trợ không được để trống.").MaximumLength(150);
            s.RuleFor(x => x.JobTitle).NotEmpty().WithMessage("Chức vụ nhân sự hỗ trợ không được để trống.").MaximumLength(150);
            s.RuleFor(x => x.Organization).NotEmpty().WithMessage("Đơn vị công tác nhân sự hỗ trợ không được để trống.").MaximumLength(200);
            s.RuleFor(x => x.Nationality).NotEmpty().WithMessage("Quốc tịch nhân sự hỗ trợ không được để trống.").MaximumLength(100);
        });
    }
}
