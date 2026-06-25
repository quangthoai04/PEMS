using MediatR;

namespace PEMS.Application.News.Queries.ViewNewsList;

public sealed class ViewNewsListQuery : IRequest<ViewNewsListResponse>
{
    public string? Keyword { get; init; }
    public string? Status { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 5;
}
