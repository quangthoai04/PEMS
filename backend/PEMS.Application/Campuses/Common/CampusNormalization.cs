using System.Text;

namespace PEMS.Application.Campuses.Common;

/// <summary>
/// Normalization helpers shared by UC-81 (Create) and UC-85 (Update) so the duplicate
/// checks and stored values stay consistent (00_COMMON_RULES §4.1). Every validator, handler
/// and duplicate check works on these outputs — nothing normalizes on its own. The frontend
/// mirrors them in <c>features/campus-management/validation/campusMasterValidation.ts</c>.
/// </summary>
public static class CampusNormalization
{
    /// <summary>campus_code: trim + uppercase. Separators are never rewritten (spec §3.1).</summary>
    public static string Code(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// name/address: strip control characters, collapse every whitespace run (including tabs and
    /// newlines) into one space, then trim. Casing and Vietnamese diacritics are preserved
    /// verbatim (spec §3.2/§3.4). Compare case-insensitively for duplicates.
    /// </summary>
    public static string Text(string? value) => CollapseWhitespace(value);

    /// <summary>
    /// city: trim + collapse, then map onto the canonical spelling from <see cref="CampusCities"/>.
    /// Values outside the whitelist are returned trimmed but unchanged, so legacy rows keep their
    /// stored value and the caller can decide whether to reject them (spec §3.3/§6.3).
    /// </summary>
    public static string City(string? value)
    {
        var trimmed = CollapseWhitespace(value);
        return CampusCities.TryGetCanonical(trimmed) ?? trimmed;
    }

    /// <summary>email: trim + lowercase. The local-part is never rewritten (spec §3.7).</summary>
    public static string Email(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// phone display value: strip control characters, collapse whitespace, trim. The user's
    /// chosen separators are preserved — "(024)   7300  5588" stays "(024) 7300 5588" (spec §3.5).
    /// </summary>
    public static string PhoneDisplay(string? value) => CollapseWhitespace(value);

    /// <summary>
    /// phone canonical key for duplicate comparison (spec §3.6): drop spaces, dots, hyphens and
    /// parentheses, then fold the Vietnamese international prefix "+84" onto the domestic "0" so
    /// "024 7300 5588", "024-7300-5588", "(024) 7300.5588" and "+84 24 7300 5588" all collapse to
    /// "02473005588". Never use the display string for uniqueness.
    /// </summary>
    public static string PhoneKey(string? value)
    {
        var raw = value ?? string.Empty;
        var builder = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (char.IsWhiteSpace(ch) || ch is '.' or '-' or '(' or ')') continue;
            builder.Append(ch);
        }

        var compact = builder.ToString();
        // Only the "+84" form is folded; a bare leading "84" is ambiguous (it is also a valid
        // start for a domestic number) and is rejected by the prefix rule instead.
        return compact.StartsWith("+84", StringComparison.Ordinal)
            ? string.Concat("0", compact.AsSpan(3))
            : compact;
    }

    /// <summary>
    /// Drops control characters and collapses every whitespace run into a single space. Whitespace
    /// is treated as a separator (never deleted), so "Trần\tMinh" becomes "Trần Minh".
    /// </summary>
    private static string CollapseWhitespace(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }
            if (char.IsControl(ch)) continue;

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(ch);
        }

        return builder.ToString();
    }
}
