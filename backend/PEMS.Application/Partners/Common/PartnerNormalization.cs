using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PEMS.Application.Partners.Common;

/// <summary>
/// Normalization used everywhere a partner/organization name has to be compared:
/// alias_name_key generation, duplicate checks and partner matching.
/// </summary>
public static class PartnerNormalization
{
    private static readonly HashSet<string> OrgStopWords = new(StringComparer.Ordinal)
    {
        // English organization suffixes/prefixes safe to drop for fuzzy compare
        "university", "college", "institute", "school", "academy",
        "company", "corporation", "corp", "inc", "co", "ltd", "jsc", "llc", "group", "holdings",
        // Vietnamese (already accent-stripped by NormalizeKey)
        "dai", "hoc", "truong", "tap", "doan", "cong", "ty", "co", "phan", "tnhh", "vien",
    };

    /// <summary>
    /// lower-case → strip accents (đ→d) → punctuation to space → collapse repeated spaces → trim.
    /// This is the exact form stored in partner_aliases.alias_name_key.
    /// </summary>
    public static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var lowered = value.Trim().ToLowerInvariant()
            .Replace('đ', 'd');

        var formD = lowered.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        var collapsed = string.Join(' ',
            sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return collapsed.Length > 300 ? collapsed[..300] : collapsed;
    }

    /// <summary>Normalized key with common org suffixes/prefixes removed (for fuzzy matching only).</summary>
    public static string StripOrgWords(string normalizedKey)
    {
        if (string.IsNullOrEmpty(normalizedKey)) return string.Empty;
        var kept = normalizedKey.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !OrgStopWords.Contains(t))
            .ToArray();
        // Never strip down to nothing — fall back to the full key.
        return kept.Length == 0 ? normalizedKey : string.Join(' ', kept);
    }

    /// <summary>Lower-cased domain part of an email, or null.</summary>
    public static string? EmailDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.LastIndexOf('@');
        if (at <= 0 || at == email.Length - 1) return null;
        return email[(at + 1)..].Trim().ToLowerInvariant();
    }

    /// <summary>Registrable host of a website URL ("https://www.fpt.edu.vn/x" → "fpt.edu.vn").</summary>
    public static string? WebsiteDomain(string? websiteUrl)
    {
        if (string.IsNullOrWhiteSpace(websiteUrl)) return null;
        var raw = websiteUrl.Trim();
        if (!raw.Contains("://")) raw = "https://" + raw;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return null;
        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    /// <summary>Free "generic" mail domains that must never match a partner.</summary>
    public static bool IsGenericMailDomain(string domain) =>
        domain is "gmail.com" or "googlemail.com" or "yahoo.com" or "hotmail.com"
            or "outlook.com" or "live.com" or "icloud.com" or "proton.me" or "protonmail.com";
}
