using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Faqs.Queries.ViewFAQDetail;

public sealed class ViewFAQDetailQueryHandler : IRequestHandler<ViewFAQDetailQuery, ViewFAQDetailDto>
{
    private readonly IApplicationDbContext _dbContext;

    public ViewFAQDetailQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ViewFAQDetailDto> Handle(ViewFAQDetailQuery request, CancellationToken cancellationToken)
    {
        var raw = await _dbContext.Faqs
            .AsNoTracking()
            .Where(x => x.FaqId == request.FaqId)
            .Select(x => new
            {
                x.FaqId,
                x.FaqType,
                x.Question,
                x.Answer,
                x.DisplayOrder,
                x.Status,
                x.CreatedAt,
                x.CreatedBy,
                x.UpdatedAt,
                x.UpdatedBy
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (raw is null)
            throw new NotFoundException($"FAQ with ID {request.FaqId} was not found.");

        var englishTranslation = await _dbContext.FaqTranslations
            .AsNoTracking()
            .Where(t => t.FaqId == request.FaqId && t.LanguageCode == "en")
            .Select(t => new { t.Question, t.Answer })
            .FirstOrDefaultAsync(cancellationToken);

        var userIds = new[] { raw.CreatedBy, raw.UpdatedBy }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var userNames = userIds.Any()
            ? await _dbContext.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken)
            : new Dictionary<ulong, string>();

        return new ViewFAQDetailDto
        {
            FaqId = raw.FaqId,
            FaqType = raw.FaqType,
            FaqTypeLabel = FaqConstants.ToVietnameseTypeLabel(raw.FaqType),
            Question = raw.Question,
            Answer = raw.Answer,
            EnglishQuestion = englishTranslation?.Question,
            EnglishAnswer = englishTranslation?.Answer,
            HasEnglishTranslation = englishTranslation is not null,
            DisplayOrder = raw.DisplayOrder,
            Status = raw.Status,
            StatusLabel = FaqConstants.ToVietnameseStatusLabel(raw.Status),
            CreatedAt = raw.CreatedAt,
            CreatedBy = raw.CreatedBy,
            CreatedByName = raw.CreatedBy.HasValue && userNames.TryGetValue(raw.CreatedBy.Value, out var cn) ? cn : null,
            UpdatedAt = raw.UpdatedAt,
            UpdatedBy = raw.UpdatedBy,
            UpdatedByName = raw.UpdatedBy.HasValue && userNames.TryGetValue(raw.UpdatedBy.Value, out var un) ? un : null
        };
    }
}
