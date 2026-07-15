using FluentValidation;

namespace PEMS.Application.Galleries.Commands.AddGalleryItem;

/// <summary>
/// Input validation for UC-GAL-04. Title and description are required (DB has
/// <c>gallery_items.description TEXT NOT NULL</c> — the UI label must read "Mô tả *"); at least one
/// media file is required and at most five. Location/status/file-content rules are enforced in the handler.
/// </summary>
public sealed class AddGalleryItemCommandValidator : AbstractValidator<AddGalleryItemCommand>
{
    public AddGalleryItemCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Vui lòng nhập tiêu đề.")
            .MaximumLength(255).WithMessage("Tiêu đề tối đa 255 ký tự.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Vui lòng nhập mô tả.");

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
