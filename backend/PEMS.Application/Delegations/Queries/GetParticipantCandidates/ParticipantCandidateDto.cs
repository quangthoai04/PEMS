namespace PEMS.Application.Delegations.Queries.GetParticipantCandidates;

/// <summary>A user eligible to be invited (or assigned) as a supporting participant, annotated with
/// schedule-conflict info against the campus instance window and whether they can currently be
/// invited. Reused by the IC-support / Student candidate search and the Department-staff search.</summary>
public sealed class ParticipantCandidateDto
{
    public ulong UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string? StudentCode { get; set; }

    public string RoleCode { get; set; } = default!;
    public string? SubRole { get; set; }
    public ulong? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public ulong? CampusId { get; set; }
    public string? CampusName { get; set; }

    /// <summary>Number of overlapping calendar events / live visit assignments.</summary>
    public int ConflictCount { get; set; }
    /// <summary>True when at least one conflict is a private personal calendar event (masked).</summary>
    public bool HasPrivateConflict { get; set; }
    public string? ConflictSummary { get; set; }

    /// <summary>False when the candidate already has an active row in this instance (or other rule);
    /// the UI disables the invite button and shows <see cref="DisabledReason"/>.</summary>
    public bool CanInvite { get; set; } = true;
    public string? DisabledReason { get; set; }
}
