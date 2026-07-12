using FluentValidation;

namespace PEMS.Application.Departments.Queries.GetDepartmentStatusImpact;

public sealed class GetDepartmentStatusImpactQueryValidator : AbstractValidator<GetDepartmentStatusImpactQuery>
{
    public GetDepartmentStatusImpactQueryValidator()
    {
        RuleFor(x => x.DepartmentId).GreaterThan(0UL).WithMessage("Phòng ban không hợp lệ.");
        RuleFor(x => x.NewStatus)
            .Must(s => s is "ACTIVE" or "INACTIVE")
            .WithMessage("Trạng thái phòng ban không hợp lệ.");
    }
}
