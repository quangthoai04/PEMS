using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Commands.CompleteGoogleDriveOAuth;

/// <summary>
/// Everything Google put on the callback URL. Anonymous by necessity — a browser redirect carries no
/// Authorization header — so <see cref="State"/> is the only trusted field, and the only one the handler
/// takes an identity from.
/// </summary>
/// <param name="Code">The single-use authorization code, present unless <paramref name="Error"/> is.</param>
/// <param name="Error">Google's refusal (<c>access_denied</c> when the admin declined consent).</param>
/// <param name="State">The sealed value issued by the ADMIN-only start endpoint.</param>
public sealed record CompleteGoogleDriveOAuthCommand(string? Code, string? Error, string? State)
    : IRequest<GoogleDriveOAuthCallbackResultDto>;
