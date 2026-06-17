using FluentValidation;
using PEMS.Application.Common.Security;

namespace PEMS.Application.Authentication.Commands.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.");

        RuleFor(x => x.OtpOrToken)
            .NotEmpty().WithMessage("Reset code is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .Must(PasswordPolicy.IsStrong).WithMessage(PasswordPolicy.RequirementsMessage);

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}
