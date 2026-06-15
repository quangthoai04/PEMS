using System;

namespace PEMS.Application.MeetingMinutes.Queries.ViewMinutesList;

public sealed class ViewMinutesListDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}