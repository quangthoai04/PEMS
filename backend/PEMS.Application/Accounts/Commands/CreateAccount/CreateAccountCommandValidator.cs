using FluentValidation;

namespace PEMS.Application.Accounts.Commands.CreateAccount;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
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
