using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Partners.Queries.SearchPublicPartnerOptions;

public sealed class SearchPublicPartnerOptionsQueryHandler
    : IRequestHandler<SearchPublicPartnerOptionsQuery, IReadOnlyList<PublicPartnerOptionDto>>
{
    private readonly IApplicationDbContext _context;

    public SearchPublicPartnerOptionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PublicPartnerOptionDto>> Handle(
        SearchPublicPartnerOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var keyword = request.Keyword?.Trim();
        var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 50);

        var query = _context.Partners
            .AsNoTracking()
            .Where(p =>
                p.CooperationStatus == "ACTIVE" &&
                p.ProfileStatus == "APPROVED" &&
                p.Visibility == "PUBLIC");

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword}%";

            query = query.Where(p =>
                EF.Functions.Like(p.Name, pattern) ||
                (p.ShortName != null && EF.Functions.Like(p.ShortName, pattern)) ||
                (p.PartnerCode != null && EF.Functions.Like(p.PartnerCode, pattern)) ||
                (p.Country != null && EF.Functions.Like(p.Country, pattern)) ||
                (p.City != null && EF.Functions.Like(p.City, pattern)));
        }

        return await query
            .OrderBy(p => p.Name)
            .Take(limit)
            .Select(p => new PublicPartnerOptionDto
            {
                PartnerId = p.PartnerId,
                Name = p.Name,
                ShortName = p.ShortName,
                Country = p.Country,
                City = p.City,
                PartnerType = p.PartnerType,
                DisplayName = string.IsNullOrWhiteSpace(p.ShortName)
                    ? p.Name
                    : p.Name + " (" + p.ShortName + ")"
            })
            .ToListAsync(cancellationToken);
    }
}
