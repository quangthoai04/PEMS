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

    /// <summary>
    /// Why the row is CANCELLED or FAILED, as stored — <c>"NO_ELIGIBLE_RECIPIENTS: …"</c> for a
    /// reminder that came due with nobody left to remind. Null for the ordinary cases, including a
    /// reminder the user simply turned off, which is CANCELLED too: without this, the screen cannot
    /// tell "you switched it off" from "it could not be sent to anyone".
    /// </summary>
    public string? ErrorMessage { get; set; }
}
