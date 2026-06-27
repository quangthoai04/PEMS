namespace PEMS.Application.Profiles.Commands.UploadProfileAvatar;

/// <summary>Result of a successful avatar upload (UC-15).</summary>
public sealed class UploadProfileAvatarResponse
{
    /// <summary>Id of the new row in <c>files</c>.</summary>
    public long FileId { get; init; }

    /// <summary>Backend proxy URL stored in <c>users.avatar_url</c> (e.g. <c>/api/files/123/content</c>).</summary>
    public string AvatarUrl { get; init; } = default!;

    /// <summary>Google Drive human-facing link (optional, for diagnostics).</summary>
    public string? WebViewUrl { get; init; }

    /// <summary>Google Drive thumbnail link if available.</summary>
    public string? ThumbnailUrl { get; init; }
}
