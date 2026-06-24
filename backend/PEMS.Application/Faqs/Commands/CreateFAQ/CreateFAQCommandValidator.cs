using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.Faqs.Commands.CreateFAQ;

public sealed class CreateFAQCommandValidator : AbstractValidator<CreateFAQCommand>
{
    public CreateFAQCommandValidator()
    {
        RuleFor(x => x.FaqType)
            .NotEmpty().WithMessage("FAQ type is required.")
            .Must(t => FaqConstants.Type.All.Contains(t?.Trim() ?? string.Empty))
            .WithMessage("FAQ type is invalid.");

        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Question is required.")
            .MaximumLength(500).WithMessage("Question must not exceed 500 characters.");

        RuleFor(x => x.Answer)
            .NotEmpty().WithMessage("Answer is required.");

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) ||
                       s.Trim() == FaqConstants.Status.Published ||
                       s.Trim() == FaqConstants.Status.Hidden)
            .WithMessage("FAQ status is invalid. Accepted: PUBLISHED, HIDDEN.");
    }
}
