using System.Linq;
using System.Text.RegularExpressions;

namespace PEMS.Application.Campuses.Common;

/// <summary>
/// Normalization helpers shared by UC-81 (Create) and UC-85 (Update) so the duplicate
/// checks and stored values stay consistent (00_COMMON_RULES §4.1).
/// </summary>
public static class CampusNormalization
{
    private static readonly Regex MultiSpace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>campus_code: trim + uppercase.</summary>
    public static string Code(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>name/address: trim + collapse multiple spaces (compare case-insensitive).</summary>
    public static string Text(string? value) => MultiSpace.Replace((value ?? string.Empty).Trim(), " ");

    /// <summary>city: trim.</summary>
    public static string City(string? value) => (value ?? string.Empty).Trim();

    /// <summary>email: trim + lowercase.</summary>
    public static string Email(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>phone display value: trim + collapse multiple spaces.</summary>
    public static string PhoneDisplay(string? value) => MultiSpace.Replace((value ?? string.Empty).Trim(), " ");

    /// <summary>
    /// phone for duplicate comparison: strip spaces, dots, hyphens and parentheses so
    /// "024 7300 5588", "024-7300-5588" and "(024) 7300.5588" collapse to one value.
    /// </summary>
    public static string PhoneKey(string? value)
        => new string((value ?? string.Empty).Where(ch => !char.IsWhiteSpace(ch) && ch != '.' && ch != '-' && ch != '(' && ch != ')').ToArray());
}
