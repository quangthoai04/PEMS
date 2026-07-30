using FluentValidation;

namespace PEMS.Application.Emails.Commands.UpdateEmailTemplate;

/// <summary>
/// Shape checks only. Everything that depends on WHICH template is being edited — allowed variables,
/// required variables, what may appear in a subject — lives in
/// <see cref="PEMS.Application.Emails.Common.EmailTemplateContentValidator"/>, because that needs the
/// template's contract and this class only sees the request.
///
/// <para>
/// The Purpose rule that used to be here is gone along with the property: the module a template belongs
/// to is registry-owned and is not an operator's to change (G11-I).
/// </para>
/// </summary>
public sealed class UpdateEmailTemplateCommandValidator : AbstractValidator<UpdateEmailTemplateCommand>
{
    /// <summary>Matches the <c>name</c> column.</summary>
    private const int NameMaxLength = 150;

    /// <summary>Matches the <c>description</c> column.</summary>
    private const int DescriptionMaxLength = 500;

    /// <summary>Matches the <c>subject_vi</c> / <c>subject_en</c> columns.</summary>
    private const int SubjectMaxLength = 255;

    public UpdateEmailTemplateCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên mẫu là bắt buộc.")
            .MaximumLength(NameMaxLength)
            .WithMessage($"Tên mẫu tối đa {NameMaxLength} ký tự.");

        RuleFor(x => x.Description)
            .MaximumLength(DescriptionMaxLength)
            .WithMessage($"Mô tả tối đa {DescriptionMaxLength} ký tự.");

        RuleFor(x => x.SubjectVi)
            .MaximumLength(SubjectMaxLength)
            .WithMessage($"Tiêu đề tiếng Việt tối đa {SubjectMaxLength} ký tự.");

        RuleFor(x => x.SubjectEn)
            .MaximumLength(SubjectMaxLength)
            .WithMessage($"Tiêu đề tiếng Anh tối đa {SubjectMaxLength} ký tự.");

        // A template left with neither language has nothing to send. The renderer would refuse it later
        // with EMAIL_TEMPLATE_LANGUAGE_CONTENT_MISSING; refusing the save is the earlier, kinder place.
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.BodyVi) || !string.IsNullOrWhiteSpace(x.BodyEn))
            .WithMessage("Mẫu email phải có nội dung ở ít nhất một ngôn ngữ.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.SubjectVi) || !string.IsNullOrWhiteSpace(x.SubjectEn))
            .WithMessage("Mẫu email phải có tiêu đề ở ít nhất một ngôn ngữ.");

        // A language is either maintained or it is not: a subject with no body renders an empty message,
        // and a body with no subject cannot be sent at all.
        RuleFor(x => x)
            .Must(x => string.IsNullOrWhiteSpace(x.SubjectVi) == string.IsNullOrWhiteSpace(x.BodyVi))
            .WithMessage("Tiếng Việt phải có cả tiêu đề và nội dung, hoặc bỏ trống cả hai.");

        RuleFor(x => x)
            .Must(x => string.IsNullOrWhiteSpace(x.SubjectEn) == string.IsNullOrWhiteSpace(x.BodyEn))
            .WithMessage("Tiếng Anh phải có cả tiêu đề và nội dung, hoặc bỏ trống cả hai.");
    }
}
