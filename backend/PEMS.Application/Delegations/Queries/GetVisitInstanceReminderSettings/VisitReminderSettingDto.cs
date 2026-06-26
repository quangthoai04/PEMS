using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceReminderSettings;

public sealed class GetVisitInstanceReminderSettingsResponse
{
    public List<VisitReminderSettingDto> Items { get; set; } = new();
}

/// <summary>One saved reminder schedule row. Enum columns are surfaced as their SQL string; the time
/// is formatted "HH:mm" for the time input.</summary>
public sealed class VisitReminderSettingDto
{
    public ulong ReminderSettingId { get; set; }
    public string Channel { get; set; } = default!;          // IN_APP | EMAIL
    public string TargetGroup { get; set; } = default!;      // HOST | PARTICIPANTS | HOST_AND_PARTICIPANTS
    public int DaysBefore { get; set; }
    public string ReminderTime { get; set; } = default!;     // "HH:mm"
    public string ScheduledAt { get; set; } = default!;      // "yyyy-MM-ddTHH:mm:ss" wall-clock
    public string Status { get; set; } = default!;           // PENDING | SENT | CANCELLED | FAILED
}
