using FluentValidation;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryItem;

/// <summary>
/// Input validation for UC-GAL-07. Only the always-400 basics live here. Both descriptions and the
/// keep-audio rules are validated in the handler so a missing field maps to 422 with a field-specific
/// code. The "must keep at least one active media" rule depends on existing media so it lives there too.
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

        // Optional previewed/manual EN — never truncated, so an over-cap value is a client error.
        RuleFor(x => x.TitleEn)
            .MaximumLength(500).WithMessage("Tiêu đề tiếng Anh tối đa 500 ký tự.");

        RuleFor(x => x.LocationId)
            .GreaterThan(0).WithMessage("Vui lòng chọn vị trí.");

        RuleFor(x => x)
            .Must(x => (x.NewFiles?.Count ?? 0) + (x.YoutubeUrls?.Count ?? 0) <= 20)
            .WithMessage("Chỉ được thêm tối đa 20 media mới (ảnh + video YouTube).");
    }
}
