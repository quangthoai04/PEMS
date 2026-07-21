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
    private readonly IPartnerDescriptionTranslationCache _descriptionTranslator;

    public GetPublicPartnerDetailQueryHandler(
        IApplicationDbContext db, IPartnerDescriptionTranslationCache descriptionTranslator)
    {
        _db = db;
        _descriptionTranslator = descriptionTranslator;
    }

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
                p.UpdatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Partner", idOrSlug);

        var translated = await _descriptionTranslator.TranslateAsync(
            new[] { new PartnerDescriptionTranslationSource(row.PartnerId, row.Description, row.UpdatedAt) },
            request.LanguageCode,
            cancellationToken);

        return new PublicPartnerDto
        {
            PartnerId = row.PartnerId,
            Name = row.Name,
            ShortName = row.ShortName,
            Country = row.Country,
            City = row.City,
            WebsiteUrl = row.WebsiteUrl,
            Address = row.Address,
            Description = translated.TryGetValue(row.PartnerId, out var d) ? d : row.Description,
            PartnerType = row.PartnerType,
            LogoFileId = row.LogoFileId,
            CoverFileId = row.CoverFileId,
            PublicSlug = row.PublicSlug,
        };
    }
}
