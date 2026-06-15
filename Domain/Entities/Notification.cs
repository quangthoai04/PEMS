using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("notifications")]
public class Notification
{
    [Key]
    [Column("notification_id")]
    public string NotificationId { get; set; } = null!;

    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("message")]
    public string? Message { get; set; }

    [Column("type")]
    public string? Type { get; set; }

    [Column("related_entity_type")]
    public string? RelatedEntityType { get; set; }

    [Column("related_entity_id")]
    public string? RelatedEntityId { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
