using FluentValidation;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryItem;

/// <summary>
/// Input validation for UC-GAL-07. Title and description are required; at most five new files per save.
/// The "must keep at least one active media" rule depends on existing media so it lives in the handler.
/// </summary>
public sealed class UpdateGalleryItemCommandValidator : AbstractValidator<UpdateGalleryItemCommand>
{
    public UpdateGalleryItemCommandValidator()
    {
        RuleFor(x => x.GalleryItemId)
            .GreaterThan(0);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Vui lòng nhập tiêu đề.")
            .MaximumLength(255).WithMessage("Tiêu đề tối đa 255 ký tự.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Vui lòng nhập mô tả.");

        RuleFor(x => x.LocationId)
            .GreaterThan(0).WithMessage("Vui lòng chọn vị trí.");

        RuleFor(x => x.NewFiles)
            .Must(f => f == null || f.Count <= 20)
            .WithMessage("Chỉ được tải lên tối đa 20 tệp.");
    }
}
