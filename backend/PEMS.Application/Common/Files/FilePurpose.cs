namespace PEMS.Application.Common.Files;

/// <summary>
/// What a stored file is for. Every business upload picks exactly one purpose; the shared
/// <see cref="Interfaces.IFileUploadService"/> uses it to choose the validation rule, the Google
/// Drive folder (<see cref="Interfaces.IFileStorageFolderResolver"/>) and the value written to
/// <c>files.file_purpose</c>. New upload features should reuse an existing purpose or add one here
/// rather than inventing ad-hoc <c>file_purpose</c> strings.
/// </summary>
public enum FilePurpose
{
    UserAvatar,
    GalleryImage,
    GalleryVideo,
    GalleryAreaCover,
    GalleryLocationCover,
    NewsImage,
    NewsAttachment,
    Document,
    MinutesAttachment,
    VisitRequestAttachment,
    PartnerDocument,
    LogisticsAttachment,
    Other,
}

/// <summary>
/// Canonical <c>files.file_purpose</c> string values. These are the only strings that should ever be
/// written to the column — keep them in sync with whatever the DB enum/check constraint allows.
/// <c>UserAvatar</c> stays <c>"USER_AVATAR"</c> to match the rows already written by the avatar flow.
/// </summary>
public static class FilePurposeDbValues
{
    public const string UserAvatar = "USER_AVATAR";
    public const string GalleryImage = "GALLERY_IMAGE";
    public const string GalleryVideo = "GALLERY_VIDEO";
    public const string GalleryAreaCover = "GALLERY_AREA_COVER";
    public const string GalleryLocationCover = "GALLERY_LOCATION_COVER";
    public const string NewsImage = "NEWS_IMAGE";
    public const string NewsAttachment = "NEWS_ATTACHMENT";
    public const string Document = "DOCUMENT";
    public const string MinutesAttachment = "MINUTES_ATTACHMENT";
    public const string VisitRequestAttachment = "VISIT_REQUEST_ATTACHMENT";
    public const string PartnerDocument = "PARTNER_DOCUMENT";
    public const string LogisticsAttachment = "LOGISTICS_ATTACHMENT";
    public const string Other = "OTHER";
}

public static class FilePurposeExtensions
{
    /// <summary>Maps a <see cref="FilePurpose"/> to its canonical <c>files.file_purpose</c> string.</summary>
    public static string ToDbValue(this FilePurpose purpose) => purpose switch
    {
        FilePurpose.UserAvatar => FilePurposeDbValues.UserAvatar,
        FilePurpose.GalleryImage => FilePurposeDbValues.GalleryImage,
        FilePurpose.GalleryVideo => FilePurposeDbValues.GalleryVideo,
        FilePurpose.GalleryAreaCover => FilePurposeDbValues.GalleryAreaCover,
        FilePurpose.GalleryLocationCover => FilePurposeDbValues.GalleryLocationCover,
        FilePurpose.NewsImage => FilePurposeDbValues.NewsImage,
        FilePurpose.NewsAttachment => FilePurposeDbValues.NewsAttachment,
        FilePurpose.Document => FilePurposeDbValues.Document,
        FilePurpose.MinutesAttachment => FilePurposeDbValues.MinutesAttachment,
        FilePurpose.VisitRequestAttachment => FilePurposeDbValues.VisitRequestAttachment,
        FilePurpose.PartnerDocument => FilePurposeDbValues.PartnerDocument,
        FilePurpose.LogisticsAttachment => FilePurposeDbValues.LogisticsAttachment,
        _ => FilePurposeDbValues.Other,
    };

    /// <summary>
    /// Short, stable folder prefix used by the object-key builder (and mirrors the Drive folder layout
    /// <c>avatars / gallery / news / documents / minutes / visit-requests / …</c>).
    /// </summary>
    public static string ToObjectKeyPrefix(this FilePurpose purpose) => purpose switch
    {
        FilePurpose.UserAvatar => "avatars",
        FilePurpose.GalleryImage or FilePurpose.GalleryVideo
            or FilePurpose.GalleryAreaCover or FilePurpose.GalleryLocationCover => "gallery",
        FilePurpose.NewsImage or FilePurpose.NewsAttachment => "news",
        FilePurpose.Document => "documents",
        FilePurpose.MinutesAttachment => "minutes",
        FilePurpose.VisitRequestAttachment => "visit-requests",
        FilePurpose.PartnerDocument => "partners",
        FilePurpose.LogisticsAttachment => "logistics",
        _ => "other",
    };
}
