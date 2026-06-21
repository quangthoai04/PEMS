namespace PEMS.Application.Campuses.Queries.GetActiveCampuses;

public sealed class ActiveCampusDto
{
    public ulong CampusId { get; init; }
    public string CampusCode { get; init; } = default!;
    public string CampusName { get; init; } = default!;
}
