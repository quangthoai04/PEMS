using System;

namespace PEMS.Application.Galleries.Queries.SearchGalleryItems;

public sealed class SearchGalleryItemsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}