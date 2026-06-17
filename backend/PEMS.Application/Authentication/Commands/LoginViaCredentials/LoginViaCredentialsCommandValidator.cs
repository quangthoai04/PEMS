using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.Authentication.Commands.LoginviaCredentials;

public sealed class LoginviaCredentialsCommandValidator : AbstractValidator<LoginviaCredentialsCommand>
{
    public LoginviaCredentialsCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.")
            .MaximumLength(150);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");

        RuleFor(x => x.LoginPortal)
            .NotEmpty().WithMessage("Login portal is required.")
            .Must(p => p == LoginPortals.Internal || p == LoginPortals.Visitor)
            .WithMessage("Login portal must be INTERNAL or VISITOR.");
    }
}
