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

    // SHA-256 of the opaque session/challenge token returned by /initiate.
    // The raw token is never persisted or logged. NULL on legacy rows.
    [Column("challenge_token_hash")]
    public string? ChallengeTokenHash { get; set; }

    // UUID of the UC17 submit intent this challenge is bound to. NULL on legacy rows.
    [Column("submission_id")]
    public string? SubmissionId { get; set; }

    // INITIAL / RESEND / HUMAN_RECOVERY (see OtpIssueReasons).
    [Column("issue_reason")]
    public string IssueReason { get; set; } = "INITIAL";

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("attempt_count")]
    public int AttemptCount { get; set; }

    [Column("last_attempt_at")]
    public DateTime? LastAttemptAt { get; set; }

    // Server-enforced progressive cooldown: verify before this instant is rejected (429)
    // without consuming an attempt.
    [Column("next_attempt_allowed_at")]
    public DateTime? NextAttemptAllowedAt { get; set; }

    // Set when the max_attempts-th wrong code was entered — the challenge can only be
    // recovered through human verification (Turnstile), never by plain resend.
    [Column("human_verification_required_at")]
    public DateTime? HumanVerificationRequiredAt { get; set; }

    [Column("human_verified_at")]
    public DateTime? HumanVerifiedAt { get; set; }

    [Column("invalidated_at")]
    public DateTime? InvalidatedAt { get; set; }

    [Column("invalidation_reason")]
    public string? InvalidationReason { get; set; }

    [Column("max_attempts")]
    public int MaxAttempts { get; set; } = 10;

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
