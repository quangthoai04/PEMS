using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Admin.Queries.GetAdminSessions;

public sealed class GetAdminSessionsQueryHandler
    : IRequestHandler<GetAdminSessionsQuery, PaginatedResult<AdminSessionItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public GetAdminSessionsQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PaginatedResult<AdminSessionItemDto>> Handle(
        GetAdminSessionsQuery request, CancellationToken cancellationToken)
    {
        AdminAccess.EnsureAdmin(_currentUser);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var now = _clock.VietnamNow;

        var query = _db.UserSessions.AsNoTracking();

        if (request.UserId.HasValue)
            query = query.Where(s => s.UserId == request.UserId.Value);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim().ToLower();
            query = query.Where(s =>
                s.User.Email.ToLower().Contains(kw) || s.User.FullName.ToLower().Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(request.Portal))
        {
            var portal = request.Portal.Trim().ToUpperInvariant();
            query = query.Where(s => s.LoginPortal == portal);
        }

        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            var provider = request.Provider.Trim().ToUpperInvariant();
            query = query.Where(s => s.AuthProvider != null && s.AuthProvider.ProviderType == provider);
        }

        var status = (request.Status ?? string.Empty).Trim().ToUpperInvariant();
        query = status switch
        {
            "ACTIVE" => query.Where(s => s.RevokedAt == null && s.ExpiresAt > now),
            "EXPIRED" => query.Where(s => s.RevokedAt == null && s.ExpiresAt <= now),
            "REVOKED" => query.Where(s => s.RevokedAt != null),
            _ => query,
        };

        var totalItems = await query.CountAsync(cancellationToken);
        var currentSessionId = _currentUser.SessionId ?? 0;

        var items = await query
            .OrderByDescending(s => s.SessionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new AdminSessionItemDto
            {
                SessionId = s.SessionId,
                UserId = s.UserId,
                Email = s.User.Email,
                FullName = s.User.FullName,
                RoleCode = s.User.Role.RoleCode,
                LoginPortal = s.LoginPortal,
                ProviderType = s.AuthProvider != null ? s.AuthProvider.ProviderType : null,
                IpAddress = s.IpAddress,
                UserAgent = s.UserAgent,
                CreatedAt = s.CreatedAt,
                ExpiresAt = s.ExpiresAt,
                RevokedAt = s.RevokedAt,
                RevokedReason = s.RevokedReason,
                Status = s.RevokedAt != null ? "REVOKED" : (s.ExpiresAt > now ? "ACTIVE" : "EXPIRED"),
                IsCurrentSession = currentSessionId != 0 && s.SessionId == currentSessionId,
            })
            .ToListAsync(cancellationToken);

        return PaginatedResult<AdminSessionItemDto>.Create(items, page, pageSize, totalItems);
    }
}
