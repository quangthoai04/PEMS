using FluentValidation;
using PEMS.Application.Common.Validation;

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

        // Phone is OPTIONAL, same as every other Registrant/Operational-Contact phone field in this
        // feature (see RegistrantInputV2Validator/OperationalContactV2Validator) — blank submits.
        RuleFor(x => x.Phone)
            .MustBeAPhoneNumber("Số điện thoại người đăng ký không hợp lệ.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không hợp lệ.")
            .MaximumLength(255).WithMessage("Email quá dài.");
    }
}
