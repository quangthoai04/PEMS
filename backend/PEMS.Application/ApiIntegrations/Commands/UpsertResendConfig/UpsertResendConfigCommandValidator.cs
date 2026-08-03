using FluentValidation;

namespace PEMS.Application.ApiIntegrations.Commands.UpsertResendConfig;

public sealed class UpsertResendConfigCommandValidator
    : AbstractValidator<UpsertResendConfigCommand>
{
    public UpsertResendConfigCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên cấu hình không được để trống.")
            .MaximumLength(150);
        RuleFor(x => x.FromEmail)
            .NotEmpty().WithMessage("From Email không được để trống.")
            .EmailAddress().WithMessage("From Email không đúng định dạng.");
        RuleFor(x => x.FromName)
            .NotEmpty().WithMessage("From Name không được để trống.")
            .MaximumLength(100);
        RuleFor(x => x.ReplyToEmail)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ReplyToEmail))
            .WithMessage("Reply-To Email không đúng định dạng.");
        RuleFor(x => x.RateLimitPerMinute).GreaterThan(0u);
        RuleFor(x => x.MonthlyQuota).GreaterThan(0u);
        RuleFor(x => x.TimeoutSeconds).InclusiveBetween(5, 120)
            .WithMessage("Timeout phải trong khoảng 5–120 giây.");
    }
}
