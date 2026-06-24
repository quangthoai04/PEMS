using MediatR;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Faqs.Queries.ViewListFAQ;

public sealed record ViewListFAQQuery(
    string? Keyword,
    string? FaqType,
    string? Status,
    string? SortBy,
    string? SortDirection,
    int Page = 1,
    int PageSize = 5
) : IRequest<PaginatedResult<ViewListFAQDto>>;
