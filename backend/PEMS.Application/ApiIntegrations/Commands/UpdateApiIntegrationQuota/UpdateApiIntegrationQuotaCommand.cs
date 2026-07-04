using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Commands.UpdateApiIntegrationQuota;

/// <summary>PUT /api/api-integrations/{apiConfigId}/quota — sets the GLOBAL monthly limit for the current period.</summary>
public sealed class UpdateApiIntegrationQuotaCommand : IRequest<ApiQuotaDto>
{
    public ulong ApiConfigId { get; set; }
    public int MonthlyLimit { get; set; }
}
