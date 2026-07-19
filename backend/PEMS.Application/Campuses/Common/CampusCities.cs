using System.Collections.Generic;
using System.Linq;

namespace PEMS.Application.Campuses.Common;

/// <summary>
/// Canonical whitelist of Vietnamese province-level administrative units (34 units, effective
/// 2025): 6 centrally-governed cities + 28 provinces. This is the ONLY set accepted for
/// <c>campuses.city</c> (spec §6) — free text sent straight to the API is rejected.
/// The frontend mirrors this list in <c>features/campus-management/constants.ts</c>
/// (CAMPUS_PROVINCES); the two must stay in sync, in the same order.
/// </summary>
public static class CampusCities
{
    /// <summary>Canonical spellings, in display order (cities first, then provinces).</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        // 6 thành phố trực thuộc Trung ương
        "Hà Nội",
        "TP. Hồ Chí Minh",
        "Hải Phòng",
        "Đà Nẵng",
        "Cần Thơ",
        "Huế",
        // 28 tỉnh
        "Lai Châu",
        "Điện Biên",
        "Sơn La",
        "Lào Cai",
        "Lạng Sơn",
        "Cao Bằng",
        "Tuyên Quang",
        "Thái Nguyên",
        "Phú Thọ",
        "Bắc Ninh",
        "Hưng Yên",
        "Ninh Bình",
        "Quảng Ninh",
        "Thanh Hóa",
        "Nghệ An",
        "Hà Tĩnh",
        "Quảng Trị",
        "Quảng Ngãi",
        "Gia Lai",
        "Khánh Hòa",
        "Đắk Lắk",
        "Lâm Đồng",
        "Đồng Nai",
        "Tây Ninh",
        "Vĩnh Long",
        "Đồng Tháp",
        "An Giang",
        "Cà Mau",
    };

    // Case-insensitive lookup so "hà nội" resolves to the canonical "Hà Nội" (spec §3.3).
    private static readonly Dictionary<string, string> Canonical =
        All.ToDictionary(city => city, city => city, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the canonical spelling for <paramref name="value"/>, or null when it is not a
    /// supported province. <paramref name="value"/> is expected to be trimmed/collapsed already.
    /// </summary>
    public static string? TryGetCanonical(string value)
        => Canonical.TryGetValue(value, out var canonical) ? canonical : null;

    /// <summary>True when the value maps to a supported province (case-insensitive).</summary>
    public static bool IsAllowed(string value) => Canonical.ContainsKey(value);
}
