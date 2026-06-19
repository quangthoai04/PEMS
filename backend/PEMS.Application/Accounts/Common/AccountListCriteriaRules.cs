using System;
using FluentValidation;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Shared FluentValidation rules for any <see cref="IAccountListCriteria"/> query.
/// Applied by both <c>ViewAccountListQueryValidator</c> and
/// <c>SearchandFilterAccountsQueryValidator</c>.
/// <para>
/// Note: <c>SortBy</c> is intentionally NOT validated here — the handler validates it
/// against the whitelist and throws <c>UNSUPPORTED_SORT_COLUMN</c> so the client gets a
/// precise, stable error code rather than a generic 400 validation payload.
/// </para>
/// </summary>
public static class AccountListCriteriaRules
{
    public static void Apply<T>(AbstractValidator<T> validator) where T : IAccountListCriteria
    {
        validator.RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        validator.RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        validator.RuleFor(x => x.Keyword)
            .MaximumLength(100)
            .WithMessage("Keyword must be at most 100 characters.");

        validator.RuleFor(x => x.SortDirection)
            .Must(d => string.IsNullOrWhiteSpace(d) ||
                       d.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                       d.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Sort direction must be 'asc' or 'desc'.");

        validator.RuleFor(x => x.AccountType)
            .Must(a => string.IsNullOrWhiteSpace(a) ||
                       a.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
                       a.Equals("INTERNAL", StringComparison.OrdinalIgnoreCase) ||
                       a.Equals("VISITOR", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Account type must be ALL, INTERNAL or VISITOR.");

        validator.RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate.Value <= x.ToDate.Value)
            .WithName("fromDate")
            .WithMessage("fromDate must be earlier than or equal to toDate.");

        validator.RuleFor(x => x)
            .Must(x => !x.LastLoginFrom.HasValue || !x.LastLoginTo.HasValue || x.LastLoginFrom.Value <= x.LastLoginTo.Value)
            .WithName("lastLoginFrom")
            .WithMessage("lastLoginFrom must be earlier than or equal to lastLoginTo.");
    }
}
