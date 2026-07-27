using FluentValidation;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.TransferDepartmentLeadership;

/// <summary>
/// Spec §18. Shape only — whether the id is a usable successor depends on rows that can change between
/// validation and commit, so every membership rule is re-checked in the handler under a row lock.
/// </summary>
public sealed class TransferDepartmentLeadershipCommandValidator
    : AbstractValidator<TransferDepartmentLeadershipCommand>
{
    public TransferDepartmentLeadershipCommandValidator()
    {
        RuleFor(c => c.NewLeaderUserId)
            .GreaterThan(0ul).WithMessage("Vui lòng chọn Trưởng phòng mới.");
    }
}
