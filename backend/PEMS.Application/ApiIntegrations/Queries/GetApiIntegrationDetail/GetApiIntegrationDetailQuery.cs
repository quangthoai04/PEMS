using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Queries.GetApiIntegrationDetail;

/// <summary>GET /api/api-integrations/{apiConfigId} — raw credential is never returned.</summary>
public sealed record GetApiIntegrationDetailQuery(ulong ApiConfigId) : IRequest<ApiIntegrationDto>;
