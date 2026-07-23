using System.Text.RegularExpressions;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Normalizes a Vietnamese source string BEFORE it is hashed/translated/persisted: trim + collapse
/// consecutive whitespace to a single space. Diacritics and casing are preserved — the normalized value
/// is exactly what gets stored in <c>area_name</c>/<c>location_name</c>/<c>title</c>, so the stored
/// value, the translation input and the change-detection hash always agree.
/// </summary>
public static class TranslationSourceNormalizer
{
    public static string Normalize(string? input)
        => string.IsNullOrWhiteSpace(input) ? string.Empty : Regex.Replace(input.Trim(), @"\s+", " ");
}
