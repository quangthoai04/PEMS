using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.News.Commands.TranslateNewsDraft;

public sealed class TranslateNewsDraftCommandValidator : AbstractValidator<TranslateNewsDraftCommand>
{
    public TranslateNewsDraftCommandValidator()
    {
        RuleFor(x => x.SourceLanguage)
            .NotEmpty().WithMessage("Vui lòng chọn ngôn ngữ nguồn.");

        RuleFor(x => x.TargetLanguage)
            .NotEmpty().WithMessage("Vui lòng chọn ngôn ngữ đích.")
            .Must(t => NewsConstants.Languages.Allowed.Contains(t))
            .WithMessage("Ngôn ngữ đích không được hỗ trợ.");

        RuleFor(x => x)
            .Must(x => !string.Equals(x.SourceLanguage, x.TargetLanguage, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Ngôn ngữ nguồn và ngôn ngữ đích không được trùng nhau.");

        // Title is the only field that must already have content — draft translation is
        // triggered live while the user is still typing, so section title/body are frequently
        // still empty (e.g. right after adding a new section, or before the body is written).
        // Those sections are simply skipped by the handler rather than rejected here; only the
        // section count is capped.
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề không được để trống.");

        RuleFor(x => x.Sections)
            .Must(s => s.Count <= 10).WithMessage("Tối đa 10 mục nội dung.");
    }
}
