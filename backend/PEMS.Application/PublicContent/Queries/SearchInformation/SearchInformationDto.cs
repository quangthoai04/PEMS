using System;
using System.Collections.Generic;

namespace PEMS.Application.PublicContent.Queries.SearchInformation;

public sealed class SearchInformationDto
{
    public IReadOnlyList<SearchNewsResultDto> News { get; init; } = Array.Empty<SearchNewsResultDto>();
    public IReadOnlyList<SearchPartnerResultDto> Partners { get; init; } = Array.Empty<SearchPartnerResultDto>();
    public IReadOnlyList<SearchGalleryResultDto> Galleries { get; init; } = Array.Empty<SearchGalleryResultDto>();
    public IReadOnlyList<SearchFaqResultDto> Faqs { get; init; } = Array.Empty<SearchFaqResultDto>();

    /// <summary>Per-section "there are more matches than the popup shows" flags, from the Take(limit + 1) probe.</summary>
    public SearchHasMoreDto HasMore { get; init; } = new();

    /// <summary>
    /// Number of rows actually RETURNED in this response (news + partners + galleries + faqs), i.e. what
    /// the popup renders — NOT a database-wide match count. No COUNT(*) is executed; claiming a total
    /// would be a number this query never measured. <see cref="HasMore"/> is what says "there is more".
    /// </summary>
    public int TotalCount { get; init; }
}

public sealed class SearchHasMoreDto
{
    public bool News { get; init; }
    public bool Partners { get; init; }
    public bool Galleries { get; init; }
    public bool Faqs { get; init; }
}

public sealed class SearchNewsResultDto
{
    public ulong NewsId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public DateTime? PublishedAt { get; init; }
}

public sealed class SearchPartnerResultDto
{
    public ulong PartnerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? DescriptionPreview { get; init; }
    public string? Country { get; init; }
    public string? PublicSlug { get; init; }
}

public sealed class SearchFaqResultDto
{
    public ulong FaqId { get; init; }
    public string Question { get; init; } = string.Empty;
    public string? AnswerPreview { get; init; }
    public string FaqType { get; init; } = string.Empty;
    public string FaqTypeLabel { get; init; } = string.Empty;
}

/// <summary>
/// A public gallery item match. <c>CampusCode</c> + <c>LocationId</c> + <c>GalleryItemId</c> are the three
/// fields the deep link needs: /visit-fptu/{campusCode}?locationId={locationId}&amp;itemId={galleryItemId}.
/// </summary>
public sealed class SearchGalleryResultDto
{
    public ulong GalleryItemId { get; init; }

    public string Title { get; init; } = string.Empty;
    public string? DescriptionPreview { get; init; }

    public string CampusCode { get; init; } = string.Empty;
    public string CampusName { get; init; } = string.Empty;

    public ulong AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;

    public ulong LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;

    public string MediaKind { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
}
