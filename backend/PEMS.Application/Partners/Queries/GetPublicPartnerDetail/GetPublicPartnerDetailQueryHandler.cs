using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPublicPartnerDetail;

public sealed class GetPublicPartnerDetailQueryHandler
    : IRequestHandler<GetPublicPartnerDetailQuery, PublicPartnerDto>
{
    private readonly IApplicationDbContext _db;

    public GetPublicPartnerDetailQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PublicPartnerDto> Handle(
        GetPublicPartnerDetailQuery request, CancellationToken cancellationToken)
    {
        var idOrSlug = request.PartnerIdOrSlug.Trim();
        var query = _db.Partners.AsNoTracking()
            .Where(p => p.ProfileStatus == PartnerProfileStatuses.Approved
                        && p.Visibility == PartnerVisibilities.Public);

        query = ulong.TryParse(idOrSlug, out var id)
            ? query.Where(p => p.PartnerId == id)
            : query.Where(p => p.PublicSlug == idOrSlug);

        var partner = await query
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
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Partner", idOrSlug);

        return partner;
    }
}
