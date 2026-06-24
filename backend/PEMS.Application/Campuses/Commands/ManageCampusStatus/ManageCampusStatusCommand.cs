using MediatR;

namespace PEMS.Application.Campuses.Commands.ManageCampusStatus;

/// <summary>
/// UC-86 — HO enables/disables a campus. Toggle ON = ACTIVE, toggle OFF = INACTIVE.
/// </summary>
public class ManageCampusStatusCommand : IRequest<ManageCampusStatusResponse>
{
    public ulong CampusId { get; set; }
    public string Status { get; set; } = null!;

    /// <summary>Optional reason captured in the audit log.</summary>
    public string? Reason { get; set; }
}
