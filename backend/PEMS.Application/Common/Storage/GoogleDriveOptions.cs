namespace PEMS.Application.Common.Storage;

/// <summary>
/// Configuration bound from the <c>"GoogleDrive"</c> section
/// (see <c>appsettings.Development.json</c>). Drives the Google Drive
/// file-storage integration (UC-15 avatar upload).
/// </summary>
public sealed class GoogleDriveOptions
{
    public const string SectionName = "GoogleDrive";

    public bool Enabled { get; set; }

    /// <summary>e.g. <c>"OAuthUser"</c> — user-delegated OAuth via a shared Google account.</summary>
    public string? AuthMode { get; set; }

    // --- OAuth credentials (required to mint an access token from the refresh token) ---
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>Must match exactly the redirect URI registered in Google Cloud.</summary>
    public string? RedirectUri { get; set; }

    /// <summary>Long-lived token used to mint access tokens for Drive API calls.</summary>
    public string? RefreshToken { get; set; }

    // --- Drive folder ids ---
    public string? RootFolderId { get; set; }
    public string? AvatarFolderId { get; set; }
    public string? DocumentPartnerFolderId { get; set; }
    /// <summary>Legacy shared gallery folder — kept as fallback for old GalleryImage/GalleryVideo uploads.</summary>
    public string? GalleryFolderId { get; set; }

    // Dedicated per-purpose gallery folders (area/location covers, MEDIA items, VISIT_DELEGATION items).
    public string? GalleryAreaFolderId { get; set; }
    public string? GalleryLocationFolderId { get; set; }
    public string? GalleryItemFolderId { get; set; }
    public string? GalleryDelegationFolderId { get; set; }
    /// <summary>Dedicated folder for generated gallery TTS narration audio (folder "gallery-audio").</summary>
    public string? GalleryAudioFolderId { get; set; }
    public string? NewsFolderId { get; set; }
    public string? MinutesFolderId { get; set; }
    public string? VisitRequestDocumentFolderId { get; set; }
    public string? VisitRequestPhotoFolderId { get; set; }
}
