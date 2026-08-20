using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PEMS.Shared;

/// <summary>
/// The canonical nationality/country rule for the visit-request domain (Patch 4). It exists because
/// nationality has never had a format concept at all — every write path only checked
/// <c>NotEmpty + MaximumLength(100)</c> — so the same real country ended up stored under whatever
/// spelling a form or an Excel import happened to type: "Hàn Quốc" / "South Korea" / "KR" / "UAE" vs
/// "Các Tiểu vương quốc Ả Rập Thống Nhất" are all real, currently-stored examples of one country
/// split across multiple strings.
///
/// <para>
/// Data source: the SAME package and override/alias tables the frontend's
/// <c>countryNames.ts</c> uses (<c>i18n-iso-countries</c>, <c>VI_COUNTRY_NAME_OVERRIDES</c>,
/// <c>EXTRA_NAME_ALIASES</c>), dumped once into <c>Data/countries.json</c> by a generator script so
/// the two sides can never resolve the same input to two different answers. Regenerate that file if
/// the frontend's override/alias maps ever change.
/// </para>
///
/// <para>
/// <b>Canonical persisted form is the Vietnamese short name</b> ("Hàn Quốc", not "KR" and not
/// "Korea, Republic of") — a deliberate choice recorded in the Patch 4 decision: the audited data is
/// already ~100% Vietnamese-name-convention, so this is the representation that needs zero backfill.
/// ISO alpha-2 is used only as the internal resolution key, never stored.
/// </para>
/// </summary>
public static class CountryName
{
    private sealed record CountryEntry(
        [property: JsonPropertyName("alpha2")] string Alpha2,
        [property: JsonPropertyName("vi")] string Vi,
        [property: JsonPropertyName("en")] string En,
        [property: JsonPropertyName("aliases")] List<string> Aliases);

    private sealed record CountryFile([property: JsonPropertyName("countries")] List<CountryEntry> Countries);

    private static readonly Lazy<IReadOnlyDictionary<string, string>> AliasToCanonicalVi = new(BuildAliasMap);

    /// <summary>Shown after every nationality rejection, mirrored on the frontend's country picker.</summary>
    public const string FormatHint = "Vui lòng chọn một quốc gia hợp lệ (ví dụ: Hàn Quốc, Nhật Bản, Việt Nam).";

    /// <summary>True when <paramref name="input"/> resolves to a real country.</summary>
    public static bool IsValid(string? input) => TryResolve(input, out _);

    /// <summary>
    /// Resolves free text — a Vietnamese name, an English name, an alias/abbreviation, or an ISO
    /// alpha-2 code, case-insensitive — to the canonical Vietnamese short name. Returns false (with
    /// <paramref name="canonical"/> null) for anything that does not name a real country; callers
    /// must treat that as "this write did not resolve", never as "store it as typed".
    /// </summary>
    public static bool TryResolve(string? input, out string? canonical)
    {
        canonical = null;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var key = input.Trim().ToLowerInvariant();
        if (AliasToCanonicalVi.Value.TryGetValue(key, out var found))
        {
            canonical = found;
            return true;
        }
        return false;
    }

    private static IReadOnlyDictionary<string, string> BuildAliasMap()
    {
        var assembly = typeof(CountryName).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("countries.json", StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "countries.json embedded resource not found in PEMS.Domain — check the EmbeddedResource item in PEMS.Domain.csproj.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not open embedded resource '{resourceName}'.");
        var file = JsonSerializer.Deserialize<CountryFile>(stream)
            ?? throw new InvalidOperationException("countries.json embedded resource deserialized to null.");

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var country in file.Countries)
        {
            foreach (var alias in country.Aliases)
                map[alias] = country.Vi;
            // The alias list already includes the lowercased alpha-2 code and the canonical VI/EN
            // names (see the generator), but this stays as an explicit belt-and-braces default.
            map[country.Alpha2.ToLowerInvariant()] = country.Vi;
        }
        return map;
    }
}
