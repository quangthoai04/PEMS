namespace PEMS.Application.Galleries.Common;

/// <summary>Values persisted in gallery_* <c>translation_status</c> (ENUM in the DB).</summary>
public static class GalleryTranslationStatuses
{
    public const string Pending = "PENDING";
    public const string Ready = "READY";
    public const string Failed = "FAILED";
    public const string Outdated = "OUTDATED";
}

/// <summary>Values persisted in gallery_* <c>translation_source</c> (ENUM in the DB).</summary>
public static class GalleryTranslationSources
{
    public const string Auto = "AUTO";
    public const string Manual = "MANUAL";
}

/// <summary>User-facing warning texts for the Staff Leader UI when auto-translation fails.</summary>
public static class GalleryTranslationMessages
{
    /// <summary>Shown as a WARNING toast (not an error): the Vietnamese data WAS saved successfully.</summary>
    public const string TranslationFailedWarning =
        "Đã lưu dữ liệu tiếng Việt. Bản dịch tiếng Anh chưa được tạo; trang public sẽ tạm hiển thị tiếng Việt.";
}

/// <summary>Column-length caps for the persisted English strings (translated text is never truncated —
/// a too-long result is marked FAILED instead, per BR "không cắt chuỗi dịch").</summary>
public static class GalleryTranslationLimits
{
    /// <summary>gallery_areas.area_name_en / gallery_locations.location_name_en → VARCHAR(255).</summary>
    public const int NameEnMaxLength = 255;

    /// <summary>gallery_items.title_en → VARCHAR(500).</summary>
    public const int TitleEnMaxLength = 500;
}
