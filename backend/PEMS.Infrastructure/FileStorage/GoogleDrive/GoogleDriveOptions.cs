namespace PEMS.Infrastructure.FileStorage.GoogleDrive;

/// <summary>
/// Strongly-typed binding of the <c>GoogleDrive</c> configuration section
/// (appsettings.Development.json — never committed with real secrets).
/// Folder-id property names mirror the actual config keys in use.
/// </summary>
public sealed class GoogleDriveOptions
{
    public const string SectionName = "GoogleDrive";

    public bool Enabled { get; set; }
    public string AuthMode { get; set; } = "OAuthUser";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string RootFolderId { get; set; } = string.Empty;
    public string AvatarFolderId { get; set; } = string.Empty;
    public string DocumentPartnerFolderId { get; set; } = string.Empty;
    public string GalleryFolderId { get; set; } = string.Empty;
    public string NewsFolderId { get; set; } = string.Empty;
    public string MinutesFolderId { get; set; } = string.Empty;
    public string VisitRequestDocumentFolderId { get; set; } = string.Empty;
    public string VisitRequestPhotoFolderId { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
}
