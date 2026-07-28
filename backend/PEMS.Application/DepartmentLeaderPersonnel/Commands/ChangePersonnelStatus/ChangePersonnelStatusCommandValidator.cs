using FluentValidation;
using PEMS.Application.DepartmentLeaderPersonnel.Common;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.ChangePersonnelStatus;

/// <summary>
/// Spec §18. The status whitelist lives in <see cref="DepartmentPersonnelStatusRules"/> so the
/// validator, the impact preview and the handler all read the same list.
/// </summary>
public sealed class ChangePersonnelStatusCommandValidator : AbstractValidator<ChangePersonnelStatusCommand>
{
    /// <summary>Long enough to be a real justification, short enough for an audit column.</summary>
    public const int MaxReasonLength = 500;

    public ChangePersonnelStatusCommandValidator()
    {
        RuleFor(c => c.UserId)
            .GreaterThan(0ul).WithMessage("Thiếu định danh nhân sự.");

        RuleFor(c => c.TargetStatus)
            .NotEmpty().WithMessage("Vui lòng chọn trạng thái muốn chuyển đến.")
            .Must(DepartmentPersonnelStatusRules.IsSupportedTargetStatus)
            .WithMessage("Chỉ hỗ trợ chuyển trạng thái sang ACTIVE hoặc INACTIVE.");

        RuleFor(c => c.Reason)
            .MaximumLength(MaxReasonLength)
            .WithMessage($"Lý do không được vượt quá {MaxReasonLength} ký tự.");
    }
}
