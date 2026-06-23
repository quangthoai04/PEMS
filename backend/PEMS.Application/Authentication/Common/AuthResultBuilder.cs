using Application.Common.Interfaces;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Authentication.Common;

/// Shared "issue a session + tokens" routine used by the credential
/// and SSO login handlers so the success path stays identical.
/// </summary>
public static class AuthResultBuilder
{
    public static async Task<AuthResponse> IssueAsync(
        User user,
        string loginPortal,
        ulong? authProviderId,
        string? ipAddress,
        string? userAgent,
        ISessionService sessionService,
        IJwtTokenService jwtTokenService,
        CancellationToken cancellationToken)
    {
        var session = await sessionService.CreateSessionAsync(
            user, loginPortal, authProviderId, ipAddress, userAgent, cancellationToken);

        var accessToken = jwtTokenService.GenerateAccessToken(user, session.SessionId, loginPortal);

        return new AuthResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = session.RefreshToken,
            ExpiresAt = accessToken.ExpiresAt,
            User = AuthUserMapper.ToDto(user)
        };
    }
}
