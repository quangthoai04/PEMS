using System;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.GetHostCandidates;

public sealed class HostCandidateDto
{
    public ulong UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public ulong? CampusId { get; set; }
    public string? DepartmentName { get; set; }
    public string? SubRole { get; set; }

    /// <summary>True when the candidate has any schedule overlap with the target visit window.</summary>
    public bool HasScheduleConflict { get; set; }
    /// <summary>Number of overlapping events (calendar + other hosting assignments).</summary>
    public int ConflictCount { get; set; }
    public List<HostConflictDto> Conflicts { get; set; } = new();
}

public sealed class HostConflictDto
{
    /// <summary>Where the conflict comes from: "CALENDAR" or "VISIT_INSTANCE".</summary>
    public string Source { get; set; } = default!;
    /// <summary>Display title. Private personal calendar events are masked to "Lịch cá nhân".</summary>
    public string? Title { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public ulong? VisitInstanceId { get; set; }
    public ulong? CalendarEventId { get; set; }
}
