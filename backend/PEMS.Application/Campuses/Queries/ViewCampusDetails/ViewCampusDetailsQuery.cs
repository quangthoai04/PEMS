using MediatR;

namespace PEMS.Application.Campuses.Queries.ViewCampusDetails;

/// <summary>UC-84 View Campus Details. Bound from the query string (?campusId=).</summary>
public class ViewCampusDetailsQuery : IRequest<ViewCampusDetailsDto>
{
    public ulong CampusId { get; set; }
}
