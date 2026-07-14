using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Campuses.Queries.GetRegistrationCampuses;

/// <summary>
/// Campus options for the visit registration form (UC-86 §10). Anonymous — returns ONLY
/// campuses that are fully available for registration (ACTIVE + exactly one ACTIVE IC
/// department + exactly one valid ACTIVE Staff Leader, BR-86-04). The frontend renders this
/// list as-is; hiding is UX only — submit rechecks server-side (BR-86-06).
/// </summary>
public sealed class GetRegistrationCampusesQuery : IRequest<List<RegistrationCampusDto>>
{
}

/// <summary>One selectable campus option. No readiness internals are exposed publicly.</summary>
public sealed class RegistrationCampusDto
{
    public ulong CampusId { get; init; }
    public string CampusCode { get; init; } = null!;
    public string CampusName { get; init; } = null!;
    public string? City { get; init; }
}
