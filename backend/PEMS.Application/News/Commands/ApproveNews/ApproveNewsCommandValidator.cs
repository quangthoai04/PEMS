using FluentValidation;

namespace PEMS.Application.News.Commands.ApproveNews;

public sealed class ApproveNewsCommandValidator : AbstractValidator<ApproveNewsCommand>
{
    public ApproveNewsCommandValidator()
    {
        RuleFor(x => x.NewsId)
            .GreaterThan(0ul)
            .WithMessage("NewsId không hợp lệ.");

        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(a => a == "APPROVE" || a == "REJECT")
            .WithMessage("Action phải là APPROVE hoặc REJECT.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Lý do từ chối là bắt buộc và không vượt quá 500 ký tự.")
            .When(x => x.Action == "REJECT");
    }
}
