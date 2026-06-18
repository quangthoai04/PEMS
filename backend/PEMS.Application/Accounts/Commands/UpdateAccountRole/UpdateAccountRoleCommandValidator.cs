using FluentValidation;

namespace PEMS.Application.Accounts.Commands.UpdateAccountRole;

public sealed class UpdateAccountRoleCommandValidator : AbstractValidator<UpdateAccountRoleCommand>
{
    public UpdateAccountRoleCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Target account id is required.");

        RuleFor(x => x.NewRoleCode)
            .NotEmpty().WithMessage("New role is required.");

        // Role-specific shape (sub-role / department / campus) is validated in the handler.
    }
}
