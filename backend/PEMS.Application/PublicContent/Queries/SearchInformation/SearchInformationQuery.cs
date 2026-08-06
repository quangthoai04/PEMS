using System;
using MediatR;

namespace PEMS.Application.PublicContent.Queries.SearchInformation;

/// <summary>
/// GET /api/public/search — site-wide keyword search across the four public content surfaces:
/// PUBLISHED news, APPROVED+PUBLIC partners, public-visible gallery items, and PUBLISHED faqs.
/// Anonymous. Never returns draft/hidden/pending/internal rows — each section applies the same
/// visibility rule as its own dedicated public endpoint (ViewNews, GetPublicPartners,
/// GetPublicGalleryItemDetail, ViewFaq), so a hit here always survives the click through to detail.
/// Campuses are deliberately NOT searched: the popup is a content finder, not a contact page.
/// </summary>
public sealed class SearchInformationQuery : IRequest<SearchInformationDto>
{
    public string? Keyword { get; init; }

    /// <summary>
    /// The public site language, 'vi' or 'en'. Matching AND display both happen in this language only:
    /// an EN search reads exclusively EN translations and never falls back to Vietnamese text
    /// (see <see cref="SearchInformationQueryHandler"/>). Blank defaults to 'vi'; anything that is
    /// neither is rejected by <see cref="SearchInformationQueryValidator"/>.
    /// </summary>
    public string? LanguageCode { get; init; }

    /// <summary>Max results per section (news/partners/galleries/faqs). Default 5, clamped to [1, 20].</summary>
    private readonly int _limit = 5;
    public int Limit
    {
        get => _limit;
        init => _limit = value < 1 ? 5 : Math.Min(value, 20);
    }
}

/// <summary>The two languages the public site — and therefore this search — is published in.</summary>
public static class SearchLanguages
{
    public const string Vietnamese = "vi";
    public const string English = "en";

    /// <summary>True for null/blank (defaults to vi) and for 'vi'/'en' in any casing.</summary>
    public static bool IsSupported(string? languageCode) =>
        string.IsNullOrWhiteSpace(languageCode) ||
        string.Equals(languageCode.Trim(), Vietnamese, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(languageCode.Trim(), English, StringComparison.OrdinalIgnoreCase);

    /// <summary>Blank → 'vi'; 'EN'/'en' → 'en'; everything else → 'vi' (the validator rejects it first).</summary>
    public static string Normalize(string? languageCode) =>
        string.Equals(languageCode?.Trim(), English, StringComparison.OrdinalIgnoreCase)
            ? English
            : Vietnamese;
}
