using FluentValidation;

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

        RuleFor(x => x.FullName)
            .Cascade(CascadeMode.Stop)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Vui lòng nhập họ và tên.")
            .Must(v => v.Trim().Length <= 150).WithMessage("Họ và tên không được vượt quá 150 ký tự.");

        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Vui lòng nhập email.")
            .Must(v => v.Trim().Length <= 150).WithMessage("Email không được vượt quá 150 ký tự.")
            .Must(v => new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(v.Trim()))
                .WithMessage("Email không đúng định dạng.");
    }
}
