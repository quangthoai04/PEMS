using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Enums;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// PEMS v10 (2026-06-26). A scheduled IN_APP/EMAIL reminder configuration for one campus visit
/// instance. The "Cảnh báo & Thông báo" section of VisitProcess persists these rows; a background
/// job dispatches PENDING rows when <see cref="ScheduledAt"/> is reached. Nothing is sent on save.
/// Unique per (visit_instance_id, channel, target_group).
/// </summary>
[Table("visit_instance_reminder_settings")]
public class VisitInstanceReminderSetting
{
    [Key]
    [Column("reminder_setting_id")]
    public ulong ReminderSettingId { get; set; }

    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("channel")]
    public VisitReminderChannel Channel { get; set; }

    [Column("target_group")]
    public VisitReminderTargetGroup TargetGroup { get; set; }

    [Column("days_before")]
    public int DaysBefore { get; set; }

    [Column("reminder_time")]
    public TimeSpan ReminderTime { get; set; }

    [Column("scheduled_at")]
    public DateTime ScheduledAt { get; set; }

    [Column("status")]
    public VisitReminderStatus Status { get; set; } = VisitReminderStatus.PENDING;

    [Column("last_dispatched_at")]
    public DateTime? LastDispatchedAt { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual VisitRequestCampus VisitInstance { get; set; } = null!;
}
