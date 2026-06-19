using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

[Table("otp_tokens")]
public class OtpToken
{
    [Key]
    [Column("otp_token_id")]
    public ulong OtpTokenId { get; set; }

    [Column("user_id")]
    public ulong? UserId { get; set; }

    [Column("email")]
    public string Email { get; set; } = null!;

    [Column("token_type")]
    public string TokenType { get; set; } = "OTP_CODE";

    [Column("purpose")]
    public string Purpose { get; set; } = null!;

    [Column("token_hash")]
    public string TokenHash { get; set; } = null!;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("attempt_count")]
    public int AttemptCount { get; set; }

    [Column("max_attempts")]
    public int MaxAttempts { get; set; } = 5;

    [Column("resend_count")]
    public int ResendCount { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
