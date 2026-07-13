using MediatR;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Admin.Queries.GetAdminSessions;

/// <summary>
/// GET /api/admin/sessions — paginated user_sessions view for the ADMIN console.
/// Status filter: ACTIVE (not revoked, not expired), EXPIRED, REVOKED.
/// </summary>
public sealed class GetAdminSessionsQuery : IRequest<PaginatedResult<AdminSessionItemDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    /// <summary>Email/full-name keyword.</summary>
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? Portal { get; set; }
    public string? Provider { get; set; }
    public ulong? UserId { get; set; }
}

public sealed class AdminSessionItemDto
{
    public ulong SessionId { get; set; }
    public ulong UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? RoleCode { get; set; }
    public string LoginPortal { get; set; } = string.Empty;
    public string? ProviderType { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    /// <summary>ACTIVE | EXPIRED | REVOKED (computed).</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>True when this row is the caller's own current session.</summary>
    public bool IsCurrentSession { get; set; }
}
