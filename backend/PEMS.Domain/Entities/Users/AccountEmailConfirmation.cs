using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

/// <summary>
/// One-time email-ownership proof for a newly-created internal account (P0 #1). The account is created
/// <c>PENDING_EMAIL_CONFIRMATION</c> and is activated ONLY when a matching, unexpired, PENDING token is
/// confirmed. Only the token HASH is stored — the raw token lives solely in the confirmation link that is
/// emailed to the account owner. There is at most one PENDING row per user; a resend or email edit
/// supersedes the previous row.
/// </summary>
[Table("account_email_confirmations")]
public class AccountEmailConfirmation
{
    [Key]
    [Column("confirmation_id")]
    public ulong ConfirmationId { get; set; }

    [Column("user_id")]
    public ulong UserId { get; set; }

    /// <summary>The email whose ownership this token proves (the account's normalized email at issue time).</summary>
    [Column("target_email")]
    public string TargetEmail { get; set; } = default!;

    /// <summary>SHA-256 (hex) of the raw token — never the raw token itself.</summary>
    [Column("token_hash")]
    public string TokenHash { get; set; } = default!;

    /// <summary>PENDING / CONFIRMED / EXPIRED / SUPERSEDED / CANCELLED — see AccountEmailConfirmationStatuses.</summary>
    [Column("status")]
    public string Status { get; set; } = "PENDING";

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>How many times a fresh token was re-issued for this account (rate-limited).</summary>
    [Column("resend_count")]
    public int ResendCount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    public virtual User? User { get; set; }
}
