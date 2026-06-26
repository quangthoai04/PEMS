using System;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceParticipants;

/// <summary>One participant row of a campus instance (host snapshot + invited supporters). The
/// frontend renders status badges and invite/remove affordances purely from these fields — it
/// never fabricates a response on behalf of the invitee.</summary>
public sealed class VisitParticipantListItemDto
{
    public ulong ParticipantId { get; set; }
    public ulong UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }

    public string RoleCode { get; set; } = default!;
    public string? SubRole { get; set; }
    public ulong? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    /// <summary>IC_HOST | IC_SUPPORT | DEPT_SUPPORT | STUDENT.</summary>
    public string ParticipantRole { get; set; } = default!;
    public bool IsHost { get; set; }
    /// <summary>INVITED | ACCEPTED | DECLINED | ASSIGNED | REMOVED.</summary>
    public string Status { get; set; } = default!;

    public ulong? InvitedByUserId { get; set; }
    public string? InvitedByName { get; set; }
    public DateTime? InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public ulong? AssignedByUserId { get; set; }
    public string? AssignedByName { get; set; }
    public DateTime? AssignedAt { get; set; }
    /// <summary>Free-text note; for a DECLINED row this holds the decline reason.</summary>
    public string? Note { get; set; }

    /// <summary>Populated only for a Department-Leader DEPT_SUPPORT row: which staff (if any) the
    /// leader delegated the task to. Null for non-leader / non-department rows.</summary>
    public DepartmentAssignmentDto? DepartmentAssignment { get; set; }
}

public sealed class DepartmentAssignmentDto
{
    public ulong DepartmentId { get; set; }
    public string DepartmentName { get; set; } = default!;
    public ulong LeaderUserId { get; set; }
    public ulong? AssignedStaffUserId { get; set; }
    public string? AssignedStaffName { get; set; }
}
