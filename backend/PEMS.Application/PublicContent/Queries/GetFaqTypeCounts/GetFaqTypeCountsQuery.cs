using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.PublicContent.Queries.GetFaqTypeCounts;

/// <summary>
/// GET /api/public/faqs/type-counts — every faq_type (PEMS.Domain.Constants.FaqConstants.Type.All)
/// with the count of PUBLISHED questions in that type, for the public FAQ page's topic cards.
/// Always returns all 7 types, even ones with zero published questions. Anonymous.
/// </summary>
public sealed class GetFaqTypeCountsQuery : IRequest<List<PublicFaqTypeCountDto>>
{
    public string? LanguageCode { get; init; }
}

public sealed class PublicFaqTypeCountDto
{
    /// <summary>The exact `faqs.faq_type` enum value — pass this back as the list filter's `faqType`.</summary>
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}
