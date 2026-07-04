using FluentValidation;

namespace PEMS.Application.Partners.Contacts.Commands.UpdatePartnerContact;

public sealed class UpdatePartnerContactCommandValidator : AbstractValidator<UpdatePartnerContactCommand>
{
    public UpdatePartnerContactCommandValidator()
    {
        RuleFor(x => x.PartnerId).GreaterThan(0UL);
        RuleFor(x => x.ContactId).GreaterThan(0UL);
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên người liên hệ là bắt buộc.")
            .MaximumLength(150);
        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Email không hợp lệ.")
            .MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(50);
    }
}
