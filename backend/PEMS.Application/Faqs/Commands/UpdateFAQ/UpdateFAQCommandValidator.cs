using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.Faqs.Commands.UpdateFAQ;

public sealed class UpdateFAQCommandValidator : AbstractValidator<UpdateFAQCommand>
{
    public UpdateFAQCommandValidator()
    {
        RuleFor(x => x.FaqId)
            .GreaterThan((ulong)0).WithMessage("FAQ ID is required.");

        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Question is required.")
            .MaximumLength(500).WithMessage("Question must not exceed 500 characters.");

        RuleFor(x => x.Answer)
            .NotEmpty().WithMessage("Answer is required.");
    }
}
