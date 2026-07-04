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

    [Column("api_key_encrypted")]
    public string? ApiKeyEncrypted { get; set; }

    [Column("bearer_token_encrypted")]
    public string? BearerTokenEncrypted { get; set; }

    [Column("basic_username")]
    public string? BasicUsername { get; set; }

    [Column("basic_password_encrypted")]
    public string? BasicPasswordEncrypted { get; set; }

    [Column("oauth_client_id")]
    public string? OauthClientId { get; set; }

    [Column("oauth_client_secret_encrypted")]
    public string? OauthClientSecretEncrypted { get; set; }

    [Column("oauth_token_url")]
    public string? OauthTokenUrl { get; set; }

    [Column("oauth_scope")]
    public string? OauthScope { get; set; }

    [Column("body_template_text")]
    public string? BodyTemplateText { get; set; }

    /// <summary>Non-secret provider settings JSON (project_id, location, processor_id, endpoint...).</summary>
    [Column("settings_json")]
    public string? SettingsJson { get; set; }

    /// <summary>Encrypted credential payload (e.g. Google service account JSON). Never exposed raw.</summary>
    [Column("credentials_json_encrypted")]
    public string? CredentialsJsonEncrypted { get; set; }

    /// <summary>Env/secret-manager reference used when credentials are not stored in DB.</summary>
    [Column("secret_ref")]
    public string? SecretRef { get; set; }

    /// <summary>PUBLIC | INTERNAL | CONFIDENTIAL | RESTRICTED</summary>
    [Column("data_sensitivity")]
    public string DataSensitivity { get; set; } = "CONFIDENTIAL";

    [Column("allows_provider_training")]
    public bool AllowsProviderTraining { get; set; }

    /// <summary>How long raw OCR text/draft is retained before purge.</summary>
    [Column("retention_days")]
    public uint? RetentionDays { get; set; }

    [Column("rate_limit_per_minute")]
    public uint? RateLimitPerMinute { get; set; }

    [Column("monthly_quota")]
    public uint? MonthlyQuota { get; set; }

    [Column("retry_enabled")]
    public bool RetryEnabled { get; set; }

    [Column("max_retries")]
    public uint MaxRetries { get; set; }

    [Column("cache_ttl_seconds")]
    public uint? CacheTtlSeconds { get; set; }

    [Column("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 30;

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>DB enum SUCCESS | FAILED; NULL = not tested yet.</summary>
    [Column("last_test_status")]
    public string? LastTestStatus { get; set; }

    [Column("last_tested_at")]
    public DateTime? LastTestedAt { get; set; }

    [Column("last_test_message")]
    public string? LastTestMessage { get; set; }

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
    public virtual ICollection<ApiConfigurationHeader> Headers { get; set; } = new List<ApiConfigurationHeader>();
}
