using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

public sealed class ViewGuestDelegationListQueryHandler : IRequestHandler<ViewGuestDelegationListQuery, PaginatedResult<VisitRequestManagementItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ViewGuestDelegationListQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<VisitRequestManagementItemDto>> Handle(ViewGuestDelegationListQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Current user is not authenticated.");
        }

        var userId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;

        var query = from vr in _context.VisitRequests
                    join partner in _context.Partners on vr.PartnerId equals partner.PartnerId into partners
                    from p in partners.DefaultIfEmpty()
                    join vrc in _context.VisitRequestCampuses on vr.VisitRequestId equals vrc.VisitRequestId into campuses
                    from c in campuses.DefaultIfEmpty()
                    join campus in _context.Campuses on c.CampusId equals campus.CampusId into campusNames
                    from cn in campusNames.DefaultIfEmpty()
                    join hostUser in _context.Users on c.CurrentHostUserId equals hostUser.UserId into hostUsers
                    from hu in hostUsers.DefaultIfEmpty()
                    join visitorUser in _context.Users on vr.VisitorUserId equals visitorUser.UserId into visitorUsers
                    from vu in visitorUsers.DefaultIfEmpty()
                    select new { vr, c, p, cn, hu, vu };

        // Data Scope Rules
        if (roleCode == RoleCodes.Visitor)
        {
            query = query.Where(x => x.vr.VisitorUserId == userId || x.vr.CreatedBy == userId);
        }
        else if (roleCode == RoleCodes.Staff)
        {
            if (_currentUser.SubRole == SubRoles.Leader)
            {
                if (!_currentUser.PrimaryCampusId.HasValue)
                {
                    throw new UnauthorizedAccessException("Staff Leader missing PrimaryCampusId");
                }
                var primaryCampusId = _currentUser.PrimaryCampusId.Value;
                query = query.Where(x => x.c.CampusId == primaryCampusId || x.vr.CampusInstances.Any(ci => ci.CampusId == primaryCampusId));
            }
            else
            {
                query = query.Where(x => x.vr.CreatedBy == userId || 
                                         (x.c != null && x.c.CurrentHostUserId == userId) || 
                                         (x.c != null && _context.VisitParticipants.Any(p => p.VisitInstanceId == x.c.VisitInstanceId && p.UserId == userId)));
            }
        }
        else if (roleCode == RoleCodes.Ho || roleCode == RoleCodes.Admin)
        {
            // Allowed to see all based on permission (handled at controller level typically, but we don't restrict query here)
        }
        else
        {
            throw new UnauthorizedAccessException("Role is not supported for this view.");
        }

        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.ToLower();
            query = query.Where(x =>
                (x.vr.DelegationName != null && x.vr.DelegationName.ToLower().Contains(keyword)) ||
                (x.vr.RequestCode != null && x.vr.RequestCode.ToLower().Contains(keyword)) ||
                (x.p != null && x.p.Name != null && x.p.Name.ToLower().Contains(keyword)) ||
                (x.vr.RegistrantOrganization != null && x.vr.RegistrantOrganization.ToLower().Contains(keyword)) ||
                (x.hu != null && x.hu.FullName != null && x.hu.FullName.ToLower().Contains(keyword)) ||
                (x.vu != null && x.vu.FullName != null && x.vu.FullName.ToLower().Contains(keyword)) ||
                (x.cn != null && x.cn.Name != null && x.cn.Name.ToLower().Contains(keyword))
            );
        }

        if (request.CancelledOnly)
        {
            query = query.Where(x => x.vr.Status == "CANCELLED" || (x.c != null && x.c.Status == "CANCELLED"));
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.RequestStatus))
            {
                query = query.Where(x => x.vr.Status == request.RequestStatus);
            }

            if (!string.IsNullOrWhiteSpace(request.CampusStatus))
            {
                query = query.Where(x => x.c != null && x.c.Status == request.CampusStatus);
            }
        }

        if (request.CampusId.HasValue)
        {
            query = query.Where(x => x.c != null && x.c.CampusId == request.CampusId.Value);
        }
        
        if (!string.IsNullOrWhiteSpace(request.VisitScope))
        {
            query = query.Where(x => x.vr.VisitScope == request.VisitScope);
        }
        
        if (!string.IsNullOrWhiteSpace(request.VisitScopes))
        {
            var scopes = request.VisitScopes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            if (scopes.Any())
            {
                query = query.Where(x => scopes.Contains(x.vr.VisitScope));
            }
        }

        if (request.FromDate.HasValue)
        {
            var fromDateStart = request.FromDate.Value.Date;
            query = query.Where(x => x.c != null && x.c.PlannedEndAt >= fromDateStart);
        }
        
        if (request.ToDate.HasValue)
        {
            var toDateEnd = request.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.c != null && x.c.PlannedStartAt <= toDateEnd);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.vr.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new VisitRequestManagementItemDto
            {
                VisitRequestId = x.vr.VisitRequestId,
                VisitInstanceId = x.c != null ? x.c.VisitInstanceId : (ulong?)null,
                RequestCode = x.vr.RequestCode,
                DelegationName = x.vr.DelegationName,
                PartnerName = x.p != null ? x.p.Name : x.vr.RegistrantOrganization,
                RequestStatus = x.vr.Status,
                CampusStatus = x.c != null ? x.c.Status : null,
                VisitScope = x.vr.VisitScope,
                CampusId = x.c != null ? x.c.CampusId : (ulong?)null,
                CampusName = x.cn != null ? x.cn.Name : null,
                CreatedByUserId = x.vr.CreatedBy,
                CurrentHostUserId = x.c != null ? x.c.CurrentHostUserId : (ulong?)null,
                HostName = x.hu != null ? x.hu.FullName : null,
                VisitorUserId = x.vr.VisitorUserId,
                VisitorName = x.vu != null ? x.vu.FullName : null,
                IsCurrentUserParticipant = x.c != null && _context.VisitParticipants.Any(p => p.VisitInstanceId == x.c.VisitInstanceId && p.UserId == userId),
                ExpectedStartAt = x.c != null ? x.c.PlannedStartAt : (DateTime?)null,
                ExpectedEndAt = x.c != null ? x.c.PlannedEndAt : (DateTime?)null,
                PlannedStartAt = x.c != null ? x.c.PlannedStartAt : (DateTime?)null,
                PlannedEndAt = x.c != null ? x.c.PlannedEndAt : (DateTime?)null,
                ExpectedGuestCount = x.vr.ExpectedGuestCount,
                CreatedAt = x.vr.CreatedAt,
                SubmittedAt = x.vr.SubmittedAt,
                CancelledAt = x.c != null ? x.c.CancelledAt : x.vr.CancelledAt,
                CancellationReason = x.c != null ? x.c.CancellationReason : x.vr.CancellationReason
            })
            .ToListAsync(cancellationToken);

        return PaginatedResult<VisitRequestManagementItemDto>.Create(items, request.Page, request.PageSize, totalItems);
    }
}