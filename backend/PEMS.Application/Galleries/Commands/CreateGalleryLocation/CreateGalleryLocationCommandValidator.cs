using FluentValidation;

namespace PEMS.Application.Galleries.Commands.CreateGalleryLocation;

/// <summary>
/// Input validation for UC-LOC-04/05 (FE §28.6). <c>locationName</c> is always required; the
/// mode-dependent area rules (areaId vs newAreaName) and all scope/duplicate checks live in the handler.
/// </summary>
public sealed class CreateGalleryLocationCommandValidator : AbstractValidator<CreateGalleryLocationCommand>
{
    public CreateGalleryLocationCommandValidator()
    {
        RuleFor(x => x.LocationName)
            .NotEmpty().WithMessage("Vui lòng nhập vị trí cụ thể.")
            .MaximumLength(150).WithMessage("Vị trí cụ thể tối đa 150 ký tự.");

        RuleFor(x => x.NewAreaName)
            .MaximumLength(150).WithMessage("Tên khu vực tối đa 150 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.NewAreaName));
    }
}
