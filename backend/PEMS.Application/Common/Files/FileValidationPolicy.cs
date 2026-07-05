using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Common.Files;

/// <summary>
/// Default per-purpose validation rules. Image purposes require magic-byte checks and reject SVG;
/// document purposes allow the office/PDF set. Tune limits here in one place — handlers never set
/// their own sizes/types.
/// </summary>
public sealed class FileValidationPolicy : IFileValidationPolicy
{
    private const long Mb = 1024 * 1024;

    private static readonly IReadOnlySet<string> ImageMimes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    private static readonly IReadOnlySet<string> ImageExts =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private static readonly IReadOnlySet<string> VideoMimes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "video/mp4", "video/webm" };

    private static readonly IReadOnlySet<string> VideoExts =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm" };

    private static readonly IReadOnlySet<string> DocMimes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "image/jpeg",
            "image/png",
        };

    private static readonly IReadOnlySet<string> DocExts =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".docx", ".xlsx", ".pptx", ".jpg", ".jpeg", ".png" };

    public FileValidationRule GetRule(FilePurpose purpose) => purpose switch
    {
        FilePurpose.UserAvatar => new FileValidationRule
        {
            MaxSizeBytes = 2 * Mb,
            AllowedMimeTypes = ImageMimes,
            AllowedExtensions = ImageExts,
            RequireImageMagicBytes = true,
        },

        FilePurpose.GalleryImage or FilePurpose.NewsImage
            or FilePurpose.GalleryAreaCover or FilePurpose.GalleryLocationCover
            or FilePurpose.GalleryItemImage or FilePurpose.GalleryDelegationImage => new FileValidationRule
        {
            MaxSizeBytes = 5 * Mb,
            AllowedMimeTypes = ImageMimes,
            AllowedExtensions = ImageExts,
            RequireImageMagicBytes = true,
        },

        FilePurpose.GalleryVideo or FilePurpose.GalleryItemVideo
            or FilePurpose.GalleryDelegationVideo => new FileValidationRule
        {
            MaxSizeBytes = 100 * Mb,
            AllowedMimeTypes = VideoMimes,
            AllowedExtensions = VideoExts,
            RequireImageMagicBytes = false,
        },

        FilePurpose.NewsAttachment or FilePurpose.Document or FilePurpose.PartnerDocument
            or FilePurpose.LogisticsAttachment => new FileValidationRule
        {
            MaxSizeBytes = 20 * Mb,
            AllowedMimeTypes = DocMimes,
            AllowedExtensions = DocExts,
            RequireImageMagicBytes = false,
        },

        FilePurpose.MinutesAttachment => new FileValidationRule
        {
            MaxSizeBytes = 10 * Mb,
            AllowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "application/pdf",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            },
            AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx" },
            RequireImageMagicBytes = false,
        },

        FilePurpose.VisitRequestAttachment => new FileValidationRule
        {
            MaxSizeBytes = 10 * Mb,
            AllowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "application/pdf",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "image/jpeg",
                "image/png",
            },
            AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".docx", ".jpg", ".jpeg", ".png" },
            RequireImageMagicBytes = false,
        },

        // Business-card scans: the OCR handler already enforces the admin-configured size/mime rule
        // (settings_json) before calling storage — this is just a generous outer cap matching the
        // controller's 15 MB hard limit, mirroring the mixed image+PDF approach used above.
        FilePurpose.BusinessCard => new FileValidationRule
        {
            MaxSizeBytes = 15 * Mb,
            AllowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "application/pdf", "image/jpeg", "image/png", "image/webp",
            },
            AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".jpg", ".jpeg", ".png", ".webp" },
            RequireImageMagicBytes = false,
        },

        _ => new FileValidationRule
        {
            MaxSizeBytes = 10 * Mb,
            AllowedMimeTypes = DocMimes,
            AllowedExtensions = DocExts,
            RequireImageMagicBytes = false,
        },
    };
}
