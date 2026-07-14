using Application.Common.Interfaces;
using MediatR;
using PEMS.Application.Authentication.Common;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Application.Authentication.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private const string ExpiredMessage = "Your session has expired. Please sign in again.";

    private readonly ISessionService _sessionService;
    private readonly IJwtTokenService _jwtTokenService;

    public RefreshTokenCommandHandler(
        ISessionService sessionService,
        IJwtTokenService jwtTokenService)
    {
        _sessionService = sessionService;
        _jwtTokenService = jwtTokenService;
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
            || user.Role.Status != EntityStatuses.Active)
        {
            await _sessionService.RevokeSessionAsync(session.SessionId, SessionRevokeReasons.AccountDeactivated, null, cancellationToken);
            throw new AuthenticationFailedException(ExpiredMessage, "account_or_role_inactive");
        }

        // UC-106: a DEPARTMENT account may not refresh while its department is INACTIVE.
        // Revoke the session so the old refresh token stays dead even after re-enable.
        if (DepartmentAccessRule.IsBlocked(user.Role.RoleCode, user.Department?.Status))
        {
            await _sessionService.RevokeSessionAsync(session.SessionId, SessionRevokeReasons.DepartmentDisabled, null, cancellationToken);
            throw new AuthenticationFailedException(ExpiredMessage, "department_inactive");
        }

        // UC-86 force-logout: a STAFF/DEPARTMENT/STUDENT account may not refresh while its
        // primary campus is INACTIVE. Revoke the session so the old refresh token stays dead
        // even after the campus is re-enabled (BR-AUTH-CAMPUS-09). 403 + machine-readable code
        // (not a generic "session expired") so the frontend force-logs-out with the right
        // message (doc §11: never a vague Unauthorized/Session expired).
        if (CampusAccessRule.IsBlocked(user.Role.RoleCode, user.PrimaryCampus?.Status))
        {
            await _sessionService.RevokeSessionAsync(session.SessionId, SessionRevokeReasons.CampusDisabled, null, cancellationToken);
            throw new AuthBusinessException(
                AuthErrorCodes.CampusInactiveAccessDenied, CampusAccessRule.BlockedMessage, 403);
        }

        var rotated = await _sessionService.RotateRefreshTokenAsync(session, cancellationToken);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, session.SessionId, session.LoginPortal);

        return new AuthResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = rotated.RefreshToken,
            ExpiresAt = accessToken.ExpiresAt,
            User = AuthUserMapper.ToDto(user)
        };
    }
}
