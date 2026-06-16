using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Notifications;

[Table("notifications")]
public class Notification
{
    [Key]
    [Column("notification_id")]
    public string NotificationId { get; set; } = null!;

    [Column("recipient_user_id")]
    public string RecipientUserId { get; set; } = null!;

    [Column("notification_type")]
    public string NotificationType { get; set; } = null!;

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("message")]
    public string? Message { get; set; }

    [Column("related_type")]
    public string? RelatedType { get; set; }

    [Column("related_id")]
    public string? RelatedId { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
