using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Domain.Constants;

namespace PEMS.Application.PublicContent.Queries.ViewFAQ;

public sealed class ViewFaqQueryHandler : IRequestHandler<ViewFaqQuery, PaginatedResult<ViewFaqDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public ViewFaqQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedResult<ViewFaqDto>> Handle(
        ViewFaqQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page;
        var pageSize = request.PageSize;
        var keyword = request.Keyword?.Trim();
        var faqType = request.FaqType?.Trim();

        var query = _dbContext.Faqs
            .AsNoTracking()
            .Where(x => x.Status == "PUBLISHED");

        if (!string.IsNullOrWhiteSpace(faqType) &&
            !string.Equals(faqType, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.FaqType == faqType);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Question, pattern) ||
                EF.Functions.Like(x.Answer, pattern) ||
                EF.Functions.Like(x.FaqType, pattern));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var rawItems = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.FaqId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.FaqId,
                x.FaqType,
                x.Question,
                x.Answer,
                x.DisplayOrder,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(x => new ViewFaqDto
            {
                FaqId = x.FaqId,
                FaqType = x.FaqType,
                FaqTypeLabel = FaqConstants.ToTypeLabel(x.FaqType, request.LanguageCode),
                Question = x.Question,
                Answer = x.Answer,
                DisplayOrder = x.DisplayOrder,
                CreatedAt = x.CreatedAt
            })
            .ToList();

        return PaginatedResult<ViewFaqDto>.Create(items, page, pageSize, totalItems);
    }
}
