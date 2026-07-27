using FluentValidation;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.ResendPersonnelEmailConfirmation;

/// <summary>Spec §18 — the only input is the target id; everything else is server-resolved.</summary>
public sealed class ResendPersonnelEmailConfirmationCommandValidator
    : AbstractValidator<ResendPersonnelEmailConfirmationCommand>
{
    public ResendPersonnelEmailConfirmationCommandValidator()
    {
        RuleFor(c => c.UserId)
            .GreaterThan(0ul).WithMessage("Thiếu định danh nhân sự.");
    }
}
