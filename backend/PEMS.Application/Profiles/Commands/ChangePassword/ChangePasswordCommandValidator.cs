using FluentValidation;
using PEMS.Application.Common.Security;

namespace PEMS.Application.Profiles.Commands.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .Must(PasswordPolicy.IsStrong).WithMessage(PasswordPolicy.RequirementsMessage);

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}