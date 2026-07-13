using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Admin.Commands.RevokeUserSessions;

public sealed class RevokeUserSessionsCommandHandler
    : IRequestHandler<RevokeUserSessionsCommand, RevokeUserSessionsResponse>
{
    private const string AdminRevokeReason = "ADMIN_REVOKED";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionService _sessionService;
    private readonly IDateTimeService _clock;

    public RevokeUserSessionsCommandHandler(
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

    public async Task<RevokeUserSessionsResponse> Handle(
        RevokeUserSessionsCommand request, CancellationToken cancellationToken)
    {
        AdminAccess.EnsureAdmin(_currentUser);

        var userExists = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.UserId == request.UserId, cancellationToken);
        if (!userExists)
            throw new NotFoundException("User", request.UserId);

        var revoked = await _sessionService.RevokeAllActiveSessionsAsync(
            request.UserId, AdminRevokeReason, _currentUser.UserId, cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            Action = "ADMIN_REVOKE_USER_SESSIONS",
            EntityType = "User",
            EntityId = request.UserId,
            CreatedAt = _clock.VietnamNow,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new RevokeUserSessionsResponse
        {
            UserId = request.UserId,
            RevokedSessions = revoked,
            AffectsCurrentUser = _currentUser.UserId == request.UserId,
            Message = revoked > 0
                ? $"Đã thu hồi {revoked} phiên đăng nhập của người dùng."
                : "Người dùng không có phiên đăng nhập nào đang hoạt động.",
        };
    }
}
