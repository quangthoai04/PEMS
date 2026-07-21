using FluentValidation;
using PEMS.Application.Accounts.Common;

namespace PEMS.Application.Accounts.Commands.UpdateBasicAccountInfo;

/// <summary>
/// Structural validation only. Whether the caller may edit this target (HO scope, self, LOCKED,
/// role) and email uniqueness are enforced in the handler — the final authorization gate.
/// </summary>
public sealed class UpdateBasicAccountInfoCommandValidator : AbstractValidator<UpdateBasicAccountInfoCommand>
{
    public UpdateBasicAccountInfoCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Thiếu định danh tài khoản cần chỉnh sửa.");

        // Identity fields share one rule set with CreateAccount / ReplaceStaffLeader.
        RuleFor(x => x.FullName).ApplyAccountFullNameRules();

        RuleFor(x => x.Email).ApplyAccountEmailRules();
    }
}
