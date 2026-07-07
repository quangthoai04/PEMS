using MediatR;

namespace PEMS.Application.PublicContent.Queries.SearchInformation;

/// <summary>
/// GET /api/public/search — site-wide keyword search across the public surfaces: PUBLISHED news,
/// APPROVED+PUBLIC partners, PUBLISHED faqs, and active campuses. Anonymous. Never returns
/// draft/hidden/pending/internal rows — each section applies the same visibility rule as its own
/// dedicated public endpoint (ViewNews, GetPublicPartners, ViewFaq, GetActiveCampuses).
/// </summary>
public sealed class SearchInformationQuery : IRequest<SearchInformationDto>
{
    public string? Keyword { get; init; }

    /// <summary>Max results per section (news/partners/faqs/campuses). Default 5, clamped to [1, 20].</summary>
    private readonly int _limit = 5;
    public int Limit
    {
        get => _limit;
        init => _limit = value < 1 ? 5 : System.Math.Min(value, 20);
    }
}
