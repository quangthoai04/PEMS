using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Identity;

/// <summary>
/// Persists <c>user_sessions</c> and manages refresh tokens (stored as SHA-256
/// hashes only). DB triggers enforce portal/campus consistency.
/// </summary>
public sealed class SessionService : ISessionService
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeService _clock;
    private readonly int _refreshDays;

    public SessionService(IApplicationDbContext db, IDateTimeService clock, IConfiguration configuration)
    {
        _db = db;
        _clock = clock;
        _refreshDays = int.TryParse(configuration["JwtSettings:RefreshTokenDays"], out var d) ? d : 7;
    }

    public async Task<SessionTokens> CreateSessionAsync(
        User user,
        string loginPortal,
        ulong? authProviderId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.VietnamNow;
        var rawRefresh = SecureTokenGenerator.GenerateOpaqueToken();
        var refreshExpires = now.AddDays(_refreshDays);

        var session = new UserSession
        {
            // SessionId is DB-generated (BIGINT AUTO_INCREMENT).
            UserId = user.UserId,
            LoginPortal = loginPortal,
            // INTERNAL → own primary campus; VISITOR → NULL (enforced by trigger).
            SelectedCampusId = loginPortal == LoginPortals.Internal ? user.PrimaryCampusId : null,
            AuthProviderId = authProviderId,
            RefreshTokenHash = SecureTokenGenerator.Hash(rawRefresh),
            RefreshExpiresAt = refreshExpires,
            IpAddress = Truncate(ipAddress, 45),
            UserAgent = Truncate(userAgent, 500),
            CreatedAt = now,
            ExpiresAt = refreshExpires
        };

        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        return new SessionTokens(session.SessionId, rawRefresh, refreshExpires, session.ExpiresAt);
    }

    public async Task<UserSession?> GetActiveByRefreshTokenAsync(
        string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            return null;

        var hash = SecureTokenGenerator.Hash(rawRefreshToken);
        var now = _clock.VietnamNow;

        return await _db.UserSessions
            .Include(s => s.User).ThenInclude(u => u.Role)
            .Include(s => s.User).ThenInclude(u => u.PrimaryCampus)
            // UC-106: refresh must re-check the CURRENT department status from the DB.
            .Include(s => s.User).ThenInclude(u => u.Department)
            .FirstOrDefaultAsync(s =>
                    s.RefreshTokenHash == hash
                    && s.RevokedAt == null
                    && s.RefreshRevokedAt == null
                    && s.RefreshExpiresAt != null && s.RefreshExpiresAt > now
                    && s.ExpiresAt > now,
                cancellationToken);
    }

    public async Task<bool> IsSessionActiveAsync(ulong sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == 0)
            return false;

        var now = _clock.VietnamNow;
        return await _db.UserSessions.AsNoTracking()
            .AnyAsync(s => s.SessionId == sessionId && s.RevokedAt == null && s.ExpiresAt > now,
                cancellationToken);
    }

    public async Task<SessionTokens> RotateRefreshTokenAsync(
        UserSession session, CancellationToken cancellationToken = default)
    {
        var now = _clock.VietnamNow;
        var rawRefresh = SecureTokenGenerator.GenerateOpaqueToken();
        var refreshExpires = now.AddDays(_refreshDays);

        session.RefreshTokenHash = SecureTokenGenerator.Hash(rawRefresh);
        session.RefreshExpiresAt = refreshExpires;
        session.ExpiresAt = refreshExpires;

        await _db.SaveChangesAsync(cancellationToken);
        return new SessionTokens(session.SessionId, rawRefresh, refreshExpires, session.ExpiresAt);
    }

    public async Task RevokeSessionAsync(
        ulong sessionId, string reason, ulong? revokedBy = null, CancellationToken cancellationToken = default)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
        if (session is null || session.RevokedAt != null)
            return;

        var now = _clock.VietnamNow;
        session.RevokedAt = now;
        session.RefreshRevokedAt = now;
        session.RevokedReason = Truncate(reason, 255);
        session.RevokedBy = revokedBy;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> RevokeAllActiveSessionsAsync(
        ulong userId, string reason, ulong? revokedBy = null, CancellationToken cancellationToken = default)
    {
        var now = _clock.VietnamNow;
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.RevokedAt = now;
            session.RefreshRevokedAt = now;
            session.RevokedReason = Truncate(reason, 255);
            session.RevokedBy = revokedBy;
        }

        if (sessions.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return sessions.Count;
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
