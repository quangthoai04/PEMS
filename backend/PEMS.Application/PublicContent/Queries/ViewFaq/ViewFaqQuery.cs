using MediatR;
using PEMS.Application.Common.Models;

namespace PEMS.Application.PublicContent.Queries.ViewFAQ;

public sealed record ViewFaqQuery(
    string? Keyword,
    string? FaqType,
    int Page = 1,
    int PageSize = 10
) : IRequest<PaginatedResult<ViewFaqDto>>;
