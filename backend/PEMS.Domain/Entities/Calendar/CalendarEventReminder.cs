using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Calendar;

[Table("calendar_event_reminders")]
public class CalendarEventReminder
{
    [Key]
    [Column("calendar_event_reminder_id")]
    public ulong CalendarEventReminderId { get; set; }

    [Column("calendar_event_id")]
    public ulong CalendarEventId { get; set; }

    [Column("reminder_type")]
    public string ReminderType { get; set; } = "NOTIFICATION";

    [Column("minutes_before")]
    public uint MinutesBefore { get; set; }

    [Column("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }

    [Column("status")]
    public string Status { get; set; } = "PENDING";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual CalendarEvent CalendarEvent { get; set; } = null!;
}
