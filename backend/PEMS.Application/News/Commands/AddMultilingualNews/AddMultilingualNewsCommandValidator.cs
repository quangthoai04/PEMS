using FluentValidation;

namespace PEMS.Application.News.Commands.AddMultilingualNews;

public sealed class AddMultilingualNewsCommandValidator : AbstractValidator<AddMultilingualNewsCommand>
{
    public AddMultilingualNewsCommandValidator()
    {
        RuleFor(x => x.NewsId).GreaterThan(0ul).WithMessage("NewsId không hợp lệ.");
        RuleFor(x => x.LanguageCode)
            .NotEmpty().WithMessage("Vui lòng chọn ngôn ngữ bản dịch.")
            .MaximumLength(20);
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề bản dịch không được để trống.")
            .MaximumLength(250);
        RuleFor(x => x.Sections)
            .NotEmpty().WithMessage("Bản dịch phải có ít nhất một mục nội dung.");
        RuleForEach(x => x.Sections).ChildRules(section =>
        {
            section.RuleFor(s => s.SectionTitle)
                .NotEmpty().WithMessage("Tiêu đề mục nội dung không được để trống.");
            section.RuleFor(s => s.SectionBodyHtml)
                .NotEmpty().WithMessage("Nội dung mục không được để trống.");
        });
    }
}
