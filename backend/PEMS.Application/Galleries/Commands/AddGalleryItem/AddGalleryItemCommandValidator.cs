using FluentValidation;

namespace PEMS.Application.Galleries.Commands.AddGalleryItem;

/// <summary>
/// Input validation for UC-GAL-04. Only the always-400 basics live here (title required + max length,
/// a location chosen, a content type chosen, media count). The four bilingual fields (descriptionVi +
/// audioVi + descriptionEn + audioEn) are validated in the handler so a missing one maps to 422 with a
/// field-specific error code (not the FluentValidation 400).
/// </summary>
public sealed class AddGalleryItemCommandValidator : AbstractValidator<AddGalleryItemCommand>
{
    public AddGalleryItemCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Vui lòng nhập tiêu đề.")
            .MaximumLength(255).WithMessage("Tiêu đề tối đa 255 ký tự.");

        RuleFor(x => x.LocationId)
            .GreaterThan(0).WithMessage("Vui lòng chọn vị trí.");

        RuleFor(x => x.ItemType)
            .NotEmpty().WithMessage("Vui lòng chọn loại nội dung.");

        // At least one media source: an uploaded image OR a YouTube URL.
        RuleFor(x => x)
            .Must(x => (x.Files?.Count ?? 0) + (x.YoutubeUrls?.Count ?? 0) >= 1)
            .WithMessage("Vui lòng chọn ít nhất một ảnh hoặc thêm một video YouTube.");

        // Total media (images + YouTube) capped at 20.
        RuleFor(x => x)
            .Must(x => (x.Files?.Count ?? 0) + (x.YoutubeUrls?.Count ?? 0) <= 20)
            .WithMessage("Chỉ được tối đa 20 media (ảnh + video YouTube).");
    }
}
