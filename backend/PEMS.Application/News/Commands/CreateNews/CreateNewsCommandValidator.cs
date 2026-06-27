using FluentValidation;

namespace PEMS.Application.News.Commands.CreateNews;

public sealed class CreateNewsCommandValidator : AbstractValidator<CreateNewsCommand>
{
    private static readonly HashSet<string> ValidUsageTypes =
        new(StringComparer.OrdinalIgnoreCase) { "INLINE_IMAGE", "ATTACHMENT" };

    public CreateNewsCommandValidator()
    {
        RuleFor(x => x.VisitInstanceId)
            .GreaterThan(0ul)
            .WithMessage("Visit instance không hợp lệ.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề không được để trống.")
            .MaximumLength(150).WithMessage("Tiêu đề không được vượt quá 150 ký tự.");

        RuleFor(x => x.Summary)
            .NotEmpty().WithMessage("Mô tả ngắn không được để trống.")
            .MaximumLength(250).WithMessage("Mô tả ngắn không được vượt quá 250 ký tự.");

        RuleFor(x => x.ContentSections)
            .NotNull().WithMessage("Nội dung chi tiết là bắt buộc.")
            .Must(s => s != null && s.Count >= 1).WithMessage("Bài viết phải có ít nhất 1 nội dung chi tiết.")
            .Must(s => s == null || s.Count <= 10).WithMessage("Mỗi bài tin tức chỉ được tối đa 10 nội dung chi tiết.")
            .Must(s => s == null || s.Select(x => x.SectionOrder).Distinct().Count() == s.Count)
            .WithMessage("Thứ tự nội dung không được trùng nhau.");

        RuleForEach(x => x.ContentSections).ChildRules(section =>
        {
            section.RuleFor(s => s.SectionOrder)
                .InclusiveBetween(1, 10)
                .WithMessage("Thứ tự nội dung phải từ 1 đến 10.");

            section.RuleFor(s => s.SectionTitle)
                .NotEmpty().WithMessage("Tiêu đề nội dung không được để trống.")
                .MaximumLength(255).WithMessage("Tiêu đề nội dung không được vượt quá 255 ký tự.");

            section.RuleFor(s => s.SectionBodyHtml)
                .NotEmpty().WithMessage("Nội dung chi tiết không được để trống.");

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
