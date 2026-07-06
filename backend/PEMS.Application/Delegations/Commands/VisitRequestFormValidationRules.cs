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
    // minStartAdvanceHours: 72h for a brand-new submit (UC-17); the Visitor edit/resubmit
    // flow only requires 24h (spec "Visitor sửa đơn / gửi lại / hủy trước 24h").
    public static void ApplyVisitRequestFormRules<T>(this AbstractValidator<T> v, int minStartAdvanceHours = 72)
        where T : IVisitRequestFormCommand
    {
        // ── Registrant ────────────────────────────────────────────────────────
        v.RuleFor(x => x.RegistrantFullName)
            .NotEmpty().WithMessage("Họ tên người đăng ký không được để trống.")
            .MaximumLength(150);

        v.RuleFor(x => x.RegistrantOrganization)
            .NotEmpty().WithMessage("Đơn vị công tác không được để trống.")
            .MaximumLength(200);

        v.RuleFor(x => x.RegistrantPosition)
            .MaximumLength(150);

        v.RuleFor(x => x.RegistrantPhone)
            .MaximumLength(50);

        v.RuleFor(x => x.RegistrantEmail)
            .NotEmpty().WithMessage("Email người đăng ký không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(150);

        v.RuleFor(x => x.RegistrantNationality)
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
        v.RuleFor(x => x.CampusVisits)
            .NotEmpty().WithMessage("Phải có ít nhất 1 lịch thăm.");

        v.RuleFor(x => x.CampusVisits)
            .Must((cmd, slots) => HasValidCampusCount(cmd.VisitScope, slots))
            .WithMessage("SINGLE_CAMPUS phải chọn đúng 1 cơ sở; MULTI_CAMPUS phải chọn từ 2 cơ sở trở lên.")
            .When(x => x.CampusVisits is { Count: > 0 });

        v.RuleFor(x => x.CampusVisits)
            .Must(NoDuplicateCampus)
            .WithMessage("Không được chọn trùng cơ sở.")
            .When(x => x.CampusVisits is { Count: > 0 });

        v.RuleForEach(x => x.CampusVisits).ChildRules(slot =>
        {
            slot.RuleFor(s => s.CampusId)
                .NotEmpty().WithMessage("Vui lòng chọn cơ sở.");
            
            slot.RuleFor(s => s.StartDatetime)
                .Must(start => start >= DateTime.Now.AddHours(minStartAdvanceHours))
                .WithMessage($"Thời gian bắt đầu phải ít nhất {minStartAdvanceHours} giờ so với thời điểm hiện tại.");

            slot.RuleFor(s => s)
                .Must(s => s.EndDatetime > s.StartDatetime)
                .WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");

            slot.RuleFor(s => s)
                .Must(s => (s.EndDatetime - s.StartDatetime).TotalHours >= 3)
                .WithMessage("Thời gian tham quan tối thiểu 3 giờ.");
        });


        // ── Guests ────────────────────────────────────────────────────────────
        v.RuleForEach(x => x.Visitors).ChildRules(guest =>
        {
            guest.RuleFor(g => g.FullName)
                .NotEmpty().WithMessage("Họ tên khách không được để trống.")
                .MaximumLength(150);
            guest.RuleFor(g => g.Nationality)
                .NotEmpty().WithMessage("Quốc tịch khách không được để trống.")
                .MaximumLength(100);
            guest.RuleFor(g => g.Organization)
                .NotEmpty().WithMessage("Đơn vị công tác khách không được để trống.")
                .MaximumLength(200);
            guest.RuleFor(g => g.JobTitle)
                .NotEmpty().WithMessage("Chức vụ khách không được để trống.")
                .MaximumLength(150);
        });

        // ── Support team / contact ────────────────────────────────────────────
        v.RuleForEach(x => x.SupportMembers).ChildRules(support =>
        {
            support.RuleFor(s => s.FullName)
                .NotEmpty().WithMessage("Họ tên nhân sự hỗ trợ không được để trống.")
                .MaximumLength(150);
            support.RuleFor(s => s.JobTitle)
                .MaximumLength(150);
            support.RuleFor(s => s.Organization)
                .MaximumLength(200);
            support.RuleFor(s => s.Nationality)
                .MaximumLength(100);
        });

        v.RuleFor(x => x.ContactPerson).NotNull();
        v.RuleFor(x => x.ContactPerson.FullName)
            .NotEmpty().WithMessage("Họ tên đầu mối liên hệ không được để trống.")
            .When(x => x.ContactPerson is not null);
        v.RuleFor(x => x.ContactPerson.Email)
            .NotEmpty().WithMessage("Email đầu mối liên hệ không được để trống.")
            .EmailAddress().WithMessage("Email đầu mối liên hệ không đúng định dạng.")
            .When(x => x.ContactPerson is not null);
        v.RuleFor(x => x.ContactPerson.Phone)
            .NotEmpty().WithMessage("Số điện thoại đầu mối liên hệ không được để trống.")
            .When(x => x.ContactPerson is not null);

        // ── Additional ────────────────────────────────────────────────────────
        v.RuleFor(x => x.VisitType)
            .NotEmpty().WithMessage("Loại hình tham quan không được để trống.")
            .Must(t => t is "CAMPUS_TOUR" or "MEETING" or "WORKSHOP" or "SIGNING_CEREMONY" or "EXCHANGE" or "OTHER")
            .WithMessage("Loại hình tham quan không hợp lệ.");

        v.RuleFor(x => x.VisitTypeOther)
            .NotEmpty().WithMessage("Vui lòng ghi rõ loại hình tham quan khác.")
            .When(x => x.VisitType == "OTHER");

        v.RuleFor(x => x.WorkingLanguage)
            .Must(l => l is "EN" or "VI")
            .WithMessage("Ngôn ngữ làm việc phải là EN hoặc VI.");

        // Transportation is free text now (transportation_note): optional, bounded, no HTML/script.
        v.RuleFor(x => x.TransportationNote)
            .MaximumLength(2000).WithMessage("Nhận diện phương tiện di chuyển tối đa 2000 ký tự.")
            .Must(note => string.IsNullOrEmpty(note) || (!note.Contains('<') && !note.Contains('>')))
            .WithMessage("Nhận diện phương tiện di chuyển không được chứa HTML/script.");

        v.RuleFor(x => x.MediaConsentStatus)
            .NotEmpty().WithMessage("Trạng thái truyền thông không được để trống.")
            .Must(s => s is "AGREED" or "DECLINED")
            .WithMessage("Trạng thái truyền thông không hợp lệ.");
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
