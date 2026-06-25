using FluentValidation;

namespace PEMS.Application.Departments.Commands.ManageDepartmentStatus;

public sealed class ManageDepartmentStatusCommandValidator : AbstractValidator<ManageDepartmentStatusCommand>
{
    public ManageDepartmentStatusCommandValidator()
    {
        RuleFor(x => x.DepartmentId).GreaterThan(0UL).WithMessage("Phòng ban không hợp lệ.");
        RuleFor(x => x.Status)
            .Must(s => s is "ACTIVE" or "INACTIVE")
            .WithMessage("Trạng thái phòng ban không hợp lệ.");
    }
}
