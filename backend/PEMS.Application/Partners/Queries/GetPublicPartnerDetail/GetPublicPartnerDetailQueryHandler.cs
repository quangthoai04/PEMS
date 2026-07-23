using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.Partners.Queries.GetPublicPartnerDetail;

/// <summary>
/// Public partner detail — reads pre-translated VI/EN content straight from
/// <c>partner_translations</c>. Never calls a translation API — same requested-language → 'vi' →
/// legacy-column fallback rule as <c>GetPublicPartnersQueryHandler</c>.
/// </summary>
public sealed class GetPublicPartnerDetailQueryHandler
    : IRequestHandler<GetPublicPartnerDetailQuery, PublicPartnerDto>
{
    private readonly IApplicationDbContext _db;

    public GetPublicPartnerDetailQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PublicPartnerDto> Handle(
        GetPublicPartnerDetailQuery request, CancellationToken cancellationToken)
    {
        var idOrSlug = request.PartnerIdOrSlug.Trim();
        var requestedLang = string.IsNullOrWhiteSpace(request.LanguageCode)
            ? NewsConstants.Languages.Default
            : request.LanguageCode.Trim().ToLowerInvariant();

        var query = _db.Partners.AsNoTracking()
            .Where(p => p.ProfileStatus == PartnerProfileStatuses.Approved
                        && p.Visibility == PartnerVisibilities.Public);

        query = ulong.TryParse(idOrSlug, out var id)
            ? query.Where(p => p.PartnerId == id)
            : query.Where(p => p.PublicSlug == idOrSlug);

        var row = await query
            .Select(p => new
            {
                p.PartnerId,
                p.Name,
                p.ShortName,
                p.Country,
                p.City,
                p.WebsiteUrl,
                p.Address,
                p.Description,
                p.PartnerType,
                p.LogoFileId,
                p.CoverFileId,
                p.PublicSlug,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Partner", idOrSlug);

        var translations = await _db.PartnerTranslations
            .AsNoTracking()
            .Where(t => t.PartnerId == row.PartnerId && (t.LanguageCode == requestedLang || t.LanguageCode == "vi"))
            .ToListAsync(cancellationToken);

        var chosen = translations.FirstOrDefault(t => t.LanguageCode == requestedLang)
                   ?? translations.FirstOrDefault(t => t.LanguageCode == "vi");

        return new PublicPartnerDto
        {
            PartnerId = row.PartnerId,
            Name = chosen?.Name ?? row.Name,
            ShortName = chosen?.ShortName ?? row.ShortName,
            Country = chosen?.Country ?? row.Country,
            City = chosen?.City ?? row.City,
            WebsiteUrl = row.WebsiteUrl,
            Address = chosen?.Address ?? row.Address,
            Description = chosen?.Description ?? row.Description,
            PartnerType = row.PartnerType,
            LogoFileId = row.LogoFileId,
            CoverFileId = row.CoverFileId,
            PublicSlug = row.PublicSlug,
        };
    }
}
