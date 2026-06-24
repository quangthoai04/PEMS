using MediatR;

namespace PEMS.Application.Campuses.Queries.GetCampusFilterOptions;

/// <summary>UC-83 — campus/city/status filter options for the HO campus screen.</summary>
public class GetCampusFilterOptionsQuery : IRequest<CampusFilterOptionsDto>
{
}
