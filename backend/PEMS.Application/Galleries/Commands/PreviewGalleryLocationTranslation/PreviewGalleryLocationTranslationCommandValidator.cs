using FluentValidation;

namespace PEMS.Application.Galleries.Commands.PreviewGalleryLocationTranslation;

/// <summary>
/// Input validation for the translation preview. Mode/include semantics and the "field required when
/// included" rules live in the handler; here we only cap the raw lengths (mirrors create/update: VI
/// names ≤ 150 chars) so oversized payloads never reach the provider.
/// </summary>
public sealed class PreviewGalleryLocationTranslationCommandValidator
    : AbstractValidator<PreviewGalleryLocationTranslationCommand>
{
    public PreviewGalleryLocationTranslationCommandValidator()
    {
        RuleFor(x => x.AreaNameVi)
            .MaximumLength(150).WithMessage("Tên khu vực tối đa 150 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.AreaNameVi));

        RuleFor(x => x.LocationNameVi)
            .MaximumLength(150).WithMessage("Vị trí cụ thể tối đa 150 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.LocationNameVi));
    }
}
