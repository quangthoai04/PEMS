using FluentValidation;
using PEMS.Domain.Constants;
using System.Linq;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

public class ViewGuestDelegationListQueryValidator : AbstractValidator<ViewGuestDelegationListQuery>
{
    public ViewGuestDelegationListQueryValidator()
    {
        RuleFor(x => x.VisitScope)
            .Must(scope => string.IsNullOrWhiteSpace(scope) || 
                           scope == VisitScopes.SingleCampus || 
                           scope == VisitScopes.MultiCampus)
            .WithMessage("Phạm vi đơn (VisitScope) không hợp lệ.");

        RuleFor(x => x.VisitScopes)
            .Must(scopes => string.IsNullOrWhiteSpace(scopes) || 
                            scopes.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                                  .Select(s => s.Trim())
                                  .All(s => s == VisitScopes.SingleCampus || s == VisitScopes.MultiCampus))
            .WithMessage("Một hoặc nhiều Phạm vi đơn (VisitScopes) không hợp lệ.");
    }
}
