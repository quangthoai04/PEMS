using FluentValidation;
using PEMS.Application.Accounts.Common;

namespace PEMS.Application.Accounts.Queries.ViewAccountList;

/// <summary>Input validation for UC-95. Shared rules live in <see cref="AccountListCriteriaRules"/>.</summary>
public sealed class ViewAccountListQueryValidator : AbstractValidator<ViewAccountListQuery>
{
    public ViewAccountListQueryValidator() => AccountListCriteriaRules.Apply(this);
}
