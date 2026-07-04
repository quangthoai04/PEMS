using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PEMS.Application.BusinessCardOcr.Services;

namespace PEMS.Infrastructure.Ocr;

/// <summary>
/// Deterministic business-card field extraction from raw OCR text — regex +
/// keyword heuristics only, no generative AI (02 prompt §7.4).
/// </summary>
public sealed class BusinessCardTextParser : IBusinessCardTextParser
{
    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled);

    private static readonly Regex PhoneRegex = new(
        @"(?:\+?\d{1,3}[\s.\-]?)?(?:\(\d{1,4}\)[\s.\-]?)?\d{2,4}(?:[\s.\-]?\d{2,4}){1,4}",
        RegexOptions.Compiled);

    private static readonly Regex UrlRegex = new(
        @"(?:https?://)?(?:www\.)?[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)+(?:/\S*)?",
        RegexOptions.Compiled);

    private static readonly string[] OrgKeywords =
    {
        "university", "college", "institute", "academy", "school", "company", "corporation",
        "corp", "co., ltd", "co. ltd", "ltd", "jsc", "inc", "group", "holdings", "fpt",
        "đại học", "trường", "học viện", "tập đoàn", "công ty", "cong ty", "dai hoc", "truong",
    };

    private static readonly string[] TitleKeywords =
    {
        "director", "manager", "professor", "lecturer", "coordinator", "head", "officer",
        "specialist", "analyst", "engineer", "president", "dean", "chief", "ceo", "cto", "cfo",
        "founder", "partner", "consultant", "assistant", "associate", "supervisor", "leader",
        "giám đốc", "trưởng phòng", "trưởng", "phó", "chuyên viên", "giảng viên", "hiệu trưởng",
    };

    private static readonly string[] DepartmentKeywords =
    {
        "department", "division", "faculty", "office of", "phòng", "khoa", "ban", "bộ phận",
    };

    private static readonly string[] AddressKeywords =
    {
        "street", "st.", "road", "rd.", "avenue", "ave", "district", "city", "ward", "tower",
        "building", "floor", "km", "no.", "đường", "quận", "phường", "thành phố", "tp.", "tòa",
        "tầng", "khu", "số", "hà nội", "hanoi", "ho chi minh", "đà nẵng", "vietnam", "việt nam",
    };

    public BusinessCardParsedFields Parse(string rawText)
    {
        var result = new BusinessCardParsedFields();
        if (string.IsNullOrWhiteSpace(rawText)) return result;

        var lines = rawText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 1)
            .ToList();

        // Email — first match anywhere.
        var emailMatch = EmailRegex.Match(rawText);
        if (emailMatch.Success) result.Email = emailMatch.Value.ToLowerInvariant();

        // Website — a URL that is not just the email domain occurrence.
        foreach (Match match in UrlRegex.Matches(rawText))
        {
            var candidate = match.Value.TrimEnd('.', ',', ';');
            if (result.Email is not null && candidate.Contains('@')) continue;
            if (result.Email is not null && result.Email.EndsWith(candidate, StringComparison.OrdinalIgnoreCase)) continue;
            if (candidate.Contains('.') && !candidate.Contains('@'))
            {
                result.WebsiteUrl = candidate;
                break;
            }
        }

        // Phone — prefer lines labelled tel/mobile/phone, else the densest digit run.
        var phoneLine = lines.FirstOrDefault(l =>
            Regex.IsMatch(l, @"(?i)\b(tel|mobile|phone|cell|hotline|đt|sđt|dđ)\b"));
        var phoneSource = phoneLine ?? rawText;
        var phoneCandidates = PhoneRegex.Matches(phoneSource)
            .Select(m => m.Value.Trim())
            .Where(v => v.Count(char.IsDigit) >= 8 && v.Count(char.IsDigit) <= 15)
            .ToList();
        if (phoneCandidates.Count == 0 && phoneLine is null)
        {
            phoneCandidates = PhoneRegex.Matches(rawText)
                .Select(m => m.Value.Trim())
                .Where(v => v.Count(char.IsDigit) >= 8 && v.Count(char.IsDigit) <= 15)
                .ToList();
        }
        result.Phone = phoneCandidates.FirstOrDefault();

        // Organization — first line containing an org keyword.
        result.Organization = lines.FirstOrDefault(l =>
            OrgKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)));

        // Job title — first line containing a title keyword that is not the org line.
        result.JobTitle = lines.FirstOrDefault(l =>
            !ReferenceEquals(l, result.Organization)
            && l != result.Organization
            && TitleKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)));

        // Department — distinct from both org and title.
        result.DepartmentName = lines.FirstOrDefault(l =>
            l != result.Organization && l != result.JobTitle
            && DepartmentKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)));

        // Address — longest line hitting address keywords or containing digits + commas.
        result.Address = lines
            .Where(l => l != result.Organization && l != result.JobTitle
                        && AddressKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(l => l.Length)
            .FirstOrDefault();

        // Full name — a short "name-like" line near the top that is none of the above
        // and carries no digits / @ / URL.
        result.FullName = lines
            .Take(6)
            .FirstOrDefault(l =>
                l != result.Organization && l != result.JobTitle
                && l != result.DepartmentName && l != result.Address
                && !l.Contains('@') && !l.Any(char.IsDigit)
                && !UrlRegex.IsMatch(l)
                && l.Length is >= 4 and <= 60
                && l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length is >= 2 and <= 6
                && !OrgKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)));

        return result;
    }
}
