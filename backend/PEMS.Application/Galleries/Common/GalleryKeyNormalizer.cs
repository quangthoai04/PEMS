using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Normalizes an area/location display name into a stable, diacritic-free key used for duplicate
/// detection (UC §7). "TÒA DELTA", "Toà Delta" and "  TÒA   DELTA " all collapse to "toa-delta",
/// so a near-duplicate can never slip past the uniqueness check on a raw text comparison.
/// </summary>
internal static class GalleryKeyNormalizer
{
    /// <summary>Collapses whitespace and trims — the value stored in <c>area_name</c>/<c>location_name</c>.</summary>
    public static string CleanName(string? input)
        => string.IsNullOrWhiteSpace(input) ? string.Empty : Regex.Replace(input.Trim(), @"\s+", " ");

    /// <summary>
    /// Produces the normalized key: trim → collapse spaces → lowercase → strip Vietnamese diacritics →
    /// replace non-alphanumerics with '-' → collapse/trim '-'.
    /// </summary>
    public static string ToKey(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Đ/đ have no decomposition form, so handle them explicitly before stripping marks.
        var pre = input.Trim().Replace('Đ', 'D').Replace('đ', 'd');

        var decomposed = pre.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var ascii = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var key = Regex.Replace(ascii, @"[^a-z0-9]+", "-");
        key = Regex.Replace(key, "-{2,}", "-").Trim('-');
        return key.Length > 150 ? key[..150] : key;
    }
}
