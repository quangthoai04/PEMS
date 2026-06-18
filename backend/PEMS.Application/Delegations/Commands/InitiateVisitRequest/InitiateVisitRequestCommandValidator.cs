using FluentValidation;

namespace PEMS.Application.Delegations.Commands.InitiateVisitRequest;

public sealed class InitiateVisitRequestCommandValidator : AbstractValidator<InitiateVisitRequestCommand>
{
    public InitiateVisitRequestCommandValidator()
    {
        RuleFor(x => x.RegisterFullName)
            .NotEmpty().WithMessage("Họ tên người đăng ký không được để trống.")
            .MaximumLength(100);

        RuleFor(x => x.RegisterEmail)
            .NotEmpty().WithMessage("Email người đăng ký không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.RegisterPhone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .MaximumLength(30);

        RuleFor(x => x.RegisterOrganization)
            .NotEmpty().WithMessage("Đơn vị công tác không được để trống.")
            .MaximumLength(200);

        RuleFor(x => x.RegisterJobTitle)
            .NotEmpty().WithMessage("Chức danh không được để trống.")
            .MaximumLength(100);

        RuleFor(x => x.RegisterNationality)
            .NotEmpty().WithMessage("Quốc tịch không được để trống.");

        RuleFor(x => x.DelegationName)
            .NotEmpty().WithMessage("Tên đoàn không được để trống.")
            .MaximumLength(200);

        RuleFor(x => x.VisitScope)
            .NotEmpty()
            .Must(s => s is "SINGLE_CAMPUS" or "MULTI_CAMPUS")
            .WithMessage("VisitScope phải là SINGLE_CAMPUS hoặc MULTI_CAMPUS.");

        RuleFor(x => x.VisitSlots)
            .NotEmpty().WithMessage("Phải có ít nhất 1 lịch thăm.")
            .Must(slots => slots.All(s =>
                !string.IsNullOrWhiteSpace(s.CampusId) &&
                s.StartDatetime < s.EndDatetime))
            .WithMessage("Thông tin lịch thăm không hợp lệ.");

        RuleFor(x => x.Purpose)
            .NotEmpty().WithMessage("Mục đích thăm không được để trống.")
            .MaximumLength(500);

        RuleFor(x => x.Visitors)
            .NotEmpty().WithMessage("Phải có ít nhất 1 khách trong danh sách.");

        RuleForEach(x => x.Visitors).ChildRules(v =>
        {
            v.RuleFor(g => g.FullName).NotEmpty().WithMessage("Họ tên khách không được để trống.");
            v.RuleFor(g => g.PassportId).NotEmpty().WithMessage("Số hộ chiếu/CMND không được để trống.");
            v.RuleFor(g => g.Email).EmailAddress().WithMessage("Email khách không đúng định dạng.");
            v.RuleFor(g => g.Nationality).NotEmpty().WithMessage("Quốc tịch khách không được để trống.");
        });

        RuleFor(x => x.SupportTeam)
            .NotEmpty().WithMessage("Phải có ít nhất 1 nhân sự hỗ trợ.");

        RuleFor(x => x.ContactPoint).NotNull();
        RuleFor(x => x.ContactPoint.FullName)
            .NotEmpty().WithMessage("Họ tên đầu mối liên hệ không được để trống.");
        RuleFor(x => x.ContactPoint.Email)
            .EmailAddress().WithMessage("Email đầu mối liên hệ không đúng định dạng.");
        RuleFor(x => x.ContactPoint.Phone)
            .NotEmpty().WithMessage("Số điện thoại đầu mối liên hệ không được để trống.");

        RuleFor(x => x.Language)
            .Must(l => l is "EN" or "VI")
            .WithMessage("Ngôn ngữ phải là EN hoặc VI.");
    }
}
