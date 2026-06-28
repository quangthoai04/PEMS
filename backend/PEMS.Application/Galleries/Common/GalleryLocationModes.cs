namespace PEMS.Application.Galleries.Common;

/// <summary>
/// The two modes the create/edit "khu vực" modal can submit (UC-LOC-04..07). EXISTING_AREA attaches the
/// location to a chosen area; NEW_AREA creates a brand-new area first.
/// </summary>
internal static class GalleryLocationModes
{
    public const string ExistingArea = "EXISTING_AREA";
    public const string NewArea = "NEW_AREA";
}
