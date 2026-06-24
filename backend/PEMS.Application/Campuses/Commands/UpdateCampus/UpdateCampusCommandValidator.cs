using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;

namespace PEMS.Application.Campuses.Commands.UpdateCampus;

public sealed class UpdateCampusCommandValidator : AbstractValidator<UpdateCampusCommand>
{
    private static readonly Regex CodePattern = new(@"^[A-Za-z0-9_-]+$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"^\+?[0-9\s().-]{8,30}$", RegexOptions.Compiled);

    public UpdateCampusCommandValidator()
    {
        RuleFor(x => x.CampusId)
            .GreaterThan(0ul).WithMessage("Campus không hợp lệ.");

        RuleFor(x => x.CampusCode)
            .NotEmpty().WithMessage("Vui lòng nhập mã campus.")
            .MaximumLength(20).WithMessage("Mã campus tối đa 20 ký tự.")
            .Must(c => CodePattern.IsMatch(c.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.CampusCode))
            .WithMessage("Mã campus chỉ gồm chữ, số, dấu gạch ngang hoặc gạch dưới.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Vui lòng nhập tên campus.")
            .MaximumLength(150).WithMessage("Tên campus tối đa 150 ký tự.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("Vui lòng chọn thành phố.")
            .MaximumLength(100).WithMessage("Thành phố tối đa 100 ký tự.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Vui lòng nhập địa chỉ.")
            .MaximumLength(255).WithMessage("Địa chỉ tối đa 255 ký tự.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Vui lòng nhập số điện thoại.")
            .MaximumLength(30).WithMessage("Số điện thoại tối đa 30 ký tự.")
            .Must(p => PhonePattern.IsMatch(p.Trim()) && p.Count(char.IsDigit) >= 8)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Số điện thoại không đúng định dạng.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Vui lòng nhập email.")
            .MaximumLength(150).WithMessage("Email tối đa 150 ký tự.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");
    }
}
