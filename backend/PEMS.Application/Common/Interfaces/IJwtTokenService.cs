using PEMS.Domain.Entities.Users;

namespace Application.Common.Interfaces;

/// <summary>Result of generating an access token.</summary>
public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);

public interface IJwtTokenService
{
    /// <summary>
    /// Builds a signed JWT access token for the given user and active session.
    /// The user is expected to have <see cref="User.Role"/> loaded.
    /// </summary>
    AccessTokenResult GenerateAccessToken(User user, string sessionId, string loginPortal);
}
