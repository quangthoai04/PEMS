using FluentValidation;

namespace PEMS.Application.Departments.Commands.UpdateDepartment;

/// <summary>UC-102 input validation: name required after trim, max 150 chars (§10.1).</summary>
public sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.DepartmentId).GreaterThan(0UL).WithMessage("Phòng ban không hợp lệ.");
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Tên phòng ban không được để trống.")
            .Must(n => n == null || n.Trim().Length <= 150).WithMessage("Tên phòng ban không được vượt quá 150 ký tự.");
    }
}
