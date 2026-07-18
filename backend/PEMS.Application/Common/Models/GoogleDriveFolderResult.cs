namespace PEMS.Application.Common.Models;

/// <summary>
/// A Google Drive folder resolved (found or created) by
/// <see cref="Interfaces.IGoogleDriveStorageService.EnsureChildFolderAsync"/>.
/// </summary>
public sealed class GoogleDriveFolderResult
{
    /// <summary>Google Drive folder id.</summary>
    public string ExternalFolderId { get; init; } = default!;

    /// <summary>Human-facing Drive link (<c>webViewLink</c>) when Drive returned one.</summary>
    public string? WebViewUrl { get; init; }
}
