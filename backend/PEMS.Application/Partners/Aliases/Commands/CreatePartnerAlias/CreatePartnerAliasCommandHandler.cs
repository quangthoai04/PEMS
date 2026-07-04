using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Partners.Aliases.Commands.CreatePartnerAlias;

public sealed class CreatePartnerAliasCommandHandler
    : IRequestHandler<CreatePartnerAliasCommand, PartnerAliasDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public CreatePartnerAliasCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PartnerAliasDto> Handle(CreatePartnerAliasCommand request, CancellationToken cancellationToken)
    {
        var partner = await _db.Partners
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanManagePartnerChildren(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền thêm tên gọi khác cho đối tác này.", 403);

        var aliasName = request.AliasName.Trim();
        var key = PartnerNormalization.NormalizeKey(aliasName);
        if (key.Length == 0)
            throw new BusinessRuleException("Tên gọi khác không hợp lệ.", PartnerErrorCodes.AliasDuplicated);

        var existing = await _db.PartnerAliases
            .FirstOrDefaultAsync(a => a.PartnerId == partner.PartnerId && a.AliasNameKey == key, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == "ACTIVE")
                throw new ConflictException("Tên gọi khác này đã tồn tại cho đối tác.", PartnerErrorCodes.AliasDuplicated);
            // Revive a previously deactivated alias instead of violating the unique key.
            existing.Status = "ACTIVE";
            existing.AliasName = aliasName;
            existing.UpdatedAt = _clock.UtcNow;
            existing.UpdatedBy = _currentUser.UserId;
            await _db.SaveChangesAsync(cancellationToken);
            return ToDto(existing);
        }

        var now = _clock.UtcNow;
        var alias = new PartnerAlias
        {
            PartnerId = partner.PartnerId,
            AliasName = aliasName,
            AliasNameKey = key,
            Source = "MANUAL",
            Status = "ACTIVE",
            CreatedAt = now,
            CreatedBy = _currentUser.UserId,
        };
        _db.PartnerAliases.Add(alias);
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = partner.OwnerCampusId,
            Action = "CREATE_PARTNER_ALIAS",
            EntityType = "PartnerAlias",
            EntityId = null,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(alias);
    }

    private static PartnerAliasDto ToDto(PartnerAlias a) => new()
    {
        PartnerAliasId = a.PartnerAliasId,
        PartnerId = a.PartnerId,
        AliasName = a.AliasName,
        AliasNameKey = a.AliasNameKey,
        Source = a.Source,
        Status = a.Status,
        CreatedAt = a.CreatedAt,
    };
}
