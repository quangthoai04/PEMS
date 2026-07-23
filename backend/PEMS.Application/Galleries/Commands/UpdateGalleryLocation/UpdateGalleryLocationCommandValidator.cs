using FluentValidation;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryLocation;

/// <summary>Input validation for the direct area/location edit. Scope, duplicate and translation-origin
/// rules live in the handler.</summary>
public sealed class UpdateGalleryLocationCommandValidator : AbstractValidator<UpdateGalleryLocationCommand>
{
    public UpdateGalleryLocationCommandValidator()
    {
        RuleFor(x => x.LocationId)
            .GreaterThan(0).WithMessage("Không tìm thấy vị trí Gallery.");

        RuleFor(x => x.AreaName)
            .NotEmpty().WithMessage("Vui lòng nhập tên khu vực/tòa.")
            .MaximumLength(150).WithMessage("Tên khu vực tối đa 150 ký tự.");

        RuleFor(x => x.LocationName)
            .NotEmpty().WithMessage("Vui lòng nhập vị trí cụ thể.")
            .MaximumLength(150).WithMessage("Vị trí cụ thể tối đa 150 ký tự.");

        RuleFor(x => x.AreaNameEn)
            .MaximumLength(255).WithMessage("Tên khu vực tiếng Anh tối đa 255 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.AreaNameEn));

        RuleFor(x => x.LocationNameEn)
            .MaximumLength(255).WithMessage("Vị trí tiếng Anh tối đa 255 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.LocationNameEn));
    }
}
