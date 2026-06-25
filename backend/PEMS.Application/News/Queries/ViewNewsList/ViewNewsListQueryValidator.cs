using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.News.Queries.ViewNewsList;

public sealed class ViewNewsListQueryValidator : AbstractValidator<ViewNewsListQuery>
{
    public ViewNewsListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.Status)
            .Must(BeValidStatus)
            .WithMessage("News status is invalid. Accepted: ALL, PENDING_REVIEW, REJECTED, PUBLISHED, HIDDEN.");

        RuleFor(x => x.SortBy)
            .Must(BeValidSortBy)
            .WithMessage("Sort field is invalid. Accepted: createdAt, reviewedAt.");

        RuleFor(x => x.SortDirection)
            .Must(BeValidSortDirection)
            .WithMessage("Sort direction is invalid. Accepted: asc, desc.");
    }

    private static bool BeValidStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = value.Trim();
        if (string.Equals(normalized, "ALL", StringComparison.OrdinalIgnoreCase)) return true;
        return NewsConstants.Status.All.Contains(normalized);
    }

    private static bool BeValidSortBy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return value.Trim() is "createdAt" or "reviewedAt";
    }

    private static bool BeValidSortDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return value.Trim().ToLowerInvariant() is "asc" or "desc";
    }
}
