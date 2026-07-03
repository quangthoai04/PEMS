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

    /// <summary>At least one media file is required when creating an item. → 422.</summary>
    public const string FilesRequired = "GALLERY_FILES_REQUIRED";

    /// <summary>More than the allowed number of files (20) were supplied. → 422.</summary>
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

    // ── Area / Location management (UC-LOC-01..09) ──

    /// <summary>The caller is not allowed to manage this area/location (wrong campus). → 403.</summary>
    public const string LocationManageForbidden = "GALLERY_LOCATION_MANAGE_FORBIDDEN";

    /// <summary>The target area does not exist (or not in the caller's campus). → 404.</summary>
    public const string AreaNotFound = "GALLERY_AREA_NOT_FOUND";

    /// <summary><c>areaId</c> is required when mode = EXISTING_AREA. → 422.</summary>
    public const string AreaRequired = "GALLERY_AREA_REQUIRED";

    /// <summary><c>newAreaName</c> is required when mode = NEW_AREA. → 422.</summary>
    public const string NewAreaNameRequired = "GALLERY_NEW_AREA_NAME_REQUIRED";

    /// <summary><c>locationName</c> is required (empty after trim). → 422.</summary>
    public const string LocationNameRequired = "GALLERY_LOCATION_NAME_REQUIRED";

    /// <summary>An area with the same normalized key already exists in this campus. → 409.</summary>
    public const string AreaDuplicate = "GALLERY_AREA_DUPLICATE";

    /// <summary>A location with the same normalized key already exists in this area. → 409.</summary>
    public const string LocationDuplicate = "GALLERY_LOCATION_DUPLICATE";

    /// <summary>The target area is INACTIVE so a location cannot be added/moved into it. → 422.</summary>
    public const string AreaInactive = "GALLERY_AREA_INACTIVE";

    /// <summary><c>mode</c> is neither EXISTING_AREA nor NEW_AREA. → 422.</summary>
    public const string InvalidMode = "GALLERY_INVALID_MODE";

    /// <summary>Tried to publish an item whose location is INACTIVE. → 409.</summary>
    public const string ItemPublishBlockedLocationInactive = "GALLERY_ITEM_PUBLISH_BLOCKED_LOCATION_INACTIVE";

    /// <summary>Tried to publish an item whose area is INACTIVE. → 409.</summary>
    public const string ItemPublishBlockedAreaInactive = "GALLERY_ITEM_PUBLISH_BLOCKED_AREA_INACTIVE";

    // ── Area / Location cover image + gallery item type (cover & item_type phase) ──

    /// <summary>An area cover image is required when creating a new area. → 422.</summary>
    public const string AreaCoverRequired = "GALLERY_AREA_COVER_REQUIRED";

    /// <summary>A location cover image is required when creating a location. → 422.</summary>
    public const string LocationCoverRequired = "GALLERY_LOCATION_COVER_REQUIRED";

    /// <summary>The area cover file is not a supported image. → 422.</summary>
    public const string AreaCoverInvalid = "GALLERY_AREA_COVER_INVALID";

    /// <summary>The location cover file is not a supported image. → 422.</summary>
    public const string LocationCoverInvalid = "GALLERY_LOCATION_COVER_INVALID";

    /// <summary><c>itemType</c> is required when creating/editing a gallery item. → 422.</summary>
    public const string ItemTypeRequired = "GALLERY_ITEM_TYPE_REQUIRED";

    /// <summary><c>itemType</c> is neither MEDIA nor VISIT_DELEGATION. → 422.</summary>
    public const string ItemTypeInvalid = "GALLERY_ITEM_TYPE_INVALID";
}
