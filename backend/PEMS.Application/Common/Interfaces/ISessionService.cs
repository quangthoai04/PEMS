using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Common.Interfaces;

/// <summary>Tokens produced when creating or rotating a session.</summary>
public sealed record SessionTokens(
    string SessionId,
    string RefreshToken,
    DateTime RefreshExpiresAt,
    DateTime SessionExpiresAt);

/// <summary>
/// Manages <c>user_sessions</c> rows and the associated refresh tokens. Refresh
/// tokens are generated as cryptographically-random strings and only ever stored
/// hashed (SHA-256); the raw value is returned to the caller exactly once.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Creates a new active session for the user and returns the raw refresh token.
    /// Persists immediately. The DB trigger enforces portal/campus rules.
    /// </summary>
    Task<SessionTokens> CreateSessionAsync(
        User user,
        string loginPortal,
        string? authProviderId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active (not revoked / not expired) session matching the raw
    /// refresh token, with <c>User</c> and <c>Role</c> loaded, or null.
    /// </summary>
    Task<UserSession?> GetActiveByRefreshTokenAsync(string rawRefreshToken, CancellationToken cancellationToken = default);

    /// <summary>True when the session exists, is not revoked and has not expired.</summary>
    Task<bool> IsSessionActiveAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Rotates the refresh token of an existing session and returns the new raw token.</summary>
    Task<SessionTokens> RotateRefreshTokenAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>Revokes a single session (logout).</summary>
    Task RevokeSessionAsync(string sessionId, string reason, string? revokedBy = null, CancellationToken cancellationToken = default);

    /// <summary>Revokes every active session of a user. Returns the number revoked.</summary>
    Task<int> RevokeAllActiveSessionsAsync(string userId, string reason, string? revokedBy = null, CancellationToken cancellationToken = default);
}
