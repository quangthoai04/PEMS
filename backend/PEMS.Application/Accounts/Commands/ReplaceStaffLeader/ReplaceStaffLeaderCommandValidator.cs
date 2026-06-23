using FluentValidation;

namespace PEMS.Application.Accounts.Commands.ReplaceStaffLeader;

public sealed class ReplaceStaffLeaderCommandValidator : AbstractValidator<ReplaceStaffLeaderCommand>
{
    public ReplaceStaffLeaderCommandValidator()
    {
        RuleFor(x => x.CampusId)
            .GreaterThan(0ul).WithMessage("Vui lòng chọn cơ sở.");

        // BR-RSL-22: reason is mandatory.
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do thay thế.")
            .MaximumLength(500);

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
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Vui lòng nhập họ và tên.")
                .MaximumLength(150);

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Vui lòng nhập email.")
                .EmailAddress().WithMessage("Vui lòng nhập email hợp lệ.")
                .MaximumLength(150);

            RuleFor(x => x.Gender)
                .IsInEnum().WithMessage("Giới tính không hợp lệ.");
        });
    }
}
