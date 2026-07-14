using MediatR;

namespace PEMS.Application.Admin.Commands.RevokeSession;

/// <summary>POST /api/admin/sessions/{id}/revoke — ADMIN revokes one session.</summary>
public sealed class RevokeSessionCommand : IRequest<RevokeSessionResponse>
{
    public ulong SessionId { get; set; }
    public string? Reason { get; set; }
}

public sealed class RevokeSessionResponse
{
    public ulong SessionId { get; set; }
    public bool Revoked { get; set; }
    /// <summary>True when the admin revoked their own current session (they will be logged out).</summary>
    public bool WasCurrentSession { get; set; }
    public string Message { get; set; } = string.Empty;
}
