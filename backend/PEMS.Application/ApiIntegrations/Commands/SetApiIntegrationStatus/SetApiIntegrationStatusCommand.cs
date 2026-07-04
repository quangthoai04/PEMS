using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Commands.SetApiIntegrationStatus;

/// <summary>
/// POST /api/api-integrations/{apiConfigId}/enable | /disable.
/// Enable requires the last connection test to have succeeded.
/// </summary>
public sealed record SetApiIntegrationStatusCommand(ulong ApiConfigId, bool Enable) : IRequest<ApiIntegrationDto>;
