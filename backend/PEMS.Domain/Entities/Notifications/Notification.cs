using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Enums;

namespace PEMS.Domain.Entities.Notifications;

[Table("notifications")]
public class Notification
{
    [Key]
    [Column("notification_id")]
    public ulong NotificationId { get; set; }

    [Column("recipient_user_id")]
    public ulong RecipientUserId { get; set; }

    /// <summary>User who caused the event (actor), if any. NULL for system-generated notifications.</summary>
    [Column("actor_user_id")]
    public ulong? ActorUserId { get; set; }

    [Column("notification_type")]
    public string NotificationType { get; set; } = null!;

    /// <summary>UI grouping/filter bucket - see NotificationCategories in
    /// PEMS.Application.Notifications.Common.NotificationConstants.</summary>
    [Column("category")]
    public string Category { get; set; } = "GENERAL";

    [Column("priority")]
    public NotificationPriority Priority { get; set; } = NotificationPriority.NORMAL;

    [Column("is_action_required")]
    public bool IsActionRequired { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("message")]
    public string? Message { get; set; }

    [Column("related_type")]
    public string? RelatedType { get; set; }

    [Column("related_id")]
    public ulong? RelatedId { get; set; }

    [Column("visit_request_id")]
    public ulong? VisitRequestId { get; set; }

    [Column("visit_instance_id")]
    public ulong? VisitInstanceId { get; set; }

    [Column("campus_id")]
    public ulong? CampusId { get; set; }

    /// <summary>Frontend action key, e.g. OPEN_VISIT_DETAIL, OPEN_VISIT_INVITATION. Static -
    /// written once at creation time, never resolved at read time.</summary>
    [Column("action_type")]
    public string? ActionType { get; set; }

    /// <summary>Frontend route to open on click. Static - written once at creation time.</summary>
    [Column("action_url")]
    public string? ActionUrl { get; set; }

    /// <summary>Small JSON payload for modal/detail rendering (raw text; not a source of truth).</summary>
    [Column("metadata_json")]
    public string? MetadataJson { get; set; }

    /// <summary>Idempotency key. Combined with recipient_user_id it is UNIQUE in the DB (NULLs
    /// don't collide with each other under MySQL unique-key semantics).</summary>
    [Column("dedupe_key")]
    public string? DedupeKey { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    /// <summary>Soft archive/hide timestamp for the recipient (reserved; not used by any query yet).</summary>
    [Column("archived_at")]
    public DateTime? ArchivedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
