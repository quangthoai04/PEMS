using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("login_logs")]
public class LoginLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    [Column("session_id")]
    public string? SessionId { get; set; }

    [Column("logout_at")]
    public DateTime? LogoutAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
