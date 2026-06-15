using System;

namespace PEMS.Application.Delegations.Commands.CreateMeetingMinutes;

public sealed class CreateMeetingMinutesResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}