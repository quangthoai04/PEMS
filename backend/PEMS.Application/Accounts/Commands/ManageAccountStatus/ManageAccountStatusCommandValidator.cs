using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Commands.ManageAccountStatus;

public sealed class ManageAccountStatusCommandValidator : AbstractValidator<ManageAccountStatusCommand>
{
    private static readonly string[] AllowedStatuses =
    {
        UserStatuses.Active,
        UserStatuses.Inactive,
        UserStatuses.Locked,
    };

    public ManageAccountStatusCommandValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0ul).WithMessage("UserId is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(status => AllowedStatuses.Contains(status?.Trim().ToUpperInvariant()))
            .WithMessage("Status must be one of ACTIVE, INACTIVE, LOCKED.");

        RuleFor(x => x.Reason)
            .MaximumLength(500);
    }
}
