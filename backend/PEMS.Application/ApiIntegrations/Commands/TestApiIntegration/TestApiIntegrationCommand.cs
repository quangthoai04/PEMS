using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Commands.TestApiIntegration;

/// <summary>POST /api/api-integrations/{apiConfigId}/test — checks processor access, saves last_test_* and a sanitized log.</summary>
public sealed record TestApiIntegrationCommand(ulong ApiConfigId) : IRequest<ApiConnectionTestResultDto>;
