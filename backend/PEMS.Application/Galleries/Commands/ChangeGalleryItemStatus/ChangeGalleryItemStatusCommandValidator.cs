using FluentValidation;

namespace PEMS.Application.Galleries.Commands.ChangeGalleryItemStatus;

public sealed class ChangeGalleryItemStatusCommandValidator : AbstractValidator<ChangeGalleryItemStatusCommand>
{
    public ChangeGalleryItemStatusCommandValidator()
    {
        RuleFor(x => x.GalleryItemId).GreaterThan(0);
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Vui lòng chọn trạng thái.")
            .Must(s => s != null && (s.Trim().ToUpperInvariant() is "PUBLISHED" or "HIDDEN"))
            .WithMessage("Trạng thái không hợp lệ.");
    }
}
