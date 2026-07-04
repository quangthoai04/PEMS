using FluentValidation;

namespace PEMS.Application.ApiIntegrations.Commands.UpsertGoogleDocumentAiOcrConfig;

public sealed class UpsertGoogleDocumentAiOcrConfigCommandValidator
    : AbstractValidator<UpsertGoogleDocumentAiOcrConfigCommand>
{
    public UpsertGoogleDocumentAiOcrConfigCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tên cấu hình là bắt buộc.").MaximumLength(150);
        RuleFor(x => x.ProjectId).NotEmpty().WithMessage("projectId là bắt buộc.").MaximumLength(150);
        RuleFor(x => x.Location).NotEmpty().WithMessage("location là bắt buộc.").MaximumLength(50);
        RuleFor(x => x.ProcessorId).NotEmpty().WithMessage("processorId là bắt buộc.").MaximumLength(255);
        RuleFor(x => x.Endpoint).NotEmpty().WithMessage("endpoint là bắt buộc.").MaximumLength(255);
        RuleFor(x => x.TimeoutSeconds).InclusiveBetween(5, 120)
            .WithMessage("timeoutSeconds phải trong khoảng 5–120.");
        RuleFor(x => x.RateLimitPerMinute).GreaterThan(0u)
            .WithMessage("rateLimitPerMinute phải > 0.");
        RuleFor(x => x.MonthlyQuota).GreaterThan(0u)
            .WithMessage("monthlyQuota phải > 0.");
        RuleFor(x => x.RetentionDays).InclusiveBetween(1u, 365u)
            .WithMessage("retentionDays phải trong khoảng 1–365.");
    }
}
