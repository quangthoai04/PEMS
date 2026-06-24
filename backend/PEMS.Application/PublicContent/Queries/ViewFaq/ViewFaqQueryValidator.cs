using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.PublicContent.Queries.ViewFAQ;

public sealed class ViewFaqQueryValidator : AbstractValidator<ViewFaqQuery>
{
    public ViewFaqQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.FaqType)
            .Must(BeValidFaqType)
            .WithMessage("FAQ type is invalid.");
    }

    private static bool BeValidFaqType(string? faqType)
    {
        if (string.IsNullOrWhiteSpace(faqType))
            return true;

        var normalized = faqType.Trim();

        if (string.Equals(normalized, "ALL", StringComparison.OrdinalIgnoreCase))
            return true;

        return FaqConstants.Type.All.Contains(normalized);
    }
}
