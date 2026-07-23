using FluentValidation;

namespace PEMS.Application.Galleries.Commands.PreviewGalleryItemTranslation;

/// <summary>
/// Input validation for the item-title translation preview. EntityType/field semantics and the
/// "source required" rule live in the handler (422 with a stable code); here we only cap the raw
/// length (mirrors create/update: title ≤ 255 chars) so oversized payloads never reach the provider.
/// </summary>
public sealed class PreviewGalleryItemTranslationCommandValidator
    : AbstractValidator<PreviewGalleryItemTranslationCommand>
{
    public PreviewGalleryItemTranslationCommandValidator()
    {
        RuleFor(x => x.SourceText)
            .MaximumLength(255).WithMessage("Tiêu đề tối đa 255 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.SourceText));
    }
}
