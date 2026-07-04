using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Aliases.Queries.GetPartnerAliases;

public sealed class GetPartnerAliasesQueryHandler
    : IRequestHandler<GetPartnerAliasesQuery, List<PartnerAliasDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPartnerAliasesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<PartnerAliasDto>> Handle(GetPartnerAliasesQuery request, CancellationToken cancellationToken)
    {
        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanViewPartner(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền xem tên gọi khác của đối tác này.", 403);

        return await _db.PartnerAliases.AsNoTracking()
            .Where(a => a.PartnerId == request.PartnerId && a.Status == "ACTIVE")
            .OrderBy(a => a.PartnerAliasId)
            .Select(a => new PartnerAliasDto
            {
                PartnerAliasId = a.PartnerAliasId,
                PartnerId = a.PartnerId,
                AliasName = a.AliasName,
                AliasNameKey = a.AliasNameKey,
                Source = a.Source,
                Status = a.Status,
                CreatedAt = a.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }
}
