using MediatR;

namespace PEMS.Application.Admin.Commands.RevokeUserSessions;

/// <summary>POST /api/admin/users/{id}/revoke-sessions — ADMIN revokes every active session of one user.</summary>
public sealed class RevokeUserSessionsCommand : IRequest<RevokeUserSessionsResponse>
{
    public ulong UserId { get; set; }
    public string? Reason { get; set; }
}

public sealed class RevokeUserSessionsResponse
{
    public ulong UserId { get; set; }
    public int RevokedSessions { get; set; }
    /// <summary>True when the caller revoked their own sessions (they will be logged out).</summary>
    public bool AffectsCurrentUser { get; set; }
    public string Message { get; set; } = string.Empty;
}
