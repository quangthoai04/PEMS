using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPendingPartnerApprovals;

public sealed class GetPendingPartnerApprovalsQueryHandler
    : IRequestHandler<GetPendingPartnerApprovalsQuery, PartnerListResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPendingPartnerApprovalsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PartnerListResponse> Handle(
        GetPendingPartnerApprovalsQuery request, CancellationToken cancellationToken)
    {
        if (!PartnerAccess.IsStaffLeader(_currentUser) || _currentUser.PrimaryCampusId is not { } campusId)
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Chỉ Trưởng phòng IC (Staff Leader) mới xem được danh sách đối tác chờ duyệt.", 403);

        var query = _db.Partners.AsNoTracking()
            .Where(p => p.OwnerCampusId == campusId
                        && p.ProfileStatus == PartnerProfileStatuses.PendingApproval);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.PartnerId, p.PartnerCode, p.Name, p.ShortName, p.Country, p.City,
                p.PartnerType, p.OwnerCampusId, p.CreatedBy, p.ProfileStatus,
                p.Visibility, p.CooperationStatus, p.LogoFileId, p.CreatedAt, p.ReviewedAt,
            })
            .ToListAsync(cancellationToken);

        var campusName = await _db.Campuses
            .Where(c => c.CampusId == campusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var creatorIds = rows.Where(r => r.CreatedBy != null).Select(r => r.CreatedBy!.Value).Distinct().ToList();
        var creatorNames = creatorIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.Where(u => creatorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        return new PartnerListResponse
        {
            Items = rows.Select(r => new PartnerListItemDto
            {
                PartnerId = r.PartnerId,
                PartnerCode = r.PartnerCode,
                Name = r.Name,
                ShortName = r.ShortName,
                Country = r.Country,
                City = r.City,
                PartnerType = r.PartnerType,
                OwnerCampusId = r.OwnerCampusId,
                OwnerCampusName = campusName,
                CreatorName = r.CreatedBy != null && creatorNames.TryGetValue(r.CreatedBy.Value, out var un) ? un : null,
                ProfileStatus = r.ProfileStatus,
                Visibility = r.Visibility,
                CooperationStatus = r.CooperationStatus,
                LogoFileId = r.LogoFileId,
                CreatedAt = r.CreatedAt,
                ReviewedAt = r.ReviewedAt,
                AllowedActions = new List<string> { "VIEW", "APPROVE", "REJECT" },
            }).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            CanCreate = true,
        };
    }
}
