using System;
using System.Collections.Generic;

namespace PEMS.Application.PublicContent.Queries.SearchInformation;

public sealed class SearchInformationDto
{
    public IReadOnlyList<SearchNewsResultDto> News { get; init; } = Array.Empty<SearchNewsResultDto>();
    public IReadOnlyList<SearchPartnerResultDto> Partners { get; init; } = Array.Empty<SearchPartnerResultDto>();
    public IReadOnlyList<SearchFaqResultDto> Faqs { get; init; } = Array.Empty<SearchFaqResultDto>();
    public IReadOnlyList<SearchCampusResultDto> Campuses { get; init; } = Array.Empty<SearchCampusResultDto>();
    public int TotalCount { get; init; }
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
    public string? Country { get; init; }
    public string? PublicSlug { get; init; }
}

public sealed class SearchFaqResultDto
{
    public ulong FaqId { get; init; }
    public string Question { get; init; } = string.Empty;
    public string FaqTypeLabel { get; init; } = string.Empty;
}

public sealed class SearchCampusResultDto
{
    public ulong CampusId { get; init; }
    public string CampusCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}
