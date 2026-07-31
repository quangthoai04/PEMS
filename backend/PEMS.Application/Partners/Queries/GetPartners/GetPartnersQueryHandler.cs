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

namespace PEMS.Application.Partners.Queries.GetPartners;

public sealed class GetPartnersQueryHandler : IRequestHandler<GetPartnersQuery, PartnerListResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPartnersQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PartnerListResponse> Handle(GetPartnersQuery request, CancellationToken cancellationToken)
    {
        if (!PartnerAccess.CanViewPartnerModule(_currentUser))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền xem danh sách đối tác.", 403);

        var query = _db.Partners.AsNoTracking().AsQueryable();

        // ── Role scope ──
        if (PartnerAccess.ApprovedOnly(_currentUser))
        {
            query = query.Where(p => p.ProfileStatus == PartnerProfileStatuses.Approved
                                     && p.Visibility != PartnerVisibilities.Private);
        }
        else if (!PartnerAccess.SeesAllCampuses(_currentUser))
        {
            // Staff / Staff Leader: own campus rows + other campuses' APPROVED non-private rows.
            var campusId = _currentUser.PrimaryCampusId ?? 0;
            query = query.Where(p => p.OwnerCampusId == campusId
                                     || (p.ProfileStatus == PartnerProfileStatuses.Approved
                                         && p.Visibility != PartnerVisibilities.Private));
        }

        // ── Filters ──
        // Lọc đúng 1 đối tác khi đi từ thông báo — đặt sau role scope để không lộ hồ sơ ngoài quyền xem.
        if (request.PartnerId is { } partnerIdFilter)
            query = query.Where(p => p.PartnerId == partnerIdFilter);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{s}%")
                || (p.ShortName != null && EF.Functions.Like(p.ShortName, $"%{s}%"))
                || (p.PartnerCode != null && EF.Functions.Like(p.PartnerCode, $"%{s}%"))
                || p.Contacts.Any(c => EF.Functions.Like(c.FullName, $"%{s}%")));
        }
        if (!string.IsNullOrWhiteSpace(request.Country))
            query = query.Where(p => p.Country == request.Country);
        if (request.CampusId is { } campusFilter)
            query = query.Where(p => p.OwnerCampusId == campusFilter);
        if (!string.IsNullOrWhiteSpace(request.ProfileStatus))
            query = query.Where(p => p.ProfileStatus == request.ProfileStatus);
        if (!string.IsNullOrWhiteSpace(request.Visibility))
            query = query.Where(p => p.Visibility == request.Visibility);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.PartnerId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.PartnerId, p.PartnerCode, p.Name, p.ShortName, p.Country, p.City,
                p.PartnerType, p.OwnerCampusId, p.CreatedBy, p.ProfileStatus,
                p.Visibility, p.CooperationStatus, p.LogoFileId, p.CreatedAt, p.ReviewedAt,
            })
            .ToListAsync(cancellationToken);

        // Enrich campus/creator names in-memory (Pomelo: no correlated subqueries on optional FKs).
        var campusIds = rows.Select(r => r.OwnerCampusId).Distinct().ToList();
        var campusNames = await _db.Campuses
            .Where(c => campusIds.Contains(c.CampusId))
            .Select(c => new { c.CampusId, c.Name })
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        var creatorIds = rows.Where(r => r.CreatedBy != null).Select(r => r.CreatedBy!.Value).Distinct().ToList();
        var creatorNames = creatorIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users
                .Where(u => creatorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        var partnerIds = rows.Select(r => r.PartnerId).ToList();
        var englishByPartnerId = await _db.PartnerTranslations
            .AsNoTracking()
            .Where(t => partnerIds.Contains(t.PartnerId) && t.LanguageCode == "en")
            .Select(t => new { t.PartnerId, t.Name, t.ShortName })
            .ToDictionaryAsync(t => t.PartnerId, cancellationToken);

        var items = rows.Select(r =>
        {
            var actions = new List<string> { "VIEW" };
            var stub = new PEMS.Domain.Entities.Partners.Partner
            {
                PartnerId = r.PartnerId,
                OwnerCampusId = r.OwnerCampusId,
                ProfileStatus = r.ProfileStatus,
                Visibility = r.Visibility,
            };
            if (PartnerAccess.CanEditPartner(_currentUser, stub)) actions.Add("EDIT");
            if (r.ProfileStatus == PartnerProfileStatuses.PendingApproval
                && PartnerAccess.CanDecidePartner(_currentUser, stub))
            {
                actions.Add("APPROVE");
                actions.Add("REJECT");
            }

            englishByPartnerId.TryGetValue(r.PartnerId, out var en);

            return new PartnerListItemDto
            {
                PartnerId = r.PartnerId,
                PartnerCode = r.PartnerCode,
                Name = r.Name,
                ShortName = r.ShortName,
                Country = r.Country,
                City = r.City,
                PartnerType = r.PartnerType,
                OwnerCampusId = r.OwnerCampusId,
                OwnerCampusName = campusNames.TryGetValue(r.OwnerCampusId, out var cn) ? cn : string.Empty,
                CreatorName = r.CreatedBy != null && creatorNames.TryGetValue(r.CreatedBy.Value, out var un) ? un : null,
                ProfileStatus = r.ProfileStatus,
                Visibility = r.Visibility,
                CooperationStatus = r.CooperationStatus,
                LogoFileId = r.LogoFileId,
                CreatedAt = r.CreatedAt,
                ReviewedAt = r.ReviewedAt,
                AllowedActions = actions,
                EnglishName = en?.Name,
                EnglishShortName = en?.ShortName,
                HasEnglishTranslation = en is not null,
            };
        }).ToList();


        return new PartnerListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            CanCreate = PartnerAccess.CanCreatePartner(_currentUser),
        };
    }
}
