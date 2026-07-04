using FluentValidation;

namespace PEMS.Application.Partners.Contacts.Commands.CreatePartnerContact;

public sealed class CreatePartnerContactCommandValidator : AbstractValidator<CreatePartnerContactCommand>
{
    public CreatePartnerContactCommandValidator()
    {
        RuleFor(x => x.PartnerId).GreaterThan(0UL);
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên người liên hệ là bắt buộc.")
            .MaximumLength(150);
        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Email không hợp lệ.")
            .MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.JobTitle).MaximumLength(150);
        RuleFor(x => x.DepartmentName).MaximumLength(150);
    }
}
