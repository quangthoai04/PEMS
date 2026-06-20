using FluentValidation;
using PEMS.Application.Common.DTOs;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands;

/// <summary>
/// Shared FluentValidation rules for the public UC-17 visit-request form.
/// Applied by both the Initiate (send-OTP) and VerifyAndCreate (submit) validators
/// so the full payload is validated identically at both steps. Field limits mirror
/// the SQL v8.3 column definitions; campus existence/ACTIVE checks are business
/// validation and live in VisitRequestService.CreateAsync (they need the database).
/// </summary>
public static class VisitRequestFormValidationRules
{
    public static void ApplyVisitRequestFormRules<T>(this AbstractValidator<T> v)
        where T : IVisitRequestFormCommand
    {
        // ── Registrant ────────────────────────────────────────────────────────
        v.RuleFor(x => x.RegisterFullName)
            .NotEmpty().WithMessage("Họ tên người đăng ký không được để trống.")
            .MaximumLength(150);

        v.RuleFor(x => x.RegisterOrganization)
            .NotEmpty().WithMessage("Đơn vị công tác không được để trống.")
            .MaximumLength(200);

        v.RuleFor(x => x.RegisterJobTitle)
            .MaximumLength(150);

        v.RuleFor(x => x.RegisterPhone)
            .MaximumLength(50);

        v.RuleFor(x => x.RegisterEmail)
            .NotEmpty().WithMessage("Email người đăng ký không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(150);

        v.RuleFor(x => x.RegisterNationality)
            .MaximumLength(100);

        // ── Delegation / visit ────────────────────────────────────────────────
        v.RuleFor(x => x.DelegationName)
            .NotEmpty().WithMessage("Tên đoàn không được để trống.")
            .MaximumLength(200);

        v.RuleFor(x => x.VisitScope)
            .NotEmpty()
            .Must(s => s is VisitScopes.SingleCampus or VisitScopes.MultiCampus)
            .WithMessage("VisitScope phải là SINGLE_CAMPUS hoặc MULTI_CAMPUS.");

        v.RuleFor(x => x.Purpose)
            .NotEmpty().WithMessage("Mục đích thăm không được để trống.")
            .MaximumLength(2000);

        // ── Campus slots ──────────────────────────────────────────────────────
        v.RuleFor(x => x.VisitSlots)
            .NotEmpty().WithMessage("Phải có ít nhất 1 lịch thăm.");

        v.RuleFor(x => x.VisitSlots)
            .Must((cmd, slots) => HasValidCampusCount(cmd.VisitScope, slots))
            .WithMessage("SINGLE_CAMPUS phải chọn đúng 1 cơ sở; MULTI_CAMPUS phải chọn từ 2 cơ sở trở lên.")
            .When(x => x.VisitSlots is { Count: > 0 });

        v.RuleFor(x => x.VisitSlots)
            .Must(NoDuplicateCampus)
            .WithMessage("Không được chọn trùng cơ sở.")
            .When(x => x.VisitSlots is { Count: > 0 });

        v.RuleForEach(x => x.VisitSlots).ChildRules(slot =>
        {
            slot.RuleFor(s => s.CampusId)
                .NotEmpty().WithMessage("Vui lòng chọn cơ sở.");
            
            slot.RuleFor(s => s.StartDatetime)
                .Must(start => start >= DateTime.Now.AddHours(72))
                .WithMessage("Thời gian bắt đầu phải ít nhất 72 giờ so với thời điểm hiện tại.");

            slot.RuleFor(s => s)
                .Must(s => s.EndDatetime > s.StartDatetime)
                .WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");

            slot.RuleFor(s => s)
                .Must(s => (s.EndDatetime - s.StartDatetime).TotalHours >= 3)
                .WithMessage("Thời gian tham quan tối thiểu 3 giờ.");
        });

        // ── Guests ────────────────────────────────────────────────────────────
        v.RuleFor(x => x.Visitors)
            .NotEmpty().WithMessage("Phải có ít nhất 1 khách trong danh sách.");

        v.RuleForEach(x => x.Visitors).ChildRules(guest =>
        {
            guest.RuleFor(g => g.FullName)
                .NotEmpty().WithMessage("Họ tên khách không được để trống.")
                .MaximumLength(150);
            guest.RuleFor(g => g.Nationality)
                .MaximumLength(100);
            guest.RuleFor(g => g.Organization)
                .MaximumLength(200);
            guest.RuleFor(g => g.JobTitle)
                .MaximumLength(150);
            guest.RuleFor(g => g.Email)
                .NotEmpty().WithMessage("Email khách không được để trống.")
                .EmailAddress().WithMessage("Email khách không đúng định dạng.")
                .MaximumLength(150);
        });

        // ── Support team / contact ────────────────────────────────────────────
        v.RuleFor(x => x.SupportTeam)
            .NotEmpty().WithMessage("Phải có ít nhất 1 nhân sự hỗ trợ.");

        v.RuleFor(x => x.ContactPoint).NotNull();
        v.RuleFor(x => x.ContactPoint.FullName)
            .NotEmpty().WithMessage("Họ tên đầu mối liên hệ không được để trống.")
            .When(x => x.ContactPoint is not null);
        v.RuleFor(x => x.ContactPoint.Email)
            .NotEmpty().WithMessage("Email đầu mối liên hệ không được để trống.")
            .EmailAddress().WithMessage("Email đầu mối liên hệ không đúng định dạng.")
            .When(x => x.ContactPoint is not null);
        v.RuleFor(x => x.ContactPoint.Phone)
            .NotEmpty().WithMessage("Số điện thoại đầu mối liên hệ không được để trống.")
            .When(x => x.ContactPoint is not null);

        // ── Additional ────────────────────────────────────────────────────────
        v.RuleFor(x => x.Language)
            .Must(l => l is WorkingLanguages.English or WorkingLanguages.Vietnamese or WorkingLanguages.Other)
            .WithMessage("Ngôn ngữ phải là EN, VI hoặc OTHER.");
    }

    /// <summary>Distinct (case-insensitive) campus count must match the visit scope.</summary>
    private static bool HasValidCampusCount(string? scope, IList<VisitSlotDto> slots)
    {
        var distinct = DistinctCampusCodes(slots).Count;
        return scope == VisitScopes.MultiCampus ? distinct >= 2 : distinct == 1;
    }

    /// <summary>No campus code may appear twice (mirrors the SQL unique key on request+campus).</summary>
    private static bool NoDuplicateCampus(IList<VisitSlotDto> slots)
    {
        var codes = slots
            .Where(s => !string.IsNullOrWhiteSpace(s.CampusId))
            .Select(s => s.CampusId.Trim().ToUpperInvariant())
            .ToList();
        return codes.Count == codes.Distinct().Count();
    }

    private static List<string> DistinctCampusCodes(IList<VisitSlotDto> slots)
        => slots
            .Where(s => !string.IsNullOrWhiteSpace(s.CampusId))
            .Select(s => s.CampusId.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
}
