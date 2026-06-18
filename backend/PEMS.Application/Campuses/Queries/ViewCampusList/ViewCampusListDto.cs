using System.Collections.Generic;

namespace PEMS.Application.Campuses.Queries.ViewCampusList;

public sealed class ViewCampusListDto
{
    public List<CampusItemDto> Campuses { get; init; } = new();
}

public sealed class CampusItemDto
{
    public string CampusId { get; init; } = null!;
    public string CampusCode { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? City { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? IcHeadUserId { get; init; }
    public string? IcHeadUserName { get; init; }
    public string Status { get; init; } = null!;
}