using FluentValidation;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryLocation;

/// <summary>Input validation for UC-LOC-06/07. Mode-dependent + scope/duplicate rules live in the handler.</summary>
public sealed class UpdateGalleryLocationCommandValidator : AbstractValidator<UpdateGalleryLocationCommand>
{
    public UpdateGalleryLocationCommandValidator()
    {
        RuleFor(x => x.LocationId)
            .GreaterThan(0).WithMessage("Không tìm thấy vị trí Gallery.");

        RuleFor(x => x.LocationName)
            .NotEmpty().WithMessage("Vui lòng nhập vị trí cụ thể.")
            .MaximumLength(150).WithMessage("Vị trí cụ thể tối đa 150 ký tự.");

        RuleFor(x => x.NewAreaName)
            .MaximumLength(150).WithMessage("Tên khu vực tối đa 150 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.NewAreaName));
    }
}
