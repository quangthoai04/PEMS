namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Where the EN value carried by a create/update payload came from (client-declared, server-verified).
/// Decides whether the save may reuse the client EN or must call the translation provider itself.
/// </summary>
public static class GalleryTranslationOrigins
{
    /// <summary>No preview happened; EN empty → the save auto-translates (AUTO_ON_SAVE path).</summary>
    public const string None = "NONE";

    /// <summary>The EN came from the preview endpoint unchanged — reusable ONLY while the source hash
    /// still matches the current Vietnamese name (otherwise the save fails PREVIEW_STALE).</summary>
    public const string AutoPreview = "AUTO_PREVIEW";

    /// <summary>The Staff Leader typed/edited the EN by hand — saved as MANUAL, provider never called.</summary>
    public const string Manual = "MANUAL";

    /// <summary>Explicit request for the legacy translate-during-save behavior.</summary>
    public const string AutoOnSave = "AUTO_ON_SAVE";
}
