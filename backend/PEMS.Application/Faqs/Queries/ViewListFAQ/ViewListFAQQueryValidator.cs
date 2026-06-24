using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.Faqs.Queries.ViewListFAQ;

public sealed class ViewListFAQQueryValidator : AbstractValidator<ViewListFAQQuery>
{
    public ViewListFAQQueryValidator()
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

        RuleFor(x => x.Status)
            .Must(BeValidStatus)
            .WithMessage("FAQ status is invalid. Accepted values: PUBLISHED, HIDDEN, ALL.");

        RuleFor(x => x.SortBy)
            .Must(BeValidSortBy)
            .WithMessage("Sort field is invalid. Accepted values: createdAt, displayOrder.");

        RuleFor(x => x.SortDirection)
            .Must(BeValidSortDirection)
            .WithMessage("Sort direction is invalid. Accepted values: asc, desc.");
    }

    private static bool BeValidFaqType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = value.Trim();
        if (string.Equals(normalized, "ALL", StringComparison.OrdinalIgnoreCase)) return true;
        return FaqConstants.Type.All.Contains(normalized);
    }

    private static bool BeValidStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = value.Trim();
        if (string.Equals(normalized, "ALL", StringComparison.OrdinalIgnoreCase)) return true;
        return normalized == FaqConstants.Status.Published || normalized == FaqConstants.Status.Hidden;
    }

    private static bool BeValidSortBy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return value.Trim() is "createdAt" or "displayOrder";
    }

    private static bool BeValidSortDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return value.Trim().ToLowerInvariant() is "asc" or "desc";
    }
}
