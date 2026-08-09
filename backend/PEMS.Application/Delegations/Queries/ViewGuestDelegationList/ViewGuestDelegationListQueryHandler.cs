using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Policies;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

/// <summary>
/// UC-20 View Guest Delegation List. Returns rows already filtered to the caller's
/// responsibility scope (the backend is the single authority — the frontend never
/// post-filters by role).
///
/// <para>
/// THREE THINGS ARE KEPT APART HERE, and conflating any two of them is the bug this handler has to
/// keep not having:
/// </para>
/// <list type="bullet">
///   <item><b>FILTER</b> — why a row is on screen. A tab is a POPULATION, nothing more.</item>
///   <item><b>AUTHORIZATION</b> — what the caller may do, computed from the relations they genuinely
///     hold (<see cref="BuildRelationContexts"/>) and then narrowed by the lifecycle. A tab is never
///     an input.</item>
///   <item><b>ENTRY CONTEXT</b> — which screen the row opens
///     (<see cref="VisitRequestManagementItemDto.PrimaryEntryContext"/>). This is the one thing a
///     filter may change, and changing it changes no rights at all.</item>
/// </list>
///
/// Tabs (the populations):
///   • "responsible": Visitor = CONTACT-OWNER rows (one per request); HO = monitor (one per
///     request); campus actors (Staff Leader/Staff, Dept, Student) = one row per relevant
///     campus instance (regular Staff = instances they officially HOST).
///   • "attending" (Đơn mời tham dự): requests the user was invited to as a non-host
///     participant, any response status (INVITED/ACCEPTED/DECLINED) — same population as the
///     dedicated "Lời mời tham dự" tab (GetVisitInvitationsQueryHandler).
///   • "registered" (Đơn tôi đăng ký / Tôi là người đăng ký): requests where the caller is
///     the REGISTRANT (registrant_user_id). Just that — a person who is also the contact, the
///     Host or the campus reviewer appears here too, and keeps every one of those rights.
///   • "hosted" (Tôi là host): instance rows the caller officially hosts (Staff Leader's
///     dedicated host view).
///   • "all": every population above, merged to ONE ROW PER REQUEST carrying all of its relations.
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
    // REGISTRANT (registrant_user_id). Available to Visitor, regular Staff and Staff Leader.
    // It is a POPULATION, not a permission level: someone who is registrant AND contact owner
    // appears on both filters and holds both sets of rights on either one.
    private const string TabRegistered = "registered";
    // "hosted" (Tôi là host): instance-level rows the caller officially hosts. Gives the Staff
    // Leader a dedicated host view separate from the campus-review tab.
    private const string TabHosted = "hosted";
    // "all" (Tất cả các loại đơn): Staff (Leader or regular) and Visitor — the roles with more
    // than one relationship tab worth merging (HO/Dept/Student only ever have one tab, so "all"
    // would be identical to it). See QueryAllMergedAsync for what merges for which role.
    private const string TabAll = "all";
    // Cap on how many rows each source query may contribute to a merged "all" list before the
    // in-memory sort/paginate step. A real campus's total live+recent order volume is nowhere near
    // this; it exists so a merge can never turn into an unbounded fetch.
    private const int MergeFetchCap = 1000;

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
            TabAll        => TabAll,
            _             => "responsible",
        };

        // Admin does not take part in the reception flow (also has no UC-20 grant).
        // The "Đơn mời tham dự" (attending) tab is ONLY for users who can be invited as a
        // non-host participant: regular Staff, Dept, Student, and Staff Leader. HO and
        // Visitor are never invitees (they approve / assign / own), so they have no Tab 2.
        // "registered" is for Visitor/Staff/Staff Leader (the only roles that may create);
        // "hosted" is instance-hosting Staff (in practice the Staff Leader's dedicated view).
        // "all" is Staff (either sub-role) and Visitor — see the TabAll doc comment.
        if (roleCode == RoleCodes.Admin ||
            (tab == TabAttending &&
                (roleCode == RoleCodes.Visitor || roleCode == RoleCodes.Ho)) ||
            (tab == TabRegistered && !(roleCode == RoleCodes.Visitor || isStaffRole)) ||
            (tab == TabHosted && !isStaffRole) ||
            (tab == TabAll && !(isStaffRole || roleCode == RoleCodes.Visitor)))
        {
            return PaginatedResult<VisitRequestManagementItemDto>.Create(
                new List<VisitRequestManagementItemDto>(), request.Page, request.PageSize, 0);
        }

        List<VisitRequestManagementItemDto> items;
        int totalItems;
        // Only populated for tab == TabAll: each merged row remembers which source won it, which the
        // enrichment below uses for the DISPLAY tab type and for the default entry context. It is not
        // consulted for authorization — that comes from the row's relations.
        Dictionary<VisitRequestManagementItemDto, string>? mergedTabByItem = null;

        if (tab == TabAll)
        {
            (items, totalItems, mergedTabByItem) = await QueryAllMergedAsync(request, userId, roleCode, cancellationToken);
        }
        else if (tab == TabAttending)
        {
            (items, totalItems) = await QueryInstanceLevelAsync(request, userId, attending: true, hostedOnly: false, cancellationToken);
        }
        else if (tab == TabRegistered)
        {
            // Requests this caller registered (one row per request), any creator role. What they may
            // DO with them is decided further down, from their relations — not from being here.
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
        var leaderCampusId = isStaffLeader ? _currentUser.PrimaryCampusId : null;
        // One internal account works at ONE campus (business rule), so a staff account's CAMPUS-scoped
        // relations — Host, campus reviewer — are only ever considered at that campus: holding DN can
        // never answer a question about HN. Null for Visitor/HO, who are not campus-bound.
        var ownCampusId = isStaffRole ? _currentUser.PrimaryCampusId : null;

        foreach (var item in items)
        {
            var itemTab = mergedTabByItem != null && mergedTabByItem.TryGetValue(item, out var originTab) ? originTab : tab;

            // ── Relations FIRST. Everything below reads authorization off what the caller genuinely IS
            //    to this row (registrant id, this campus's contact/host, their own campus, their
            //    participation) — never off which filter produced the row. ──
            var contexts = BuildRelationContexts(item, userId, ownCampusId, isStaffRole, isStaffLeader);
            item.RelationContexts = contexts;
            item.Relations = contexts.Select(c => c.Relation).Distinct().ToList();
            // A request-level row has no single host column to read, so the flag is completed from the
            // relation set — otherwise a registrant who also hosts their own single-campus visit read
            // as "not the host" on the very tab that lists their request.
            if (!item.CurrentUserIsHost && item.VisitInstanceId.HasValue)
                item.CurrentUserIsHost = contexts.Any(c => c.Relation == VisitRowRelations.Host
                    && c.VisitInstanceId == item.VisitInstanceId);

            item.AllowedActions = BuildAllowedActions(item, userId, now, contexts);
            item.Capabilities = BuildRowCapabilities(item, leaderCampusId, now);
            // The flat list stays the ENABLED subset, so a button can never appear for a verdict that
            // refused it. Actions with no verdict (navigation, cancel, the approval decision) keep
            // their existing booleans and are already in the list.
            foreach (var capability in item.Capabilities)
                if (capability.Enabled && !item.AllowedActions.Contains(capability.Code))
                    item.AllowedActions.Add(capability.Code);

            AttachCampusCapabilities(item, leaderCampusId, now);

            // Entry context is NAVIGATION, decided after (and separately from) authorization: the same
            // rights open a different default screen depending on which relation the reader is looking
            // through — which is exactly what a filter is allowed to change, and all it may change.
            var (entryContext, entryInstanceId) = ResolvePrimaryEntry(item, itemTab, contexts);
            item.PrimaryEntryContext = entryContext;
            item.PrimaryEntryVisitInstanceId = entryInstanceId;

            item.TabType = ResolveTabType(itemTab, roleCode);
            item.CurrentUserRelation = ResolveRelation(item, itemTab, roleCode, isStaffLeader);
            item.RelationLabel = VisitRowLabels.Relation(item.CurrentUserRelation);
            item.StatusLabel = VisitRowLabels.Status(item.RequestStatus, item.CampusStatus, roleCode);
            // Multi-campus SUMMARY row (no single instance of its own): visit_requests.status only
            // tracks the approval aggregate, so a request stuck at "Đã duyệt" forever even after
            // every campus finished was stale data. Re-derive from the campus instances themselves.
            if (item.CampusStatus is null && item.RequestStatus == VisitRequestStatuses.Approved
                && item.CampusProgressItems.Count > 0)
            {
                var progressLabel = VisitRowLabels.MultiCampusProgress(
                    item.CampusProgressItems.Select(cp => (string?)cp.InstanceStatus), roleCode);
                if (progressLabel is not null) item.StatusLabel = progressLabel;
            }
            // Read-only when no mutating action is available (only VIEW_DETAIL, or none).
            item.IsReadOnly = !item.AllowedActions.Any(a => a != VisitListActions.ViewDetail);
        }

        await AttachInstanceChangeSummariesAsync(items, userId, cancellationToken);
        await AttachNextTasksAsync(items, userId, leaderCampusId, now, cancellationToken);

        return PaginatedResult<VisitRequestManagementItemDto>.Create(items, request.Page, request.PageSize, totalItems);
    }

    /// <summary>
    /// "all" tab (Tất cả các loại đơn). What merges depends on the caller's role, because each
    /// role's "responsible" tab is a different shape:
    ///
    ///   • Staff (Leader or regular) — responsible (instance-level: every campus instance the
    ///     Leader's own campus has, or every instance a regular Staff hosts) + attending
    ///     (instance-level) + registered (request-level). "hosted" is deliberately NOT a 4th
    ///     source: a Leader's hosted instances are always at their own campus, so they are
    ///     already inside "responsible"; for a regular Staff "responsible" IS the hosted-only
    ///     view already (see QueryInstanceLevelAsync's role branch) — either way querying
    ///     "hosted" again would just re-add the same rows.
    ///   • Visitor — responsible (request-level: rows they are the CONTACT OWNER of) +
    ///     registered (request-level: rows they registered). The two OVERLAP on purpose — one person
    ///     is very often both — so the same request arrives twice and the grouping below is what
    ///     makes it one row. Visitor has no "attending" tab at all.
    ///
    /// ONE REAL-WORLD REQUEST PRODUCES EXACTLY ONE ROW, and that row keeps every relation the caller
    /// holds on it. The old shape dropped a later candidate outright once its request/instance id had
    /// been seen, which silently destroyed the very thing this list exists to show: a Staff Leader who
    /// had also registered the visit lost the registrant half (no edit, no resubmit, no accordion) purely
    /// because the campus source happened to be queried first. Here the candidates are GROUPED, the most
    /// urgent one becomes the row, and the rest are folded into it.
    ///
    /// Each source is fetched unpaginated (capped at <see cref="MergeFetchCap"/>) under the SAME
    /// filters as the caller asked for, then merged, sorted and paginated in memory — there is no
    /// single SQL query that can UNION a request-level aggregate with an instance-level shape and
    /// still paginate correctly.
    /// </summary>
    private async Task<(List<VisitRequestManagementItemDto> Items, int Total, Dictionary<VisitRequestManagementItemDto, string> TabByItem)>
        QueryAllMergedAsync(ViewGuestDelegationListQuery request, ulong userId, string? roleCode, CancellationToken ct)
    {
        var fetchAll = CloneForMerge(request, MergeFetchCap);
        var isStaffLeader = roleCode == RoleCodes.Staff
            && string.Equals(_currentUser.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase);
        var ownCampusId = roleCode == RoleCodes.Staff ? _currentUser.PrimaryCampusId : null;

        var candidates = new List<(VisitRequestManagementItemDto Item, string Tab, int SourceOrder)>();
        void AddSource(List<VisitRequestManagementItemDto> items, string originTab, int order)
        {
            foreach (var item in items) candidates.Add((item, originTab, order));
        }

        if (roleCode == RoleCodes.Visitor)
        {
            var (responsibleItems, _) = await QueryRequestLevelAsync(fetchAll, userId, roleCode, ct);
            var (registeredItems, _) = await QueryRequestLevelAsync(fetchAll, userId, roleCode, ct, registeredView: true);
            AddSource(responsibleItems, "responsible", 0);
            AddSource(registeredItems, TabRegistered, 1);
        }
        else
        {
            var (responsibleItems, _) = await QueryInstanceLevelAsync(fetchAll, userId, attending: false, hostedOnly: false, ct);
            var (attendingItems, _) = await QueryInstanceLevelAsync(fetchAll, userId, attending: true, hostedOnly: false, ct);
            var (registeredItems, _) = await QueryRequestLevelAsync(fetchAll, userId, roleCode, ct, registeredView: true);
            AddSource(responsibleItems, "responsible", 0);
            AddSource(attendingItems, TabAttending, 1);
            AddSource(registeredItems, TabRegistered, 2);
        }

        var tabByItem = new Dictionary<VisitRequestManagementItemDto, string>();
        var merged = new List<VisitRequestManagementItemDto>();

        foreach (var group in candidates.GroupBy(c => c.Item.VisitRequestId))
        {
            var ordered = group
                .OrderBy(c => CandidateRank(c.Item, c.Tab, userId, ownCampusId, isStaffLeader))
                .ThenBy(c => c.SourceOrder)
                .ToList();

            var primary = ordered[0];
            foreach (var other in ordered.Skip(1))
                MergeCandidateInto(primary.Item, other.Item);

            merged.Add(primary.Item);
            tabByItem[primary.Item] = primary.Tab;
        }

        var sorted = request.SortOrder?.ToLower() == "asc"
            ? merged.OrderBy(i => i.PlannedStartAt ?? DateTime.MaxValue).ThenBy(i => i.VisitRequestId).ToList()
            : merged.OrderByDescending(i => i.PlannedStartAt ?? DateTime.MinValue).ThenByDescending(i => i.VisitRequestId).ToList();

        var total = sorted.Count;
        var paged = sorted.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        return (paged, total, tabByItem);
    }

    /// <summary>
    /// Which of a request's candidate rows should BE the row on the merged list — the most urgent job
    /// the caller has on it. Deliberately cheap: it reads the same facts the full relation pass reads
    /// later, but has to run before enrichment in order to choose what to enrich.
    /// </summary>
    private static int CandidateRank(
        VisitRequestManagementItemDto item, string tab, ulong userId, ulong? ownCampusId, bool isStaffLeader)
    {
        var requestActive = item.RequestStatus != VisitRequestStatuses.Cancelled;
        var ownCampus = ownCampusId.HasValue && item.CampusId == ownCampusId.Value;

        if (isStaffLeader && ownCampus && requestActive
            && item.CampusStatus == VisitInstanceStatus.WaitingRequestApproval)
            return VisitRelationPriority.CampusReviewRequired;

        if (item.CurrentUserIsHost && (!ownCampusId.HasValue || ownCampus) && requestActive
            && IsInstanceOperational(item.CampusStatus) && item.CampusStatus != VisitInstanceStatus.Closed)
            return VisitRelationPriority.HostProcessRequired;

        if (tab == TabAttending) return VisitRelationPriority.InvitationAction;

        if (item.RegistrantUserId == userId && (item.CanEditPending || item.CanResubmit))
            return VisitRelationPriority.RegistrantAction;

        return VisitRelationPriority.Tracking;
    }

    /// <summary>
    /// Folds a losing candidate's REQUEST-LEVEL knowledge into the row that will be rendered.
    ///
    /// <para>
    /// Only the request-level source knows the request-wide facts — whether the whole request is still
    /// editable, whether it can be resubmitted, and what every campus of it is doing. An instance-level
    /// row deliberately omits all of that, because a campus actor must not be shown a sibling campus.
    /// The registrant is the one caller for whom that restriction does not apply: it is their request,
    /// end to end. So when the two describe the same request, the survivor inherits the wider view
    /// instead of the merge quietly discarding it.
    /// </para>
    /// <para>
    /// Nothing here can WIDEN authorization on its own: every field copied is a lifecycle fact or the
    /// registrant's own request content, and each still has to pass the relation test in
    /// <see cref="BuildAllowedActions"/> before it becomes an action.
    /// </para>
    /// </summary>
    private static void MergeCandidateInto(
        VisitRequestManagementItemDto primary, VisitRequestManagementItemDto other)
    {
        primary.RegistrantUserId ??= other.RegistrantUserId;

        primary.CanEditPending |= other.CanEditPending;
        primary.CanResubmit |= other.CanResubmit;
        primary.HasCancellableInstance |= other.HasCancellableInstance;
        primary.HasStartedCampus |= other.HasStartedCampus;
        primary.CanViewRejectReason |= other.CanViewRejectReason;
        primary.CanViewCancelReason |= other.CanViewCancelReason;
        primary.IsCurrentUserParticipant |= other.IsCurrentUserParticipant;
        primary.ParticipantRole ??= other.ParticipantRole;

        // The participant's OWN row id and status. Only the attending source carries them, and it is
        // routinely the source that LOSES the merge (a campus decision or a host task outranks an
        // invitation). Dropping them here is precisely the bug that left a merged attending row with
        // nothing to address but the generic request detail — which the invitation relation does not
        // necessarily authorize. They travel with the surviving row so the secondary entry can still
        // open the invitation / department-task screen that belongs to it.
        if (primary.ParticipantId is null && other.ParticipantId is not null)
        {
            primary.ParticipantId = other.ParticipantId;
            primary.ParticipantStatus = other.ParticipantStatus;
        }

        // Effective read scope is a UNION over relations: if ANY relation the caller holds on this
        // request opens the request detail, it is open. Each candidate computed this from the same
        // canonical rules (VisitFormReadService.ComputeScopeAsync), just from its own row's angle.
        primary.CanViewRequestDetail |= other.CanViewRequestDetail;

        primary.ResubmissionCount = Math.Max(primary.ResubmissionCount, other.ResubmissionCount);
        primary.LastResubmittedAt ??= other.LastResubmittedAt;
        primary.LastResubmittedBy ??= other.LastResubmittedBy;
        primary.LastResubmittedByName ??= other.LastResubmittedByName;

        if (!primary.IsAlsoHost && other.IsAlsoHost)
        {
            primary.IsAlsoHost = true;
            primary.AlsoHostVisitInstanceId ??= other.AlsoHostVisitInstanceId;
        }

        // The per-campus accordion. An instance row has none by design; inheriting the registrant's own
        // is what keeps "Xem N cơ sở" working on the merged list instead of collapsing to one campus.
        if (primary.CampusProgressItems.Count == 0 && other.CampusProgressItems.Count > 0)
        {
            primary.CampusProgressItems = other.CampusProgressItems;
            primary.CanExpandCampuses = other.CanExpandCampuses;
        }
        primary.CampusCount = Math.Max(primary.CampusCount, other.CampusCount);

        // "Something changed here", computed against the wider scope, is the better of the two.
        primary.ChangeSummary ??= other.ChangeSummary;

        MergeMatchedContexts(primary, other);
    }

    /// <summary>
    /// Unions the search-match contexts of two candidates for the same request. Safe by construction:
    /// both candidates were produced by queries already scoped to this caller, so neither can contribute
    /// a campus they were not entitled to see.
    /// </summary>
    private static void MergeMatchedContexts(
        VisitRequestManagementItemDto primary, VisitRequestManagementItemDto other)
    {
        if (other.MatchedContexts is not { Count: > 0 }) return;
        if (primary.MatchedContexts is not { Count: > 0 })
        {
            primary.MatchedContexts = other.MatchedContexts;
            return;
        }

        foreach (var incoming in other.MatchedContexts)
        {
            var existing = primary.MatchedContexts.FirstOrDefault(
                c => c.Scope == incoming.Scope && c.VisitInstanceId == incoming.VisitInstanceId);
            if (existing is null)
            {
                primary.MatchedContexts.Add(incoming);
                continue;
            }
            foreach (var field in incoming.MatchedFields)
                if (!existing.MatchedFields.Contains(field))
                    existing.MatchedFields.Add(field);
        }
    }

    // ── Relations ─────────────────────────────────────────────────────────────────────────────
    // FILTER != AUTHORIZATION != ENTRY CONTEXT. The filter says why a row is on screen; the relations
    // below say what the caller is to it; the lifecycle then says what that lets them do. Nothing in
    // this region may read the tab.

    /// <summary>One campus instance a row covers, flattened so both row shapes can be read the same way.</summary>
    private readonly record struct CampusScope(
        ulong VisitInstanceId, ulong CampusId, string? CampusName, string? Status,
        ulong? HostUserId, ulong? ContactUserId, bool IsParticipant);

    /// <summary>
    /// The campuses a row is about. An instance-level row is exactly one; a request-level row is every
    /// campus of the request (its progress items), which is the only shape that can hold "contact of HN
    /// but not DN". Progress items win when present — on a single-campus request-level row they carry
    /// the same instance as the flat columns, and yielding both would double every relation.
    /// </summary>
    private static IEnumerable<CampusScope> EnumerateCampusScopes(VisitRequestManagementItemDto item)
    {
        if (item.CampusProgressItems.Count > 0)
        {
            foreach (var cp in item.CampusProgressItems)
                yield return new CampusScope(cp.VisitInstanceId, cp.CampusId, cp.CampusName, cp.InstanceStatus,
                    cp.HostUserId, cp.OperationalContactUserId, IsParticipant: false);
            yield break;
        }

        if (item.VisitInstanceId.HasValue && item.CampusId.HasValue)
            yield return new CampusScope(item.VisitInstanceId.Value, item.CampusId.Value, item.CampusName,
                item.CampusStatus, item.CurrentHostUserId, item.OperationalContactUserId,
                item.IsCurrentUserParticipant);
    }

    /// <summary>A campus instance is live once it has been approved, and stays readable after it closes.</summary>
    private static bool IsInstanceOperational(string? status) =>
        status == VisitInstanceStatus.Assigned
        || status == VisitInstanceStatus.BeforeVisit
        || status == VisitInstanceStatus.DuringVisit
        || status == VisitInstanceStatus.AfterVisit
        || status == VisitInstanceStatus.Closed;

    /// <summary>
    /// Every relation the caller genuinely holds on this row, each with the scope it holds it at.
    ///
    /// <para>
    /// One person is routinely several of these at once. The list used to keep a single value derived
    /// from the tab, so a Staff Leader who had also registered the visit read as "registrant" and lost
    /// their campus decision, or read as "reviewer" and lost their edit — the filter, not the data, was
    /// deciding. Here the relations are simply all collected; the caller of this method unions their
    /// rights and then applies the lifecycle.
    /// </para>
    /// </summary>
    private static List<VisitRelationContextDto> BuildRelationContexts(
        VisitRequestManagementItemDto item, ulong userId, ulong? ownCampusId,
        bool isStaffRole, bool isStaffLeader)
    {
        var contexts = new List<VisitRelationContextDto>();
        var requestActive = item.RequestStatus != VisitRequestStatuses.Cancelled;

        // REQUEST scope. The registrant owns the delegation itself, at every campus of it — which is
        // why this relation carries no instance and cannot be taken away by a campus-level fact.
        if (item.RegistrantUserId == userId)
        {
            var due = item.CanEditPending || item.CanResubmit;
            contexts.Add(new VisitRelationContextDto
            {
                Relation = VisitRowRelations.Registrant,
                Scope = VisitActionScopes.Request,
                EntryContext = VisitEntryContexts.RequestDetail,
                RequiresAction = due,
                Priority = due ? VisitRelationPriority.RegistrantAction : VisitRelationPriority.Tracking,
            });
        }

        foreach (var campus in EnumerateCampusScopes(item))
        {
            var live = IsInstanceOperational(campus.Status);
            var working = live && campus.Status != VisitInstanceStatus.Closed && requestActive;
            // Internal staff work at ONE campus, so a Host / reviewer question about any other campus
            // is not theirs to answer.
            var outsideOwnCampus = isStaffRole && ownCampusId.HasValue && campus.CampusId != ownCampusId.Value;

            VisitRelationContextDto At(string relation, string entry, bool requiresAction, int priority) => new()
            {
                Relation = relation,
                Scope = VisitActionScopes.Instance,
                VisitInstanceId = campus.VisitInstanceId,
                CampusId = campus.CampusId,
                CampusName = campus.CampusName,
                EntryContext = entry,
                RequiresAction = requiresAction,
                Priority = priority,
            };

            if (isStaffLeader && ownCampusId.HasValue && campus.CampusId == ownCampusId.Value)
            {
                var reviewDue = requestActive && campus.Status == VisitInstanceStatus.WaitingRequestApproval;
                contexts.Add(At(VisitRowRelations.CampusReviewer,
                    reviewDue ? VisitEntryContexts.CampusReview
                        : live ? VisitEntryContexts.ProcessSummary
                        : VisitEntryContexts.RequestDetail,
                    reviewDue,
                    reviewDue ? VisitRelationPriority.CampusReviewRequired : VisitRelationPriority.Tracking));
            }

            if (campus.HostUserId == userId && !outsideOwnCampus)
            {
                contexts.Add(At(VisitRowRelations.Host,
                    live ? VisitEntryContexts.HostProcess : VisitEntryContexts.RequestDetail,
                    working,
                    working ? VisitRelationPriority.HostProcessRequired : VisitRelationPriority.Tracking));
            }

            if (campus.IsParticipant)
            {
                contexts.Add(At(VisitRowRelations.Participant,
                    live ? VisitEntryContexts.Contribution : VisitEntryContexts.RequestDetail,
                    working,
                    working ? VisitRelationPriority.InvitationAction : VisitRelationPriority.Tracking));
            }

            // Guest side, and deliberately NOT confined to the account's own campus: confirming as the
            // contact of a visit is a guest-side act that says nothing about where the person works.
            if (campus.ContactUserId == userId)
            {
                contexts.Add(At(VisitRowRelations.OperationalContact,
                    live ? VisitEntryContexts.ReceptionDetail : VisitEntryContexts.RequestDetail,
                    requiresAction: false, VisitRelationPriority.Tracking));
            }
        }

        return contexts
            .OrderBy(c => c.Priority)
            .ThenBy(c => RelationRank(c.Relation))
            .ToList();
    }

    /// <summary>Stable tie-break inside one priority band. Never an authorization input.</summary>
    private static int RelationRank(string relation) => relation switch
    {
        VisitRowRelations.CampusReviewer => 0,
        VisitRowRelations.Host => 1,
        VisitRowRelations.Participant => 2,
        VisitRowRelations.Registrant => 3,
        _ => 4,
    };

    /// <summary>
    /// Where this row opens by default — routing, decided AFTER and separately from authorization.
    ///
    /// <para>
    /// A specific filter answers the question itself: somebody who picked "Đơn tôi đăng ký" is looking
    /// at their request through the registrant relation, so it opens the request — even though they may
    /// also be its Host, and still hold every host right there. Only the merged list has no relation to
    /// look through, and there the most urgent one wins.
    /// </para>
    /// </summary>
    private static (string? EntryContext, ulong? VisitInstanceId) ResolvePrimaryEntry(
        VisitRequestManagementItemDto item, string tab, List<VisitRelationContextDto> contexts)
    {
        // No relation at all: HO monitoring, or a Department/Student row that exists because of a
        // logistics/agenda assignment. Their routing is established elsewhere — inventing an entry
        // here would move screens nobody asked to move.
        if (contexts.Count == 0) return (null, null);

        // A multi-campus SUMMARY row is not one campus, so it cannot open one campus's screen — the
        // reader chooses the campus from the accordion first.
        if (!item.VisitInstanceId.HasValue)
            return item.CanViewRequestDetail ? (VisitEntryContexts.RequestDetail, null) : (null, null);

        var pick = tab switch
        {
            TabRegistered => contexts.FirstOrDefault(c => c.Relation == VisitRowRelations.Registrant),
            TabHosted => contexts.FirstOrDefault(c => c.Relation == VisitRowRelations.Host),
            TabAttending => contexts.FirstOrDefault(c => c.Relation == VisitRowRelations.Participant),
            _ => null,
        } ?? contexts[0];

        // A relation can point at the request without the request being open to it: a declined
        // invitation still holds the participant relation, and an agenda/logistics assignment is a
        // relation to ONE campus's work. CanViewRequestDetail mirrors what VisitFormReadService
        // actually admits, so naming REQUEST_DETAIL against it would hand the UI a 403 to walk into.
        // Fall back to the most urgent context that opens something this caller can really reach.
        if (pick.EntryContext == VisitEntryContexts.RequestDetail && !item.CanViewRequestDetail)
        {
            pick = contexts.FirstOrDefault(c => c.EntryContext != VisitEntryContexts.RequestDetail)
                ?? pick;
            if (pick.EntryContext == VisitEntryContexts.RequestDetail) return (null, null);
        }

        return (pick.EntryContext, pick.VisitInstanceId);
    }

    /// <summary>Shallow copy with Page/PageSize overridden — used to fetch a merge source unpaginated.</summary>
    private static ViewGuestDelegationListQuery CloneForMerge(ViewGuestDelegationListQuery source, int pageSize) => new()
    {
        Tab = source.Tab,
        Page = 1,
        PageSize = pageSize,
        Keyword = source.Keyword,
        RequestStatus = source.RequestStatus,
        CampusStatus = source.CampusStatus,
        CampusId = source.CampusId,
        VisitScope = source.VisitScope,
        VisitScopes = source.VisitScopes,
        FromDate = source.FromDate,
        ToDate = source.ToDate,
        CancelledOnly = source.CancelledOnly,
        Relation = source.Relation,
        ReadOnlyOnly = source.ReadOnlyOnly,
        ActionableOnly = source.ActionableOnly,
        Timing = source.Timing,
        SortBy = source.SortBy,
        SortOrder = source.SortOrder,
    };

    /// <summary>
    /// Attaches the change summary to INSTANCE-level rows (the campus actors' tabs).
    ///
    /// Only the request-level query did this, which meant the one audience whose job is to react to a
    /// change — the campus Staff Leader looking at their own campus — was the only audience never shown
    /// that something had changed. Scope is the row's OWN instance, so this cannot reveal a sibling.
    /// Rows that already carry a summary (request-level) are left alone.
    /// </summary>
    private async Task AttachInstanceChangeSummariesAsync(
        List<VisitRequestManagementItemDto> items, ulong userId, CancellationToken ct)
    {
        var pending = items.Where(i => i.VisitInstanceId is not null && i.ChangeSummary is null).ToList();
        if (pending.Count == 0) return;

        var visibleByRequest = new Dictionary<ulong, HashSet<ulong>>();
        var campusNameByInstance = new Dictionary<ulong, string>();
        foreach (var item in pending)
        {
            var instanceId = item.VisitInstanceId!.Value;
            if (!visibleByRequest.TryGetValue(item.VisitRequestId, out var set))
                visibleByRequest[item.VisitRequestId] = set = new HashSet<ulong>();
            set.Add(instanceId);
            if (item.CampusName is not null) campusNameByInstance[instanceId] = item.CampusName;
        }

        var summaries = await VisitChangeSummaryBuilder.BuildAsync(
            _context, userId, visibleByRequest.Keys.ToList(), visibleByRequest, campusNameByInstance, ct);

        foreach (var item in pending)
            if (summaries.TryGetValue(item.VisitRequestId, out var summary))
                item.ChangeSummary = summary;
    }

    /// <summary>
    /// Mutation verdicts for the ROW itself. Today that means the Host handover, which is the action
    /// §11 moves onto the list — and the one whose refusal ("chỉ được chuyển ít nhất 6 giờ trước")
    /// the list has to be able to state rather than silently omit.
    ///
    /// A REQUEST-LEVEL row (multi-campus summary, <c>VisitInstanceId == null</c>) gets nothing: the
    /// handover picks a campus, and a summary row cannot say which. Those verdicts live on the campus
    /// progress items instead — see <see cref="AttachCampusCapabilities"/>.
    /// </summary>
    private List<VisitActionCapabilityDto> BuildRowCapabilities(
        VisitRequestManagementItemDto item, ulong? leaderCampusId, DateTime now)
    {
        var capabilities = new List<VisitActionCapabilityDto>();
        // The campus test below IS the reviewer relation, so the tab short-circuit that used to sit
        // here was pure duplication with the wrong source of truth: a Staff Leader reading their own
        // campus through "Đơn tôi đăng ký" was refused a handover they were entitled to.
        if (item.VisitInstanceId is null || item.CampusStatus is null) return capabilities;
        if (leaderCampusId is null || item.CampusId != leaderCampusId) return capabilities;

        capabilities.Add(TransferHostVerdict(
            item.RequestStatus, item.CampusStatus, item.PlannedStartAt,
            item.CurrentHostUserId, item.VisitInstanceId.Value, item.CampusName, now));
        return capabilities;
    }

    /// <summary>
    /// Per-campus verdicts for the multi-campus accordion. Each campus is measured on its own start
    /// and its own status, so handing over the Host at one campus is never gated by a sibling — and a
    /// campus this caller does not lead simply comes back refused with the reason, never enabled.
    /// </summary>
    private void AttachCampusCapabilities(
        VisitRequestManagementItemDto item, ulong? leaderCampusId, DateTime now)
    {
        if (item.CampusProgressItems.Count == 0) return;
        foreach (var campus in item.CampusProgressItems)
        {
            // Same as above: the campus test is the reviewer relation; the tab never was one.
            if (leaderCampusId is null || campus.CampusId != leaderCampusId) continue;

            var verdict = TransferHostVerdict(
                item.RequestStatus, campus.InstanceStatus, campus.PlannedStartAt,
                campus.HostUserId, campus.VisitInstanceId, campus.CampusName, now);
            campus.Capabilities.Add(verdict);
            campus.CanTransferHost = verdict.Enabled;
        }
    }

    /// <summary>
    /// One handover verdict, from the SAME policy the transfer command re-checks inside its
    /// transaction — so the list cannot offer a handover the command would then refuse.
    /// </summary>
    private static VisitActionCapabilityDto TransferHostVerdict(
        string requestStatus, string? instanceStatus, DateTime? plannedStartAt,
        ulong? currentHostUserId, ulong visitInstanceId, string? campusName, DateTime now)
    {
        var start = plannedStartAt ?? now;
        var decision = VisitMutationPolicy.Evaluate(new VisitMutationContext(
            VisitMutationAction.TransferHost, requestStatus, instanceStatus ?? string.Empty,
            start, now, VisitViewerRelations.CampusLeader));

        // Handing the role over presupposes there IS one. Before approval the Host arrives WITH the
        // decision, which is a different action — offering "chuyển" there would just confuse it.
        var noHost = currentHostUserId is null;
        var enabled = decision.Allowed && !noHost;

        return new VisitActionCapabilityDto
        {
            Code = VisitFormActions.TransferHost,
            Scope = VisitActionScopes.Instance,
            VisitInstanceId = (long)visitInstanceId,
            Enabled = enabled,
            DisabledReasonCode = enabled
                ? null
                : noHost ? VisitMutationErrorCodes.LifecycleNotAllowed : decision.ErrorCode,
            DisabledReason = enabled
                ? null
                : noHost ? "Cơ sở này chưa có người phụ trách để chuyển giao." : decision.DisabledReason,
            CutoffAt = decision.CutoffAt,
            PlannedStartAt = plannedStartAt,
            CampusName = campusName,
            RequiredLeadHours = decision.RequiredLeadHours,
        };
    }

    /// <summary>
    /// Attaches "what should I do next" to every row, batched over the page.
    ///
    /// A row with nothing to do is handed the empty task rather than skipped: "Không có nhiệm vụ cần
    /// xử lý" is an answer, and a missing field would leave the UI to invent one from the status — the
    /// exact thing this contract exists to stop.
    ///
    /// The two viewer flags come from the caller's real relations. They used to be blanked whenever the
    /// row arrived on the attending or registered tab, which told a Host reading their own registered
    /// request that they had no preparation to finish.
    /// </summary>
    private async Task AttachNextTasksAsync(
        List<VisitRequestManagementItemDto> items, ulong userId,
        ulong? leaderCampusId, DateTime now, CancellationToken ct)
    {
        if (items.Count == 0) return;

        var rows = items.Select(item => new VisitNextTaskBuilder.Row(
                item.VisitRequestId,
                item.VisitInstanceId,
                item.RequestStatus,
                item.CampusStatus,
                item.PlannedStartAt,
                item.PlannedEndAt,
                ViewerIsHost: item.CurrentUserIsHost,
                ViewerLeadsCampus: leaderCampusId is not null && item.CampusId == leaderCampusId,
                item.AllowedActions))
            .ToList();

        var tasks = await VisitNextTaskBuilder.BuildAsync(_context, userId, rows, now, ct);
        for (var i = 0; i < items.Count; i++)
            items[i].NextTask = tasks[i];
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
            // Status: any response state (INVITED/ACCEPTED/DECLINED) — matches the dedicated
            // "Lời mời tham dự" tab (GetVisitInvitationsQueryHandler), which shows pending
            // invitations too, not just ones the user already accepted. Only REMOVED (revoked)
            // and ASSIGNED (a different, department-task relation) are excluded.
            q = q.Where(x =>
                x.vr.Status != VisitRequestStatuses.Rejected &&
                x.vr.Status != VisitRequestStatuses.Cancelled &&
                x.c.Status != VisitInstanceStatus.Cancelled &&
                x.c.CurrentHostUserId != userId &&
                x.vr.CreatedBy != userId &&
                x.vr.RegistrantUserId != userId &&
                (string.IsNullOrEmpty(currentUserEmail) || x.vr.RegistrantEmail == null || x.vr.RegistrantEmail.ToLower() != currentUserEmail) &&
                !x.vr.CampusInstances.Any(ci => ci.OperationalContactUserId == userId) &&
                _context.VisitParticipants.Any(pp =>
                    pp.VisitInstanceId == x.c.VisitInstanceId &&
                    pp.UserId == userId &&
                    !pp.IsHost &&
                    pp.Status != ParticipantStatuses.Removed &&
                    pp.Status != ParticipantStatuses.Assigned &&
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
                // campus (single or multi) — no HO gate anymore — but only once the GLOBAL
                // confirmation gate has opened. While ANY campus of the request is still missing
                // its operational contact, the request is invisible to EVERY campus's Staff
                // Leader, including one whose own campus already reached
                // WAITING_REQUEST_APPROVAL. The gate belongs to the request, not the campus.
                //
                // A Staff Leader who REGISTERED the request still sees it — through the
                // "registered" tab (and its own source inside the merged "all" tab), which is the
                // registrant relation and is unaffected by this reviewer-side filter.
                q = q.Where(x => x.c.CampusId == primaryCampusId
                    && x.vr.Status != VisitRequestStatuses.PendingContactConfirmation);
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
            // Rows match on THIS instance's own detail name — never a hidden sibling campus's content,
            // and never a request-level name, which does not exist.
            var keyword = request.Keyword.ToLower();
            q = q.Where(x =>
                ( x.c.FormDetail != null && x.c.FormDetail.DelegationName.ToLower().Contains(keyword)) ||
                (x.vr.RequestCode != null && x.vr.RequestCode.ToLower().Contains(keyword)) ||
                (x.vr.RegistrantOrganization != null && x.vr.RegistrantOrganization.ToLower().Contains(keyword)) ||
                (x.vr.RegistrantFullName != null && x.vr.RegistrantFullName.ToLower().Contains(keyword)) ||
                (x.vr.RegistrantNationality != null && x.vr.RegistrantNationality.ToLower().Contains(keyword)) ||
                (x.vr.RegistrantJobTitle != null && x.vr.RegistrantJobTitle.ToLower().Contains(keyword)) ||
                _context.Partners.Any(p => p.PartnerId == x.vr.PartnerId && p.Name != null && p.Name.ToLower().Contains(keyword)) ||
                _context.Campuses.Any(cc => cc.CampusId == x.c.CampusId && cc.Name.ToLower().Contains(keyword)) ||
                _context.Users.Any(u => u.UserId == x.c.CurrentHostUserId && u.FullName.ToLower().Contains(keyword)) ||
                _context.Users.Any(u => u.UserId == x.c.OperationalContactUserId && u.FullName.ToLower().Contains(keyword)));
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
                x.c.RowVersion,

                x.c.PlannedStartAt,
                x.c.PlannedEndAt,
                CampusCancelledAt = x.c.CancelledAt,
                CampusCancellationReason = x.c.CancellationReason,
                CampusCancellationActorType = x.c.CancellationActorType,
                CampusCancellationSource = x.c.CancellationSource,
                CampusCancelledBy = x.c.CancelledBy,
                x.vr.RequestCode,
                x.vr.HasMixedCampusDetails,
                // Instance row: always THIS campus's own detail, never a sibling's.
                DelegationName = x.c.FormDetail != null ? x.c.FormDetail.DelegationName : null,
                x.vr.PartnerId,
                x.vr.RegistrantOrganization,
                RequestStatus = x.vr.Status,
                x.vr.VisitScope,
                x.vr.CreatedBy,
                x.c.OperationalContactUserId,
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
        var userIds = page.SelectMany(r => new[] { r.CurrentHostUserId, r.OperationalContactUserId, r.CampusCancelledBy, r.RequestCancelledBy, r.DecidedBy })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var campusCountByRequest = (await _context.VisitRequestCampuses
                .Where(vrc => requestIds.Contains(vrc.VisitRequestId))
                .Select(vrc => vrc.VisitRequestId)
                .ToListAsync(ct))
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        // The caller's OWN participant rows on the page's instances. ONE query feeds two different
        // questions, which are deliberately not the same question:
        //   • which row to SHOW and address (id + status + role) — the live one;
        //   • whether the PARTICIPANT relation is held at all — a stricter predicate over ALL rows.
        var myParticipations = await _context.VisitParticipants
            .Where(pp => instanceIds.Contains(pp.VisitInstanceId) && pp.UserId == userId)
            .Select(pp => new { pp.VisitInstanceId, pp.ParticipantId, pp.ParticipantRole, pp.IsHost, pp.Status, pp.InvitedBy })
            .ToListAsync(ct);

        // The row to address. The id is what lets an attending-origin row reach the invitation /
        // department-task screen from the merged "all" tab; the status is what tells a DECLINED row
        // apart from a live one, which is the difference between a relation that grants the request
        // detail and one that has ended.
        var myParticipation = myParticipations
            .GroupBy(p => p.VisitInstanceId)
            // Prefer a live row over a superseded one: a re-invitation after a decline leaves both.
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(p => p.Status == ParticipantStatuses.Accepted
                                            || p.Status == ParticipantStatuses.Assigned
                                            || p.Status == ParticipantStatuses.Invited)
                      .ThenByDescending(p => p.ParticipantId)
                      .First());

        // The PARTICIPANT *relation* is stricter than "has a row in visit_participants": it is the
        // same predicate the attending tab populates from, so "matched the invitation filter" and
        // "holds the participant relation" can never disagree. A revoked slot, the host's own row and
        // a department ASSIGNED task (its own relation) are all excluded. Evaluated over EVERY row,
        // not just the one picked above — a stale REMOVED row must not mask a live invitation.
        var myParticipantRelation = myParticipations
            .Where(p => !p.IsHost
                && p.Status != ParticipantStatuses.Removed
                && p.Status != ParticipantStatuses.Assigned
                && (p.ParticipantRole == ParticipantRoles.IcSupport
                    || p.ParticipantRole == ParticipantRoles.DeptSupport
                    || p.ParticipantRole == ParticipantRoles.Student)
                && (p.InvitedBy == null || p.InvitedBy != userId))
            .Select(p => p.VisitInstanceId)
            .ToHashSet();

        // ── Everything the request-detail scope needs, batched. These mirror
        //    VisitFormReadService.ComputeScopeAsync exactly; see the verdict built per row below. ──
        var contactHeldRequestIds = requestIds.Count == 0
            ? new HashSet<ulong>()
            : (await _context.VisitRequestCampuses
                .Where(vrc => requestIds.Contains(vrc.VisitRequestId) && vrc.OperationalContactUserId == userId)
                .Select(vrc => vrc.VisitRequestId)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();

        var logisticsInstanceIds = instanceIds.Count == 0
            ? new HashSet<ulong>()
            : (await _context.VisitLogisticsItems
                .Where(l => instanceIds.Contains(l.VisitInstanceId) && l.AssignedToUserId == userId)
                .Select(l => l.VisitInstanceId)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();

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
        // Role facts for the request-detail verdict below, read once rather than per row.
        var isHoViewer = _currentUser.RoleCode == RoleCodes.Ho;
        var leaderCampusIdForDetail =
            _currentUser.RoleCode == RoleCodes.Staff
            && string.Equals(_currentUser.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
                ? _currentUser.PrimaryCampusId
                : null;
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
            string? contactName = r.OperationalContactUserId.HasValue && userNames.TryGetValue(r.OperationalContactUserId.Value, out var vn) ? vn : null;
            myParticipation.TryGetValue(r.VisitInstanceId, out var participation);
            var participantRole = participation?.ParticipantRole;

            // ── Does the generic request detail actually admit this caller for THIS instance? ──
            // Same relations as VisitFormReadService.ComputeScopeAsync, asked row by row. A row is
            // listed because SOME relation earned it; that relation is not always one the request
            // detail honours — an agenda assignment and a declined invitation both put a row here
            // and neither opens the request. Saying so on the row is what stops the UI guessing.
            var participantGrantsDetail = participation is not null
                && (participation.Status == ParticipantStatuses.Invited
                    || participation.Status == ParticipantStatuses.Accepted
                    || participation.Status == ParticipantStatuses.Assigned);
            bool canViewRequestDetail =
                r.RegistrantUserId == userId
                || isHoViewer
                || contactHeldRequestIds.Contains(r.VisitRequestId)
                || (leaderCampusIdForDetail is { } lcid && r.CampusId == lcid
                    && !VisitRequestStatuses.IsBehindContactGate(r.RequestStatus))
                || r.CurrentHostUserId == userId
                || participantGrantsDetail
                || logisticsInstanceIds.Contains(r.VisitInstanceId);

            // Instance-level cancel preferred; fall back to request-level when the whole request was cancelled.
            bool requestCancelled = r.RequestStatus == VisitRequestStatuses.Cancelled;
            bool instanceCancelled = r.CampusStatus == VisitInstanceStatus.Cancelled;
            var cancelledById = r.CampusCancelledBy ?? r.RequestCancelledBy;
            string? cancelledByName = cancelledById.HasValue && userNames.TryGetValue(cancelledById.Value, out var cbn) ? cbn : null;

            // ── Match contexts (instance-level): the authorized campus IS this single row instance, so a
            // sibling campus can never appear. Fields mirror the instance-level keyword predicate exactly;
            // the RAW partner name (not the RegistrantOrganization fallback) is tested for PARTNER. ──
            string? rawPartnerName = r.PartnerId.HasValue && partnerNames.TryGetValue(r.PartnerId.Value, out var rpn) ? rpn : null;
            var reqMatchFields = new List<VisitSearchMatchContextBuilder.Field>
            {
                new(VisitSearchFieldCodes.RequestCode, r.RequestCode),
                new(VisitSearchFieldCodes.RegistrantOrganization, r.RegistrantOrganization),
                new(VisitSearchFieldCodes.Partner, rawPartnerName),
                new(VisitSearchFieldCodes.OperationalContact, contactName),
            };
            var campusMatchFields = new List<VisitSearchMatchContextBuilder.Field>
            {
                new(VisitSearchFieldCodes.Campus, campusName),
                new(VisitSearchFieldCodes.Host, hostName),
                // Pure V2: the delegation name belongs to the CAMPUS scope (it is per-campus), never to the
                // request-level field list. r.DelegationName here is this row's own instance detail.
                new(VisitSearchFieldCodes.DelegationName, r.DelegationName),
            };
            var matchedContexts = VisitSearchMatchContextBuilder.Build(
                request.Keyword, reqMatchFields,
                new[] { new VisitSearchMatchContextBuilder.CampusScope(r.VisitInstanceId, r.CampusId, campusName, campusMatchFields) });

            return new VisitRequestManagementItemDto
            {
                VisitRequestId = r.VisitRequestId,
                VisitInstanceId = r.VisitInstanceId,
                RequestCode = r.RequestCode,
                DelegationName = r.DelegationName,
                PartnerName = partnerName,
                MatchedContexts = matchedContexts,
                RequestStatus = r.RequestStatus,
                CampusStatus = r.CampusStatus,
                VisitScope = r.VisitScope,
                HasMixedCampusDetails = r.HasMixedCampusDetails,
                CampusId = r.CampusId,
                CampusName = campusName,
                CampusCount = campusCountByRequest.TryGetValue(r.VisitRequestId, out var cc2) ? cc2 : 1,
                CreatedByUserId = r.CreatedBy,
                CurrentHostUserId = r.CurrentHostUserId,
                HostName = hostName,
                RowVersion = r.RowVersion,

                CurrentUserIsHost = r.CurrentHostUserId == userId,
                OperationalContactUserId = r.OperationalContactUserId,
                RegistrantUserId = r.RegistrantUserId,
                OperationalContactName = contactName,
                IsCurrentUserParticipant = myParticipantRelation.Contains(r.VisitInstanceId),
                ParticipantRole = participantRole,
                ParticipantId = participation?.ParticipantId,
                ParticipantStatus = participation?.Status,
                CanViewRequestDetail = canViewRequestDetail,
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

        await AttachRegistrantRequestFactsAsync(items, userId, ct);

        // NOT given a sibling-campus accordion. An instance-level row belongs to a SCOPED actor
        // (Staff Leader = own campus, Staff = own hosted instance, Dept/Student = own assignment),
        // and every one of them is scoped to a single campus. Filling CampusProgressItems here
        // would hand them a sibling campus's status, host name, decision note and cancellation
        // reason — data the scope explicitly withholds. The accordion stays request-level, where
        // the caller (Visitor owner / HO / registrant) actually holds the whole request.
        return (items, total);
    }

    /// <summary>
    /// Fills the REQUEST-level lifecycle facts on instance-level rows the caller REGISTERED.
    ///
    /// <para>
    /// An instance row is built from ONE campus and deliberately knows nothing about its siblings — a
    /// campus actor must not be shown them. The registrant is the one exception: it is their request
    /// end to end, so "can this whole request still be edited / resubmitted / cancelled" is a question
    /// they are entitled to an answer to no matter which filter put the row on screen.
    /// </para>
    /// <para>
    /// Without this the very same person kept their edit on "Đơn tôi đăng ký" and lost it on "Đoàn tôi
    /// phụ trách" — a filter changing a right, which is the thing this whole area exists to stop. One
    /// batched query for the page, and only when the page actually contains a request they registered.
    /// </para>
    /// </summary>
    private async Task AttachRegistrantRequestFactsAsync(
        List<VisitRequestManagementItemDto> items, ulong userId, CancellationToken ct)
    {
        var requestIds = items
            .Where(i => i.RegistrantUserId == userId)
            .Select(i => i.VisitRequestId).Distinct().ToList();
        if (requestIds.Count == 0) return;

        var siblings = (await _context.VisitRequestCampuses
                .Where(c => requestIds.Contains(c.VisitRequestId))
                .Select(c => new { c.VisitRequestId, c.Status, c.PlannedStartAt })
                .ToListAsync(ct))
            .GroupBy(c => c.VisitRequestId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var vnNow = _clock.VietnamNow;
        foreach (var item in items)
        {
            if (item.RegistrantUserId != userId) continue;
            if (!siblings.TryGetValue(item.VisitRequestId, out var instances) || instances.Count == 0) continue;

            // Same rules as QueryRequestLevelAsync, measured over the WHOLE request — because that is
            // what the edit / resubmit / cancel commands themselves measure. BOTH pre-decision stages
            // count: a request still waiting on its operational contacts is editable by its registrant.
            item.CanEditPending = (item.RequestStatus == VisitRequestStatuses.PendingApproval
                    || item.RequestStatus == VisitRequestStatuses.PendingContactConfirmation)
                && instances.All(i => i.Status == VisitInstanceStatus.WaitingRequestApproval
                                   || i.Status == VisitInstanceStatus.WaitingContactConfirmation)
                && instances.Min(i => i.PlannedStartAt) >= vnNow.AddHours(VisitMutationPolicy.RequiredLeadHours);

            item.CanResubmit = item.RequestStatus == VisitRequestStatuses.Rejected
                && instances.All(i => i.Status == VisitInstanceStatus.Rejected);

            var active = instances
                .Where(i => i.Status != VisitInstanceStatus.Cancelled && i.Status != VisitInstanceStatus.Rejected)
                .ToList();
            var anyStarted = active.Any(i => i.Status == VisitInstanceStatus.DuringVisit
                || i.Status == VisitInstanceStatus.AfterVisit
                || i.Status == VisitInstanceStatus.Closed);
            item.HasCancellableInstance = active.Count > 0 && !anyStarted
                && active.All(i =>
                    (i.Status == VisitInstanceStatus.WaitingRequestApproval
                        || i.Status == VisitInstanceStatus.Assigned
                        || i.Status == VisitInstanceStatus.BeforeVisit)
                    && i.PlannedStartAt >= vnNow.AddHours(24));
        }
    }

    // ── Request-level: responsible tab for Visitor & HO, and the REGISTERED tab
    // (registeredView: rows where the caller is the registrant — full stop) ──
    private async Task<(List<VisitRequestManagementItemDto> Items, int Total)> QueryRequestLevelAsync(
        ViewGuestDelegationListQuery request, ulong userId, string? roleCode, CancellationToken ct,
        bool registeredView = false)
    {
        var q = _context.VisitRequests.AsQueryable();

        if (registeredView)
        {
            // "Tôi là người đăng ký / Đơn tôi đăng ký" answers exactly one question: which requests did
            // I register? Nothing else.
            //
            // It used to also subtract "…and am not the contact of", which made the two Visitor filters
            // mutually exclusive: a person who registered a visit AND then confirmed as its contact
            // disappeared from "Tôi là người đăng ký" — the filter denied a relation the data plainly
            // recorded. Holding a second relation is not a reason to stop holding the first. The merged
            // "all" list dedupes by request id, so matching both filters cannot double a row.
            q = q.Where(vr => vr.RegistrantUserId == userId);
        }
        // Visitor "Tôi là đầu mối": CONTACT-OWNER rows only. Rows where the Visitor merely
        // registered for someone else live on the "registered" tab (actor relation). Legacy
        // rows without an owner fall back to created_by.
        else if (roleCode == RoleCodes.Visitor)
            q = q.Where(vr => vr.CampusInstances.Any(ci => ci.OperationalContactUserId == userId)
                || (!vr.CampusInstances.Any(ci => ci.OperationalContactUserId != null) && vr.CreatedBy == userId));
        // HO sees every MULTI_CAMPUS request (they decide it) AND every SINGLE_CAMPUS request
        // in read-only monitoring mode (business rule chốt 2026-06: HO theo dõi SINGLE_CAMPUS).
        // No filter is applied for HO here â€” read-only is enforced via AllowedActions (the HO
        // action builder only grants HO_APPROVE/HO_REJECT to MULTI_CAMPUS pending requests).
        // else if (roleCode == RoleCodes.Ho)  â†’ all requests visible.

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            // Visitor tabs: the actor is the registrant/contact, so EVERY campus of their own request is
            // in scope — a request matches when ANY of its per-campus details matches, which is the only
            // place its content lives.
            var kw = request.Keyword.ToLower();
            q = q.Where(vr =>
                ( vr.CampusInstances.Any(ci => ci.FormDetail != null
                        && ci.FormDetail.DelegationName.ToLower().Contains(kw))) ||
                (vr.RequestCode != null && vr.RequestCode.ToLower().Contains(kw)) ||
                (vr.RegistrantOrganization != null && vr.RegistrantOrganization.ToLower().Contains(kw)) ||
                (vr.RegistrantFullName != null && vr.RegistrantFullName.ToLower().Contains(kw)) ||
                (vr.RegistrantNationality != null && vr.RegistrantNationality.ToLower().Contains(kw)) ||
                (vr.RegistrantJobTitle != null && vr.RegistrantJobTitle.ToLower().Contains(kw)) ||
                (vr.Partner != null && vr.Partner.Name != null && vr.Partner.Name.ToLower().Contains(kw)));
        }

        if (request.CancelledOnly)
        {
            q = q.Where(vr => vr.Status == VisitRequestStatuses.Cancelled || vr.CampusInstances.Any(i => i.Status == VisitInstanceStatus.Cancelled));
        }
        else if (request.PendingApprovalAny)
        {
            // HO's "Chờ duyệt" row: union of the two former separate options (a campus still
            // waiting, or a multi-campus request partially decided) — see the DTO doc comment.
            q = q.Where(vr => vr.Status == VisitRequestStatuses.PartiallyApproved
                || vr.CampusInstances.Any(i => i.Status == VisitInstanceStatus.WaitingRequestApproval));
        }
        else if (request.ApprovedAny)
        {
            // HO's "Đã duyệt" row: union of the two former separate options (a campus already
            // assigned a host, or a request whose aggregate is fully approved).
            q = q.Where(vr => vr.Status == VisitRequestStatuses.Approved
                || vr.CampusInstances.Any(i => i.Status == VisitInstanceStatus.Assigned));
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
                q = q.Where(vr => vr.CampusInstances.Any(ci => ci.OperationalContactUserId == userId));
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
                .ThenInclude(c => c.FormDetail) // per-campus delegation names for v2 match contexts (all campuses authorized here)
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
                .Concat(vr.CampusInstances.Select(i => i.OperationalContactUserId))
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
            //
            // These are pure LIFECYCLE verdicts — "is this request in a state that can still be edited
            // / resubmitted" — and say nothing about who is asking. WHO is asked separately, once, in
            // BuildAllowedActions (`item.RegistrantUserId == userId`), which is the relation that owns
            // a request-level act. They used to also carry `!registeredView`, i.e. the tab the row was
            // fetched under silently removed a right the registrant still had. ──
            // The lead time comes from VisitMutationPolicy — the same answer the detail screen and the
            // command handler give. It used to be a local `AddHours(24)`, so the list and the detail
            // could disagree about whether the same request was still editable.
            // Both pre-decision stages, matching VisitMutationPolicy: a request still waiting on its
            // operational contacts is editable by its registrant, and the list must say so or the row
            // hides an action the detail screen and the command both allow.
            bool canEditPending = (vr.Status == VisitRequestStatuses.PendingApproval
                    || vr.Status == VisitRequestStatuses.PendingContactConfirmation)
                && count > 0
                && instances.All(i => i.Status == VisitInstanceStatus.WaitingRequestApproval
                                   || i.Status == VisitInstanceStatus.WaitingContactConfirmation)
                && instances.Min(i => i.PlannedStartAt) >= vnNow.AddHours(VisitMutationPolicy.RequiredLeadHours);
            bool canResubmit = vr.Status == VisitRequestStatuses.Rejected
                && count > 0
                && instances.All(i => i.Status == VisitInstanceStatus.Rejected);

            // Cancel-eligibility (UC-136): REQUEST level.
            // Rule 1: Visitor can cancel the whole request only if ALL active campuses are cancellable
            // (i.e. status is Waiting/Assigned/BeforeVisit AND >= 24h).
            //
            // The 24h here is CANCELLATION, a different rule from the mutation window and deliberately
            // left alone: calling off a visit obliges a campus to stand down people, rooms and security
            // clearance, so it needs more notice than correcting a note does.
            var activeInstances = instances.Where(i => i.Status != VisitInstanceStatus.Cancelled && i.Status != VisitInstanceStatus.Rejected).ToList();
            bool hasStartedCampus = activeInstances.Any(i => i.Status == VisitInstanceStatus.DuringVisit || i.Status == VisitInstanceStatus.AfterVisit || i.Status == VisitInstanceStatus.Closed);
            
            // Lifecycle only, same as canEditPending above — the guest-side relation test lives in
            // BuildAllowedActions, not in the tab this row arrived on.
            bool hasCancellableInstance = activeInstances.Any()
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
            // Only a single-campus row can name a contact: a grouped row spans campuses that are
            // routinely run by different people, and picking one of them would be a guess.
            ulong? contactUserId = single?.OperationalContactUserId;
            string? contactName = contactUserId.HasValue && userNames.TryGetValue(contactUserId.Value, out var vnm) ? vnm : null;
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
            // Contact-owner rule unchanged (still the campus holder, still Visitor-role) — only the
            // `!registeredView` clause is gone, which had made the SAME person lose the per-campus
            // cancel purely by switching to the "Tôi là người đăng ký" filter.
            bool isVisitorOwner = isVisitor && (vr.CampusInstances.Any(ci => ci.OperationalContactUserId == userId) || (!vr.CampusInstances.Any(ci => ci.OperationalContactUserId != null) && vr.CreatedBy == userId));
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
                        // Per-campus contact: on a multi-campus request this is the only thing that can
                        // tell "I hold HN" apart from "I hold DN" — the row-level field is null there.
                        OperationalContactUserId = i.OperationalContactUserId,
                        RowVersion = i.RowVersion,
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

            // "Đồng thời là host" badge: the caller also officially hosts ≥1 instance of this request.
            // No longer restricted to the registered view — the fact is true (or not) regardless of
            // which filter produced the row, and the host RIGHTS now travel with it (see the HOST
            // relation context) instead of being stranded on another tab.
            var alsoHostedInstance = instances.FirstOrDefault(i => i.CurrentHostUserId == userId);

            // ── Match contexts (request-level): Visitor owner / HO / registrant see EVERY campus of their
            // own request, so iterating all instances leaks nothing. Fields mirror the request-level keyword
            // predicate exactly (code/reg-org/partner — NO campus/host/owner name). Pure V2: the delegation
            // name is per-campus, so it is reported under each campus scope, never as a request-level field. ──
            var reqMatchFields = new List<VisitSearchMatchContextBuilder.Field>
            {
                new(VisitSearchFieldCodes.RequestCode, vr.RequestCode),
                new(VisitSearchFieldCodes.RegistrantOrganization, vr.RegistrantOrganization),
                new(VisitSearchFieldCodes.Partner, vr.Partner?.Name),
            };
            var campusMatchScopes = instances
                .Select(i => new VisitSearchMatchContextBuilder.CampusScope(
                    i.VisitInstanceId, i.CampusId,
                    campusNames.TryGetValue(i.CampusId, out var cnm3) ? cnm3 : null,
                    new List<VisitSearchMatchContextBuilder.Field>
                    {
                        new(VisitSearchFieldCodes.DelegationName, i.FormDetail != null ? i.FormDetail.DelegationName : null),
                    }))
                .ToList();
            var matchedContexts = VisitSearchMatchContextBuilder.Build(request.Keyword, reqMatchFields, campusMatchScopes);

            return new VisitRequestManagementItemDto
            {
                VisitRequestId = vr.VisitRequestId,
                VisitInstanceId = single?.VisitInstanceId,
                RequestCode = vr.RequestCode,
                MatchedContexts = matchedContexts,
                // A request-level row cannot represent a MIXED request with one name, and there is no
                // request-level name to borrow — so the row is explicitly labeled instead, and the
                // per-campus names live in the campus progress items/detail view (plan §8.3).
                // Not mixed ⇒ every instance carries identical content, so any instance's detail is THE
                // request-level value (deterministic, not "whichever campus happened to be first").
                DelegationName = vr.HasMixedCampusDetails
                    ? "Khác nhau theo cơ sở"
                    : instances.FirstOrDefault()?.FormDetail?.DelegationName,
                PartnerName = vr.Partner != null ? vr.Partner.Name : vr.RegistrantOrganization,
                RequestStatus = vr.Status,
                CampusStatus = single?.Status,
                VisitScope = vr.VisitScope,
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
                OperationalContactUserId = contactUserId,
                OperationalContactName = contactName,
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

        await AttachChangeSummariesAsync(items, requests, userId, campusNames, ct);
        return (items, total);
    }

    /// <summary>
    /// Attaches the "something changed here" summary to each row, batched over the whole page.
    ///
    /// Scope is worked out here and passed in explicitly rather than inferred inside the builder: a
    /// Staff Leader sees only their own campus, and an unread badge must never become a way to learn
    /// that a campus they cannot see exists on this request.
    ///
    /// <para>
    /// WHY WIDENING THIS FOR THE REGISTRANT LEAKS NOTHING (audited when the relation model made
    /// REGISTRANT a REQUEST-scoped relation). The summary is not a report of what happened — it is a
    /// count of what THIS PERSON was already told: <see cref="VisitChangeSummaryBuilder"/> reads only
    /// <c>notifications.recipient_user_id == userId</c>, so a wider scope can never surface an event
    /// addressed to somebody else. On top of that:
    /// </para>
    /// <list type="bullet">
    ///   <item>the payload is a closed four-code vocabulary (<see cref="VisitListEventCodes"/>) plus
    ///     counts and timestamps — no decision notes, no actor names, no free text, no PII;</item>
    ///   <item>the only identifying fields are the instance id and campus NAME of a campus on the
    ///     caller's own request, which the same row already hands them in full detail (status, host
    ///     name, decision note) via <c>CampusProgressItems</c> — so nothing new is disclosed;</item>
    ///   <item><c>PendingAmendmentCount</c> cannot move at all: the builder's amendment query is
    ///     independently gated on <c>VisitInstance.CurrentHostUserId == userId</c>, and every such
    ///     instance was already in <c>visible</c> through the host clause below;</item>
    ///   <item>this method only ever runs on REQUEST-level rows (its single caller is
    ///     <c>QueryRequestLevelAsync</c>). Instance-level rows keep the strictly per-instance scope of
    ///     <see cref="AttachInstanceChangeSummariesAsync"/>.</item>
    /// </list>
    /// So the correct fix for anything found here is to narrow the CONTENT, never to take the badge
    /// away from someone whose own request changed.
    /// </summary>
    private async Task AttachChangeSummariesAsync(
        List<VisitRequestManagementItemDto> items,
        List<Domain.Entities.Delegations.VisitRequest> requests,
        ulong userId,
        Dictionary<ulong, string> campusNames,
        CancellationToken ct)
    {
        if (items.Count == 0) return;

        var roleCode = _currentUser.RoleCode?.ToUpperInvariant();
        var isWholeRequestViewer = roleCode == RoleCodes.Ho || roleCode == RoleCodes.Visitor;
        var isStaffLeader = roleCode == RoleCodes.Staff
            && string.Equals(_currentUser.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase);
        var primaryCampusId = _currentUser.PrimaryCampusId;

        var visibleByRequest = new Dictionary<ulong, HashSet<ulong>>();
        var campusNameByInstance = new Dictionary<ulong, string>();
        foreach (var vr in requests)
        {
            var visible = new HashSet<ulong>();
            // The registrant holds the REQUEST, so every campus of their own request is in scope —
            // regardless of role. Without this a Staff who registered a two-campus visit was shown
            // "something changed" for their own campus only, on a request they own end to end.
            var ownsWholeRequest = isWholeRequestViewer || vr.RegistrantUserId == userId;
            foreach (var instance in vr.CampusInstances)
            {
                var canSee = ownsWholeRequest
                    || (isStaffLeader && primaryCampusId == instance.CampusId)
                    || instance.CurrentHostUserId == userId;
                if (!canSee) continue;
                visible.Add(instance.VisitInstanceId);
                if (campusNames.TryGetValue(instance.CampusId, out var name))
                    campusNameByInstance[instance.VisitInstanceId] = name;
            }
            visibleByRequest[vr.VisitRequestId] = visible;
        }

        var summaries = await VisitChangeSummaryBuilder.BuildAsync(
            _context, userId, requests.Select(r => r.VisitRequestId).ToList(),
            visibleByRequest, campusNameByInstance, ct);

        foreach (var item in items)
            if (summaries.TryGetValue(item.VisitRequestId, out var summary))
                item.ChangeSummary = summary;
    }

    /// <summary>
    /// Computes the business actions the caller may take on a row. This is the single
    /// source of truth the frontend renders buttons from; every action is re-validated
    /// server-side by its command handler.
    /// </summary>
    private List<string> BuildAllowedActions(
        VisitRequestManagementItemDto item, ulong userId, DateTime now,
        List<VisitRelationContextDto> contexts)
    {
        var actions = new List<string> { "VIEW_DETAIL" };

        // There are no tab short-circuits here any more. "attending" and "registered" used to return
        // immediately, which meant the FILTER, not the data, decided somebody was read-only: a Staff
        // Leader looking at their own campus through "Đơn tôi đăng ký" lost the decision they still
        // owed, and a registrant lost the edit their own request still allowed. Both populations are
        // now simply relation sets like any other, and the per-action guards below — which already
        // named the right relation — decide on their own.

        var roleCode = _currentUser.RoleCode?.ToUpperInvariant();
        var subRole = _currentUser.SubRole;

        bool isHo = roleCode == RoleCodes.Ho;
        bool isStaffLeader = roleCode == RoleCodes.Staff && string.Equals(subRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase);
        bool beforeStart = !item.PlannedStartAt.HasValue || item.PlannedStartAt.Value > now;
        bool requestActive = item.RequestStatus != VisitRequestStatuses.Cancelled;
        // The global confirmation gate, re-asked on the row itself. The leader queue already
        // filters behind-gate rows out, but a row can also arrive here through the merged "all"
        // tab (registrant source), and offering a decision button there would contradict
        // CampusApprovalExecutor, which refuses the call outright.
        bool contactGateOpen = !VisitRequestStatuses.IsBehindContactGate(item.RequestStatus);

        // Relations of THIS row's own campus instance. An action is instance-scoped, so holding the
        // relation on a sibling campus must not answer for this one.
        bool Here(string relation) => item.VisitInstanceId.HasValue
            && contexts.Any(c => c.Relation == relation && c.VisitInstanceId == item.VisitInstanceId);

        bool isRegistrant = contexts.Any(c => c.Relation == VisitRowRelations.Registrant);
        bool isHostHere = Here(VisitRowRelations.Host);
        bool isContactHere = Here(VisitRowRelations.OperationalContact);
        bool isParticipantHere = Here(VisitRowRelations.Participant);
        // Replaces the old `isStaffLeader && item.CampusId == PrimaryCampusId`: the reviewer relation is
        // emitted under exactly that condition, so this is the same test read from one place.
        bool isReviewerHere = Here(VisitRowRelations.CampusReviewer);

        // Guest side of this row: the registrant of the request, or the confirmed contact of this
        // campus. No role test — a registrant may be a STAFF or STAFF LEADER account.
        bool isGuestSide = isRegistrant || isContactHere;

        // HO never approves/rejects anymore (campus-independent approval) — monitor/read-only.

        // Staff Leader — decides their own campus instance regardless of scope: approve
        // (must pick host in the same action) or reject, only while it awaits their decision.
        // WHO may decide comes from the real relation (campus reviewer of THIS campus) — not from the
        // tab. Self-overlap is deliberate: a Staff Leader who also registered this visit keeps the
        // decision; forbidding self-approval would be a business rule of its own, with its own tests.
        // WHETHER it may be decided yet is the global contact gate: while any campus of the request is
        // still missing its operational contact, CampusApprovalExecutor refuses the call outright, so
        // the list must not offer the button — including on a row that arrived through the registrant
        // source of the merged "all" tab, which the reviewer-side query never filtered.
        if (isReviewerHere && requestActive && contactGateOpen
            && item.CampusStatus == VisitInstanceStatus.WaitingRequestApproval)
        {
            actions.Add("APPROVE_AND_ASSIGN_HOST"); // duyệt & gán host (opens host picker)
            actions.Add("CAMPUS_REJECT");
        }

        // Registrant — edit a still-fully-pending request / resubmit a fully-rejected one. Both are
        // request-level acts, so a campus’s contact does not get them (see the edit/resubmit handlers).
        // Eligibility (status + 24h window) is precomputed per row in QueryRequestLevelAsync;
        // the commands re-validate everything server-side.
        if (isRegistrant)
        {
            if (item.CanEditPending)
                actions.Add("EDIT_PENDING_REQUEST");
            if (item.CanResubmit)
                actions.Add("RESUBMIT_REJECTED_REQUEST");
        }

        // Guest side — self-cancel (UC-136). Request-level cancel is the registrant’s; the campus
        // holder gets the instance-scoped one, which the cancel command scopes by visitInstanceId.
        if (isGuestSide)
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
        if (!isStaffLeader && isHostHere
            && (item.CampusStatus == VisitInstanceStatus.Assigned
                || item.CampusStatus == VisitInstanceStatus.BeforeVisit)
            && beforeStart)
        {
            actions.Add("CANCEL_BY_HOST");
        }

        // Navigation Actions — driven by the campus instance lifecycle (never the request
        // aggregate: a PARTIALLY_APPROVED request already has live instances).
        bool instanceOperational = IsInstanceOperational(item.CampusStatus);
        if (instanceOperational && requestActive)
        {
            if (isHostHere)
            {
                actions.Add("OPEN_HOST_PROCESS");
                // The campus is theirs but not open yet — offer the one action that opens it, from
                // the list, so the Host does not have to guess why the process screen is read-only.
                if (item.CampusStatus == VisitInstanceStatus.Assigned)
                    actions.Add(VisitListActions.StartPreparation);
            }
            if (item.CampusId != null && (isHo || isReviewerHere))
            {
                actions.Add("OPEN_PROCESS_SUMMARY");
            }
            if (isGuestSide)
            {
                actions.Add("VIEW_RECEPTION_DETAIL");
            }
            // Was `tab == TabAttending`. The participant relation is the real grant, and it is now
            // carried on the row itself — so a Host who was ALSO invited keeps their contribution
            // entry on every tab, instead of only on the one that happened to be named "attending".
            if (isParticipantHere)
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
    /// Best-effort SINGLE relation label for the caller (display/telemetry only, kept for the existing
    /// consumers). It cannot represent someone who is several things at once, which is precisely why
    /// the authoritative answer is <see cref="VisitRequestManagementItemDto.RelationContexts"/> and why
    /// nothing may authorize from this value; every action is still re-validated server-side.
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
