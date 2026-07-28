using FluentValidation;
using PEMS.Application.Accounts.Common;
using PEMS.Application.DepartmentLeaderPersonnel.Common;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.CreateDepartmentPersonnel;

/// <summary>
/// Spec §10/§18. Validates the NORMALIZED value of each field through the same shared rules the
/// handler re-applies, so a direct API call is held to exactly the standard the modal is. Uniqueness
/// is not checked here — it needs the database and belongs in the handler's transaction.
/// </summary>
public sealed class CreateDepartmentPersonnelCommandValidator : AbstractValidator<CreateDepartmentPersonnelCommand>
{
    public CreateDepartmentPersonnelCommandValidator()
    {
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
