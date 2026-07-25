using FluentValidation;

namespace PEMS.Application.News.Commands.SetNewsFeatured;

public sealed class SetNewsFeaturedCommandValidator : AbstractValidator<SetNewsFeaturedCommand>
{
    public SetNewsFeaturedCommandValidator()
    {
        RuleFor(x => x.NewsId)
            .GreaterThan(0ul)
            .WithMessage("NewsId không hợp lệ.");
    }
}
