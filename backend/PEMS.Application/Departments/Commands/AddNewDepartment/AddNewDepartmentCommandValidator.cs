using FluentValidation;

namespace PEMS.Application.Departments.Commands.AddNewDepartment;

/// <summary>UC-101 input validation: name required after trim, max 150 chars.</summary>
public sealed class AddNewDepartmentCommandValidator : AbstractValidator<AddNewDepartmentCommand>
{
    public AddNewDepartmentCommandValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Tên phòng ban là bắt buộc.")
            .Must(n => n == null || n.Trim().Length <= 150).WithMessage("Tên phòng ban không được vượt quá 150 ký tự.");
    }
}
