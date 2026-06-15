using System;

namespace PEMS.Application.PublicContent.Queries.ViewGallery;

public sealed class ViewGalleryDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}