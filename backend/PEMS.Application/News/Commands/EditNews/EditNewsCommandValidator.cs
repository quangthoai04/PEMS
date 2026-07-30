using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.News.Commands.EditNews;

public sealed class EditNewsCommandValidator : AbstractValidator<EditNewsCommand>
{
    private static readonly HashSet<string> ValidUsageTypes =
        new(StringComparer.OrdinalIgnoreCase) { "INLINE_IMAGE", "ATTACHMENT" };

    private const int MaxSectionFiles = 10;

    public EditNewsCommandValidator()
    {
        RuleFor(x => x.NewsId).GreaterThan(0ul).WithMessage("NewsId không hợp lệ.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề không được để trống.")
            .MaximumLength(150).WithMessage("Tiêu đề không được vượt quá 150 ký tự.");

        RuleFor(x => x.Summary)
            .NotEmpty().WithMessage("Mô tả ngắn không được để trống.")
            .MaximumLength(NewsConstants.Limits.SummaryMaxLength)
            .WithMessage($"Mô tả ngắn không được vượt quá {NewsConstants.Limits.SummaryMaxLength} ký tự.");

        RuleFor(x => x.ContentSections)
            .NotNull().WithMessage("Nội dung chi tiết là bắt buộc.")
            .Must(s => s != null && s.Count >= 1).WithMessage("Bài viết phải có ít nhất 1 nội dung chi tiết.")
            .Must(s => s == null || s.Count <= 10).WithMessage("Mỗi bài tin tức chỉ được tối đa 10 nội dung chi tiết.");

        RuleForEach(x => x.ContentSections).ChildRules(section =>
        {
            section.RuleFor(s => s.SectionTitle)
                .MaximumLength(255).WithMessage("Tiêu đề nội dung không được vượt quá 255 ký tự.");

            section.RuleFor(s => s.SectionBodyHtml)
                .NotEmpty().WithMessage("Nội dung chi tiết không được để trống.");

            section.RuleFor(s => s.SectionFiles)
                .Must(files => files == null || files.Count <= MaxSectionFiles)
                .WithMessage($"Mỗi mục nội dung chỉ được tối đa {MaxSectionFiles} ảnh/video.");

            section.RuleForEach(s => s.SectionFiles)
                .ChildRules(file =>
                {
                    file.RuleFor(f => f.FileId)
                        .GreaterThan(0ul).WithMessage("File ID không hợp lệ.");

                    file.RuleFor(f => f.UsageType)
                        .Must(t => ValidUsageTypes.Contains(t))
                        .WithMessage("Loại file phải là INLINE_IMAGE hoặc ATTACHMENT.");

                    file.RuleFor(f => f.DisplayOrder)
                        .GreaterThanOrEqualTo(0).WithMessage("Thứ tự hiển thị không hợp lệ.");
                })
                .When(s => s.SectionFiles != null && s.SectionFiles.Count > 0);
        });
    }
}