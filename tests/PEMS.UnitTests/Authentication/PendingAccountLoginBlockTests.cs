using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using PEMS.Application.Authentication.Commands.RefreshToken;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using Xunit;

namespace PEMS.UnitTests.Authentication;

/// <summary>
/// P0 #1 login enforcement: a <c>PENDING_EMAIL_CONFIRMATION</c> account cannot obtain access. The refresh
/// boundary re-checks account state on every call and rejects any non-active status — session revoked, no
/// tokens issued. Credentials and SSO login gate on the SAME <c>Status != Active</c> rule
/// (LoginViaCredentials/LoginViaSso), so a mistyped-but-valid email can never sign in before confirming.
/// </summary>
public class PendingAccountLoginBlockTests
{
    private sealed class FakeSessionService : ISessionService
    {
        public UserSession? Session { get; set; }
        public List<(ulong SessionId, string Reason)> RevokeCalls { get; } = new();
        public bool Rotated { get; private set; }

        public Task<UserSession?> GetActiveByRefreshTokenAsync(string t, CancellationToken ct = default) => Task.FromResult(Session);
        public Task RevokeSessionAsync(ulong sessionId, string reason, ulong? revokedBy = null, CancellationToken ct = default)
        { RevokeCalls.Add((sessionId, reason)); return Task.CompletedTask; }
        public Task<SessionTokens> RotateRefreshTokenAsync(UserSession s, CancellationToken ct = default)
        { Rotated = true; return Task.FromResult(new SessionTokens(s.SessionId, "r", new DateTime(2027, 1, 1))); }
        public Task<SessionTokens> CreateSessionAsync(User u, string p, ulong? a, string? ip, string? ua, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsSessionActiveAsync(ulong id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> RevokeAllActiveSessionsAsync(ulong userId, string reason, ulong? revokedBy = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeJwt : IJwtTokenService
    {
        public AccessTokenResult GenerateAccessToken(User user, ulong sessionId, string loginPortal) => new("access", new DateTime(2026, 12, 31));
    }

    [Fact]
    public async Task Pending_account_cannot_refresh_and_its_session_is_revoked()
    {
        var user = new User
        {
            UserId = 100,
            FullName = "Pending",
            Email = "p@fpt.edu.vn",
            RoleId = 3,
            Role = new Role { RoleId = 3, RoleCode = RoleCodes.Staff, Name = "STAFF", Status = EntityStatuses.Active, CreatedAt = new DateTime(2026, 1, 1) },
            Status = UserStatuses.PendingEmailConfirmation,
            CreatedAt = new DateTime(2026, 1, 1),
        };
        var session = new UserSession
        {
            SessionId = 500,
            UserId = 100,
            User = user,
            LoginPortal = "INTERNAL",
            CreatedAt = new DateTime(2026, 7, 1),
            ExpiresAt = new DateTime(2027, 1, 1),
        };
        var sessions = new FakeSessionService { Session = session };
        var handler = new RefreshTokenCommandHandler(sessions, new FakeJwt());

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => handler.Handle(new RefreshTokenCommand { RefreshToken = "raw" }, CancellationToken.None));

        Assert.False(sessions.Rotated);                                   // no new access/refresh token
        Assert.Contains(sessions.RevokeCalls, c => c.SessionId == 500);   // the session is killed
    }
}
