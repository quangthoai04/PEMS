using System;

namespace PEMS.Application.MeetingMinutes.Queries.SearchAndFilterMinutes;

public sealed class SearchAndFilterMinutesDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}