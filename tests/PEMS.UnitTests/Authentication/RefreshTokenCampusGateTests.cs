using Application.Common.Interfaces;
using PEMS.Application.Authentication.Commands.RefreshToken;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Users;
using Xunit;

namespace PEMS.UnitTests.Authentication;

/// <summary>
/// UC-86 force-logout at the refresh boundary (doc 08 §10/§18): a STAFF/DEPARTMENT/STUDENT
/// account may not refresh while its primary campus is INACTIVE — no new tokens, the session
/// is revoked (stays dead even after re-enable), and the error is the machine-readable
/// CAMPUS_INACTIVE_ACCESS_DENIED (403), never a vague "session expired".
/// </summary>
public class RefreshTokenCampusGateTests
{
    private sealed class FakeSessionService : ISessionService
    {
        public UserSession? Session { get; set; }
        public List<(ulong SessionId, string Reason)> RevokeCalls { get; } = new();
        public bool Rotated { get; private set; }

        public Task<UserSession?> GetActiveByRefreshTokenAsync(string rawRefreshToken, CancellationToken ct = default)
            => Task.FromResult(Session);

        public Task RevokeSessionAsync(ulong sessionId, string reason, ulong? revokedBy = null, CancellationToken ct = default)
        {
            RevokeCalls.Add((sessionId, reason));
            return Task.CompletedTask;
        }

        public Task<SessionTokens> RotateRefreshTokenAsync(UserSession session, CancellationToken ct = default)
        {
            Rotated = true;
            return Task.FromResult(new SessionTokens(session.SessionId, "new-refresh-token",
                new DateTime(2027, 1, 1)));
        }

        public Task<SessionTokens> CreateSessionAsync(User user, string loginPortal, ulong? authProviderId,
            string? ipAddress, string? userAgent, CancellationToken ct = default)
            => throw new NotSupportedException("Refresh must not create sessions.");

        public Task<bool> IsSessionActiveAsync(ulong sessionId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<int> RevokeAllActiveSessionsAsync(ulong userId, string reason, ulong? revokedBy = null, CancellationToken ct = default)
            => throw new NotSupportedException("Refresh revokes the single session only.");
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public AccessTokenResult GenerateAccessToken(User user, ulong sessionId, string loginPortal)
            => new("access-token", new DateTime(2026, 12, 31));
    }

    private static UserSession CreateSession(string campusStatus, string roleCode = RoleCodes.Staff)
    {
        var campus = new Campus
        {
            CampusId = 1,
            CampusCode = "C1",
            Name = "Campus 1",
            Status = campusStatus,
            CreatedAt = new DateTime(2026, 1, 1),
        };
        var user = new User
        {
            UserId = 100,
            FullName = "User 100",
            Email = "user100@test.local",
            RoleId = 3,
            Role = new Role { RoleId = 3, RoleCode = roleCode, Name = roleCode, Status = EntityStatuses.Active, CreatedAt = new DateTime(2026, 1, 1) },
            SubRole = roleCode == RoleCodes.Staff ? UserSubRoles.Leader : null,
            Status = UserStatuses.Active,
            PrimaryCampusId = campus.CampusId,
            PrimaryCampus = campus,
            CreatedAt = new DateTime(2026, 1, 1),
        };
        return new UserSession
        {
            SessionId = 500,
            UserId = user.UserId,
            User = user,
            LoginPortal = "INTERNAL",
            CreatedAt = new DateTime(2026, 7, 1),
            ExpiresAt = new DateTime(2027, 1, 1),
        };
    }

    [Fact]
    public async Task Refresh_CampusInactive_Returns403Code_RevokesSession_IssuesNoTokens()
    {
        var sessions = new FakeSessionService { Session = CreateSession(EntityStatuses.Inactive) };
        var handler = new RefreshTokenCommandHandler(sessions, new FakeJwtTokenService());

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(
            () => handler.Handle(new RefreshTokenCommand { RefreshToken = "raw" }, CancellationToken.None));

        Assert.Equal(AuthErrorCodes.CampusInactiveAccessDenied, ex.ErrorCode);
        Assert.Equal(403, ex.StatusCode);
        var revoke = Assert.Single(sessions.RevokeCalls);
        Assert.Equal(500UL, revoke.SessionId);
        Assert.Equal(SessionRevokeReasons.CampusDisabled, revoke.Reason);
        Assert.False(sessions.Rotated); // no new refresh token, no new access token
    }

    [Fact]
    public async Task Refresh_CampusActive_IssuesNewTokens()
    {
        var sessions = new FakeSessionService { Session = CreateSession(EntityStatuses.Active) };
        var handler = new RefreshTokenCommandHandler(sessions, new FakeJwtTokenService());

        var response = await handler.Handle(
            new RefreshTokenCommand { RefreshToken = "raw" }, CancellationToken.None);

        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("new-refresh-token", response.RefreshToken);
        Assert.Empty(sessions.RevokeCalls);
    }

    [Fact]
    public async Task Refresh_HoOfInactiveCampus_IsNotBlockedByCampusGate()
    {
        var session = CreateSession(EntityStatuses.Inactive, RoleCodes.Ho);
        var sessions = new FakeSessionService { Session = session };
        var handler = new RefreshTokenCommandHandler(sessions, new FakeJwtTokenService());

        var response = await handler.Handle(
            new RefreshTokenCommand { RefreshToken = "raw" }, CancellationToken.None);

        Assert.Equal("access-token", response.AccessToken);
        Assert.Empty(sessions.RevokeCalls);
    }
}
