using System;

namespace PEMS.Application.Calendars.Queries.ViewDepartmentCalendar;

public sealed class ViewDepartmentCalendarDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}