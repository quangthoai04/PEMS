using FluentValidation;

namespace PEMS.Application.ApiIntegrations.Commands.UpdateApiIntegrationQuota;

public sealed class UpdateApiIntegrationQuotaCommandValidator
    : AbstractValidator<UpdateApiIntegrationQuotaCommand>
{
    public UpdateApiIntegrationQuotaCommandValidator()
    {
        RuleFor(x => x.ApiConfigId).GreaterThan(0UL);
        RuleFor(x => x.MonthlyLimit).GreaterThan(0).WithMessage("monthlyLimit phải > 0.");
    }
}
