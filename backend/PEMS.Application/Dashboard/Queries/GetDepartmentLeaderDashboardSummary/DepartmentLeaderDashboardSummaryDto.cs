using System;
using System.Collections.Generic;

namespace PEMS.Application.Dashboard.Queries.GetDepartmentLeaderDashboardSummary;

public class DepartmentLeaderQuickTaskDto
{
    public string ItemType { get; set; } = "REQUEST";
    public ulong LogisticsItemId { get; set; }
    public ulong? ParticipantId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public ulong VisitRequestId { get; set; }
    public string DelegationName { get; set; } = null!;
    public string TaskTitle { get; set; } = null!;
    public string? DueAt { get; set; }
    public string Status { get; set; } = null!;
    public ulong? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
}

public class DepartmentLeaderUpcomingScheduleDto
{
    public string ItemType { get; set; } = "REQUEST";
    public ulong? LogisticsItemId { get; set; }
    public ulong? ParticipantId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public ulong VisitRequestId { get; set; }
    public string DelegationName { get; set; } = null!;
    public string? OrganizationName { get; set; }
    public string PlannedStartAt { get; set; } = null!;
    public string PlannedEndAt { get; set; } = null!;
    public string CampusName { get; set; } = null!;
    public string? Location { get; set; }
    public string Status { get; set; } = null!;
}

public class DepartmentLeaderDashboardSummaryDto
{
    public string ServerNow { get; set; } = null!;
    public int PendingAssignmentCount { get; set; }
    public int UpcomingDelegationCount { get; set; }
    public int ProcessingDelegationCount { get; set; }
    public int ActivePersonnelCount { get; set; }
    public List<DepartmentLeaderQuickTaskDto> QuickTasks { get; set; } = new();
    public List<DepartmentLeaderUpcomingScheduleDto> UpcomingSchedules { get; set; } = new();
}
