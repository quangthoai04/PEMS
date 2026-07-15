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

        // ── Identity fields (Staff Leader flow). Only structural checks live here; whether the
        //    target may actually be edited (and email uniqueness) is enforced in the handler.
        //    A null value means "unchanged", so the rules only apply when the field is sent. ──
        When(x => x.FullName is not null, () =>
        {
            RuleFor(x => x.FullName!)
                .Cascade(CascadeMode.Stop)
                .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Vui lòng nhập họ và tên.")
                .Must(v => v.Trim().Length <= 150).WithMessage("Họ và tên không được vượt quá 150 ký tự.");
        });

        When(x => x.Email is not null, () =>
        {
            RuleFor(x => x.Email!)
                .Cascade(CascadeMode.Stop)
                .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Vui lòng nhập địa chỉ email.")
                .Must(v => v.Trim().Length <= 150).WithMessage("Email không được vượt quá 150 ký tự.")
                .Must(v => new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(v.Trim()))
                    .WithMessage("Địa chỉ email không hợp lệ.");
        });

        // Role-specific shape (sub-role / department / campus) is validated in the handler.
    }
}
