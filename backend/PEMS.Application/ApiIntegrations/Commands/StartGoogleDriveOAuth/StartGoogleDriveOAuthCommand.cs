using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Commands.StartGoogleDriveOAuth;

/// <summary>
/// ADMIN presses "Kết nối lại Google Drive". Produces the Google consent URL for the browser to follow;
/// carries no input because there is exactly one shared Drive account per deployment.
/// </summary>
public sealed record StartGoogleDriveOAuthCommand : IRequest<GoogleDriveOAuthStartResultDto>;
