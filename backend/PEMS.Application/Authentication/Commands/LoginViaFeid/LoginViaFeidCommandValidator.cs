using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.Authentication.Commands.LoginviaFeid;

public sealed class LoginviaFeidCommandValidator : AbstractValidator<LoginviaFeidCommand>
{
    public LoginviaFeidCommandValidator()
    {
        RuleFor(x => x.IdTokenOrCode)
            .NotEmpty().WithMessage("FEID token or code is required.");

        RuleFor(x => x.LoginPortal)
            .NotEmpty().WithMessage("Login portal is required.")
            .Must(p => p == LoginPortals.Internal || p == LoginPortals.Visitor)
            .WithMessage("Login portal must be INTERNAL or VISITOR.");
    }
}
