using FluentValidation;

namespace PEMS.Application.Partners.Contacts.Commands.CreatePartnerContact;

/// <summary>
/// Partner Contact is external business-card/partner-supplied data, not an authentication or identity
/// field (plan CanhIter3FixBug "Partner Contact / Business Card Data Capture"). Email/Phone are
/// intentionally NOT format-validated here — only length-bounded to the actual DB columns
/// (partner_contacts.email VARCHAR(150), .phone VARCHAR(50)) — so a real foreign phone/email as printed
/// on a card (extensions, local formats, non-ASCII) is never rejected. FullName stays required; PartnerId
/// stays a real scope check. This exception is documented and covered by
/// PhoneValidatorDiscoveryTests/EmailValidatorDiscoveryTests — do not "fix" it back to
/// MustBeAPhoneNumber()/EmailAddress() without re-reading that plan.
/// </summary>
public sealed class CreatePartnerContactCommandValidator : AbstractValidator<CreatePartnerContactCommand>
{
    public CreatePartnerContactCommandValidator()
    {
        RuleFor(x => x.PartnerId).GreaterThan(0UL);
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên người liên hệ là bắt buộc.")
            .MaximumLength(150);
        RuleFor(x => x.Email).MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.JobTitle).MaximumLength(150);
        RuleFor(x => x.DepartmentName).MaximumLength(150);
    }
}
