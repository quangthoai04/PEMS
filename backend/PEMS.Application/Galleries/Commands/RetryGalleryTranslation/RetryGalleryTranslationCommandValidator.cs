using FluentValidation;

namespace PEMS.Application.Galleries.Commands.RetryGalleryTranslation;

public sealed class RetryGalleryTranslationCommandValidator
    : AbstractValidator<RetryGalleryTranslationCommand>
{
    public RetryGalleryTranslationCommandValidator()
    {
        RuleFor(x => x.EntityType)
            .NotEmpty().WithMessage("Vui lòng chọn loại đối tượng dịch.")
            .Must(t => (t ?? string.Empty).Trim().ToUpperInvariant()
                is GalleryTranslationEntityTypes.Area
                or GalleryTranslationEntityTypes.Location
                or GalleryTranslationEntityTypes.Item)
            .WithMessage("Loại đối tượng dịch không hợp lệ (AREA / LOCATION / ITEM).");

        RuleFor(x => x.EntityId)
            .GreaterThan(0).WithMessage("EntityId không hợp lệ.");
    }
}
