using MediatR;

namespace PEMS.Application.Campuses.Queries.GetCampusStatusImpact;

/// <summary>
/// UC-86 §18 status-impact preview, called before the HO confirms a status change.
/// Read-only — the PATCH-equivalent command still rechecks everything in its own
/// transaction (BR-86-17). Bound from the query string.
/// </summary>
public sealed class GetCampusStatusImpactQuery : IRequest<GetCampusStatusImpactResponse>
{
    public ulong CampusId { get; set; }

    /// <summary>Requested target status: ACTIVE or INACTIVE.</summary>
    public string TargetStatus { get; set; } = string.Empty;
}
