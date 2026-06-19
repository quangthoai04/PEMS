using FluentValidation;
using PEMS.Application.Accounts.Common;

namespace PEMS.Application.Accounts.Queries.SearchandFilterAccounts;

/// <summary>Input validation for UC-99. Shared rules live in <see cref="AccountListCriteriaRules"/>.</summary>
public sealed class SearchandFilterAccountsQueryValidator : AbstractValidator<SearchandFilterAccountsQuery>
{
    public SearchandFilterAccountsQueryValidator() => AccountListCriteriaRules.Apply(this);
}
