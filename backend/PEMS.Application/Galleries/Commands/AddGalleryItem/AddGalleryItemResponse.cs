using System;

namespace PEMS.Application.Galleries.Commands.AddGalleryItem;

public sealed class AddGalleryItemResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}