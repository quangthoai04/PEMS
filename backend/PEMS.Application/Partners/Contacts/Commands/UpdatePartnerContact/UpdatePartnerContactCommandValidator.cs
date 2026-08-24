using FluentValidation;

namespace PEMS.Application.Partners.Contacts.Commands.UpdatePartnerContact;

/// <summary>Same relaxed policy as CreatePartnerContactCommandValidator — see its doc comment. Create and
/// Update must be symmetric: a value Create accepts must not become rejected on Update.</summary>
public sealed class UpdatePartnerContactCommandValidator : AbstractValidator<UpdatePartnerContactCommand>
{
    public UpdatePartnerContactCommandValidator()
    {
        RuleFor(x => x.PartnerId).GreaterThan(0UL);
        RuleFor(x => x.ContactId).GreaterThan(0UL);
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên người liên hệ là bắt buộc.")
            .MaximumLength(150);
        RuleFor(x => x.Email).MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.JobTitle).MaximumLength(150);
        RuleFor(x => x.DepartmentName).MaximumLength(150);
    }
}
