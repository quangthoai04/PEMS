using FluentValidation;

namespace PEMS.Application.ApiIntegrations.Commands.UpsertGoogleVisionFaceDetectionConfig;

public sealed class UpsertGoogleVisionFaceDetectionConfigCommandValidator
    : AbstractValidator<UpsertGoogleVisionFaceDetectionConfigCommand>
{
    public UpsertGoogleVisionFaceDetectionConfigCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên cấu hình không được để trống.")
            .MaximumLength(150);
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID không được để trống.")
            .MaximumLength(100);
        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("Location không được để trống.")
            .MaximumLength(50);
        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("Endpoint không được để trống.")
            .MaximumLength(150);
        RuleFor(x => x.RateLimitPerMinute).GreaterThan(0u);
        RuleFor(x => x.MonthlyQuota).GreaterThan(0u);
        RuleFor(x => x.TimeoutSeconds).InclusiveBetween(5, 120)
            .WithMessage("Timeout phải trong khoảng 5–120 giây.");
    }
}
