using System;

namespace PEMS.Application.Calendars.Commands.DeletePersonalEvent;

public sealed class DeletePersonalEventResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}