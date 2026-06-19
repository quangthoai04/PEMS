using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Calendar;

[Table("calendar_events")]
public class CalendarEvent
{
    [Key]
    [Column("calendar_event_id")]
    public ulong CalendarEventId { get; set; }

    [Column("owner_user_id")]
    public ulong OwnerUserId { get; set; }

    [Column("campus_id")]
    public ulong? CampusId { get; set; }

    [Column("visit_instance_id")]
    public ulong? VisitInstanceId { get; set; }

    [Column("logistics_item_id")]
    public ulong? LogisticsItemId { get; set; }

    [Column("source_type")]
    public string SourceType { get; set; } = "PERSONAL";

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("location")]
    public string? Location { get; set; }

    [Column("start_at")]
    public DateTime StartAt { get; set; }

    [Column("end_at")]
    public DateTime EndAt { get; set; }

    [Column("timezone")]
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";

    [Column("visibility")]
    public string Visibility { get; set; } = "PRIVATE";

    [Column("attendees_json")]
    public string? AttendeesJson { get; set; }

    [Column("reminders_json")]
    public string? RemindersJson { get; set; }

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

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
}
