using MediatR;

namespace PEMS.Application.PublicContent.Queries.ViewNews;

/// <summary>
/// Public (anonymous) paged list of PUBLISHED news articles.
/// Only published posts are ever returned; hidden/pending/rejected posts are invisible here.
/// </summary>
public class ViewNewsQuery : IRequest<ViewNewsDto>
{
    private const int MaxPageSize = 50;

    public int PageIndex { get; init; } = 1;

    private readonly int _pageSize = 6;
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? 6 : Math.Min(value, MaxPageSize);
    }

    /// <summary>Preferred translation language; falls back to 'vi' then to any available translation.</summary>
    public string? LanguageCode { get; init; }

    /// <summary>Optional keyword matched against title/summary.</summary>
    public string? Keyword { get; init; }

    public bool? IsFeatured { get; init; }
}
