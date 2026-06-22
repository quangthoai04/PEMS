using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

/// <summary>
/// UC-20 View Guest Delegation List. Returns rows already filtered to the caller's
/// responsibility scope (the backend is the single authority â€” the frontend never
/// post-filters by role). Two tabs:
///   â€¢ "responsible" (ÄÆ¡n phá»¥ trÃ¡ch): requests the user creates / approves / hosts /
///     is assigned a task on. Visitor &amp; HO see one row per request; campus actors
///     (Staff Leader/Staff, Dept, Student) see one row per relevant campus instance.
///   â€¢ "attending" (ÄÆ¡n má»i tham dá»±): requests the user has ACCEPTED an invitation for.
/// Each row also carries <see cref="VisitRequestManagementItemDto.AllowedActions"/>.
/// </summary>
public sealed class ViewGuestDelegationListQueryHandler
    : IRequestHandler<ViewGuestDelegationListQuery, PaginatedResult<VisitRequestManagementItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    private const string TabAttending = "attending";

    public ViewGuestDelegationListQueryHandler(
        IApplicationDbContext context, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PaginatedResult<VisitRequestManagementItemDto>> Handle(
        ViewGuestDelegationListQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("Current user is not authenticated.");

        var userId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;
        var isStaffLeader = roleCode == RoleCodes.Staff && subRole == UserSubRoles.Leader;
        var tab = string.Equals(request.Tab, TabAttending, StringComparison.OrdinalIgnoreCase)
            ? TabAttending
            : "responsible";

        // Admin does not take part in the reception flow (also has no UC-20 grant).
        // The "ÄÆ¡n má»i tham dá»±" (attending) tab is ONLY for users who can be invited as a
        // non-host participant: regular Staff, Dept, Student. HO, Staff Leader/IC Head and
        // Visitor are never invitees (they approve / assign / own), so they have no Tab 2.
        if (roleCode == RoleCodes.Admin ||
            (tab == TabAttending &&
                (roleCode == RoleCodes.Visitor || roleCode == RoleCodes.Ho || isStaffLeader)))
        {
            return PaginatedResult<VisitRequestManagementItemDto>.Create(
                new List<VisitRequestManagementItemDto>(), request.Page, request.PageSize, 0);
        }

        List<VisitRequestManagementItemDto> items;
        int totalItems;

        if (tab == TabAttending)
        {
            (items, totalItems) = await QueryInstanceLevelAsync(request, userId, attending: true, cancellationToken);
        }
        else if (roleCode == RoleCodes.Visitor || roleCode == RoleCodes.Ho)
        {
            // Request-level rows (one per delegation): HO acts on the whole request,
            // Visitor owns the whole request.
            (items, totalItems) = await QueryRequestLevelAsync(request, userId, roleCode, cancellationToken);
        }
        else
        {
            // Campus actors (Staff Leader/Staff, Dept, Student): one row per relevant instance.
            (items, totalItems) = await QueryInstanceLevelAsync(request, userId, attending: false, cancellationToken);
        }

        var now = _clock.UtcNow;
        foreach (var item in items)
        {
            item.AllowedActions = BuildAllowedActions(item, tab, userId, now);
            item.TabType = ResolveTabType(tab, roleCode);
            item.CurrentUserRelation = ResolveRelation(item, tab, roleCode, isStaffLeader);
            // Read-only when no mutating action is available (only VIEW_DETAIL, or none).
            item.IsReadOnly = !item.AllowedActions.Any(a => a != "VIEW_DETAIL");
        }

        return PaginatedResult<VisitRequestManagementItemDto>.Create(items, request.Page, request.PageSize, totalItems);
    }

    // â”€â”€ Instance-level (attending tab, or responsible tab for campus actors) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Rooted on campus instances (INNER join to the request) and projected to flat columns,
    // then enriched in memory. This keeps the SQL translatable on Pomelo/MySQL â€” correlated
    // subqueries over an optional LEFT-JOIN side or scalar subqueries in the projection
    // (the previous shape) fail to translate there.
    private async Task<(List<VisitRequestManagementItemDto> Items, int Total)> QueryInstanceLevelAsync(
        ViewGuestDelegationListQuery request, ulong userId, bool attending, CancellationToken ct)
    {
        var q = from c in _context.VisitRequestCampuses
                join vr in _context.VisitRequests on c.VisitRequestId equals vr.VisitRequestId
                select new { c, vr };

        if (attending)
        {
            var currentUserEmail = _currentUser.Email?.ToLower();
            // Tab 2 â€” "ÄÆ¡n má»i tham dá»±": instances the user was INVITED to by someone else as a
            // NON-host participant. Anything where the user is the host /
            // creator / visitor belongs in Tab 1, so it is excluded here.
            q = q.Where(x =>
                x.vr.Status != VisitRequestStatuses.Rejected &&
                x.vr.Status != VisitRequestStatuses.Cancelled &&
                x.c.Status != VisitInstanceStatus.Cancelled &&
                x.c.CurrentHostUserId != userId &&
                x.vr.CreatedBy != userId &&
                (string.IsNullOrEmpty(currentUserEmail) || x.vr.RegistrantEmail == null || x.vr.RegistrantEmail.ToLower() != currentUserEmail) &&
                x.vr.VisitorUserId != userId &&
                _context.VisitParticipants.Any(pp =>
                    pp.VisitInstanceId == x.c.VisitInstanceId &&
                    pp.UserId == userId &&
                    !pp.IsHost &&
                    pp.Status == ParticipantStatuses.Accepted &&
                    (pp.ParticipantRole == ParticipantRoles.IcSupport || pp.ParticipantRole == ParticipantRoles.DeptSupport || pp.ParticipantRole == ParticipantRoles.Student) &&
                    (pp.InvitedBy == null || pp.InvitedBy != userId)));
        }
        else
        {
            var roleCode = _currentUser.RoleCode;
            var subRole = _currentUser.SubRole;

            if (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Leader)
            {
                var primaryCampusId = _currentUser.PrimaryCampusId
                    ?? throw new UnauthorizedAccessException("Staff Leader missing PrimaryCampusId");

                // Single-campus of my campus (I am the approver); or multi-campus of my
                // campus only AFTER HO approval (before that it isn't my concern yet).
                q = q.Where(x => x.c.CampusId == primaryCampusId
                    && (x.vr.VisitScope == VisitScopes.SingleCampus
                        || (x.vr.VisitScope == VisitScopes.MultiCampus && x.vr.Status == VisitRequestStatuses.Approved)));
            }
            else if (roleCode == RoleCodes.Staff)
            {
                var currentUserEmail = _currentUser.Email?.ToLower();
                // Regular Staff: instances I host or am creator of.
                q = q.Where(x =>
                    x.c.CurrentHostUserId == userId
                    || x.vr.CreatedBy == userId
                    || (!string.IsNullOrEmpty(currentUserEmail) && x.vr.RegistrantEmail != null && x.vr.RegistrantEmail.ToLower() == currentUserEmail));
            }
            else if (roleCode == RoleCodes.Department || roleCode == RoleCodes.Student)
            {
                // Dept (incl. Dept Leader = DEPARTMENT + sub_role Leader) / Student appear in Tab 1
                // only when given a concrete assignment â€” a logistics/agenda task, or an
                // ASSIGNED (not merely INVITED) participant slot. No throw; empty if none.
                q = q.Where(x =>
                    _context.VisitLogisticsItems.Any(l => l.VisitInstanceId == x.c.VisitInstanceId && l.AssignedToUserId == userId)
                    || _context.VisitAgendas.Any(a => a.VisitInstanceId == x.c.VisitInstanceId && a.ResponsibleUserId == userId)
                    || _context.VisitParticipants.Any(pp => pp.VisitInstanceId == x.c.VisitInstanceId && pp.UserId == userId && pp.Status == ParticipantStatuses.Assigned));
            }
            else
            {
                // Unsupported role for the responsible tab â†’ empty list (never throw).
                return (new List<VisitRequestManagementItemDto>(), 0);
            }
        }

        // â”€â”€ Common filters â”€â”€
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.ToLower();
            q = q.Where(x =>
                (x.vr.DelegationName != null && x.vr.DelegationName.ToLower().Contains(keyword)) ||
                (x.vr.RequestCode != null && x.vr.RequestCode.ToLower().Contains(keyword)) ||
                (x.vr.RegistrantOrganization != null && x.vr.RegistrantOrganization.ToLower().Contains(keyword)) ||
                _context.Partners.Any(p => p.PartnerId == x.vr.PartnerId && p.Name != null && p.Name.ToLower().Contains(keyword)) ||
                _context.Campuses.Any(cc => cc.CampusId == x.c.CampusId && cc.Name.ToLower().Contains(keyword)) ||
                _context.Users.Any(u => u.UserId == x.c.CurrentHostUserId && u.FullName.ToLower().Contains(keyword)) ||
                _context.Users.Any(u => u.UserId == x.vr.VisitorUserId && u.FullName.ToLower().Contains(keyword)));
        }

        if (request.CancelledOnly)
        {
            q = q.Where(x => x.vr.Status == VisitRequestStatuses.Cancelled || x.c.Status == VisitInstanceStatus.Cancelled);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.RequestStatus))
                q = q.Where(x => x.vr.Status == request.RequestStatus);
            if (!string.IsNullOrWhiteSpace(request.CampusStatus))
                q = q.Where(x => x.c.Status == request.CampusStatus);
        }

        if (request.CampusId.HasValue)
            q = q.Where(x => x.c.CampusId == request.CampusId.Value);

        if (!string.IsNullOrWhiteSpace(request.VisitScope))
            q = q.Where(x => x.vr.VisitScope == request.VisitScope);

        if (!string.IsNullOrWhiteSpace(request.VisitScopes))
        {
            var scopes = request.VisitScopes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            if (scopes.Any())
                q = q.Where(x => scopes.Contains(x.vr.VisitScope));
        }

        if (request.FromDate.HasValue)
        {
            var fromDateStart = request.FromDate.Value.Date;
            q = q.Where(x => x.c.PlannedEndAt >= fromDateStart);
        }
        if (request.ToDate.HasValue)
        {
            var toDateEnd = request.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            q = q.Where(x => x.c.PlannedStartAt <= toDateEnd);
        }

        if (!string.IsNullOrWhiteSpace(request.Timing))
        {
            var timing = request.Timing.ToUpperInvariant();
            var nowUtc = _clock.UtcNow;
            if (timing == "UPCOMING")
                q = q.Where(x => x.c.PlannedStartAt > nowUtc && x.vr.Status != VisitRequestStatuses.Rejected && x.vr.Status != VisitRequestStatuses.Cancelled && x.c.Status != VisitInstanceStatus.Cancelled && x.c.Status != VisitInstanceStatus.Closed);
            else if (timing == "ONGOING")
                q = q.Where(x => x.c.Status == VisitInstanceStatus.DuringVisit);
            else if (timing == "ENDED")
                q = q.Where(x => x.c.Status == VisitInstanceStatus.Closed);
        }

        if (!string.IsNullOrWhiteSpace(request.Relation))
        {
            var rel = request.Relation.ToUpperInvariant();
            if (rel == "HOST")
                q = q.Where(x => x.c.CurrentHostUserId == userId);
            else if (rel == "TASK_ASSIGNEE")
                q = q.Where(x => x.c.CurrentHostUserId != userId); // simplified relation for staff
            else if (rel == "PENDING_HOST_ASSIGNMENT")
                q = q.Where(x => x.vr.VisitScope == VisitScopes.MultiCampus 
                    && x.vr.Status == VisitRequestStatuses.Approved
                    && x.c.HostAssignmentSource == "AUTO_STAFF_LEADER"
                    && (x.c.Status == VisitInstanceStatus.Assigned || x.c.Status == VisitInstanceStatus.BeforeVisit));
        }

        if (request.ActionableOnly == true)
        {
            var roleCode = _currentUser.RoleCode;
            var subRole = _currentUser.SubRole;
            if (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Leader)
            {
                var nowUtc = _clock.UtcNow;
                q = q.Where(x => (x.vr.VisitScope == VisitScopes.SingleCampus && x.vr.Status == VisitRequestStatuses.PendingApproval)
                    || (x.vr.VisitScope == VisitScopes.MultiCampus && x.vr.Status == VisitRequestStatuses.Approved && x.c.Status == VisitInstanceStatus.Assigned && (x.c.PlannedStartAt == null || x.c.PlannedStartAt > nowUtc)));
            }
        }
        if (request.ReadOnlyOnly == true)
        {
            var roleCode = _currentUser.RoleCode;
            var subRole = _currentUser.SubRole;
            if (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Leader)
            {
                var nowUtc = _clock.UtcNow;
                q = q.Where(x => !((x.vr.VisitScope == VisitScopes.SingleCampus && x.vr.Status == VisitRequestStatuses.PendingApproval)
                    || (x.vr.VisitScope == VisitScopes.MultiCampus && x.vr.Status == VisitRequestStatuses.Approved && x.c.Status == VisitInstanceStatus.Assigned && (x.c.PlannedStartAt == null || x.c.PlannedStartAt > nowUtc))));
            }
        }

        var total = await q.CountAsync(ct);

        var pageQuery = q;
        if (request.SortBy?.ToLower() == "plannedstartat")
        {
            if (request.SortOrder?.ToLower() == "asc")
                pageQuery = pageQuery.OrderBy(x => x.c.PlannedStartAt).ThenBy(x => x.c.VisitInstanceId);
            else
                pageQuery = pageQuery.OrderByDescending(x => x.c.PlannedStartAt).ThenByDescending(x => x.c.VisitInstanceId);
        }
        else if (request.SortOrder?.ToLower() == "asc")
            pageQuery = pageQuery.OrderBy(x => x.c.PlannedStartAt).ThenBy(x => x.c.VisitInstanceId);
        else if (request.SortOrder?.ToLower() == "desc")
            pageQuery = pageQuery.OrderByDescending(x => x.c.PlannedStartAt).ThenByDescending(x => x.c.VisitInstanceId);
        else
            pageQuery = pageQuery.OrderByDescending(x => x.vr.CreatedAt).ThenByDescending(x => x.c.VisitInstanceId);

        var page = await pageQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new
            {
                x.c.VisitInstanceId,
                x.c.VisitRequestId,
                x.c.CampusId,
                CampusStatus = x.c.Status,
                x.c.CurrentHostUserId,
                x.c.HostAssignmentSource,
                x.c.PlannedStartAt,
                x.c.PlannedEndAt,
                CampusCancelledAt = x.c.CancelledAt,
                CampusCancellationReason = x.c.CancellationReason,
                CampusCancellationActorType = x.c.CancellationActorType,
                CampusCancellationSource = x.c.CancellationSource,
                CampusCancelledBy = x.c.CancelledBy,
                x.vr.RequestCode,
                x.vr.DelegationName,
                x.vr.PartnerId,
                x.vr.RegistrantOrganization,
                RequestStatus = x.vr.Status,
                x.vr.VisitScope,
                x.vr.CreatedBy,
                x.vr.VisitorUserId,
                x.vr.ExpectedGuestCount,
                x.vr.CreatedAt,
                x.vr.SubmittedAt,
                RequestCancelledAt = x.vr.CancelledAt,
                RequestCancellationReason = x.vr.CancellationReason,
                RequestCancellationActorType = x.vr.CancellationActorType,
                RequestCancellationSource = x.vr.CancellationSource,
                RequestCancelledBy = x.vr.CancelledBy,
                x.vr.DecisionNote,
            })
            .ToListAsync(ct);

        if (page.Count == 0)
            return (new List<VisitRequestManagementItemDto>(), total);

        // â”€â”€ Enrich in memory via batched lookups (Pomelo-friendly: no projection subqueries) â”€â”€
        var instanceIds = page.Select(r => r.VisitInstanceId).Distinct().ToList();
        var requestIds = page.Select(r => r.VisitRequestId).Distinct().ToList();
        var campusIds = page.Select(r => r.CampusId).Distinct().ToList();
        var partnerIds = page.Where(r => r.PartnerId.HasValue).Select(r => r.PartnerId!.Value).Distinct().ToList();
        var userIds = page.SelectMany(r => new[] { r.CurrentHostUserId, (ulong?)r.VisitorUserId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var campusCountByRequest = (await _context.VisitRequestCampuses
                .Where(vrc => requestIds.Contains(vrc.VisitRequestId))
                .Select(vrc => vrc.VisitRequestId)
                .ToListAsync(ct))
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        var myParticipationRole = (await _context.VisitParticipants
                .Where(pp => instanceIds.Contains(pp.VisitInstanceId) && pp.UserId == userId)
                .Select(pp => new { pp.VisitInstanceId, pp.ParticipantRole })
                .ToListAsync(ct))
            .GroupBy(p => p.VisitInstanceId)
            .ToDictionary(g => g.Key, g => g.First().ParticipantRole);

        var campusNames = campusIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _context.Campuses.Where(cc => campusIds.Contains(cc.CampusId)).ToDictionaryAsync(cc => cc.CampusId, cc => cc.Name, ct);
        var partnerNames = partnerIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _context.Partners.Where(p => partnerIds.Contains(p.PartnerId)).ToDictionaryAsync(p => p.PartnerId, p => p.Name, ct);
        var userNames = userIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _context.Users.Where(u => userIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        var items = page.Select(r =>
        {
            string? partnerName = r.PartnerId.HasValue && partnerNames.TryGetValue(r.PartnerId.Value, out var pn) ? pn : r.RegistrantOrganization;
            string? campusName = campusNames.TryGetValue(r.CampusId, out var cn) ? cn : null;
            string? hostName = r.CurrentHostUserId.HasValue && userNames.TryGetValue(r.CurrentHostUserId.Value, out var hn) ? hn : null;
            string? visitorName = r.VisitorUserId.HasValue && userNames.TryGetValue(r.VisitorUserId.Value, out var vn) ? vn : null;
            myParticipationRole.TryGetValue(r.VisitInstanceId, out var participantRole);

            return new VisitRequestManagementItemDto
            {
                VisitRequestId = r.VisitRequestId,
                VisitInstanceId = r.VisitInstanceId,
                RequestCode = r.RequestCode,
                DelegationName = r.DelegationName,
                PartnerName = partnerName,
                RequestStatus = r.RequestStatus,
                CampusStatus = r.CampusStatus,
                VisitScope = r.VisitScope,
                CampusId = r.CampusId,
                CampusName = campusName,
                CampusCount = campusCountByRequest.TryGetValue(r.VisitRequestId, out var cc2) ? cc2 : 1,
                CreatedByUserId = r.CreatedBy,
                CurrentHostUserId = r.CurrentHostUserId,
                HostName = hostName,
                HostAssignmentSource = r.HostAssignmentSource,
                CurrentUserIsHost = r.CurrentHostUserId == userId,
                VisitorUserId = r.VisitorUserId,
                VisitorName = visitorName,
                IsCurrentUserParticipant = participantRole != null,
                ParticipantRole = participantRole,
                ExpectedStartAt = r.PlannedStartAt,
                ExpectedEndAt = r.PlannedEndAt,
                PlannedStartAt = r.PlannedStartAt,
                PlannedEndAt = r.PlannedEndAt,
                ExpectedGuestCount = r.ExpectedGuestCount,
                CreatedAt = r.CreatedAt,
                SubmittedAt = r.SubmittedAt,
                CancelledAt = r.CampusCancelledAt ?? r.RequestCancelledAt,
                CancellationReason = r.CampusCancellationReason ?? r.RequestCancellationReason,
                CancellationActorType = r.CampusCancellationActorType ?? r.RequestCancellationActorType,
                CancellationSource = r.CampusCancellationSource ?? r.RequestCancellationSource,
                CancelledBy = r.CampusCancelledBy ?? r.RequestCancelledBy,
                DecisionNote = r.DecisionNote,
            };
        }).ToList();

        return (items, total);
    }

    // â”€â”€ Request-level (responsible tab for Visitor &amp; HO): one row per delegation â”€â”€
    private async Task<(List<VisitRequestManagementItemDto> Items, int Total)> QueryRequestLevelAsync(
        ViewGuestDelegationListQuery request, ulong userId, string? roleCode, CancellationToken ct)
    {
        var q = _context.VisitRequests.AsQueryable();

        if (roleCode == RoleCodes.Visitor)
            q = q.Where(vr => vr.VisitorUserId == userId || vr.CreatedBy == userId);
        // HO sees every MULTI_CAMPUS request (they decide it) AND every SINGLE_CAMPUS request
        // in read-only monitoring mode (business rule chá»‘t 2026-06: HO theo dÃµi SINGLE_CAMPUS).
        // No filter is applied for HO here â€” read-only is enforced via AllowedActions (the HO
        // action builder only grants HO_APPROVE/HO_REJECT to MULTI_CAMPUS pending requests).
        // else if (roleCode == RoleCodes.Ho)  â†’ all requests visible.

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.ToLower();
            q = q.Where(vr =>
                (vr.DelegationName != null && vr.DelegationName.ToLower().Contains(kw)) ||
                (vr.RequestCode != null && vr.RequestCode.ToLower().Contains(kw)) ||
                (vr.RegistrantOrganization != null && vr.RegistrantOrganization.ToLower().Contains(kw)) ||
                (vr.Partner != null && vr.Partner.Name != null && vr.Partner.Name.ToLower().Contains(kw)));
        }

        if (request.CancelledOnly)
        {
            q = q.Where(vr => vr.Status == VisitRequestStatuses.Cancelled || vr.CampusInstances.Any(i => i.Status == VisitInstanceStatus.Cancelled));
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.RequestStatus))
                q = q.Where(vr => vr.Status == request.RequestStatus);
            if (!string.IsNullOrWhiteSpace(request.CampusStatus))
                q = q.Where(vr => vr.CampusInstances.Any(i => i.Status == request.CampusStatus));
        }

        if (request.CampusId.HasValue)
            q = q.Where(vr => vr.CampusInstances.Any(i => i.CampusId == request.CampusId.Value));

        if (!string.IsNullOrWhiteSpace(request.VisitScope))
            q = q.Where(vr => vr.VisitScope == request.VisitScope);

        if (!string.IsNullOrWhiteSpace(request.VisitScopes))
        {
            var scopes = request.VisitScopes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            if (scopes.Any())
                q = q.Where(vr => scopes.Contains(vr.VisitScope));
        }

        if (request.FromDate.HasValue)
        {
            var from = request.FromDate.Value.Date;
            q = q.Where(vr => vr.CampusInstances.Any(i => i.PlannedEndAt >= from));
        }
        if (request.ToDate.HasValue)
        {
            var to = request.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            q = q.Where(vr => vr.CampusInstances.Any(i => i.PlannedStartAt <= to));
        }

        if (!string.IsNullOrWhiteSpace(request.Timing))
        {
            var timing = request.Timing.ToUpperInvariant();
            var nowUtc = _clock.UtcNow;
            if (timing == "UPCOMING")
                q = q.Where(vr => vr.CampusInstances.Any(i => i.PlannedStartAt > nowUtc) && vr.Status != VisitRequestStatuses.Rejected && vr.Status != VisitRequestStatuses.Cancelled && !vr.CampusInstances.All(i => i.Status == VisitInstanceStatus.Cancelled || i.Status == VisitInstanceStatus.Closed));
            else if (timing == "ONGOING")
                q = q.Where(vr => vr.CampusInstances.Any(i => i.Status == VisitInstanceStatus.DuringVisit));
            else if (timing == "ENDED")
                q = q.Where(vr => vr.CampusInstances.Any(i => i.Status == VisitInstanceStatus.Closed));
        }

        if (!string.IsNullOrWhiteSpace(request.Relation))
        {
            var rel = request.Relation.ToUpperInvariant();
            if (rel == "VISITOR_OWNER")
                q = q.Where(vr => vr.VisitorUserId == userId);
        }

        if (request.ActionableOnly == true && roleCode == RoleCodes.Ho)
        {
            q = q.Where(vr => vr.VisitScope == VisitScopes.MultiCampus && vr.Status == VisitRequestStatuses.PendingApproval);
        }
        if (request.ReadOnlyOnly == true && roleCode == RoleCodes.Ho)
        {
            q = q.Where(vr => vr.VisitScope == VisitScopes.SingleCampus || vr.Status != VisitRequestStatuses.PendingApproval);
        }

        var total = await q.CountAsync(ct);

        // Load the page with instances + partner, then shape request-level rows in memory
        // (avoids fragile conditional-subquery projection translation).
        var pageQuery = q;
        if (request.SortBy?.ToLower() == "plannedstartat")
        {
            if (request.SortOrder?.ToLower() == "asc")
                pageQuery = pageQuery.OrderBy(vr => vr.CampusInstances.Min(i => i.PlannedStartAt)).ThenBy(vr => vr.VisitRequestId);
            else
                pageQuery = pageQuery.OrderByDescending(vr => vr.CampusInstances.Max(i => i.PlannedStartAt)).ThenByDescending(vr => vr.VisitRequestId);
        }
        else if (request.SortOrder?.ToLower() == "asc")
            pageQuery = pageQuery.OrderBy(vr => vr.CampusInstances.Min(i => i.PlannedStartAt)).ThenBy(vr => vr.VisitRequestId);
        else if (request.SortOrder?.ToLower() == "desc")
            pageQuery = pageQuery.OrderByDescending(vr => vr.CampusInstances.Max(i => i.PlannedStartAt)).ThenByDescending(vr => vr.VisitRequestId);
        else
            pageQuery = pageQuery.OrderByDescending(vr => vr.CreatedAt).ThenByDescending(vr => vr.VisitRequestId);

        var requests = await pageQuery
            .Include(vr => vr.Partner)
            .Include(vr => vr.CampusInstances)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        // Batch-resolve campus (single-campus rows) + host + visitor display names.
        var campusIds = requests
            .Where(vr => vr.CampusInstances.Count == 1)
            .Select(vr => vr.CampusInstances.First().CampusId)
            .Distinct().ToList();
        var userIds = requests
            .SelectMany(vr => new[]
            {
                vr.CampusInstances.Count == 1 ? vr.CampusInstances.First().CurrentHostUserId : null,
                (ulong?)vr.VisitorUserId,
            })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var campusNames = campusIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _context.Campuses.Where(cc => campusIds.Contains(cc.CampusId)).ToDictionaryAsync(cc => cc.CampusId, cc => cc.Name, ct);
        var userNames = userIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _context.Users.Where(u => userIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        var items = requests.Select(vr =>
        {
            var instances = vr.CampusInstances;
            var count = instances.Count;
            var single = count == 1 ? instances.First() : null;

            string? campusName = single != null && campusNames.TryGetValue(single.CampusId, out var cnm) ? cnm
                : count > 1 ? $"{count} cÆ¡ sá»Ÿ"
                : null;
            ulong? hostUserId = single?.CurrentHostUserId;
            string? hostName = hostUserId.HasValue && userNames.TryGetValue(hostUserId.Value, out var hnm) ? hnm : null;
            string? visitorName = vr.VisitorUserId.HasValue && userNames.TryGetValue(vr.VisitorUserId.Value, out var vnm) ? vnm : null;
            DateTime? minStart = count > 0 ? instances.Min(i => i.PlannedStartAt) : (DateTime?)null;
            DateTime? maxEnd = count > 0 ? instances.Max(i => i.PlannedEndAt) : (DateTime?)null;

            return new VisitRequestManagementItemDto
            {
                VisitRequestId = vr.VisitRequestId,
                VisitInstanceId = single?.VisitInstanceId,
                RequestCode = vr.RequestCode,
                DelegationName = vr.DelegationName,
                PartnerName = vr.Partner != null ? vr.Partner.Name : vr.RegistrantOrganization,
                RequestStatus = vr.Status,
                CampusStatus = single?.Status,
                VisitScope = vr.VisitScope,
                CampusId = single?.CampusId,
                CampusName = campusName,
                CampusCount = count,
                CreatedByUserId = vr.CreatedBy,
                CurrentHostUserId = hostUserId,
                HostName = hostName,
                HostAssignmentSource = single?.HostAssignmentSource,
                CurrentUserIsHost = false,
                VisitorUserId = vr.VisitorUserId,
                VisitorName = visitorName,
                IsCurrentUserParticipant = false,
                ParticipantRole = null,
                ExpectedStartAt = minStart,
                ExpectedEndAt = maxEnd,
                PlannedStartAt = minStart,
                PlannedEndAt = maxEnd,
                ExpectedGuestCount = vr.ExpectedGuestCount,
                CreatedAt = vr.CreatedAt,
                SubmittedAt = vr.SubmittedAt,
                CancelledAt = single?.CancelledAt ?? vr.CancelledAt,
                CancellationReason = single?.CancellationReason ?? vr.CancellationReason,
                CancellationActorType = single?.CancellationActorType ?? vr.CancellationActorType,
                CancellationSource = single?.CancellationSource ?? vr.CancellationSource,
                CancelledBy = single?.CancelledBy ?? vr.CancelledBy,
                DecisionNote = vr.DecisionNote,
            };
        }).ToList();

        return (items, total);
    }

    /// <summary>
    /// Computes the business actions the caller may take on a row. This is the single
    /// source of truth the frontend renders buttons from; every action is re-validated
    /// server-side by its command handler.
    /// </summary>
    private List<string> BuildAllowedActions(VisitRequestManagementItemDto item, string tab, ulong userId, DateTime now)
    {
        var actions = new List<string> { "VIEW_DETAIL" };
        if (tab == TabAttending)
            return actions; // attending tab is read-only

        var roleCode = _currentUser.RoleCode?.ToUpperInvariant();
        var subRole = _currentUser.SubRole;
        var primaryCampusId = _currentUser.PrimaryCampusId;

        bool isHo = roleCode == RoleCodes.Ho;
        bool isStaffLeader = roleCode == RoleCodes.Staff && string.Equals(subRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase);
        bool isVisitor = roleCode == RoleCodes.Visitor;
        bool isMulti = item.VisitScope == VisitScopes.MultiCampus;
        bool isSingle = item.VisitScope == VisitScopes.SingleCampus;
        bool beforeStart = !item.PlannedStartAt.HasValue || item.PlannedStartAt.Value > now;
        bool sameCampus = item.CampusId.HasValue && primaryCampusId.HasValue && item.CampusId == primaryCampusId;

        // HO â€” multi-campus request decisions (whole request).
        if (isHo && isMulti && item.RequestStatus == VisitRequestStatuses.PendingApproval)
        {
            actions.Add("HO_APPROVE");
            actions.Add("HO_REJECT");
        }

        // Staff Leader â€” own campus only.
        if (isStaffLeader && sameCampus)
        {
            if (isSingle && item.RequestStatus == VisitRequestStatuses.PendingApproval)
            {
                actions.Add("APPROVE_AND_ASSIGN_HOST"); // approve + pick host (opens host picker)
                actions.Add("CAMPUS_REJECT");
            }
            else if (isMulti && item.RequestStatus == VisitRequestStatuses.Approved
                     && (item.CampusStatus == VisitInstanceStatus.Assigned || item.CampusStatus == VisitInstanceStatus.BeforeVisit) && beforeStart
                     && item.HostAssignmentSource == "AUTO_STAFF_LEADER")
            {
                actions.Add("TRANSFER_HOST"); // HO already approved + auto-assigned the IC head; SL hands off to a real staff
            }
        }

        // Visitor â€” self-cancel own request (pending or approved) before it starts.
        if (isVisitor && item.VisitorUserId == userId
            && (item.RequestStatus == VisitRequestStatuses.PendingApproval || item.RequestStatus == VisitRequestStatuses.Approved)
            && beforeStart)
        {
            actions.Add("CANCEL_BY_VISITOR");
        }

        // Host â€” cancel the campus instance they own before it starts.
        bool isTempHost = item.CurrentUserIsHost && item.HostAssignmentSource == "AUTO_STAFF_LEADER";
        if (!isStaffLeader && item.CurrentUserIsHost && !isTempHost
            && (item.CampusStatus == VisitInstanceStatus.Assigned || item.CampusStatus == VisitInstanceStatus.BeforeVisit)
            && beforeStart)
        {
            actions.Add("CANCEL_BY_HOST");
        }

        // If official host assigned, don't allow transfer for Staff Leader, unless we explicitly allow it later.
        if (isStaffLeader && sameCampus && item.CampusStatus != VisitInstanceStatus.Cancelled)
        {
            if (item.HostAssignmentSource != "MANUAL_APPROVAL" && item.HostAssignmentSource != "TRANSFERRED")
            {
                // We handle transfer permission below, actually we already added TRANSFER_HOST for AUTO_STAFF_LEADER.
            }
        }

        return actions;
    }

    /// <summary>Which tab/section a row belongs to, for the frontend's convenience.</summary>
    private static string ResolveTabType(string tab, string? roleCode)
    {
        if (tab == TabAttending) return "INVITED";
        if (roleCode == RoleCodes.Visitor) return "MY_REQUESTS";
        return "RESPONSIBLE";
    }

    /// <summary>
    /// Best-effort relation of the caller to a row (display/telemetry only â€” never an
    /// authorization input; every action is still re-validated server-side).
    /// </summary>
    private string ResolveRelation(VisitRequestManagementItemDto item, string tab, string? roleCode, bool isStaffLeader)
    {
        if (tab == TabAttending)
        {
            return item.ParticipantRole switch
            {
                ParticipantRoles.IcSupport => "IC_SUPPORT",
                ParticipantRoles.DeptSupport => "DEPT_SUPPORT",
                ParticipantRoles.Student => "STUDENT_SUPPORT",
                _ => "NONE",
            };
        }

        if (item.CurrentUserIsHost)
            return item.HostAssignmentSource == "AUTO_STAFF_LEADER" ? "PENDING_HOST_ASSIGNMENT" : "HOST";
        if (roleCode == RoleCodes.Visitor)
            return "VISITOR_OWNER";
        if (roleCode == RoleCodes.Ho)
            // HO can DECIDE only a pending multi-campus request; everything else is monitoring.
            return item.VisitScope == VisitScopes.MultiCampus
                && item.RequestStatus == VisitRequestStatuses.PendingApproval
                ? "HO_APPROVER" : "HO_MONITOR";
        if (isStaffLeader)
        {
            if (item.VisitScope == VisitScopes.MultiCampus
                && item.RequestStatus == VisitRequestStatuses.Approved
                && item.HostAssignmentSource == "AUTO_STAFF_LEADER"
                && item.CurrentHostUserId == _currentUser.UserId
                && (item.CampusStatus == VisitInstanceStatus.Assigned || item.CampusStatus == VisitInstanceStatus.BeforeVisit))
            {
                return "PENDING_HOST_ASSIGNMENT";
            }
            return "CAMPUS_APPROVER";
        }
        if (roleCode == RoleCodes.Department || roleCode == RoleCodes.Student)
            return "DEPARTMENT_TASK_OWNER";
        return "NONE";
    }
}
