namespace PEMS.Application.Galleries.Commands.DeleteGalleryItem;

/// <summary>Minimal result of a delete — enough for the list to drop the row and toast.</summary>
public sealed class DeleteGalleryItemResponse
{
    public ulong GalleryItemId { get; init; }
    public string Message { get; init; } = string.Empty;
}
