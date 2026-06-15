using System;

namespace PEMS.Application.PublicContent.Queries.ViewNotifications;

public sealed class ViewNotificationsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}