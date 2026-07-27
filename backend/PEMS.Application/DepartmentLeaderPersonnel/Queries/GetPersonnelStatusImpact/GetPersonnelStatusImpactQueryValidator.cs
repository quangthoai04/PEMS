using FluentValidation;
using PEMS.Application.DepartmentLeaderPersonnel.Common;

namespace PEMS.Application.DepartmentLeaderPersonnel.Queries.GetPersonnelStatusImpact;

/// <summary>Spec §18 — the preview accepts exactly the two statuses the toggle can produce.</summary>
public sealed class GetPersonnelStatusImpactQueryValidator : AbstractValidator<GetPersonnelStatusImpactQuery>
{
    public GetPersonnelStatusImpactQueryValidator()
    {
        RuleFor(q => q.UserId)
            .GreaterThan(0ul).WithMessage("Thiếu định danh nhân sự.");

        RuleFor(q => q.TargetStatus)
            .NotEmpty().WithMessage("Vui lòng chọn trạng thái muốn chuyển đến.")
            .Must(DepartmentPersonnelStatusRules.IsSupportedTargetStatus)
            .WithMessage("Chỉ hỗ trợ chuyển trạng thái sang ACTIVE hoặc INACTIVE.");
    }
}
