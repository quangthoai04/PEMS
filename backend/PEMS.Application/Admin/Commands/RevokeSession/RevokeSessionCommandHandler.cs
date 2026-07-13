using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Admin.Commands.RevokeSession;

public sealed class RevokeSessionCommandHandler : IRequestHandler<RevokeSessionCommand, RevokeSessionResponse>
{
    private const string AdminRevokeReason = "ADMIN_REVOKED";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionService _sessionService;
    private readonly IDateTimeService _clock;

    public RevokeSessionCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISessionService sessionService,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _sessionService = sessionService;
        _clock = clock;
    }

    public async Task<RevokeSessionResponse> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        AdminAccess.EnsureAdmin(_currentUser);

        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken)
            ?? throw new NotFoundException("UserSession", request.SessionId);

        if (session.RevokedAt != null)
        {
            return new RevokeSessionResponse
            {
                SessionId = session.SessionId,
                Revoked = false,
                WasCurrentSession = false,
                Message = "Phiên này đã bị thu hồi trước đó.",
            };
        }

        await _sessionService.RevokeSessionAsync(
            session.SessionId, AdminRevokeReason, _currentUser.UserId, cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            Action = "ADMIN_REVOKE_SESSION",
            EntityType = "UserSession",
            EntityId = session.SessionId,
            CreatedAt = _clock.VietnamNow,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new RevokeSessionResponse
        {
            SessionId = session.SessionId,
            Revoked = true,
            WasCurrentSession = _currentUser.SessionId == session.SessionId,
            Message = "Đã thu hồi phiên đăng nhập.",
        };
    }
}
