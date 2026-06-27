using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.News.Commands.ManageNewsVisibility;

public sealed class ManageNewsVisibilityCommandValidator : AbstractValidator<ManageNewsVisibilityCommand>
{
    public ManageNewsVisibilityCommandValidator()
    {
        RuleFor(x => x.NewsId)
            .GreaterThan(0ul)
            .WithMessage("NewsId không hợp lệ.");

        RuleFor(x => x.TargetStatus)
            .NotEmpty()
            .Must(s => s == NewsConstants.Status.Hidden || s == NewsConstants.Status.Published)
            .WithMessage("TargetStatus phải là HIDDEN hoặc PUBLISHED.");
    }
}
