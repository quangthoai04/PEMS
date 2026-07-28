using FluentValidation;
using PEMS.Application.Accounts.Common;
using PEMS.Application.DepartmentLeaderPersonnel.Common;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.UpdateDepartmentPersonnel;

/// <summary>
/// Spec §12.5/§18. All four fields are always submitted (the modal is a full form), so each is
/// validated unconditionally against the NORMALIZED value. Uniqueness and the identity side effects
/// are the handler's job — they need the database and a transaction.
/// </summary>
public sealed class UpdateDepartmentPersonnelCommandValidator : AbstractValidator<UpdateDepartmentPersonnelCommand>
{
    public UpdateDepartmentPersonnelCommandValidator()
    {
        RuleFor(c => c.UserId)
            .GreaterThan(0ul).WithMessage("Thiếu định danh nhân sự.");

        RuleFor(c => c.FullName)
            .Must(v => AccountIdentityRules.ValidateFullName(v) is null)
            .WithMessage((_, v) => AccountIdentityRules.ValidateFullName(v) ?? string.Empty);

        RuleFor(c => c.Email)
            .Must(v => AccountIdentityRules.ValidateEmail(v) is null)
            .WithMessage((_, v) => AccountIdentityRules.ValidateEmail(v) ?? string.Empty);

        RuleFor(c => c.Phone)
            .Must(v => DepartmentPersonnelPhoneRules.Validate(v) is null)
            .WithMessage((_, v) => DepartmentPersonnelPhoneRules.Validate(v) ?? string.Empty);

        RuleFor(c => c.Gender)
            .Must(DepartmentPersonnelGenders.IsValid)
            .WithMessage(DepartmentPersonnelGenders.InvalidMessage);
    }
}
