namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Stable, machine-readable error codes for the Staff Leader VisitFPTU Gallery management UCs
/// (UC-GAL-01 List, -02 Search/Filter, -03 Detail, -04 Add, -05 Enable, -06 Disable, -07 Edit).
/// Surfaced via the controlled exceptions so the frontend can map them to localized messages.
/// </summary>
public static class GalleryErrorCodes
{
    /// <summary>Caller is not an active Staff Leader (STAFF/LEADER + campus). → 403.</summary>
    public const string GalleryManagementForbidden = "GALLERY_MANAGEMENT_FORBIDDEN";

    /// <summary>The Staff Leader has no primary campus assigned. → 422.</summary>
    public const string NoCampusAssigned = "GALLERY_NO_CAMPUS_ASSIGNED";

    /// <summary>The target gallery item does not exist (or is soft-deleted). → 404.</summary>
    public const string GalleryItemNotFound = "GALLERY_ITEM_NOT_FOUND";

    /// <summary>The gallery item exists but belongs to another campus. → 403.</summary>
    public const string GalleryScopeForbidden = "GALLERY_SCOPE_FORBIDDEN";

    /// <summary>The target location does not exist. → 404.</summary>
    public const string LocationNotFound = "GALLERY_LOCATION_NOT_FOUND";

    /// <summary>The target location belongs to another campus. → 403.</summary>
    public const string LocationScopeForbidden = "GALLERY_LOCATION_SCOPE_FORBIDDEN";

    /// <summary>The target area/location is not ACTIVE. → 422.</summary>
    public const string LocationInactive = "GALLERY_LOCATION_INACTIVE";

    /// <summary>Another gallery item already uses this location (one item per location). → 409.</summary>
    public const string LocationAlreadyUsed = "GALLERY_LOCATION_ALREADY_USED";

    /// <summary>At least one media file is required when creating an item. → 422.</summary>
    public const string FilesRequired = "GALLERY_FILES_REQUIRED";

    /// <summary>More than the allowed number of files (5) were supplied. → 422.</summary>
    public const string TooManyFiles = "GALLERY_TOO_MANY_FILES";

    /// <summary>A supplied file is neither a supported image nor video. → 422.</summary>
    public const string InvalidMediaFile = "GALLERY_INVALID_MEDIA_FILE";

    /// <summary>The item would be left with no active media after the edit. → 422.</summary>
    public const string MediaRequired = "GALLERY_MEDIA_REQUIRED";

    /// <summary>The requested status is not PUBLISHED / HIDDEN. → 422.</summary>
    public const string InvalidStatus = "GALLERY_INVALID_STATUS";

    /// <summary>The supplied primaryMediaId does not belong to the item. → 422.</summary>
    public const string PrimaryMediaInvalid = "GALLERY_PRIMARY_MEDIA_INVALID";

    /// <summary>Tried to enable an item that has no active media. → 422.</summary>
    public const string NoActiveMedia = "GALLERY_NO_ACTIVE_MEDIA";
}
