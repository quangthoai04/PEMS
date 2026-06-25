namespace PEMS.Application.Delegations.Queries.GetVisitProcessDetail;

public sealed class VisitProcessDetailDto
{
    public ulong VisitRequestId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public string DelegationName { get; set; } = default!;
    public string InstanceStatus { get; set; } = default!;
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }
    public string? CampusName { get; set; }
    public ulong? HostUserId { get; set; }
    public string? HostName { get; set; }

    /// <summary>HOST | STAFF_LEADER | HO | VISITOR_OWNER | IC_SUPPORT | DEPT_SUPPORT | STUDENT | NONE.</summary>
    public string Relation { get; set; } = "NONE";

    /// <summary>True only for the official host while the instance is in the editable prep window.</summary>
    public bool CanEditBefore { get; set; }

    public List<AgendaItemDto> Agenda { get; set; } = new();
}

public sealed class AgendaItemDto
{
    public ulong AgendaId { get; set; }
    public string Title { get; set; } = default!;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
}
