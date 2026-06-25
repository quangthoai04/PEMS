using FluentValidation;

namespace PEMS.Application.Delegations.Commands.UpdateRegistrantInfo;

public sealed class UpdateRegistrantInfoCommandValidator
    : AbstractValidator<UpdateRegistrantInfoCommand>
{
    public UpdateRegistrantInfoCommandValidator()
    {
        RuleFor(x => x.VisitRequestId)
            .GreaterThan(0ul).WithMessage("VisitRequestId không hợp lệ.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(255).WithMessage("Họ và tên quá dài.");

        RuleFor(x => x.Organization)
            .NotEmpty().WithMessage("Đơn vị công tác không được để trống.")
            .MaximumLength(255).WithMessage("Đơn vị công tác quá dài.");

        RuleFor(x => x.JobTitle)
            .MaximumLength(255).WithMessage("Chức danh quá dài.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .MaximumLength(30).WithMessage("Số điện thoại quá dài.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không hợp lệ.")
            .MaximumLength(255).WithMessage("Email quá dài.");
    }
}
