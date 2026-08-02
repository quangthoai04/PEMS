using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPartnerVisitHistory;

public sealed class GetPartnerVisitHistoryQueryHandler
    : IRequestHandler<GetPartnerVisitHistoryQuery, List<PartnerVisitHistoryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVisitFormReadService _formReadService;

    public GetPartnerVisitHistoryQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IVisitFormReadService formReadService)
    {
        _db = db;
        _currentUser = currentUser;
        _formReadService = formReadService;
    }

    public async Task<List<PartnerVisitHistoryDto>> Handle(
        GetPartnerVisitHistoryQuery request, CancellationToken cancellationToken)
    {
        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanViewPartner(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền xem hồ sơ đối tác này.", 403);

        // 1. Direct partner visits
        var directInstances = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequest.PartnerId == request.PartnerId && c.Status != "CANCELLED")
            .Select(c => new { c.VisitInstanceId, LinkType = "DIRECT" })
            .ToListAsync(cancellationToken);

        // 2. Visits linked via guest members (VisitGuestPartnerLink)
        var guestLinkedInstances = await (
            from l in _db.VisitGuestPartnerLinks.AsNoTracking()
            join g in _db.VisitInstanceGuestMembers.AsNoTracking() on l.GuestMemberId equals g.GuestMemberId
            join c in _db.VisitRequestCampuses.AsNoTracking() on g.VisitInstanceId equals c.VisitInstanceId
            where l.PartnerId == request.PartnerId
                  && l.MatchStatus == "CONFIRMED"
                  && c.Status != "CANCELLED"
            select new { c.VisitInstanceId, LinkType = "GUEST_LINK" }
        ).ToListAsync(cancellationToken);

        var instanceLinkTypeMap = new Dictionary<ulong, string>();
        foreach (var x in directInstances)
            instanceLinkTypeMap[x.VisitInstanceId] = "DIRECT";
        foreach (var x in guestLinkedInstances)
        {
            if (!instanceLinkTypeMap.ContainsKey(x.VisitInstanceId))
                instanceLinkTypeMap[x.VisitInstanceId] = "GUEST_LINK";
        }

        var instanceIds = instanceLinkTypeMap.Keys.ToList();
        if (instanceIds.Count == 0)
            return new List<PartnerVisitHistoryDto>();

        var rows = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => instanceIds.Contains(c.VisitInstanceId))
            .Select(c => new
            {
                c.VisitInstanceId,
                c.VisitRequestId,
                c.CampusId,
                c.PlannedStartAt,
                c.PlannedEndAt,
                c.Status,
                c.CurrentHostUserId,
            })
            .ToListAsync(cancellationToken);

        var requestIds = rows.Select(r => r.VisitRequestId).Distinct().ToList();
        var campusIds = rows.Select(r => r.CampusId).Distinct().ToList();
        var hostUserIds = rows.Where(r => r.CurrentHostUserId.HasValue).Select(r => r.CurrentHostUserId!.Value).Distinct().ToList();

        var campusNames = await _db.Campuses.AsNoTracking()
            .Where(c => campusIds.Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        var hostNames = hostUserIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => hostUserIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        var guestCounts = (await _db.VisitInstanceGuestMembers.AsNoTracking()
                .Where(g => instanceIds.Contains(g.VisitInstanceId))
                .GroupBy(g => g.VisitInstanceId)
                .Select(g => new { InstanceId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.InstanceId, x => x.Count);

        var requests = await _db.VisitRequests.AsNoTracking()
            .Where(v => requestIds.Contains(v.VisitRequestId))
            .ToListAsync(cancellationToken);

        var nameByInstance = new Dictionary<ulong, string>();
        foreach (var visit in requests)
        {
            var visitInstanceIds = rows
                .Where(r => r.VisitRequestId == visit.VisitRequestId)
                .Select(r => r.VisitInstanceId)
                .ToList();
            var content = await _formReadService.ResolveCampusFormContentAsync(
                visit, visitInstanceIds, cancellationToken);
            foreach (var id in visitInstanceIds)
                nameByInstance[id] = content[id].DelegationName;
        }

        var result = rows
            .Select(r => new PartnerVisitHistoryDto
            {
                VisitInstanceId = r.VisitInstanceId,
                VisitRequestId = r.VisitRequestId,
                DelegationName = nameByInstance.TryGetValue(r.VisitInstanceId, out var dn) ? dn : "Đoàn khách",
                CampusId = r.CampusId,
                CampusName = campusNames.TryGetValue(r.CampusId, out var cn) ? cn : string.Empty,
                PlannedStartAt = r.PlannedStartAt,
                PlannedEndAt = r.PlannedEndAt,
                Status = r.Status,
                GuestCount = guestCounts.TryGetValue(r.VisitInstanceId, out var gc) ? gc : 0,
                HostName = r.CurrentHostUserId is { } hId && hostNames.TryGetValue(hId, out var hn) ? hn : null,
                LinkType = instanceLinkTypeMap.TryGetValue(r.VisitInstanceId, out var lt) ? lt : "DIRECT",
            })
            .OrderByDescending(r => r.PlannedStartAt)
            .ThenByDescending(r => r.VisitInstanceId)
            .ToList();

        return result;
    }
}
