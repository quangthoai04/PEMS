using FluentValidation;
using PEMS.Application.Accounts.Common;

namespace PEMS.Application.Accounts.Commands.ReplaceStaffLeader;

public sealed class ReplaceStaffLeaderCommandValidator : AbstractValidator<ReplaceStaffLeaderCommand>
{
    public ReplaceStaffLeaderCommandValidator()
    {
        RuleFor(x => x.CampusId)
            .GreaterThan(0ul).WithMessage("Vui lòng chọn cơ sở.");

        // BR-RSL-22: reason is mandatory, 10–500 chars and must carry actual content.
        RuleFor(x => x.Reason).ApplyReplacementReasonRules();

        RuleFor(x => x.Mode)
            .Must(m => m == ReplaceStaffLeaderModes.ExistingUser || m == ReplaceStaffLeaderModes.CreateNewUser)
            .WithMessage("Chế độ thay thế không hợp lệ.");

        // Mode EXISTING_USER → a target user is required.
        When(x => x.Mode == ReplaceStaffLeaderModes.ExistingUser, () =>
        {
            RuleFor(x => x.NewLeaderUserId)
                .NotNull().WithMessage("Vui lòng chọn nhân sự thay thế.")
                .Must(id => id is > 0).WithMessage("Vui lòng chọn nhân sự thay thế.");
        });

        // Mode CREATE_NEW_USER → name + valid email required; gender (if sent) must be a valid enum.
        When(x => x.Mode == ReplaceStaffLeaderModes.CreateNewUser, () =>
        {
            // Same identity rule set as CreateAccount / UpdateBasicAccountInfo.
            RuleFor(x => x.FullName).ApplyAccountFullNameRules();

            RuleFor(x => x.Email).ApplyAccountEmailRules();

            RuleFor(x => x.Gender)
                .IsInEnum().WithMessage("Giới tính không hợp lệ.");
        });
    }
}
