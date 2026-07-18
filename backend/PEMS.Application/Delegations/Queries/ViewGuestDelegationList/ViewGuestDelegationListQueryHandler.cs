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
/// responsibility scope (the backend is the single authority — the frontend never
/// post-filters by role). Tabs (actor relation):
///   • "responsible": Visitor = CONTACT-OWNER rows (one per request); HO = monitor (one per
///     request); campus actors (Staff Leader/Staff, Dept, Student) = one row per relevant
///     campus instance (regular Staff = instances they officially HOST).
///   • "attending" (Đơn mời tham dự): requests the user has ACCEPTED an invitation for.
///   • "registered" (Đơn tôi đăng ký / Tôi là người đăng ký): requests where the caller is
///     the REGISTRANT (registrant_user_id) — strictly read-only tracking rows.
///   • "hosted" (Tôi là host): instance rows the caller officially hosts (Staff Leader's
///     dedicated host view).
/// Each row also carries <see cref="VisitRequestManagementItemDto.AllowedActions"/>.
/// </summary>
public sealed class ViewGuestDelegationListQueryHandler
    : IRequestHandler<ViewGuestDelegationListQuery, PaginatedResult<VisitRequestManagementItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    private const string TabAttending = "attending";
    // "registered" (Đơn tôi đăng ký / Tôi là người đăng ký): requests where the caller is the
    // REGISTRANT (registrant_user_id) — strictly read-only tracking. Available to Visitor,
    // regular Staff and Staff Leader. A Visitor who is BOTH registrant and contact owner sees
    // the row only on their owner tab, never here.
    private const string TabRegistered = "registered";
    // "hosted" (Tôi là host): instance-level rows the caller officially hosts. Gives the Staff
    // Leader a dedicated host view separate from the campus-review tab.
    private const string TabHosted = "hosted";

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
        var isStaffRole = roleCode == RoleCodes.Staff;
        var tab = (request.Tab ?? string.Empty).ToLowerInvariant() switch
        {
            TabAttending  => TabAttending,
            TabRegistered => TabRegistered,
            TabHosted     => TabHosted,
            _             => "responsible",
        };

        // Admin does not take part in the reception flow (also has no UC-20 grant).
        // The "Đơn mời tham dự" (attending) tab is ONLY for users who can be invited as a
        // non-host participant: regular Staff, Dept, Student, and Staff Leader. HO and
        // Visitor are never invitees (they approve / assign / own), so they have no Tab 2.
        // "registered" is for Visitor/Staff/Staff Leader (the only roles that may create);
        // "hosted" is instance-hosting Staff (in practice the Staff Leader's dedicated view).
        if (roleCode == RoleCodes.Admin ||
            (tab == TabAttending &&
                (roleCode == RoleCodes.Visitor || roleCode == RoleCodes.Ho)) ||
            (tab == TabRegistered && !(roleCode == RoleCodes.Visitor || isStaffRole)) ||
            (tab == TabHosted && !isStaffRole))
        {
            return PaginatedResult<VisitRequestManagementItemDto>.Create(
                new List<VisitRequestManagementItemDto>(), request.Page, request.PageSize, 0);
        }

        List<VisitRequestManagementItemDto> items;
        int totalItems;

        if (tab == TabAttending)
        {
            (items, totalItems) = await QueryInstanceLevelAsync(request, userId, attending: true, hostedOnly: false, cancellationToken);
        }
        else if (tab == TabRegistered)
        {
            // Read-only registrant tracking rows (one per request), any creator role.
            (items, totalItems) = await QueryRequestLevelAsync(request, userId, roleCode, cancellationToken, registeredView: true);
        }
        else if (tab == TabHosted)
        {
            (items, totalItems) = await QueryInstanceLevelAsync(request, userId, attending: false, hostedOnly: true, cancellationToken);
        }
        else if (roleCode == RoleCodes.Visitor || roleCode == RoleCodes.Ho)
        {
            // Request-level rows (one per delegation): HO monitors the whole request,
            // the Visitor CONTACT OWNER owns the whole request.
            (items, totalItems) = await QueryRequestLevelAsync(request, userId, roleCode, cancellationToken);
        }
        else
        {
            // Campus actors (Staff Leader/Staff, Dept, Student): one row per relevant instance.
            (items, totalItems) = await QueryInstanceLevelAsync(request, userId, attending: false, hostedOnly: false, cancellationToken);
        }

        var now = _clock.VietnamNow;
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
        ViewGuestDelegationListQuery request, ulong userId, bool attending, bool hostedOnly, CancellationToken ct)
    {
        var q = from c in _context.VisitRequestCampuses
                join vr in _context.VisitRequests on c.VisitRequestId equals vr.VisitRequestId
                select new { c, vr };

        if (hostedOnly)
        {
            // "Tôi là host" tab: instances the caller officially hosts, regardless of sub-role.
            q = q.Where(x => x.c.CurrentHostUserId == userId);
        }
        else if (attending)
        {
            var currentUserEmail = _currentUser.Email?.ToLower();
            // Tab 2 — "Đơn mời tham dự": instances the user was INVITED to by someone else as a
            // NON-host participant. Anything where the user is the host /
            // creator / visitor belongs in Tab 1, so it is excluded here.
            q = q.Where(x =>
                x.vr.Status != VisitRequestStatuses.Rejected &&
                x.vr.Status != VisitRequestStatuses.Cancelled &&
                x.c.Status != VisitInstanceStatus.Cancelled &&
                x.c.CurrentHostUserId != userId &&
                x.vr.CreatedBy != userId &&
                x.vr.RegistrantUserId != userId &&
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

                // Campus-independent approval: the Staff Leader sees EVERY instance of their
                // campus (single or multi) immediately after submit — no HO gate anymore.
                q = q.Where(x => x.c.CampusId == primaryCampusId);
            }
            else if (roleCode == RoleCodes.Staff)
            {
                // Regular Staff "Đơn phụ trách": ONLY instances I officially host. Requests I
                // merely REGISTERED moved to the read-only "registered" tab (actor relation).
                q = q.Where(x => x.c.CurrentHostUserId == userId);
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
            // Scope-before-keyword: q is already reduced to the staff actor's own campus/instances above.
            // Mixed per-campus v2 rows match on THIS instance's detail name — never the global projection,
            // never a hidden sibling campus's content.
            var keyword = request.Keyword.ToLower();
            q = q.Where(x =>
                ((x.vr.FormSchemaVersion >= FormSchemaVersions.PerCampus && x.vr.HasMixedCampusDetails)
                    ? (x.c.FormDetail != null && x.c.FormDetail.DelegationName.ToLower().Contains(keyword))
                    : (x.vr.DelegationName != null && x.vr.DelegationName.ToLower().Contains(keyword))) ||
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
            var nowVn = _clock.VietnamNow;
            if (timing == "UPCOMING")
                q = q.Where(x => x.c.PlannedStartAt > nowVn && x.vr.Status != VisitRequestStatuses.Rejected && x.vr.Status != VisitRequestStatuses.Cancelled && x.c.Status != VisitInstanceStatus.Cancelled && x.c.Status != VisitInstanceStatus.Closed);
            else if (timing == "ONGOING")
                q = q.Where(x => x.c.Status == VisitInstanceStatus.DuringVisit);
            else if (timing == "ENDED")
                q = q.Where(x => x.c.Status == VisitInstanceStatus.Closed);
        }

        if (!string.IsNullOrWhiteSpace(request.Relation))
        {
            // Campus-independent approval: PENDING_HOST_ASSIGNMENT no longer exists (approve
            // assigns the host in the same action) — HOST is the only instance-level relation filter.
            var rel = request.Relation.ToUpperInvariant();
            if (rel == "HOST")
            {
                q = q.Where(x => x.c.CurrentHostUserId == userId);
            }
        }

        if (request.ActionableOnly == true)
        {
            var roleCode = _currentUser.RoleCode;
            var subRole = _currentUser.SubRole;
            if (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Leader)
            {
                // Actionable for a Staff Leader = instances of my campus still waiting for MY decision.
                q = q.Where(x => x.c.Status == VisitInstanceStatus.WaitingRequestApproval
                    && x.vr.Status != VisitRequestStatuses.Cancelled);
            }
        }
        if (request.ReadOnlyOnly == true)
        {
            var roleCode = _currentUser.RoleCode;
            var subRole = _currentUser.SubRole;
            if (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Leader)
            {
                q = q.Where(x => !(x.c.Status == VisitInstanceStatus.WaitingRequestApproval
                    && x.vr.Status != VisitRequestStatuses.Cancelled));
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

                x.c.PlannedStartAt,
                x.c.PlannedEndAt,
                CampusCancelledAt = x.c.CancelledAt,
                CampusCancellationReason = x.c.CancellationReason,
                CampusCancellationActorType = x.c.CancellationActorType,
                CampusCancellationSource = x.c.CancellationSource,
                CampusCancelledBy = x.c.CancelledBy,
                x.vr.RequestCode,
                x.vr.FormSchemaVersion,
                x.vr.HasMixedCampusDetails,
                DelegationName = x.vr.FormSchemaVersion >= FormSchemaVersions.PerCampus && x.vr.HasMixedCampusDetails
                    ? (x.c.FormDetail != null ? x.c.FormDetail.DelegationName : null)
                    : x.vr.DelegationName,
                x.vr.PartnerId,
                x.vr.RegistrantOrganization,
                RequestStatus = x.vr.Status,
                x.vr.VisitScope,
                x.vr.CreatedBy,
                x.vr.VisitorUserId,
                x.vr.RegistrantUserId,
                x.vr.CreatedAt,
                x.vr.SubmittedAt,
                RequestCancelledAt = x.vr.CancelledAt,
                RequestCancellationReason = x.vr.CancellationReason,

                RequestCancelledBy = x.vr.CancelledBy,
                // Decision fields live on the campus instance now (campus-independent approval).
                x.c.DecisionNote,
                x.c.DecidedBy,
                x.c.DecidedAt,
                x.c.DecisionActorRole,
            })
            .ToListAsync(ct);

        if (page.Count == 0)
            return (new List<VisitRequestManagementItemDto>(), total);

        // â”€â”€ Enrich in memory via batched lookups (Pomelo-friendly: no projection subqueries) â”€â”€
        var instanceIds = page.Select(r => r.VisitInstanceId).Distinct().ToList();
        var requestIds = page.Select(r => r.VisitRequestId).Distinct().ToList();
        var campusIds = page.Select(r => r.CampusId).Distinct().ToList();
        var partnerIds = page.Where(r => r.PartnerId.HasValue).Select(r => r.PartnerId!.Value).Distinct().ToList();
        var userIds = page.SelectMany(r => new[] { r.CurrentHostUserId, (ulong?)r.VisitorUserId, r.CampusCancelledBy, r.RequestCancelledBy, r.DecidedBy })
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

        var nowForCancel = _clock.VietnamNow;
        var items = page.Select(r =>
        {
            string? partnerName = r.PartnerId.HasValue && partnerNames.TryGetValue(r.PartnerId.Value, out var pn) ? pn : r.RegistrantOrganization;
            bool hasCancellableInstance = (r.RequestStatus == VisitRequestStatuses.Approved
                    || r.RequestStatus == VisitRequestStatuses.PartiallyApproved)
                && (r.CampusStatus == VisitInstanceStatus.Assigned
                    || r.CampusStatus == VisitInstanceStatus.BeforeVisit)
                && r.PlannedStartAt > nowForCancel;
            bool hasStartedCampus = r.CampusStatus == VisitInstanceStatus.DuringVisit 
                || r.CampusStatus == VisitInstanceStatus.AfterVisit 
                || r.CampusStatus == VisitInstanceStatus.Closed;
            string? campusName = campusNames.TryGetValue(r.CampusId, out var cn) ? cn : null;
            string? hostName = r.CurrentHostUserId.HasValue && userNames.TryGetValue(r.CurrentHostUserId.Value, out var hn) ? hn : null;
            string? visitorName = r.VisitorUserId.HasValue && userNames.TryGetValue(r.VisitorUserId.Value, out var vn) ? vn : null;
            myParticipationRole.TryGetValue(r.VisitInstanceId, out var participantRole);

            // Instance-level cancel preferred; fall back to request-level when the whole request was cancelled.
            bool requestCancelled = r.RequestStatus == VisitRequestStatuses.Cancelled;
            bool instanceCancelled = r.CampusStatus == VisitInstanceStatus.Cancelled;
            var cancelledById = r.CampusCancelledBy ?? r.RequestCancelledBy;
            string? cancelledByName = cancelledById.HasValue && userNames.TryGetValue(cancelledById.Value, out var cbn) ? cbn : null;

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
                FormSchemaVersion = r.FormSchemaVersion,
                HasMixedCampusDetails = r.HasMixedCampusDetails,
                CampusId = r.CampusId,
                CampusName = campusName,
                CampusCount = campusCountByRequest.TryGetValue(r.VisitRequestId, out var cc2) ? cc2 : 1,
                CreatedByUserId = r.CreatedBy,
                CurrentHostUserId = r.CurrentHostUserId,
                HostName = hostName,

                CurrentUserIsHost = r.CurrentHostUserId == userId,
                VisitorUserId = r.VisitorUserId,
                RegistrantUserId = r.RegistrantUserId,
                VisitorName = visitorName,
                IsCurrentUserParticipant = participantRole != null,
                ParticipantRole = participantRole,
                ExpectedStartAt = r.PlannedStartAt,
                ExpectedEndAt = r.PlannedEndAt,
                PlannedStartAt = r.PlannedStartAt,
                PlannedEndAt = r.PlannedEndAt,
                CreatedAt = r.CreatedAt,
                SubmittedAt = r.SubmittedAt,
                IsCancelled = requestCancelled || instanceCancelled,
                CancellationLevel = requestCancelled ? "REQUEST" : (instanceCancelled ? "CAMPUS_INSTANCE" : null),
                CancelledAt = r.CampusCancelledAt ?? r.RequestCancelledAt,
                CancellationReason = r.CampusCancellationReason ?? r.RequestCancellationReason,
                CancellationActorType = r.CampusCancellationActorType,
                CancellationSource = r.CampusCancellationSource,
                CancelledBy = cancelledById,
                CancelledByName = cancelledByName,
                HasCancellableInstance = hasCancellableInstance,
                HasStartedCampus = hasStartedCampus,
                DecisionNote = r.DecisionNote,
                DecidedBy = r.DecidedBy,
                DecidedByName = r.DecidedBy.HasValue && userNames.TryGetValue(r.DecidedBy.Value, out var dbn) ? dbn : null,
                DecidedAt = r.DecidedAt,
                DecisionActorRole = r.DecisionActorRole,
            };
        }).ToList();

        return (items, total);
    }

    // ── Request-level: responsible tab for Visitor & HO, and the read-only REGISTERED tab
    // (registeredView: rows where the caller is the registrant but NOT the contact owner) ──
    private async Task<(List<VisitRequestManagementItemDto> Items, int Total)> QueryRequestLevelAsync(
        ViewGuestDelegationListQuery request, ulong userId, string? roleCode, CancellationToken ct,
        bool registeredView = false)
    {
        var q = _context.VisitRequests.AsQueryable();

        if (registeredView)
        {
            // "Tôi là người đăng ký / Đơn tôi đăng ký": strictly the registrant relation.
            // A Visitor who is BOTH registrant and contact owner sees the request only on
            // their owner tab — never duplicated here.
            q = q.Where(vr => vr.RegistrantUserId == userId
                && (vr.VisitorUserId == null || vr.VisitorUserId != userId));
        }
        // Visitor "Tôi là đầu mối": CONTACT-OWNER rows only. Rows where the Visitor merely
        // registered for someone else live on the "registered" tab (actor relation). Legacy
        // rows without an owner fall back to created_by.
        else if (roleCode == RoleCodes.Visitor)
            q = q.Where(vr => vr.VisitorUserId == userId
                || (vr.VisitorUserId == null && vr.CreatedBy == userId));
        // HO sees every MULTI_CAMPUS request (they decide it) AND every SINGLE_CAMPUS request
        // in read-only monitoring mode (business rule chốt 2026-06: HO theo dõi SINGLE_CAMPUS).
        // No filter is applied for HO here â€” read-only is enforced via AllowedActions (the HO
        // action builder only grants HO_APPROVE/HO_REJECT to MULTI_CAMPUS pending requests).
        // else if (roleCode == RoleCodes.Ho)  â†’ all requests visible.

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            // Visitor tabs: the actor is the registrant/contact, so EVERY campus of their own request is
            // in scope — a mixed v2 request matches when ANY of its per-campus details matches (the
            // global projection is never business content for mixed requests).
            var kw = request.Keyword.ToLower();
            q = q.Where(vr =>
                ((vr.FormSchemaVersion >= FormSchemaVersions.PerCampus && vr.HasMixedCampusDetails)
                    ? vr.CampusInstances.Any(ci => ci.FormDetail != null
                        && ci.FormDetail.DelegationName.ToLower().Contains(kw))
                    : (vr.DelegationName != null && vr.DelegationName.ToLower().Contains(kw))) ||
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
            var nowVn = _clock.VietnamNow;
            if (timing == "UPCOMING")
                q = q.Where(vr => vr.CampusInstances.Any(i => i.PlannedStartAt > nowVn) && vr.Status != VisitRequestStatuses.Rejected && vr.Status != VisitRequestStatuses.Cancelled && !vr.CampusInstances.All(i => i.Status == VisitInstanceStatus.Cancelled || i.Status == VisitInstanceStatus.Closed));
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

        // Campus-independent approval: HO never has actionable rows (monitor/read-only only).
        if (request.ActionableOnly == true && roleCode == RoleCodes.Ho)
        {
            q = q.Where(vr => false);
        }
        // ReadOnlyOnly is a no-op for HO — every row is read-only for HO now.

        var total = await q.CountAsync(ct);

        // Load the page with instances + partner, then shape request-level rows in memory
        // (avoids fragile conditional-subquery projection translation).
        var pageQuery = q;
        // Aggregate the sort key as a NULLABLE DateTime: a request can have zero campus
        // instances (or EF treats the navigation as possibly-empty), in which case Min/Max
        // over a non-nullable DateTime throws at runtime. The (DateTime?) cast lets the
        // empty case resolve to NULL instead of blowing up the whole query (was the cause
        // of the 500 on the HO "Quản lý tiếp khách" list).
        if (request.SortBy?.ToLower() == "plannedstartat")
        {
            if (request.SortOrder?.ToLower() == "asc")
                pageQuery = pageQuery.OrderBy(vr => vr.CampusInstances.Min(i => (DateTime?)i.PlannedStartAt)).ThenBy(vr => vr.VisitRequestId);
            else
                pageQuery = pageQuery.OrderByDescending(vr => vr.CampusInstances.Max(i => (DateTime?)i.PlannedStartAt)).ThenByDescending(vr => vr.VisitRequestId);
        }
        else if (request.SortOrder?.ToLower() == "asc")
            pageQuery = pageQuery.OrderBy(vr => vr.CampusInstances.Min(i => (DateTime?)i.PlannedStartAt)).ThenBy(vr => vr.VisitRequestId);
        else if (request.SortOrder?.ToLower() == "desc")
            pageQuery = pageQuery.OrderByDescending(vr => vr.CampusInstances.Max(i => (DateTime?)i.PlannedStartAt)).ThenByDescending(vr => vr.VisitRequestId);
        else
            pageQuery = pageQuery.OrderByDescending(vr => vr.CreatedAt).ThenByDescending(vr => vr.VisitRequestId);

        var requests = await pageQuery
            .Include(vr => vr.Partner)
            .Include(vr => vr.CampusInstances)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        // Batch-resolve campus (name + code) + host/decider/canceller display names. We resolve
        // EVERY instance's campus & host (not just single-campus rows) so the expandable accordion
        // has all it needs without an N+1 query — campus instances are already Include()d above.
        var campusIds = requests
            .SelectMany(vr => vr.CampusInstances.Select(i => i.CampusId))
            .Distinct().ToList();
        var userIds = requests
            .SelectMany(vr => vr.CampusInstances.Select(i => i.CurrentHostUserId)
                .Concat(vr.CampusInstances.Where(i => i.Status == VisitInstanceStatus.Cancelled).Select(i => i.CancelledBy))
                .Concat(vr.CampusInstances.Select(i => i.DecidedBy))
                .Append((ulong?)vr.VisitorUserId)
                .Append(vr.LastResubmittedBy)
                .Append(vr.CancelledBy))
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var campusRows = campusIds.Count == 0
            ? new List<(ulong CampusId, string Name, string Code)>()
            : (await _context.Campuses.Where(cc => campusIds.Contains(cc.CampusId))
                    .Select(cc => new { cc.CampusId, cc.Name, cc.CampusCode })
                    .ToListAsync(ct))
                .Select(cc => (CampusId: cc.CampusId, Name: cc.Name, Code: cc.CampusCode)).ToList();
        var campusNames = campusRows.ToDictionary(c => c.CampusId, c => c.Name);
        var campusCodes = campusRows.ToDictionary(c => c.CampusId, c => c.Code);
        var userNames = userIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _context.Users.Where(u => userIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        var nowForCancel = _clock.VietnamNow;
        // planned_start_at is a LOCAL wall-clock DATETIME → the 24h edit window must be
        // computed against VietnamNow (UtcNow would shift the window by 7 hours).
        var vnNow = _clock.VietnamNow;
        var items = requests.Select(vr =>
        {
            var instances = vr.CampusInstances;
            var count = instances.Count;
            var single = count == 1 ? instances.First() : null;

            // ── Visitor edit / resubmit eligibility (spec "sửa đơn / gửi lại sau reject").
            // NEVER on the registered view — the registrant relation is strictly read-only. ──
            bool canEditPending = !registeredView
                && vr.Status == VisitRequestStatuses.PendingApproval
                && count > 0
                && instances.All(i => i.Status == VisitInstanceStatus.WaitingRequestApproval)
                && instances.Min(i => i.PlannedStartAt) >= vnNow.AddHours(24);
            bool canResubmit = !registeredView
                && vr.Status == VisitRequestStatuses.Rejected
                && count > 0
                && instances.All(i => i.Status == VisitInstanceStatus.Rejected);

            // Cancel-eligibility (UC-136): REQUEST level.
            // Rule 1: Visitor can cancel the whole request only if ALL active campuses are cancellable
            // (i.e. status is Waiting/Assigned/BeforeVisit AND >= 24h).
            var activeInstances = instances.Where(i => i.Status != VisitInstanceStatus.Cancelled && i.Status != VisitInstanceStatus.Rejected).ToList();
            bool hasStartedCampus = activeInstances.Any(i => i.Status == VisitInstanceStatus.DuringVisit || i.Status == VisitInstanceStatus.AfterVisit || i.Status == VisitInstanceStatus.Closed);
            
            bool hasCancellableInstance = !registeredView
                && activeInstances.Any()
                && !hasStartedCampus
                && activeInstances.All(i =>
                    (i.Status == VisitInstanceStatus.WaitingRequestApproval
                        || i.Status == VisitInstanceStatus.Assigned
                        || i.Status == VisitInstanceStatus.BeforeVisit)
                    && i.PlannedStartAt >= vnNow.AddHours(24));

            string? campusName = single != null && campusNames.TryGetValue(single.CampusId, out var cnm) ? cnm
                : count > 1 ? $"{count} cơ sở"
                : null;
            ulong? hostUserId = single?.CurrentHostUserId;
            string? hostName = hostUserId.HasValue && userNames.TryGetValue(hostUserId.Value, out var hnm) ? hnm : null;
            string? visitorName = vr.VisitorUserId.HasValue && userNames.TryGetValue(vr.VisitorUserId.Value, out var vnm) ? vnm : null;
            DateTime? minStart = count > 0 ? instances.Min(i => i.PlannedStartAt) : (DateTime?)null;
            DateTime? maxEnd = count > 0 ? instances.Max(i => i.PlannedEndAt) : (DateTime?)null;

            // Cancellation: whole-request cancel ⇒ REQUEST level (actor/source borrowed from a
            // cancelled instance, since visit_requests has no actor_type/source columns);
            // otherwise an instance-level cancel while the request is still active.
            bool requestCancelled = vr.Status == VisitRequestStatuses.Cancelled;
            var cancelledInstance = instances
                .Where(i => i.Status == VisitInstanceStatus.Cancelled)
                .OrderByDescending(i => i.CancelledAt)
                .FirstOrDefault();
            bool isCancelled = requestCancelled || cancelledInstance != null;
            string? cancellationLevel = requestCancelled ? "REQUEST" : (cancelledInstance != null ? "CAMPUS_INSTANCE" : null);
            ulong? cancelledById = requestCancelled
                ? (vr.CancelledBy ?? cancelledInstance?.CancelledBy)
                : cancelledInstance?.CancelledBy;
            string? cancelledByName = cancelledById.HasValue && userNames.TryGetValue(cancelledById.Value, out var cbn2) ? cbn2 : null;
            DateTime? cancelledAt = requestCancelled ? (vr.CancelledAt ?? cancelledInstance?.CancelledAt) : cancelledInstance?.CancelledAt;
            string? cancellationReason = requestCancelled ? (vr.CancellationReason ?? cancelledInstance?.CancellationReason) : cancelledInstance?.CancellationReason;
            string? cancellationActorType = cancelledInstance?.CancellationActorType;
            string? cancellationSource = cancelledInstance?.CancellationSource;

            // ── Multi-campus expandable accordion (Phương án A). One progress row per campus
            // instance, with backend-computed action booleans. Only the Visitor owner may cancel,
            // and only when the request is APPROVED and the instance is still cancellable. ──
            bool isVisitor = roleCode == RoleCodes.Visitor;
            bool isVisitorOwner = !registeredView && isVisitor && (vr.VisitorUserId == userId || (vr.VisitorUserId == null && vr.CreatedBy == userId));
            var campusProgressItems = instances
                .OrderBy(i => i.PlannedStartAt)
                .Select(i =>
                {
                    bool instanceCancellable = (vr.Status == VisitRequestStatuses.Approved
                            || vr.Status == VisitRequestStatuses.PartiallyApproved)
                        && (i.Status == VisitInstanceStatus.WaitingRequestApproval
                            || i.Status == VisitInstanceStatus.Assigned
                            || i.Status == VisitInstanceStatus.BeforeVisit)
                        && i.PlannedStartAt >= vnNow.AddHours(24);
                    return new CampusProgressItemDto
                    {
                        VisitInstanceId = i.VisitInstanceId,
                        CampusId = i.CampusId,
                        CampusCode = campusCodes.TryGetValue(i.CampusId, out var ccode) ? ccode : null,
                        CampusName = campusNames.TryGetValue(i.CampusId, out var cnm2) ? cnm2 : null,
                        PlannedStartAt = i.PlannedStartAt,
                        PlannedEndAt = i.PlannedEndAt,
                        InstanceStatus = i.Status,
                        HostUserId = i.CurrentHostUserId,
                        HostName = i.CurrentHostUserId.HasValue && userNames.TryGetValue(i.CurrentHostUserId.Value, out var ihn) ? ihn : null,
                        DecisionNote = i.DecisionNote,
                        DecidedBy = i.DecidedBy,
                        DecidedByName = i.DecidedBy.HasValue && userNames.TryGetValue(i.DecidedBy.Value, out var idbn) ? idbn : null,
                        DecidedAt = i.DecidedAt,
                        CancellationReason = i.CancellationReason,
                        CancelledBy = i.CancelledBy,
                        CancelledByName = i.CancelledBy.HasValue && userNames.TryGetValue(i.CancelledBy.Value, out var icbn) ? icbn : null,
                        CancelledAt = i.CancelledAt,
                        CancellationActorType = i.CancellationActorType,
                        CancellationSource = i.CancellationSource,
                        CanViewCampusDetail = true,
                        CanCancelCampusVisit = isVisitorOwner && instanceCancellable,
                        CanViewCancelReason = i.Status == VisitInstanceStatus.Cancelled && !string.IsNullOrEmpty(i.CancellationReason),
                        CanViewRejectReason = i.Status == VisitInstanceStatus.Rejected && !string.IsNullOrEmpty(i.DecisionNote),
                    };
                }).ToList();

            // "Đồng thời là host" badge (registered view only): the registrant Staff also
            // officially hosts ≥1 instance; actions for that stay on the hosted tab.
            var alsoHostedInstance = registeredView
                ? instances.FirstOrDefault(i => i.CurrentHostUserId == userId)
                : null;

            return new VisitRequestManagementItemDto
            {
                VisitRequestId = vr.VisitRequestId,
                VisitInstanceId = single?.VisitInstanceId,
                RequestCode = vr.RequestCode,
                // A request-level row cannot represent a MIXED v2 request with one name — the projection
                // (smallest campus) is never shown as business content; the row is explicitly labeled and
                // the per-campus names live in the campus progress items/detail view (plan §8.3).
                DelegationName = vr.FormSchemaVersion >= FormSchemaVersions.PerCampus && vr.HasMixedCampusDetails
                    ? "Khác nhau theo cơ sở"
                    : vr.DelegationName,
                PartnerName = vr.Partner != null ? vr.Partner.Name : vr.RegistrantOrganization,
                RequestStatus = vr.Status,
                CampusStatus = single?.Status,
                VisitScope = vr.VisitScope,
                FormSchemaVersion = vr.FormSchemaVersion,
                HasMixedCampusDetails = vr.HasMixedCampusDetails,
                CampusId = single?.CampusId,
                CampusName = campusName,
                CampusCount = count,
                CreatedByUserId = vr.CreatedBy,
                CurrentHostUserId = hostUserId,
                HostName = hostName,
                RegistrantUserId = vr.RegistrantUserId,
                IsAlsoHost = alsoHostedInstance != null,
                AlsoHostVisitInstanceId = alsoHostedInstance?.VisitInstanceId,

                CurrentUserIsHost = false,
                VisitorUserId = vr.VisitorUserId,
                VisitorName = visitorName,
                IsCurrentUserParticipant = false,
                ParticipantRole = null,
                ExpectedStartAt = minStart,
                ExpectedEndAt = maxEnd,
                PlannedStartAt = minStart,
                PlannedEndAt = maxEnd,
                CreatedAt = vr.CreatedAt,
                SubmittedAt = vr.SubmittedAt,
                IsCancelled = isCancelled,
                CancellationLevel = cancellationLevel,
                CancelledAt = cancelledAt,
                CancellationReason = cancellationReason,
                CancellationActorType = cancellationActorType,
                CancellationSource = cancellationSource,
                CancelledBy = cancelledById,
                CancelledByName = cancelledByName,
                HasCancellableInstance = hasCancellableInstance,
                HasStartedCampus = hasStartedCampus,
                ResubmissionCount = (int)vr.ResubmissionCount,
                LastResubmittedAt = vr.LastResubmittedAt,
                LastResubmittedBy = vr.LastResubmittedBy,
                LastResubmittedByName = vr.LastResubmittedBy is { } lrb && userNames.TryGetValue(lrb, out var lrbn) ? lrbn : null,
                CanEditPending = canEditPending,
                CanResubmit = canResubmit,
                // Per-campus progress is meaningful for ANY multi-campus status now (each campus
                // is decided independently — pending/assigned/rejected can coexist).
                CanExpandCampuses = count > 1,
                CanViewRequestDetail = true,
                CanViewRejectReason = instances.Any(i => i.Status == VisitInstanceStatus.Rejected && !string.IsNullOrEmpty(i.DecisionNote)),
                CanViewCancelReason = isCancelled,
                CampusProgressItems = campusProgressItems,
                // Request-level decision info = the single instance's decision (decision fields
                // moved to visit_request_campuses); multi-campus rows expose them per campus
                // via CampusProgressItems instead.
                DecisionNote = single?.DecisionNote,
                DecidedBy = single?.DecidedBy,
                DecidedByName = single?.DecidedBy is { } sdb && userNames.TryGetValue(sdb, out var dbn2) ? dbn2 : null,
                DecidedAt = single?.DecidedAt,
                DecisionActorRole = single?.DecisionActorRole,
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
        if (tab == TabRegistered)
            return actions; // registrant relation is STRICTLY read-only — never owner/host actions

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
        bool requestActive = item.RequestStatus != VisitRequestStatuses.Cancelled;
        bool isVisitorOwner = isVisitor && (item.VisitorUserId == userId || (item.VisitorUserId == null && item.CreatedByUserId == userId));

        // HO never approves/rejects anymore (campus-independent approval) — monitor/read-only.

        // Staff Leader — decides their own campus instance regardless of scope: approve
        // (must pick host in the same action) or reject, only while it awaits their decision.
        if (isStaffLeader && sameCampus && requestActive
            && item.CampusStatus == VisitInstanceStatus.WaitingRequestApproval)
        {
            actions.Add("APPROVE_AND_ASSIGN_HOST"); // duyệt & gán host (opens host picker)
            actions.Add("CAMPUS_REJECT");
        }

        // Visitor — edit a still-fully-pending request / resubmit a fully-rejected one.
        // Eligibility (status + 24h window) is precomputed per row in QueryRequestLevelAsync;
        // the commands re-validate everything server-side.
        if (isVisitorOwner)
        {
            if (item.CanEditPending)
                actions.Add("EDIT_PENDING_REQUEST");
            if (item.CanResubmit)
                actions.Add("RESUBMIT_REJECTED_REQUEST");
        }

        // Visitor — self-cancel own request (UC-136).
        if (isVisitorOwner)
        {
            if (item.RequestStatus == VisitRequestStatuses.PendingApproval)
            {
                if (item.HasCancellableInstance)
                {
                    actions.Add("CANCEL_BY_VISITOR");
                }
            }
            else if (item.RequestStatus == VisitRequestStatuses.Approved
                     || item.RequestStatus == VisitRequestStatuses.PartiallyApproved)
            {
                if (item.HasCancellableInstance)
                {
                    actions.Add("CANCEL_BY_VISITOR");
                }
            }
        }

        // Host — cancel the campus instance they own before it starts.
        if (!isStaffLeader && item.CurrentUserIsHost
            && (item.CampusStatus == VisitInstanceStatus.Assigned || item.CampusStatus == VisitInstanceStatus.BeforeVisit)
            && beforeStart)
        {
            actions.Add("CANCEL_BY_HOST");
        }

        // Navigation Actions — driven by the campus instance lifecycle (never the request
        // aggregate: a PARTIALLY_APPROVED request already has live instances).
        bool instanceOperational = item.CampusStatus == VisitInstanceStatus.Assigned
            || item.CampusStatus == VisitInstanceStatus.BeforeVisit
            || item.CampusStatus == VisitInstanceStatus.DuringVisit
            || item.CampusStatus == VisitInstanceStatus.AfterVisit
            || item.CampusStatus == VisitInstanceStatus.Closed;
        if (instanceOperational && requestActive)
        {
            if (item.CurrentUserIsHost)
            {
                actions.Add("OPEN_HOST_PROCESS");
            }
            if (item.CampusId != null && (isHo || (isStaffLeader && sameCampus)))
            {
                actions.Add("OPEN_PROCESS_SUMMARY");
            }
            if (isVisitor && item.VisitorUserId == userId)
            {
                actions.Add("VIEW_RECEPTION_DETAIL");
            }
            if (tab == TabAttending)
            {
                actions.Add("OPEN_CONTRIBUTION");
            }
        }


        return actions;
    }

    /// <summary>Which tab/section a row belongs to, for the frontend's convenience.</summary>
    private static string ResolveTabType(string tab, string? roleCode)
    {
        if (tab == TabAttending) return "INVITED";
        if (tab == TabRegistered) return "REGISTERED";
        if (tab == TabHosted) return "HOSTED";
        if (roleCode == RoleCodes.Visitor) return "MY_REQUESTS";
        return "RESPONSIBLE";
    }

    /// <summary>
    /// Best-effort relation of the caller to a row (display/telemetry only â€” never an
    /// authorization input; every action is still re-validated server-side).
    /// </summary>
    private string ResolveRelation(VisitRequestManagementItemDto item, string tab, string? roleCode, bool isStaffLeader)
    {
        if (tab == TabRegistered)
            return "REGISTRANT_VIEWER";
        if (tab == TabHosted)
            return "HOST";
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
            return "HOST";
        if (roleCode == RoleCodes.Visitor)
            return "VISITOR_OWNER";
        if (roleCode == RoleCodes.Ho)
            // Campus-independent approval: HO never decides — always monitoring.
            return "HO_MONITOR";
        if (isStaffLeader)
            return "CAMPUS_APPROVER";
        if (roleCode == RoleCodes.Department || roleCode == RoleCodes.Student)
            return "DEPARTMENT_TASK_OWNER";
        return "NONE";
    }
}
