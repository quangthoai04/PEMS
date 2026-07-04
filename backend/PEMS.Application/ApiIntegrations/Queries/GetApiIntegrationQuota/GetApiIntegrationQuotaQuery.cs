using System.Collections.Generic;
using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Queries.GetApiIntegrationQuota;

/// <summary>GET /api/api-integrations/{apiConfigId}/quota — current + past periods.</summary>
public sealed record GetApiIntegrationQuotaQuery(ulong ApiConfigId) : IRequest<List<ApiQuotaDto>>;
