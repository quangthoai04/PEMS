using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Users;

namespace PEMS.Domain.Entities.Calendar;

[Table("calendar_event_attendees")]
public class CalendarEventAttendee
{
    [Key]
    [Column("calendar_event_attendee_id")]
    public ulong CalendarEventAttendeeId { get; set; }

    [Column("calendar_event_id")]
    public ulong CalendarEventId { get; set; }

    [Column("user_id")]
    public ulong? UserId { get; set; }

    [Column("attendee_email")]
    public string? AttendeeEmail { get; set; }

    [Column("attendee_name")]
    public string? AttendeeName { get; set; }

    [Column("attendee_role")]
    public string? AttendeeRole { get; set; }

    [Column("response_status")]
    public string ResponseStatus { get; set; } = "NEEDS_ACTION";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual CalendarEvent CalendarEvent { get; set; } = null!;
    public virtual User? User { get; set; }
}
