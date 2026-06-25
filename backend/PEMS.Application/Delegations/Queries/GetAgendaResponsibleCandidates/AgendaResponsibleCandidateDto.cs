namespace PEMS.Application.Delegations.Queries.GetAgendaResponsibleCandidates;

/// <summary>
/// One person eligible to be assigned as the responsible person of an agenda item: the instance's
/// current host, or an ACCEPTED supporting participant (IC_SUPPORT / DEPT_SUPPORT / STUDENT). The
/// user is always ACTIVE. This is the ONLY valid source for the "Người phụ trách" dropdown — never
/// the whole-system user list.
/// </summary>
public sealed class AgendaResponsibleCandidateDto
{
    public ulong UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;

    /// <summary>The raw participant role (IC_HOST / IC_SUPPORT / DEPT_SUPPORT / STUDENT).</summary>
    public string ParticipantRole { get; init; } = string.Empty;

    /// <summary>Human-readable role label for the dropdown (e.g. "Host chính", "IC Support").</summary>
    public string DisplayRole { get; init; } = string.Empty;

    public bool IsMainHost { get; init; }
}
