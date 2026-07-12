using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Partners.Commands.UpdatePartner;

public sealed class UpdatePartnerCommandHandler : IRequestHandler<UpdatePartnerCommand, UpdatePartnerResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public UpdatePartnerCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<UpdatePartnerResponse> Handle(UpdatePartnerCommand request, CancellationToken cancellationToken)
    {
        var partner = await _db.Partners
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanEditPartner(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền chỉnh sửa đối tác này.", 403);

        var name = request.Name.Trim();
        var code = string.IsNullOrWhiteSpace(request.PartnerCode) ? null : request.PartnerCode.Trim().ToUpperInvariant();

        if (code is not null &&
            await _db.Partners.AnyAsync(p => p.PartnerCode == code && p.PartnerId != partner.PartnerId, cancellationToken))
            throw new ConflictException("Mã đối tác đã tồn tại.", PartnerErrorCodes.CodeDuplicated);

        var nameKey = PartnerNormalization.NormalizeKey(name);
        var otherNames = await _db.Partners
            .Where(p => p.PartnerId != partner.PartnerId)
            .Select(p => new { p.PartnerId, p.Name })
            .ToListAsync(cancellationToken);
        if (otherNames.Any(p => PartnerNormalization.NormalizeKey(p.Name) == nameKey))
            throw new ConflictException("Tên đối tác đã tồn tại.", PartnerErrorCodes.NameDuplicated);

        // PUBLIC visibility is reserved for APPROVED profiles. Updating a REJECTED profile
        // resubmits it, so PUBLIC is also blocked on that path.
        var wasRejected = partner.ProfileStatus == PartnerProfileStatuses.Rejected;
        var targetStatus = wasRejected ? PartnerProfileStatuses.PendingApproval : partner.ProfileStatus;
        var visibility = string.IsNullOrWhiteSpace(request.Visibility) ? partner.Visibility : request.Visibility!;
        if (visibility == PartnerVisibilities.Public && targetStatus != PartnerProfileStatuses.Approved)
            throw new BusinessRuleException(
                "Không thể đặt hiển thị PUBLIC khi hồ sơ đối tác chưa được duyệt.",
                PartnerErrorCodes.PublicRequiresApproved);

        var now = _clock.VietnamNow;
        var before = JsonSerializer.Serialize(new
        {
            partner.PartnerCode, partner.Name, partner.ShortName, partner.Country, partner.City,
            partner.WebsiteUrl, partner.Address, partner.PartnerType, partner.CooperationStatus,
            partner.Visibility, partner.ProfileStatus,
        });

        partner.PartnerCode = code!;
        partner.Name = name;
        partner.ShortName = string.IsNullOrWhiteSpace(request.ShortName) ? null : request.ShortName.Trim();
        partner.Country = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim();
        partner.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        partner.WebsiteUrl = string.IsNullOrWhiteSpace(request.WebsiteUrl) ? null : request.WebsiteUrl.Trim();
        partner.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        partner.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (!string.IsNullOrWhiteSpace(request.PartnerType)) partner.PartnerType = request.PartnerType!;
        if (!string.IsNullOrWhiteSpace(request.CooperationStatus)) partner.CooperationStatus = request.CooperationStatus!;
        partner.Visibility = visibility;
        partner.LogoFileId = request.LogoFileId ?? partner.LogoFileId;
        partner.CoverFileId = request.CoverFileId ?? partner.CoverFileId;
        partner.UpdatedAt = now;
        partner.UpdatedBy = _currentUser.UserId;

        if (wasRejected)
        {
            partner.ProfileStatus = PartnerProfileStatuses.PendingApproval;
            partner.ReviewNote = null;
            partner.ReviewedBy = null;
            partner.ReviewedAt = null;
        }

        // Keep the primary alias in sync with the (possibly renamed) official name.
        var aliasExists = await _db.PartnerAliases.AnyAsync(
            a => a.PartnerId == partner.PartnerId && a.AliasNameKey == nameKey, cancellationToken);
        if (!aliasExists)
        {
            _db.PartnerAliases.Add(new Domain.Entities.Partners.PartnerAlias
            {
                PartnerId = partner.PartnerId,
                AliasName = name,
                AliasNameKey = nameKey,
                Source = "MANUAL",
                Status = "ACTIVE",
                CreatedAt = now,
                CreatedBy = _currentUser.UserId,
            });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = partner.OwnerCampusId,
            Action = "UPDATE_PARTNER",
            EntityType = "Partner",
            EntityId = partner.PartnerId,
            Changes = new List<AuditLogChange>
            {
                new()
                {
                    FieldName = "Partner",
                    OldValueText = before,
                    NewValueText = JsonSerializer.Serialize(new
                    {
                        partner.PartnerCode, partner.Name, partner.ShortName, partner.Country, partner.City,
                        partner.WebsiteUrl, partner.Address, partner.PartnerType, partner.CooperationStatus,
                        partner.Visibility, partner.ProfileStatus,
                    }),
                },
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdatePartnerResponse
        {
            PartnerId = partner.PartnerId,
            ProfileStatus = partner.ProfileStatus,
        };
    }
}
