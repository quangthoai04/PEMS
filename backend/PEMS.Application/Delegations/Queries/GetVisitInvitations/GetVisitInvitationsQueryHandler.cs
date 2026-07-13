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
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.GetVisitInvitations;

public sealed class GetVisitInvitationsQueryHandler
    : IRequestHandler<GetVisitInvitationsQuery, PaginatedResult<InvitationListItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public GetVisitInvitationsQueryHandler(
        IApplicationDbContext context, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PaginatedResult<InvitationListItemDto>> Handle(
        GetVisitInvitationsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode?.ToUpperInvariant();
        var subRole = _currentUser.SubRole?.ToUpperInvariant();

        var q = from p in _context.VisitParticipants
                join c in _context.VisitRequestCampuses on p.VisitInstanceId equals c.VisitInstanceId
                join vr in _context.VisitRequests on c.VisitRequestId equals vr.VisitRequestId
                where p.UserId == userId && !p.IsHost && p.Status != ParticipantStatuses.Removed
                select new { p, c, vr };

        // Lọc theo Role
        if (roleCode == RoleCodes.Staff)
        {
            q = q.Where(x => x.p.ParticipantRole == ParticipantRoles.IcSupport && x.p.Status != ParticipantStatuses.Assigned);
        }
        else if (roleCode == RoleCodes.Department || roleCode == "DEPT")
        {
            if (subRole == UserSubRoles.Leader)
            {
                // Nhận lời mời từ IC/Host
                q = q.Where(x => x.p.ParticipantRole == ParticipantRoles.DeptSupport && x.p.Status != ParticipantStatuses.Assigned);
            }
            else // Staff
            {
                // Chỉ nhận nhiệm vụ được giao
                q = q.Where(x => x.p.ParticipantRole == ParticipantRoles.DeptSupport && x.p.AssignedBy != null && x.p.Status == ParticipantStatuses.Assigned);
            }
        }
        else if (roleCode == RoleCodes.Student)
        {
            q = q.Where(x => x.p.ParticipantRole == ParticipantRoles.Student && x.p.Status != ParticipantStatuses.Assigned);
        }
        else
        {
            // Các role khác không có dữ liệu
            return PaginatedResult<InvitationListItemDto>.Create(new List<InvitationListItemDto>(), request.Page, request.PageSize, 0);
        }

        // Lọc theo keyword
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.ToLower();
            q = q.Where(x => 
                (x.vr.DelegationName != null && x.vr.DelegationName.ToLower().Contains(keyword)) ||
                (x.vr.RequestCode != null && x.vr.RequestCode.ToLower().Contains(keyword)));
        }

        // Lọc theo date
        if (request.FromDate.HasValue)
        {
            var from = request.FromDate.Value.Date;
            q = q.Where(x => x.c.PlannedEndAt >= from);
        }
        if (request.ToDate.HasValue)
        {
            var to = request.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            q = q.Where(x => x.c.PlannedStartAt <= to);
        }

        // Lọc theo trạng thái lời mời
        if (!string.IsNullOrWhiteSpace(request.InvitationStatus))
        {
            var statusStr = request.InvitationStatus.ToUpperInvariant();
            if (statusStr != "ALL")
            {
                q = q.Where(x => x.p.Status == statusStr);
            }
        }

        var total = await q.CountAsync(cancellationToken);

        var pageQuery = q.OrderByDescending(x => x.p.InvitedAt ?? x.p.AssignedAt ?? x.p.CreatedAt)
                         .Skip((request.Page - 1) * request.PageSize)
                         .Take(request.PageSize);

        var page = await pageQuery
            .Select(x => new
            {
                x.p.ParticipantId,
                x.p.VisitInstanceId,
                x.c.VisitRequestId,
                x.c.CampusId,
                x.vr.RequestCode,
                x.vr.DelegationName,
                x.vr.VisitScope,
                x.c.PlannedStartAt,
                x.c.PlannedEndAt,
                x.p.ParticipantRole,
                x.p.Status,
                VisitRequestStatus = x.vr.Status,
                CampusVisitStatus = x.c.Status,
                x.p.InvitedBy,
                x.p.InvitedAt,
                x.p.RespondedAt,
                x.p.AssignedAt
            })
            .ToListAsync(cancellationToken);

        if (page.Count == 0)
            return PaginatedResult<InvitationListItemDto>.Create(new List<InvitationListItemDto>(), request.Page, request.PageSize, total);

        var campusIds = page.Select(x => x.CampusId).Distinct().ToList();
        var userIds = page.Where(x => x.InvitedBy.HasValue).Select(x => x.InvitedBy!.Value).Distinct().ToList();

        var campusNames = campusIds.Count == 0 
            ? new Dictionary<ulong, string>() 
            : await _context.Campuses.Where(cc => campusIds.Contains(cc.CampusId)).ToDictionaryAsync(cc => cc.CampusId, cc => cc.Name, cancellationToken);
            
        var userNames = userIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _context.Users.Where(u => userIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        var now = _clock.VietnamNow;

        var items = page.Select(x => 
        {
            var dto = new InvitationListItemDto
            {
                ParticipantId = x.ParticipantId,
                VisitInstanceId = x.VisitInstanceId,
                VisitRequestId = x.VisitRequestId,
                RequestCode = x.RequestCode,
                DelegationName = x.DelegationName,
                CampusName = campusNames.TryGetValue(x.CampusId, out var cName) ? cName : "-",
                VisitScope = x.VisitScope,
                PlannedStartAt = x.PlannedStartAt,
                PlannedEndAt = x.PlannedEndAt,
                ParticipantRole = x.ParticipantRole,
                InvitationStatus = x.Status,
                VisitRequestStatus = x.VisitRequestStatus,
                CampusVisitStatus = x.CampusVisitStatus,
                InvitedByName = x.InvitedBy.HasValue && userNames.TryGetValue(x.InvitedBy.Value, out var uName) ? uName : "-",
                InvitedAt = x.InvitedAt,
                RespondedAt = x.RespondedAt,
                AssignedAt = x.AssignedAt,
                AllowedActions = new List<string> { "VIEW_INVITATION_DETAIL", "VIEW_REQUEST_FORM" }
            };

            var isClosedOrCancelled = x.VisitRequestStatus == VisitRequestStatuses.Cancelled
                || x.VisitRequestStatus == VisitRequestStatuses.Rejected
                || x.CampusVisitStatus == VisitInstanceStatuses.Rejected
                || x.CampusVisitStatus == VisitInstanceStatuses.Cancelled
                || x.CampusVisitStatus == VisitInstanceStatuses.Closed;

            bool isActiveForInvitation = (x.VisitRequestStatus == VisitRequestStatuses.Approved
                    || x.VisitRequestStatus == VisitRequestStatuses.PartiallyApproved)
                && (x.CampusVisitStatus == VisitInstanceStatuses.Assigned
                    || x.CampusVisitStatus == VisitInstanceStatuses.BeforeVisit);

            if (x.Status == ParticipantStatuses.Accepted || x.Status == ParticipantStatuses.Assigned)
            {
                dto.AllowedActions.Add("OPEN_CONTRIBUTION");
            }

            if (roleCode == RoleCodes.Department || roleCode == "DEPT")
            {
                if (subRole == UserSubRoles.Leader)
                {
                    if (x.Status == ParticipantStatuses.Invited && isActiveForInvitation)
                    {
                        dto.AllowedActions.Add("ACCEPT_INVITATION");
                        dto.AllowedActions.Add("DECLINE_INVITATION");
                        dto.AllowedActions.Add("ASSIGN_TO_DEPARTMENT_STAFF");
                    }
                    else if (x.Status == ParticipantStatuses.Accepted && isActiveForInvitation)
                    {
                        dto.AllowedActions.Add("ASSIGN_TO_DEPARTMENT_STAFF");
                    }
                }
            }
            else // Staff, Student
            {
                if (x.Status == ParticipantStatuses.Invited && isActiveForInvitation)
                {
                    dto.AllowedActions.Add("ACCEPT_INVITATION");
                    dto.AllowedActions.Add("DECLINE_INVITATION");
                }
            }

            return dto;
        }).ToList();

        return PaginatedResult<InvitationListItemDto>.Create(items, request.Page, request.PageSize, total);
    }
}
