using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

[Table("login_logs")]
public class LoginLog
{
    [Key]
    [Column("login_log_id")]
    public ulong LoginLogId { get; set; }

    [Column("user_id")]
    public ulong? UserId { get; set; }

    [Column("email")]
    public string Email { get; set; } = null!;

    [Column("login_portal")]
    public string LoginPortal { get; set; } = null!;

    /// <summary>LOCAL_PASSWORD or GOOGLE_SSO; NULL when no method was reached.</summary>
    [Column("provider_type")]
    public string? ProviderType { get; set; }

    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
