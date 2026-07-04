using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Partners.Aliases.Commands.DeactivatePartnerAlias;

public sealed class DeactivatePartnerAliasCommandHandler
    : IRequestHandler<DeactivatePartnerAliasCommand, DeactivatePartnerAliasResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public DeactivatePartnerAliasCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<DeactivatePartnerAliasResponse> Handle(
        DeactivatePartnerAliasCommand request, CancellationToken cancellationToken)
    {
        var partner = await _db.Partners
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanManagePartnerChildren(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền xoá tên gọi khác của đối tác này.", 403);

        var alias = await _db.PartnerAliases
            .FirstOrDefaultAsync(a => a.PartnerAliasId == request.AliasId && a.PartnerId == partner.PartnerId, cancellationToken)
            ?? throw new NotFoundException("PartnerAlias", request.AliasId);

        var now = _clock.UtcNow;
        alias.Status = "INACTIVE";
        alias.UpdatedAt = now;
        alias.UpdatedBy = _currentUser.UserId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = partner.OwnerCampusId,
            Action = "DEACTIVATE_PARTNER_ALIAS",
            EntityType = "PartnerAlias",
            EntityId = alias.PartnerAliasId,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new DeactivatePartnerAliasResponse { PartnerAliasId = alias.PartnerAliasId };
    }
}
