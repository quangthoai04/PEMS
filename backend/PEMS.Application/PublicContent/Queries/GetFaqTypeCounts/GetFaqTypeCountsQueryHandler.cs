using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.PublicContent.Queries.GetFaqTypeCounts;

public sealed class GetFaqTypeCountsQueryHandler
    : IRequestHandler<GetFaqTypeCountsQuery, List<PublicFaqTypeCountDto>>
{
    private readonly IApplicationDbContext _db;

    public GetFaqTypeCountsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<PublicFaqTypeCountDto>> Handle(
        GetFaqTypeCountsQuery request, CancellationToken cancellationToken)
    {
        var counts = await _db.Faqs
            .AsNoTracking()
            .Where(f => f.Status == FaqConstants.Status.Published)
            .GroupBy(f => f.FaqType)
            .Select(g => new { FaqType = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countsByType = counts.ToDictionary(c => c.FaqType, c => c.Count);

        // Always return all 7 known types (including zero-count ones) so topic cards never omit a
        // category just because it currently has no published FAQ.
        return FaqConstants.Type.All
            .Select(type => new PublicFaqTypeCountDto
            {
                Value = type,
                Label = FaqConstants.ToTypeLabel(type, request.LanguageCode),
                Count = countsByType.GetValueOrDefault(type, 0),
            })
            .OrderByDescending(t => t.Count)
            .ToList();
    }
}
