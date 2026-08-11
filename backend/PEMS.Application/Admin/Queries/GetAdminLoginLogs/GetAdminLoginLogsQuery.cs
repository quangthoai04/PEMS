using MediatR;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Admin.Queries.GetAdminLoginLogs;

/// <summary>GET /api/admin/login-logs — paginated login_logs with time/result/provider/portal/IP/user filters.</summary>
public sealed class GetAdminLoginLogsQuery : IRequest<PaginatedResult<AdminLoginLogItemDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    /// <summary>Email keyword (also matches the user's full name).</summary>
    public string? Keyword { get; set; }
    /// <summary>SUCCESS | FAILED.</summary>
    public string? Status { get; set; }
    public string? Portal { get; set; }
    public string? Provider { get; set; }
    public string? IpAddress { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class AdminLoginLogItemDto
{
    public ulong LoginLogId { get; set; }
    public ulong? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string LoginPortal { get; set; } = string.Empty;
    public string? ProviderType { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}
