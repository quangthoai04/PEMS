using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.ApiIntegrations;

[Table("api_configurations")]
public class ApiConfiguration
{
    [Key]
    [Column("api_config_id")]
    public ulong ApiConfigId { get; set; }

    [Column("api_code")]
    public string ApiCode { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("provider_name")]
    public string? ProviderName { get; set; }

    [Column("purpose")]
    public string? Purpose { get; set; }

    [Column("base_url")]
    public string BaseUrl { get; set; } = null!;

    [Column("default_method")]
    public string DefaultMethod { get; set; } = "POST";

    [Column("auth_type")]
    public string AuthType { get; set; } = "NONE";

    [Column("credentials_json")]
    public string? CredentialsJson { get; set; }

    [Column("headers_json")]
    public string? HeadersJson { get; set; }

    [Column("body_template_json")]
    public string? BodyTemplateJson { get; set; }

    [Column("settings_json")]
    public string? SettingsJson { get; set; }

    [Column("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 30;

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by")]
    public ulong? DeletedBy { get; set; }

    public virtual ICollection<ApiUsageQuota> UsageQuotas { get; set; } = new List<ApiUsageQuota>();
    public virtual ICollection<ApiRequestLog> RequestLogs { get; set; } = new List<ApiRequestLog>();
}
