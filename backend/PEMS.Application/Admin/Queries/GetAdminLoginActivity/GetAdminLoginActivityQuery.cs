using MediatR;

namespace PEMS.Application.Admin.Queries.GetAdminLoginActivity;

/// <summary>
/// GET /api/admin/dashboard/login-activity — daily SUCCESS/FAILED login counts for the
/// dashboard chart. <see cref="Days"/> is clamped to 1..90 (default 7).
/// </summary>
public sealed class GetAdminLoginActivityQuery : IRequest<List<AdminLoginActivityPointDto>>
{
    public int Days { get; set; } = 7;
}

public sealed class AdminLoginActivityPointDto
{
    /// <summary>Vietnam calendar date (yyyy-MM-dd).</summary>
    public string Date { get; set; } = string.Empty;
    public int Success { get; set; }
    public int Failed { get; set; }
}
