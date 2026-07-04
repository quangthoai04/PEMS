using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPartnerDetail;

public sealed class GetPartnerDetailQueryHandler : IRequestHandler<GetPartnerDetailQuery, PartnerDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPartnerDetailQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PartnerDetailDto> Handle(GetPartnerDetailQuery request, CancellationToken cancellationToken)
    {
        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanViewPartner(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền xem hồ sơ đối tác này.", 403);

        var campusName = await _db.Campuses
            .Where(c => c.CampusId == partner.OwnerCampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var userIds = new List<ulong>();
        if (partner.CreatedBy is { } cb) userIds.Add(cb);
        if (partner.ReviewedBy is { } rb) userIds.Add(rb);
        var names = userIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.Where(u => userIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        var actions = new List<string> { "VIEW" };
        if (PartnerAccess.CanEditPartner(_currentUser, partner)) actions.Add("EDIT");
        if (partner.ProfileStatus == PartnerProfileStatuses.PendingApproval
            && PartnerAccess.CanDecidePartner(_currentUser, partner))
        {
            actions.Add("APPROVE");
            actions.Add("REJECT");
        }
        if (PartnerAccess.CanManagePartnerChildren(_currentUser, partner)) actions.Add("MANAGE_CHILDREN");

        return new PartnerDetailDto
        {
            PartnerId = partner.PartnerId,
            PartnerCode = partner.PartnerCode,
            Name = partner.Name,
            ShortName = partner.ShortName,
            Country = partner.Country,
            City = partner.City,
            WebsiteUrl = partner.WebsiteUrl,
            Address = partner.Address,
            Description = partner.Description,
            PartnerType = partner.PartnerType,
            CooperationStatus = partner.CooperationStatus,
            ProfileStatus = partner.ProfileStatus,
            Visibility = partner.Visibility,
            LogoFileId = partner.LogoFileId,
            CoverFileId = partner.CoverFileId,
            PublicSlug = partner.PublicSlug,
            OwnerCampusId = partner.OwnerCampusId,
            OwnerCampusName = campusName,
            CreatedBy = partner.CreatedBy,
            CreatorName = partner.CreatedBy is { } c && names.TryGetValue(c, out var creator) ? creator : null,
            CreatedAt = partner.CreatedAt,
            ReviewNote = partner.ReviewNote,
            ReviewedBy = partner.ReviewedBy,
            ReviewerName = partner.ReviewedBy is { } r && names.TryGetValue(r, out var reviewer) ? reviewer : null,
            ReviewedAt = partner.ReviewedAt,
            AllowedActions = actions,
        };
    }
}
