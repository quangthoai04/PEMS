using System.Linq;
using FluentValidation;
using PEMS.Application.Accounts.Common;

namespace PEMS.Application.Accounts.Commands.CreateAccount;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    // Mirrors the users.gender ENUM in pems_full.sql.
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Gender)
            .IsInEnum().WithMessage("Gender must be one of Male, Female, Other.");

        // Identity fields share one rule set with UpdateBasicAccountInfo / ReplaceStaffLeader.
        RuleFor(x => x.Email).ApplyAccountEmailRules();

        RuleFor(x => x.FullName).ApplyAccountFullNameRules();

        RuleFor(x => x.RoleCode)
            .NotEmpty().WithMessage("Role is required.");

        // Role-specific shape (sub-role / department / campus) and password strength are
        // validated in the handler because they depend on the resolved role and config.
    }
}
