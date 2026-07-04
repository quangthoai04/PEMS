using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Queries.GetApiIntegrationLogs;

/// <summary>GET /api/api-integrations/{apiConfigId}/logs — sanitized metadata only.</summary>
public sealed class GetApiIntegrationLogsQuery : IRequest<ApiRequestLogListResponse>
{
    public ulong ApiConfigId { get; set; }
    public bool? Success { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
