using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.Campuses.Queries.GetCampusStatusImpact;

public sealed class GetCampusStatusImpactQueryValidator : AbstractValidator<GetCampusStatusImpactQuery>
{
    public GetCampusStatusImpactQueryValidator()
    {
        RuleFor(q => q.CampusId)
            .GreaterThan(0UL).WithMessage("CampusId không hợp lệ.");

        RuleFor(q => q.TargetStatus)
            .NotEmpty().WithMessage("Vui lòng chọn trạng thái muốn chuyển.")
            .Must(s => s != null
                       && (s.Trim().ToUpperInvariant() == EntityStatuses.Active
                           || s.Trim().ToUpperInvariant() == EntityStatuses.Inactive))
            .WithMessage("Trạng thái chỉ có thể là ACTIVE hoặc INACTIVE.");
    }
}
