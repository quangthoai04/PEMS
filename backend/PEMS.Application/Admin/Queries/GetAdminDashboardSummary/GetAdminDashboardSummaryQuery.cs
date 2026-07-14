using MediatR;

namespace PEMS.Application.Admin.Queries.GetAdminDashboardSummary;

/// <summary>GET /api/admin/dashboard/summary — ADMIN-only system overview counters.</summary>
public sealed class GetAdminDashboardSummaryQuery : IRequest<AdminDashboardSummaryDto>
{
}

public sealed class AdminDashboardSummaryDto
{
    public AccountSummaryDto Accounts { get; set; } = new();
    public SessionSummaryDto Sessions { get; set; } = new();
    public LoginSummaryDto Logins24h { get; set; } = new();
    public SecuritySummaryDto Security { get; set; } = new();
    public IntegrationSummaryDto Integrations { get; set; } = new();

    public sealed class AccountSummaryDto
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
        public int Locked { get; set; }
        public int NewLast30Days { get; set; }
    }

    public sealed class SessionSummaryDto
    {
        public int Active { get; set; }
        public int Expired { get; set; }
        public int Revoked { get; set; }
    }

    public sealed class LoginSummaryDto
    {
        public int Success { get; set; }
        public int Failed { get; set; }
    }

    public sealed class SecuritySummaryDto
    {
        /// <summary>HIGH/CRITICAL security events in the last 7 days.</summary>
        public int HighLast7Days { get; set; }
        public int CriticalLast7Days { get; set; }
    }

    public sealed class IntegrationSummaryDto
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int TestFailed { get; set; }
        public int MissingCredential { get; set; }
        /// <summary>Configs whose current-month GLOBAL quota usage is above 80%.</summary>
        public int QuotaAbove80Percent { get; set; }
    }
}
