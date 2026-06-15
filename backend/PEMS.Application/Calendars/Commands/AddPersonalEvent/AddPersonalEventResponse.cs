using System;

namespace PEMS.Application.Calendars.Commands.AddPersonalEvent;

public sealed class AddPersonalEventResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}