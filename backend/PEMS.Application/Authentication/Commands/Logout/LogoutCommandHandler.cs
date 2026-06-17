using MediatR;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Authentication.Commands.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, MessageResponse>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionService _sessionService;
    private readonly ISecurityAuditService _audit;

    public LogoutCommandHandler(
        ICurrentUserService currentUser,
        ISessionService sessionService,
        ISecurityAuditService audit)
    {
        _currentUser = currentUser;
        _sessionService = sessionService;
        _audit = audit;
    }

    public async Task<MessageResponse> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        // Revoke the session bound to the current access token.
        var sessionId = _currentUser.SessionId;
        if (!string.IsNullOrEmpty(sessionId))
            await _sessionService.RevokeSessionAsync(sessionId, SessionRevokeReasons.UserLogout, userId, cancellationToken);

        // Also revoke the session referenced by an explicit refresh token, if different.
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var session = await _sessionService.GetActiveByRefreshTokenAsync(request.RefreshToken, cancellationToken);
            if (session is not null && session.SessionId != sessionId)
                await _sessionService.RevokeSessionAsync(session.SessionId, SessionRevokeReasons.UserLogout, userId, cancellationToken);
        }

        await _audit.WriteSecurityEventAsync(userId, _currentUser.Email, SecurityEventTypes.Logout,
            SecuritySeverities.Low, request.IpAddress, request.UserAgent, null, cancellationToken);

        return new MessageResponse("Logged out successfully.");
    }
}
