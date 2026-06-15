using System;

namespace PEMS.Application.Calendars.Queries.ViewMyEvents;

public sealed class ViewMyEventsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}