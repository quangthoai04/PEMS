using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.ApiIntegrations;

[Table("api_usage_quotas")]
public class ApiUsageQuota
{
    [Key]
    [Column("api_usage_quota_id")]
    public string ApiUsageQuotaId { get; set; } = null!;

    [Column("api_config_id")]
    public string ApiConfigId { get; set; } = null!;

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("campus_scope_key")]
    public string CampusScopeKey { get; set; } = "GLOBAL";

    [Column("period_yyyymm")]
    public string PeriodYyyymm { get; set; } = null!;

    [Column("monthly_limit")]
    public int MonthlyLimit { get; set; }

    [Column("used_count")]
    public int UsedCount { get; set; }

    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    public virtual ApiConfiguration ApiConfiguration { get; set; } = null!;
}
