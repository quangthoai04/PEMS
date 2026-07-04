using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPublicPartners;

public sealed class GetPublicPartnersQueryHandler
    : IRequestHandler<GetPublicPartnersQuery, GetPublicPartnersResponse>
{
    private readonly IApplicationDbContext _db;

    public GetPublicPartnersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<GetPublicPartnersResponse> Handle(
        GetPublicPartnersQuery request, CancellationToken cancellationToken)
    {
        // Hard rule: the public surface never returns PENDING/REJECTED/PRIVATE/INTERNAL rows.
        var query = _db.Partners.AsNoTracking()
            .Where(p => p.ProfileStatus == PartnerProfileStatuses.Approved
                        && p.Visibility == PartnerVisibilities.Public);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{s}%")
                                     || (p.ShortName != null && EF.Functions.Like(p.ShortName, $"%{s}%")));
        }
        if (!string.IsNullOrWhiteSpace(request.Country))
            query = query.Where(p => p.Country == request.Country);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PublicPartnerDto
            {
                PartnerId = p.PartnerId,
                Name = p.Name,
                ShortName = p.ShortName,
                Country = p.Country,
                City = p.City,
                WebsiteUrl = p.WebsiteUrl,
                Address = p.Address,
                Description = p.Description,
                PartnerType = p.PartnerType,
                LogoFileId = p.LogoFileId,
                CoverFileId = p.CoverFileId,
                PublicSlug = p.PublicSlug,
            })
            .ToListAsync(cancellationToken);

        return new GetPublicPartnersResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }
}
