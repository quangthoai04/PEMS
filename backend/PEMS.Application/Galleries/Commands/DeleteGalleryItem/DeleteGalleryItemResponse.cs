using System;

namespace PEMS.Application.Galleries.Commands.DeleteGalleryItem;

public sealed class DeleteGalleryItemResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}