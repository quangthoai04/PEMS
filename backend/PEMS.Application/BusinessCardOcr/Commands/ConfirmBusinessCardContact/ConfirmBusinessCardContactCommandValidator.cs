using FluentValidation;

namespace PEMS.Application.BusinessCardOcr.Commands.ConfirmBusinessCardContact;

/// <summary>Same relaxed policy as CreatePartnerContactCommandValidator — see its doc comment. The
/// reviewer confirms the actual text read off the card; format-validating it here would reject real
/// foreign phone numbers and non-standard emails the card genuinely printed.</summary>
public sealed class ConfirmBusinessCardContactCommandValidator
    : AbstractValidator<ConfirmBusinessCardContactCommand>
{
    public ConfirmBusinessCardContactCommandValidator()
    {
        RuleFor(x => x.OcrJobId).GreaterThan(0UL);
        RuleFor(x => x.PartnerId).GreaterThan(0UL).WithMessage("Phải chọn đối tác.");
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên người liên hệ là bắt buộc.")
            .MaximumLength(150);
        RuleFor(x => x.Email).MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.JobTitle).MaximumLength(150);
        RuleFor(x => x.DepartmentName).MaximumLength(150);
    }
}
