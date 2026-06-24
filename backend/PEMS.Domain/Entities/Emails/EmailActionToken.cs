using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Users;

namespace PEMS.Domain.Entities.Emails;

/// <summary>
/// PEMS v10: one-time action tokens for email buttons (accept / decline / negotiate /
/// proposal response / handover signature). Only the token hash is stored; the raw token
/// lives only in the email link. Replaces a real inbox in this phase.
/// </summary>
[Table("email_action_tokens")]
public class EmailActionToken
{
    [Key]
    [Column("email_action_token_id")]
    public ulong EmailActionTokenId { get; set; }

    [Column("token_hash")]
    public string TokenHash { get; set; } = default!;

    [Column("action_group_key")]
    public string ActionGroupKey { get; set; } = default!;

    [Column("action_context")]
    public string ActionContext { get; set; } = default!;

    [Column("target_type")]
    public string TargetType { get; set; } = default!;

    [Column("target_id")]
    public ulong TargetId { get; set; }

    [Column("intended_action")]
    public string IntendedAction { get; set; } = default!;

    [Column("recipient_user_id")]
    public ulong? RecipientUserId { get; set; }

    [Column("recipient_email")]
    public string RecipientEmail { get; set; } = default!;

    [Column("sent_email_id")]
    public ulong? SentEmailId { get; set; }

    [Column("sent_email_recipient_id")]
    public ulong? SentEmailRecipientId { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("used_action")]
    public string? UsedAction { get; set; }

    [Column("result_status")]
    public string ResultStatus { get; set; } = "PENDING";

    [Column("result_message")]
    public string? ResultMessage { get; set; }

    [Column("used_ip")]
    public string? UsedIp { get; set; }

    [Column("used_user_agent")]
    public string? UsedUserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual User? RecipientUser { get; set; }
    public virtual SentEmail? SentEmail { get; set; }
    public virtual SentEmailRecipient? SentEmailRecipient { get; set; }
}
