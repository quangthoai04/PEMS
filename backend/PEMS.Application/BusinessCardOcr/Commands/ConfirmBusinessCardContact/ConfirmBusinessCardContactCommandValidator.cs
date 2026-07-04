using FluentValidation;

namespace PEMS.Application.BusinessCardOcr.Commands.ConfirmBusinessCardContact;

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
        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Email không hợp lệ.")
            .MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(50);
    }
}
