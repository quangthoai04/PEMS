using FluentValidation;

namespace PEMS.Application.Faqs.Commands.ChangeFAQVisibility;

public sealed class ChangeFAQVisibilityCommandValidator : AbstractValidator<ChangeFAQVisibilityCommand>
{
    public ChangeFAQVisibilityCommandValidator()
    {
        RuleFor(x => x.FaqId)
            .GreaterThan(0ul)
            .WithMessage("FaqId phải lớn hơn 0.");
    }
}