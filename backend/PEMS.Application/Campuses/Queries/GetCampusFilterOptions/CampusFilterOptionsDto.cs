using System.Collections.Generic;

namespace PEMS.Application.Campuses.Queries.GetCampusFilterOptions;

/// <summary>
/// UC-83 §8 filter options sourced from the database (BR-83-02 — never hard-coded).
/// The frontend can drive its dropdowns from <see cref="Cities"/> or <see cref="Campuses"/>.
/// </summary>
public sealed class CampusFilterOptionsDto
{
    public List<string> Cities { get; init; } = new();
    public List<CampusFilterOption> Campuses { get; init; } = new();
    public List<CampusStatusOption> Statuses { get; init; } = new();
}

public sealed class CampusFilterOption
{
    public ulong CampusId { get; init; }
    public string CampusCode { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? City { get; init; }
    public string Status { get; init; } = null!;
}

public sealed class CampusStatusOption
{
    public string Value { get; init; } = null!;
    public string Label { get; init; } = null!;
}
