using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Commands.DisconnectGoogleDriveOAuth;

/// <summary>
/// ADMIN clears the stored Google Drive credential. Returns the refreshed card so the console can show
/// "chưa kết nối" without a second round-trip.
/// </summary>
public sealed record DisconnectGoogleDriveOAuthCommand : IRequest<ApiIntegrationDto>;
