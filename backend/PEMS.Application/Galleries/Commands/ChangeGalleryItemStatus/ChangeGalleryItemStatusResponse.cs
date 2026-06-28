namespace PEMS.Application.Galleries.Commands.ChangeGalleryItemStatus;

/// <summary>Minimal result of an enable/disable toggle — enough for the list to update the badge.</summary>
public sealed class ChangeGalleryItemStatusResponse
{
    public ulong GalleryItemId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
