using Application.Common.Interfaces;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Authentication.Common;

/// <summary>
/// Shared "issue a session + tokens + permissions" routine used by the credential
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
        IPermissionChecker permissionChecker,
        CancellationToken cancellationToken)
    {
        var session = await sessionService.CreateSessionAsync(
            user, loginPortal, authProviderId, ipAddress, userAgent, cancellationToken);

        var accessToken = jwtTokenService.GenerateAccessToken(user, session.SessionId, loginPortal);
        var subRole = user.Role?.RoleCode is "STAFF" or "DEPT" ? (user.SubRole ?? "NONE") : "NONE";
        var permissions = await permissionChecker.GetPermissionsForRoleAsync(user.RoleId, subRole, cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = session.RefreshToken,
            ExpiresAt = accessToken.ExpiresAt,
            User = AuthUserMapper.ToDto(user),
            Permissions = permissions
        };
    }
}
