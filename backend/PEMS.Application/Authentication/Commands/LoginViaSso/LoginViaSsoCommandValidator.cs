using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.Authentication.Commands.LoginviaSSO;

public sealed class LoginviaSSOCommandValidator : AbstractValidator<LoginviaSSOCommand>
{
    public LoginviaSSOCommandValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty().WithMessage("Google ID token is required.");

        RuleFor(x => x.LoginPortal)
            .NotEmpty().WithMessage("Login portal is required.")
            .Must(p => p == LoginPortals.Internal || p == LoginPortals.Visitor)
            .WithMessage("Login portal must be INTERNAL or VISITOR.");
    }
}
