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
    /// <summary>
    /// MP4 video used as a gallery area's cover (the Area Showcase fullscreen background). Stored on
    /// Google Drive like the image cover — the loved <c>gallery_areas.cover_file_id</c> can point at
    /// either; the media kind is inferred from the <c>files</c> row (purpose / mime).
    /// </summary>
    GalleryAreaCoverVideo,
    GalleryLocationCover,
    GalleryItemImage,
    GalleryItemVideo,
    GalleryDelegationImage,
    GalleryDelegationVideo,
    GalleryAudio,
    /// <summary>
    /// External YouTube video referenced by a gallery item. No binary is ever stored on Drive — the
    /// <c>files</c> row is metadata only (<c>storage_provider = OTHER</c>, external id = YouTube video id).
    /// </summary>
    GalleryYouTubeVideo,
    NewsImage,
    NewsAttachment,
    Document,
    MinutesAttachment,
    VisitRequestAttachment,
    /// <summary>
    /// Private delegation photo uploaded by an ACCEPTED Student participant (visit_photos). Stored on
    /// Drive under <c>VisitRequestPhotoFolderId / VR-{visit_request_id} / {campus_code}</c>.
    /// </summary>
    VisitRequestPhoto,
    PartnerDocument,
    LogisticsAttachment,
    BusinessCard,
    /// <summary>
    /// Staff Leader/Dept Leader/HO report export (PDF/Excel/CSV) — a date-range/campus aggregate
    /// across many delegations, so it archives into the flat "Report" Drive folder (never nested
    /// under a delegation), created by <see cref="Reports.Common.IReportArchiveService"/>.
    /// </summary>
    ReportDocument,
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
    public const string GalleryAreaCoverVideo = "GALLERY_AREA_COVER_VIDEO";
    public const string GalleryLocationCover = "GALLERY_LOCATION_COVER";
    public const string GalleryItemImage = "GALLERY_ITEM_IMAGE";
    public const string GalleryItemVideo = "GALLERY_ITEM_VIDEO";
    public const string GalleryDelegationImage = "GALLERY_DELEGATION_IMAGE";
    public const string GalleryDelegationVideo = "GALLERY_DELEGATION_VIDEO";
    public const string GalleryAudio = "GALLERY_AUDIO";
    public const string GalleryYouTubeVideo = "GALLERY_YOUTUBE_VIDEO";
    public const string NewsImage = "NEWS_IMAGE";
    public const string NewsAttachment = "NEWS_ATTACHMENT";
    public const string Document = "DOCUMENT";
    public const string MinutesAttachment = "MINUTES_ATTACHMENT";
    public const string VisitRequestAttachment = "VISIT_REQUEST_ATTACHMENT";
    public const string VisitRequestPhoto = "VISIT_REQUEST_PHOTO";
    public const string PartnerDocument = "PARTNER_DOCUMENT";
    public const string LogisticsAttachment = "LOGISTICS_ATTACHMENT";
    public const string BusinessCard = "BUSINESS_CARD";
    public const string ReportDocument = "REPORT_DOCUMENT";
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
        FilePurpose.GalleryAreaCoverVideo => FilePurposeDbValues.GalleryAreaCoverVideo,
        FilePurpose.GalleryLocationCover => FilePurposeDbValues.GalleryLocationCover,
        FilePurpose.GalleryItemImage => FilePurposeDbValues.GalleryItemImage,
        FilePurpose.GalleryItemVideo => FilePurposeDbValues.GalleryItemVideo,
        FilePurpose.GalleryDelegationImage => FilePurposeDbValues.GalleryDelegationImage,
        FilePurpose.GalleryDelegationVideo => FilePurposeDbValues.GalleryDelegationVideo,
        FilePurpose.GalleryAudio => FilePurposeDbValues.GalleryAudio,
        FilePurpose.GalleryYouTubeVideo => FilePurposeDbValues.GalleryYouTubeVideo,
        FilePurpose.NewsImage => FilePurposeDbValues.NewsImage,
        FilePurpose.NewsAttachment => FilePurposeDbValues.NewsAttachment,
        FilePurpose.Document => FilePurposeDbValues.Document,
        FilePurpose.MinutesAttachment => FilePurposeDbValues.MinutesAttachment,
        FilePurpose.VisitRequestAttachment => FilePurposeDbValues.VisitRequestAttachment,
        FilePurpose.VisitRequestPhoto => FilePurposeDbValues.VisitRequestPhoto,
        FilePurpose.PartnerDocument => FilePurposeDbValues.PartnerDocument,
        FilePurpose.LogisticsAttachment => FilePurposeDbValues.LogisticsAttachment,
        FilePurpose.BusinessCard => FilePurposeDbValues.BusinessCard,
        FilePurpose.ReportDocument => FilePurposeDbValues.ReportDocument,
        _ => FilePurposeDbValues.Other,
    };

    /// <summary>
    /// Short, stable folder prefix used by the object-key builder (and mirrors the Drive folder layout
    /// <c>avatars / gallery / news / documents / minutes / visit-requests / …</c>).
    /// </summary>
    public static string ToObjectKeyPrefix(this FilePurpose purpose) => purpose switch
    {
        FilePurpose.UserAvatar => "avatars",
        FilePurpose.GalleryImage or FilePurpose.GalleryVideo => "gallery",
        FilePurpose.GalleryAreaCover or FilePurpose.GalleryAreaCoverVideo => "gallery/areas",
        FilePurpose.GalleryLocationCover => "gallery/locations",
        FilePurpose.GalleryItemImage or FilePurpose.GalleryItemVideo => "gallery/items",
        FilePurpose.GalleryDelegationImage or FilePurpose.GalleryDelegationVideo => "gallery/delegations",
        FilePurpose.GalleryAudio => "gallery/audio",
        FilePurpose.GalleryYouTubeVideo => "youtube/gallery",
        FilePurpose.NewsImage or FilePurpose.NewsAttachment => "news",
        FilePurpose.Document => "documents",
        FilePurpose.MinutesAttachment => "minutes",
        FilePurpose.VisitRequestAttachment => "visit-requests",
        FilePurpose.VisitRequestPhoto => "visit-photos",
        FilePurpose.PartnerDocument => "partners",
        FilePurpose.LogisticsAttachment => "logistics",
        FilePurpose.BusinessCard => "business-cards",
        FilePurpose.ReportDocument => "reports",
        _ => "other",
    };
}
