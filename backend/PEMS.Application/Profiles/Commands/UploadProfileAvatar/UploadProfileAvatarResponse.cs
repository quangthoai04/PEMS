namespace PEMS.Application.Profiles.Commands.UploadProfileAvatar;

/// <summary>Result of a successful avatar upload, surfaced to the frontend.</summary>
public sealed class UploadProfileAvatarResponse
{
    /// <summary>PK of the row inserted into <c>files</c>.</summary>
    public long FileId { get; set; }

    /// <summary>Backend proxy path stored in <c>users.avatar_url</c> (e.g. <c>/api/files/123/content</c>).</summary>
    public string AvatarUrl { get; set; } = default!;

    public string? WebViewUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
}
