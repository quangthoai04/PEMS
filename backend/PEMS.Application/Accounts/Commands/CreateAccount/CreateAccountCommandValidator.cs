using System.Linq;
using FluentValidation;

namespace PEMS.Application.Accounts.Commands.CreateAccount;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    // Mirrors the users.gender ENUM in pems_full.sql.
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Gender)
            .IsInEnum().WithMessage("Gender must be one of Male, Female, Other.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.")
            .MaximumLength(150);

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150);

        RuleFor(x => x.RoleCode)
            .NotEmpty().WithMessage("Role is required.");

        // Role-specific shape (sub-role / department / campus) and password strength are
        // validated in the handler because they depend on the resolved role and config.
    }
}
