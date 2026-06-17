using Application.Common.Interfaces;
using MediatR;
using PEMS.Application.Authentication.Common;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Authentication.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private const string ExpiredMessage = "Your session has expired. Please sign in again.";

    private readonly ISessionService _sessionService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPermissionChecker _permissionChecker;

    public RefreshTokenCommandHandler(
        ISessionService sessionService,
        IJwtTokenService jwtTokenService,
        IPermissionChecker permissionChecker)
    {
        _sessionService = sessionService;
        _jwtTokenService = jwtTokenService;
        _permissionChecker = permissionChecker;
    }

    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionService.GetActiveByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (session is null)
            throw new AuthenticationFailedException(ExpiredMessage, "invalid_or_expired_refresh_token");

        var user = session.User;

        // Re-check account / role state on every refresh.
        if (user is null
            || user.Status != UserStatuses.Active
            || user.Role is null
            || user.Role.Status != EntityStatuses.Active
            || user.Role.DeletedAt is not null)
        {
            await _sessionService.RevokeSessionAsync(session.SessionId, SessionRevokeReasons.AccountDeactivated, null, cancellationToken);
            throw new AuthenticationFailedException(ExpiredMessage, "account_or_role_inactive");
        }

        var rotated = await _sessionService.RotateRefreshTokenAsync(session, cancellationToken);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, session.SessionId, session.LoginPortal);
        var permissions = await _permissionChecker.GetPermissionsForRoleAsync(user.RoleId, cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = rotated.RefreshToken,
            ExpiresAt = accessToken.ExpiresAt,
            User = AuthUserMapper.ToDto(user),
            Permissions = permissions
        };
    }
}
