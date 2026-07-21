using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.News.Commands.AddMultilingualNews;

public sealed class AddMultilingualNewsCommandValidator : AbstractValidator<AddMultilingualNewsCommand>
{
    private const int MaxSectionFiles = 10;

    public AddMultilingualNewsCommandValidator()
    {
        RuleFor(x => x.NewsId).GreaterThan(0ul).WithMessage("NewsId không hợp lệ.");
        RuleFor(x => x.LanguageCode)
            .NotEmpty().WithMessage("Vui lòng chọn ngôn ngữ bản dịch.")
            .MaximumLength(20);
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề bản dịch không được để trống.")
            .MaximumLength(250);
        RuleFor(x => x.Summary)
            .MaximumLength(NewsConstants.Limits.SummaryMaxLength)
            .WithMessage($"Mô tả ngắn không được vượt quá {NewsConstants.Limits.SummaryMaxLength} ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.Summary));
        RuleFor(x => x.Sections)
            .NotEmpty().WithMessage("Bản dịch phải có ít nhất một mục nội dung.");
        RuleForEach(x => x.Sections).ChildRules(section =>
        {
            section.RuleFor(s => s.SectionTitle)
                .NotEmpty().WithMessage("Tiêu đề mục nội dung không được để trống.");
            section.RuleFor(s => s.SectionBodyHtml)
                .NotEmpty().WithMessage("Nội dung mục không được để trống.");
            section.RuleFor(s => s.SectionFiles)
                .Must(files => files == null || files.Count <= MaxSectionFiles)
                .WithMessage($"Mỗi mục nội dung chỉ được tối đa {MaxSectionFiles} ảnh/video.");
        });
    }
}
