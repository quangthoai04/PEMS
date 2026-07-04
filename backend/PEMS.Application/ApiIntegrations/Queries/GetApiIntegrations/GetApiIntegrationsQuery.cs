using System.Collections.Generic;
using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Queries.GetApiIntegrations;

/// <summary>GET /api/api-integrations — ADMIN full, HO read-only.</summary>
public sealed class GetApiIntegrationsQuery : IRequest<List<ApiIntegrationDto>>
{
    public string? Purpose { get; set; }
}
