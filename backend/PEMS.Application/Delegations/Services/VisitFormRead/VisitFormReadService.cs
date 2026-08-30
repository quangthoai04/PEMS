using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Policies;

namespace PEMS.Application.Delegations.Services.VisitFormRead;

/// <inheritdoc cref="IVisitFormReadService"/>
public sealed class VisitFormReadService : IVisitFormReadService
{
    private const string ExternalSupport = "EXTERNAL_SUPPORT";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<VisitFormReadService> _logger;
    private readonly IDateTimeService? _clock;
    private readonly PerCampusFormV2WriteOptions? _writeFlag;

    public VisitFormReadService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<VisitFormReadService> logger,
        IDateTimeService? clock = null,
        PerCampusFormV2WriteOptions? writeFlag = null)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
        _clock = clock;
        _writeFlag = writeFlag;
    }

    public async Task<ResolvedVisitFormDto> ResolveAsync(ulong visitRequestId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Current user is not authenticated.");

        var userId = _currentUser.UserId.Value;

        // 1. Load the request + all instances (+ per-campus detail for v2). A single collection
        //    Include (CampusInstances → FormDetail is a 1:1 under it) avoids any cartesian product;
        //    AsNoTracking because this is a pure read path.
        var request = await _db.VisitRequests
            .AsNoTracking()
            .Include(vr => vr.CampusInstances).ThenInclude(c => c.FormDetail)
            .FirstOrDefaultAsync(vr => vr.VisitRequestId == visitRequestId, cancellationToken);

        if (request is null)
            throw new NotFoundException("Đơn đăng ký tham quan", visitRequestId);

        // Request-level members (v1 shared list) loaded separately — a second collection Include on
        // the request above would multiply rows against CampusInstances.
        var requestMembers = await _db.VisitGuestMembers.AsNoTracking()
            .Where(m => m.VisitRequestId == request.VisitRequestId)
            .ToListAsync(cancellationToken);

        var instances = request.CampusInstances.OrderBy(c => c.PlannedStartAt).ToList();
        var instanceIds = instances.Select(c => c.VisitInstanceId).ToList();

        // 2. Compute the authorized instance set + viewer relation (batched — no N+1 across campuses).
        var scope = await ComputeScopeAsync(request, instances, instanceIds, userId, cancellationToken);
        if (scope.AuthorizedInstanceIds.Count == 0)
            throw new ForbiddenException("Bạn không có quyền xem chi tiết đơn này.");

        var visibleInstances = instances.Where(c => scope.AuthorizedInstanceIds.Contains(c.VisitInstanceId)).ToList();
        var visibleInstanceIds = visibleInstances.Select(c => c.VisitInstanceId).ToList();

        // Action computation (viewer.capabilities + per-instance capabilities). Every mutation verdict
        // comes from VisitMutationPolicy — the SAME call each command handler makes inside its
        // transaction — so the UI can no longer offer something the backend will refuse.
        var now = _clock?.VietnamNow ?? DateTime.Now;
        // The registrant owns the REQUEST: request-level edits, and requester-side actions on every
        // campus. A confirmed operational contact owns ONE campus and is added per campus below —
        // there is no single "requester side" flag covering both any more, because that is exactly
        // how one campus's contact used to acquire rights over its siblings.
        var isRegistrant = VisitRequestOwnership.IsRegistrant(request, userId);
        bool IsCurrentCampusLeader(ulong campusId) =>
            _currentUser.RoleCode == RoleCodes.Staff
            && _currentUser.SubRole == UserSubRoles.Leader
            && _currentUser.PrimaryCampusId == campusId;

        VisitActionCapabilityDto Decide(
            VisitMutationAction action, string code, VisitRequestCampus governing,
            string relation, string? campusName, bool instanceScope, string? overrideReason = null)
        {
            var decision = VisitMutationPolicy.Evaluate(new VisitMutationContext(
                action, request.Status, governing.Status, governing.PlannedStartAt, now, relation));
            // An extra business precondition (a pending amendment already exists, the campus has no
            // Host to hand over) refuses on top of the policy — never overrides an allow into a
            // different allow, only ever narrows.
            var enabled = decision.Allowed && overrideReason is null;
            return new VisitActionCapabilityDto
            {
                Code = code,
                Scope = instanceScope ? VisitActionScopes.Instance : VisitActionScopes.Request,
                VisitInstanceId = instanceScope ? (long)governing.VisitInstanceId : null,
                Enabled = enabled,
                DisabledReasonCode = enabled
                    ? null
                    : overrideReason is not null
                        ? VisitMutationErrorCodes.LifecycleNotAllowed
                        : decision.ErrorCode,
                DisabledReason = enabled ? null : overrideReason ?? decision.DisabledReason,
                CutoffAt = decision.CutoffAt,
                PlannedStartAt = governing.PlannedStartAt,
                CampusName = campusName,
                RequiredLeadHours = decision.RequiredLeadHours,
            };
        }

        // 3. Batch-load display names, per-campus members (v2), and active amendments (v2).
        var campusIds = visibleInstances.Select(c => c.CampusId).Distinct().ToList();
        var campuses = campusIds.Count == 0
            ? new Dictionary<ulong, (string Code, string Name)>()
            : await _db.Campuses.AsNoTracking()
                .Where(c => campusIds.Contains(c.CampusId))
                .Select(c => new { c.CampusId, c.CampusCode, c.Name })
                .ToDictionaryAsync(c => c.CampusId, c => (Code: c.CampusCode, Name: c.Name), cancellationToken);

        // Cancelling actors join the same batch: resolving them separately would mean a second query
        // for a name the very next line already knows how to look up.
        var actorIds = visibleInstances
            .SelectMany(c => new[] { c.CurrentHostUserId, c.DecidedBy, c.CancelledBy })
            .Concat(new[] { request.CancelledBy })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var actorNames = actorIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);
        string? NameOf(ulong? id) => id.HasValue && actorNames.TryGetValue(id.Value, out var n) ? n : null;

        // ── Reception-host people, batched. The current host and the proposed host are read together
        //    but stay two separate objects downstream: they answer different questions ("who is
        //    running this campus" vs "who was put forward"), and a screen that merges them tells the
        //    reader somebody has the job when nobody has agreed to it yet. ──
        var hostPersonIds = visibleInstances
            .SelectMany(c => new[] { c.CurrentHostUserId, c.ProposedHostUserId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var hostPeople = hostPersonIds.Count == 0
            ? new Dictionary<ulong, HostPersonRow>()
            : await _db.Users.AsNoTracking()
                .Where(u => hostPersonIds.Contains(u.UserId))
                .Select(u => new HostPersonRow(
                    u.UserId, u.FullName, u.Email ?? string.Empty, u.Phone ?? string.Empty,
                    _db.Departments.Where(d => d.DepartmentId == u.DepartmentId)
                        .Select(d => d.Name).FirstOrDefault() ?? string.Empty))
                .ToDictionaryAsync(u => u.UserId, u => u, cancellationToken);

        // Per-campus member links (v2 only). One batched query joined to member rows, grouped by instance.
        var membersByInstance = new Dictionary<ulong, List<(VisitGuestMember Member, uint LinkOrder)>>();
        if (visibleInstanceIds.Count > 0)
        {
            var links = await _db.VisitInstanceGuestMembers.AsNoTracking()
                .Where(l => visibleInstanceIds.Contains(l.VisitInstanceId))
                .Join(_db.VisitGuestMembers.AsNoTracking(),
                    l => l.GuestMemberId, m => m.GuestMemberId,
                    (l, m) => new { l.VisitInstanceId, l.DisplayOrder, Member = m })
                .ToListAsync(cancellationToken);
            membersByInstance = links
                .GroupBy(x => x.VisitInstanceId)
                .ToDictionary(g => g.Key, g => g.Select(x => (x.Member, x.DisplayOrder)).ToList());
        }

        // Active (PENDING_APPROVAL) amendment summary per visible instance (v2). Summary only — no JSON.
        var activeAmendmentByInstance = new Dictionary<ulong, ResolvedActiveAmendmentDto>();
        if (visibleInstanceIds.Count > 0)
        {
            var amendments = await _db.VisitInstanceAmendments.AsNoTracking()
                .Where(a => visibleInstanceIds.Contains(a.VisitInstanceId)
                            && a.Status == AmendmentStatuses.PendingApproval)
                .Select(a => new
                {
                    a.AmendmentId,
                    a.VisitInstanceId,
                    a.AmendmentNo,
                    a.Status,
                    a.RequestedAt,
                    ChangedFieldCount = a.Changes.Count
                })
                .ToListAsync(cancellationToken);
            activeAmendmentByInstance = amendments.ToDictionary(a => a.VisitInstanceId, a => new ResolvedActiveAmendmentDto
            {
                AmendmentId = (long)a.AmendmentId,
                AmendmentNo = a.AmendmentNo,
                Status = a.Status,
                RequestedAt = a.RequestedAt,
                ChangedFieldCount = a.ChangedFieldCount
            });
        }

        // Outstanding contact invitations, ONE batched query for every visible campus. The DB allows at
        // most one PENDING change per campus, but read the set rather than assume it — a wrong
        // assumption here would silently drop the resend/cancel actions from a campus that has one.
        var pendingContactChanges = new Dictionary<ulong, PendingContactChangeRow>();
        if (visibleInstanceIds.Count > 0)
        {
            var rows = await _db.VisitRequestIdentityChanges.AsNoTracking()
                .Where(ch => visibleInstanceIds.Contains(ch.VisitInstanceId)
                             && ch.Status == IdentityChangeStatuses.Pending)
                .Select(ch => new
                {
                    ch.VisitInstanceId,
                    ch.ChangeKind,
                    ch.NewEmailMasked,
                    ch.ExpiresAt,
                    ch.ResendCount,
                })
                .ToListAsync(cancellationToken);
            pendingContactChanges = rows
                .GroupBy(r => r.VisitInstanceId)
                .ToDictionary(g => g.Key, g =>
                {
                    var r = g.OrderByDescending(x => x.ExpiresAt).First();
                    return new PendingContactChangeRow(
                        r.ChangeKind, r.NewEmailMasked, r.ExpiresAt, r.ResendCount);
                });
        }

        // 4. Project each visible campus from ITS OWN per-campus detail.
        var campusVisits = new List<ResolvedCampusVisitDto>();
        foreach (var c in visibleInstances)
        {
            string code = "", name = "";
            if (campuses.TryGetValue(c.CampusId, out var ci)) { code = ci.Code; name = ci.Name; }

            string delegationName, visitType, purpose, workingLanguage, mediaStatus;
            string? visitTypeOther, workingContent, transportationNote, notes;
            ResolvedOperationalContactDto opContact;
            List<ResolvedMemberDto> visitors, support;
            uint formRevision, approvalRevision;
            int rowVersion;

            {
                var d = c.FormDetail;
                if (d is null)
                {
                    // Pure V2: every campus instance MUST have exactly one detail row. There is no global
                    // snapshot left to fall back to, so a missing detail is a hard data error — surface it
                    // rather than silently rendering an empty or borrowed campus.
                    _logger.LogError(
                        "Pure V2 consistency error: visit_instance {InstanceId} of request {RequestId} " +
                        "has no visit_instance_form_details row.",
                        c.VisitInstanceId, request.VisitRequestId);
                    throw new ConflictException(
                        "Dữ liệu chuyến thăm theo cơ sở đang thiếu, không thể hiển thị.",
                        VisitFormV2ErrorCodes.VisitFormDetailMissing);
                }

                delegationName = d.DelegationName;
                visitType = d.VisitType;
                visitTypeOther = d.VisitTypeOther;
                purpose = d.Purpose;
                workingContent = d.WorkingContent;
                workingLanguage = d.WorkingLanguage;
                transportationNote = d.TransportationNote;
                mediaStatus = d.MediaConsentStatus;
                notes = d.Notes;

                // Whether this campus's contact IS a delegation member with a picked Partner — read
                // from the SAME per-campus member list built below (membersByInstance), never from a
                // fresh query and never from Organization text. Looked up here, before `linked` is
                // (re)computed for Visitors/SupportMembers just below, using the identical source.
                var contactMembers = membersByInstance.TryGetValue(c.VisitInstanceId, out var cms)
                    ? cms : new List<(VisitGuestMember Member, uint LinkOrder)>();
                bool isOrgInSystem = d.OperationalContactGuestMemberId is ulong contactGmId
                    && contactMembers.FirstOrDefault(x => x.Member.GuestMemberId == contactGmId).Member
                        is { OrganizationPartnerId: not null };

                opContact = new ResolvedOperationalContactDto
                {
                    FullName = d.OperationalContactFullName,
                    // Organization, job title and email are optional (nullable in the DB). Surface ""
                    // not null so the read DTO/JSON contract stays a non-null string for the client —
                    // and so the UI can render the block without any field being present.
                    Organization = d.OperationalContactOrganization ?? string.Empty,
                    JobTitle = d.OperationalContactJobTitle ?? string.Empty,
                    Phone = d.OperationalContactPhone,
                    Email = d.OperationalContactEmail ?? string.Empty,
                    ConfirmationStatus = ContactConfirmationStatusOf(c, pendingContactChanges),
                    ConfirmationSource = c.OperationalContactConfirmationSource,
                    ConfirmedAt = c.OperationalContactConfirmedAt,
                    // Which delegation member this contact IS, when they are one (NP-03). The edit form
                    // restores its "Đầu mối là ai trong đoàn?" selection from this: without it, reopening
                    // a request would show the picker empty and quietly drop a link the user had made.
                    GuestMemberId = d.OperationalContactGuestMemberId is ulong gm ? (long)gm : null,
                    IsOrganizationInSystem = isOrgInSystem,
                };
                formRevision = d.FormRevision;
                approvalRevision = d.ApprovalRevision;
                // The per-campus rowVersion the client echoes back as expectedInstanceRowVersion / expectedRowVersion
                // is the CAMPUS INSTANCE token — that is exactly what pending-edit, safe-edit and amendment all
                // check against (visit_request_campuses.row_version, bumped by campus-approve). The form-detail's
                // own row_version diverges after an approve, so exposing it here would 409 a fresh safe-edit/amendment.
                rowVersion = c.RowVersion;

                var linked = membersByInstance.TryGetValue(c.VisitInstanceId, out var ms)
                    ? ms : new List<(VisitGuestMember Member, uint LinkOrder)>();
                visitors = linked.Where(x => x.Member.MemberType != ExternalSupport)
                    .OrderBy(x => x.LinkOrder).Select(x => MapMember(x.Member)).ToList();
                support = linked.Where(x => x.Member.MemberType == ExternalSupport)
                    .OrderBy(x => x.LinkOrder).Select(x => MapMember(x.Member)).ToList();
            }

            // ── Per-campus capabilities. Measured against THIS campus only: a sibling that is under
            //    way must not close a campus that is still a week out (and vice versa). ──
            var hasPendingAmendment = activeAmendmentByInstance.ContainsKey(c.VisitInstanceId);
            var isLeaderHere = IsCurrentCampusLeader(c.CampusId);
            var isHostHere = c.CurrentHostUserId == userId;
            // Requester side for THIS campus: the registrant (who owns every campus of their request)
            // or the person who confirmed THIS campus. Confirming a sibling grants nothing here.
            var isContactHere = VisitRequestOwnership.IsOperationalContact(c, userId);
            var requesterSideHere = isRegistrant || isContactHere;
            // WHO may edit this campus while it waits for its decision — the SAME resolver the command
            // handler authorizes with, so this capability cannot offer a call that then 403s. It answers
            // two things separately: whether the edit is open at all (a requester-side question, which a
            // STAFF LEADER account answers yes to on their OWN request, whichever campus it names), and
            // whether this actor carries the leader-only privileges inside it (this campus's leader AND
            // the registrant — the pairing behind the flag set further down).
            var pendingEdit = VisitRequestOwnership.ResolvePendingCampusEdit(request, c, _currentUser);
            var instanceCapabilities = new List<VisitActionCapabilityDto>();

            var pendingAmendmentReason = hasPendingAmendment
                ? "Cơ sở này đang có một đề xuất thay đổi chờ duyệt."
                : null;

            // Editing a campus that is still WAITING is the per-campus door, and it is open on a MIXED
            // request where the whole-request edit is not: it asks only about THIS campus. Emitted ONCE,
            // from the resolved relation — the requester side and the leader-registrant used to add it
            // separately, which put the same code in the list twice for anyone who was both.
            if (pendingEdit.CanEdit)
                instanceCapabilities.Add(Decide(
                    VisitMutationAction.EditPendingCampus, VisitFormActions.EditPendingCampus,
                    c, pendingEdit.ViewerRelation!, name, instanceScope: true));

            if (requesterSideHere)
            {
                instanceCapabilities.Add(Decide(
                    VisitMutationAction.SubmitSafeEdit, VisitFormActions.SubmitSafeEdit,
                    c, VisitViewerRelations.Requester, name, instanceScope: true));
                instanceCapabilities.Add(Decide(
                    VisitMutationAction.SubmitAmendment, VisitFormActions.SubmitAmendment,
                    c, VisitViewerRelations.Requester, name, instanceScope: true,
                    overrideReason: pendingAmendmentReason));
            }
            if (isHostHere)
            {
                // Deciding a proposal about this campus belongs to whoever is running it. The Host holds
                // the room, the schedule and the conversation with the guest, so they are the person who
                // can actually say whether a change is workable — and the person the requester is
                // already talking to. It used to be the campus Staff Leader, which routed every small
                // adjustment back through somebody who had handed the campus over days earlier.
                instanceCapabilities.Add(Decide(
                    VisitMutationAction.ApproveAmendment, VisitFormActions.ApproveAmendment,
                    c, VisitViewerRelations.Host, name, instanceScope: true,
                    overrideReason: hasPendingAmendment ? null : "Cơ sở này không có đề xuất nào chờ duyệt."));
            }
            if (isLeaderHere)
            {
                // Editing this campus is NOT here, and leading it grants nothing towards the edit: the
                // door above is requester-side and the resolver has already answered it, for a leader
                // exactly as for anyone else. Approving and rejecting the campus are separate commands
                // with their own actions, and neither asks anything about the registrant — leading the
                // campus is still the whole qualification for deciding it.

                // Transferring the Host presupposes there IS one — before approval the Host arrives with
                // the approval decision, which is a different action on a different screen.
                instanceCapabilities.Add(Decide(
                    VisitMutationAction.TransferHost, VisitFormActions.TransferHost,
                    c, VisitViewerRelations.CampusLeader, name, instanceScope: true,
                    overrideReason: c.CurrentHostUserId is null
                        ? "Cơ sở này chưa có Host để chuyển giao."
                        : null));
            }

            // The flat list stays the ENABLED subset, so it can never contradict the verdicts above.
            var instanceActions = instanceCapabilities.Where(x => x.Enabled).Select(x => x.Code).ToList();
            // Reject travels with approve (one decision, two outcomes); withdraw is the requester's own
            // way out of a proposal and stays open regardless of the cutoff — cancelling a request for a
            // change is never something to keep alive against the requester's wishes.
            if (hasPendingAmendment && isHostHere
                && instanceActions.Contains(VisitFormActions.ApproveAmendment))
                instanceActions.Add(VisitFormActions.RejectAmendment);
            if (hasPendingAmendment && requesterSideHere)
                instanceActions.Add(VisitFormActions.WithdrawAmendment);

            // ── Ordinary campus decision (approve+assign-host / reject) — the SAME two actions the
            //    list/management screen already offers this campus's Staff Leader
            //    (ViewGuestDelegationListQueryHandler.BuildAllowedActions), reproduced here on the
            //    identical four conditions rather than shared through a helper: that handler works off
            //    already-flattened row/relation DTOs it has no VisitRequest/VisitRequestCampus entities
            //    to hand a shared predicate, so matching its checks verbatim is what keeps the two
            //    read models from silently disagreeing, without reshaping either one's data model for
            //    this alone. NOT modelled as a Decide()/VisitActionCapabilityDto: unlike edit/amendment/
            //    transfer, ordinary approve and reject do not go through VisitMutationPolicy at all —
            //    ApproveCampusInstanceCommandHandler and RejectCampusInstanceCommandHandler re-authorize
            //    independently on exactly these checks (leader of THIS campus, request not cancelled,
            //    contact gate open, campus still WAITING_REQUEST_APPROVAL), so the read model only needs
            //    to OFFER — never to decide. EDIT right and DECISION right are deliberately different
            //    questions: `pendingEdit.CanEdit` above answers the first, this answers the second, and a
            //    leader who is not the registrant gets this without ever getting EditPendingCampus. ──
            if (isLeaderHere
                && request.Status != VisitRequestStatuses.Cancelled
                && !VisitRequestStatuses.IsBehindContactGate(request.Status)
                && c.Status == VisitInstanceStatuses.WaitingRequestApproval)
            {
                instanceActions.Add(VisitListActions.ApproveAndAssignHost);
                instanceActions.Add(VisitListActions.CampusReject);
            }

            // ── This campus's contact workflow, offered only where its handler would accept the call. ──
            pendingContactChanges.TryGetValue(c.VisitInstanceId, out var pendingChange);
            instanceActions.AddRange(ContactActionsFor(
                request, c, pendingChange, isRegistrant, isContactHere, now));

            campusVisits.Add(new ResolvedCampusVisitDto
            {
                VisitInstanceId = (long)c.VisitInstanceId,
                CampusId = (long)c.CampusId,
                CampusCode = code,
                CampusName = name,
                PlannedStartAt = c.PlannedStartAt,
                PlannedEndAt = c.PlannedEndAt,
                InstanceStatus = c.Status,
                CurrentHostUserId = c.CurrentHostUserId.HasValue ? (long)c.CurrentHostUserId.Value : null,
                CurrentHostName = NameOf(c.CurrentHostUserId),
                CurrentHost = BuildCurrentHost(c, hostPeople),
                ProposedHost = BuildProposedHost(c, hostPeople, name),
                HostSelection = BuildHostSelectionCapabilities(c, isLeaderHere, userId, code),
                DecidedByUserId = c.DecidedBy.HasValue ? (long)c.DecidedBy.Value : null,
                DecidedByName = NameOf(c.DecidedBy),
                DecidedAt = c.DecidedAt,
                DecisionActorRole = c.DecisionActorRole,
                DecisionNote = c.DecisionNote,
                CancelledByUserId = c.CancelledBy,
                CancelledByName = NameOf(c.CancelledBy),
                CancelledAt = c.CancelledAt,
                CancellationActorType = c.CancellationActorType,
                CancellationSource = c.CancellationSource,
                CancellationReason = c.CancellationReason,
                DelegationName = delegationName,
                VisitType = visitType,
                VisitTypeOther = visitTypeOther,
                Purpose = purpose,
                WorkingContent = workingContent,
                Visitors = visitors,
                SupportMembers = support,
                OperationalContact = opContact,
                WorkingLanguage = workingLanguage,
                TransportationNote = transportationNote,
                MediaConsentStatus = mediaStatus,
                Notes = notes,
                ContactState = new ResolvedCampusContactStateDto
                {
                    Confirmed = c.OperationalContactUserId is not null,
                    ConfirmedAt = c.OperationalContactConfirmedAt,
                    ConfirmationSource = c.OperationalContactConfirmationSource,
                    IsCurrentUser = isContactHere,
                    PendingChangeKind = pendingChange?.Kind,
                    PendingEmailMasked = pendingChange?.EmailMasked,
                    PendingExpiresAt = pendingChange?.ExpiresAt,
                    PendingResendCount = pendingChange?.ResendCount ?? 0,
                },
                FormRevision = formRevision,
                ApprovalRevision = approvalRevision,
                RowVersion = rowVersion,
                ActiveAmendment = activeAmendmentByInstance.TryGetValue(c.VisitInstanceId, out var am) ? am : null,
                AllowedActions = instanceActions,
                Capabilities = instanceCapabilities,
                // Requester side AND current Host of the same campus: there is nobody else to wait for,
                // so a submitted change is decided in the same call. The label changes; the amendment,
                // its validation and its history do not.
                AmendmentSelfApproves = requesterSideHere && isHostHere,
                // The leader-only privileges INSIDE the pending edit — the 72-hour override and "Lưu và
                // duyệt" — which need the leader of THIS campus who is also the registrant. It tracks
                // exactly what the handler will accept, so neither a leader on somebody else's request
                // nor a registrant editing a campus they do not lead can be shown a dialog or a button
                // offering something the API would refuse.
                CanOverrideScheduleLeadTime = pendingEdit.ActsAsCampusLeader,
                // Own field, own value straight from the already-computed relation — see the DTO's own
                // doc comment for why this is not read off CanOverrideScheduleLeadTime.
                CanSaveAndApprove = pendingEdit.CanSaveAndApprove,
            });
        }

        // ── Confirmation-gate progress, counted over the campuses this caller may see ─────────────
        // Counted from the visible set on purpose: a Staff Leader who sees one campus must not learn
        // how many siblings exist or where they stand. For the registrant and HO the visible set is
        // the whole request, so they get the real totals.
        var summary = BuildConfirmationSummary(request, visibleInstances, pendingContactChanges, now);

        // ── Request-wide outcome, for full-scope callers ONLY ────────────────────────────────────
        // Counted over EVERY campus, and withheld from anyone who cannot see every campus. A scoped
        // caller has no way to tell their own slice apart from the whole request, so handing them a
        // slice-derived verdict is how "this campus refused" became "every campus refused".
        var outcome = scope.CanViewAllCampuses ? BuildRequestOutcome(request, instances) : null;

        // ── Request-level capabilities ───────────────────────────────────────────────────────────
        // A request-level action touches data every campus shares, so it needs BOTH halves of the
        // multi-campus rule: refuse outright if any campus has passed the point of no return, and take
        // the deadline from the earliest campus still ahead. Only the first half used to exist for
        // pending-edit, and neither existed for safe-edit — which is why "Sửa nhanh" appeared on a
        // request whose delegation was already inside the building.
        //
        // The REGISTRANT alone gets them. A campus's operational contact runs that campus; rewriting
        // the request-level part (or resubmitting the whole thing) is not theirs to do, and letting
        // one campus's contact do it is precisely what the per-campus model exists to prevent.
        var requestCapabilities = new List<VisitActionCapabilityDto>();
        if (isRegistrant && instances.Count > 0)
        {
            // A request-level action is ALL-OR-NOTHING across campuses (§13): it edits data every
            // campus shares, so a request where only some campuses qualify offers nothing at request
            // level — the qualifying campuses each keep their own instance-scoped action instead.
            //
            // So the governing campus is: the first one that DISAGREES with the action's requirement
            // if there is one (the policy then refuses, naming that campus and its reason), otherwise
            // the EARLIEST qualifying campus (whose start sets the deadline). Note that an empty
            // qualifying set falls back to the earliest campus overall, never to "no campus, allow" —
            // that hole is what let a request-level edit through when no campus was active.
            VisitRequestCampus Governing(Func<VisitRequestCampus, bool> qualifies)
            {
                var dissenting = instances.Where(c => !qualifies(c)).OrderBy(c => c.PlannedStartAt).ToList();
                if (dissenting.Count > 0)
                {
                    // Prefer a campus that has actually moved past the point of no return, so the
                    // message says "đang diễn ra" rather than a milder reason that also applies.
                    return dissenting.FirstOrDefault(c => VisitMutationPolicy.BlocksRequestLevel(c.Status))
                        ?? dissenting[0];
                }
                return instances.OrderBy(c => c.PlannedStartAt).First();
            }

            void AddRequestCapability(VisitMutationAction action, string code, Func<VisitRequestCampus, bool> qualifies)
            {
                var governing = Governing(qualifies);
                requestCapabilities.Add(Decide(
                    action, code, governing, VisitViewerRelations.Requester,
                    campuses.TryGetValue(governing.CampusId, out var gi) ? gi.Name : null,
                    instanceScope: false));
            }

            // Either pre-decision stage — the registrant may correct a request that is still waiting on
            // its operational contacts just as much as one waiting on approval (VisitMutationPolicy
            // agrees, and so does the command guard).
            AddRequestCapability(
                VisitMutationAction.EditPendingRequest, VisitFormActions.EditPendingRequest,
                c => c.Status is VisitInstanceStatuses.WaitingContactConfirmation
                               or VisitInstanceStatuses.WaitingRequestApproval);
            AddRequestCapability(
                VisitMutationAction.ResubmitRejectedRequest, VisitFormActions.ResubmitRejectedRequest,
                c => c.Status == VisitInstanceStatuses.Rejected);
            AddRequestCapability(
                VisitMutationAction.SubmitSafeEdit, VisitFormActions.SubmitSafeEdit,
                c => c.Status is VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit);
        }

        List<string> BuildRequestActions()
        {
            // Contact actions are NOT here: every one of them names a campus, so they live on the
            // campus that owns them. A request-level list of them is what let the UI offer an action
            // without saying which campus it would hit.
            var actions = new List<string> { VisitFormActions.View };
            // Reading the request and reading its change history are two different permissions, and the
            // second is narrower. Asked of the SAME resolver the history endpoints use, so the section
            // is offered exactly when the API would serve it — a supporting participant who can open
            // this page gets VIEW without VIEW_CHANGE_HISTORY, which is the correct pair of answers.
            if (VisitHistoryVisibility.Resolve(request, _currentUser).CanViewHistory)
                actions.Add(VisitFormActions.ViewChangeHistory);
            actions.AddRange(requestCapabilities.Where(x => x.Enabled).Select(x => x.Code));
            return actions;
        }

        return new ResolvedVisitFormDto
        {
            VisitRequestId = (long)request.VisitRequestId,
            RequestCode = request.RequestCode,
            RowVersion = request.RowVersion,
            HasMixedCampusDetails = request.HasMixedCampusDetails,
            VisitScope = request.VisitScope,
            RequestStatus = request.Status,
            CreatedSource = request.CreatedSource,
            SubmittedAt = request.SubmittedAt,
            PartnerId = request.PartnerId.HasValue ? (long)request.PartnerId.Value : null,
            CancelledByUserId = request.CancelledBy,
            CancelledByName = NameOf(request.CancelledBy),
            CancelledAt = request.CancelledAt,
            CancellationReason = request.CancellationReason,
            Registrant = new ResolvedRegistrantDto
            {
                FullName = request.RegistrantFullName,
                Organization = request.RegistrantOrganization,
                JobTitle = request.RegistrantJobTitle,
                Phone = request.RegistrantPhone,
                Email = request.RegistrantEmail,
                Nationality = request.RegistrantNationality
            },
            ConfirmationSummary = summary,
            RequestOutcome = outcome,
            CampusVisits = campusVisits,
            Viewer = new ResolvedViewerContextDto
            {
                Relation = scope.Relation,
                CanViewAllCampuses = scope.CanViewAllCampuses,
                IsReadOnly = scope.IsReadOnly,
                AllowedActions = BuildRequestActions(),
                Capabilities = requestCapabilities,
            }
        };
    }

    /// <summary>Resend cap for ONE campus's invitation (OperationalContactGuards.MaxResends).</summary>
    private const int MaxContactResends = 5;

    /// <summary>The outstanding invitation of one campus, as the read model needs it.</summary>
    private sealed record PendingContactChangeRow(
        string Kind, string? EmailMasked, DateTime ExpiresAt, uint ResendCount);

    /// <summary>A host-side person's display details, batched once for every visible campus.</summary>
    private sealed record HostPersonRow(
        ulong UserId, string FullName, string Email, string Phone, string DepartmentName);

    /// <summary>
    /// Where THIS campus's guest-side contact stands, as a single word the UI can label. Derived from
    /// the campus row and its outstanding invitation rather than stored, so it can never disagree with
    /// <see cref="ResolvedCampusContactStateDto"/> beside it.
    /// </summary>
    private static string ContactConfirmationStatusOf(
        VisitRequestCampus c, IReadOnlyDictionary<ulong, PendingContactChangeRow> pending)
    {
        var hasPending = pending.TryGetValue(c.VisitInstanceId, out var change);

        // An outstanding TRANSFER while somebody still holds the campus is not "unconfirmed" — the
        // present holder keeps every right until the new person accepts.
        if (c.OperationalContactUserId is not null)
            return hasPending && change!.Kind == IdentityChangeKinds.Transfer
                ? "TRANSFER_PENDING"
                : "CONFIRMED";

        // Nobody holds the campus. The two ways that happens are NOT the same situation and used to
        // report the same word:
        //
        //   PENDING              — an invitation is out; somebody is being waited on.
        //   NO_ACTIVE_INVITATION — nobody is being waited on. The invitation was cancelled (or expired
        //                          and swept) and no new one has been sent.
        //
        // Both leave the gate shut, but only the second one requires the registrant to DO something,
        // and calling it "pending" told them to sit and wait for an email nobody was going to answer.
        return hasPending ? "PENDING" : "NO_ACTIVE_INVITATION";
    }

    private static ResolvedCurrentHostDto? BuildCurrentHost(
        VisitRequestCampus c, IReadOnlyDictionary<ulong, HostPersonRow> people)
    {
        if (c.CurrentHostUserId is null) return null;
        if (!people.TryGetValue(c.CurrentHostUserId.Value, out var p))
            return new ResolvedCurrentHostDto { UserId = (long)c.CurrentHostUserId.Value };

        return new ResolvedCurrentHostDto
        {
            UserId = (long)p.UserId,
            FullName = p.FullName,
            Email = p.Email,
            Phone = p.Phone,
            DepartmentName = p.DepartmentName,
        };
    }

    /// <summary>
    /// The proposal, if there is one worth showing. Returned even after a successful activation so a
    /// reader can see HOW the host got there, but the UI is told which state it is in via
    /// <c>ProposalStatus</c> and shows the "Host dự kiến" heading only while it is still PENDING.
    /// </summary>
    private static ResolvedProposedHostDto? BuildProposedHost(
        VisitRequestCampus c, IReadOnlyDictionary<ulong, HostPersonRow> people, string campusName)
    {
        if (c.ProposedHostUserId is null) return null;

        people.TryGetValue(c.ProposedHostUserId.Value, out var p);
        return new ResolvedProposedHostDto
        {
            UserId = (long)c.ProposedHostUserId.Value,
            FullName = p?.FullName ?? string.Empty,
            OrganizationOrDepartment = string.IsNullOrWhiteSpace(p?.DepartmentName)
                ? campusName
                : p!.DepartmentName,
            SelectionMode = c.HostSelectionMode,
            ProposalStatus = c.ProposedHostActivationStatus,
            ProposedAt = c.ProposedHostAt,
        };
    }

    /// <summary>
    /// What the caller may do about this campus's host. Only the campus's own Staff Leader, and an IC
    /// Staff acting on their own campus, have anything here — and only while the campus is still
    /// pre-decision, because after that the host changes through the handover flow, never through a
    /// proposal (plan §5.3).
    /// </summary>
    private ResolvedHostSelectionCapabilitiesDto BuildHostSelectionCapabilities(
        VisitRequestCampus c, bool isLeaderHere, ulong userId, string campusCode)
    {
        var preDecision = c.Status is VisitInstanceStatuses.WaitingContactConfirmation
                                   or VisitInstanceStatuses.WaitingRequestApproval;
        if (!preDecision)
            return new ResolvedHostSelectionCapabilitiesDto();

        var isStaffHere = _currentUser.RoleCode == RoleCodes.Staff
                          && _currentUser.SubRole == UserSubRoles.Staff
                          && _currentUser.PrimaryCampusId == c.CampusId;

        if (isLeaderHere)
            return new ResolvedHostSelectionCapabilitiesDto
            {
                CanProposeSelfAsHost = true,
                CanProposeOtherHost = true,
                CanWaitForLaterAssignment = true,
                CanUpdateProposedHost = true,
            };

        // A regular IC Staff may only speak for a proposal that is about them: their own, or a still
        // empty slot they could take. Somebody else's pick is the Leader's to change.
        if (isStaffHere)
            return new ResolvedHostSelectionCapabilitiesDto
            {
                CanProposeSelfAsHost = true,
                CanProposeOtherHost = false,
                CanWaitForLaterAssignment = true,
                CanUpdateProposedHost = c.ProposedHostUserId is null || c.ProposedHostUserId == userId,
            };

        return new ResolvedHostSelectionCapabilitiesDto { CanWaitForLaterAssignment = false };
    }

    /// <summary>
    /// The operational-contact actions for ONE campus, each granted only when the corresponding command
    /// handler would accept the call — same actor test, same lifecycle window, same resend cap, same
    /// one-pending-change rule. The handlers still re-authorize independently; this only decides what
    /// the UI may offer, so a button can no longer promise something the backend will refuse.
    ///
    /// <para>
    /// Everything here is measured against THIS campus: its own status, its own invitation. The
    /// previous version asked the request instead, so a campus that was still days away lost its
    /// transfer button because a sibling had already started.
    /// </para>
    /// <para>
    /// Every MUTATION verdict mirrors its handler's guard exactly, which for REPLACE vs TRANSFER is now
    /// two facts, not one: the persisted campus status (the same whitelist the guards apply), AND
    /// whether a confirmed holder already exists — <c>OperationalContactUserId</c>. A campus can sit at
    /// WAITING_REQUEST_APPROVAL with a real, confirmed contact (that is how it got there), so status
    /// alone can no longer tell REPLACE territory apart from TRANSFER territory. The clock is consulted
    /// for one thing only: whether an outstanding invitation has run out. That split is deliberate; a
    /// read model that answered "may this be changed" with a countdown could disagree with the handler
    /// over a campus neither had looked at.
    /// </para>
    /// </summary>
    private List<string> ContactActionsFor(
        VisitRequest request,
        VisitRequestCampus instance,
        PendingContactChangeRow? pending,
        bool isRegistrant,
        bool isContactHere,
        DateTime now)
    {
        var actions = new List<string>();

        // The commands all 404 when v2 WRITE is disabled, so with the flag off the UI must not offer
        // them either. Absent options (unit construction) means "not disabled".
        if (_writeFlag?.Enabled == false)
            return actions;
        // Only the registrant or the person who already holds THIS campus can reach any of it.
        if (!isRegistrant && !isContactHere)
            return actions;
        if (request.Status == VisitRequestStatuses.Cancelled)
            return actions;

        // ── A REJECTED campus has no contact workflow left — but it does have one thing its guest side
        //    can still do: ask for it to be looked at again. Offered here, from the SERVER, so the
        //    browser never has to infer the right from a role (plan v11 §5.2). ──
        if (instance.Status == VisitInstanceStatuses.Rejected)
        {
            actions.Add(VisitFormActions.ResubmitRejectedInstance);
            return actions;
        }

        if (instance.Status == VisitInstanceStatuses.Cancelled)
            return actions;

        // ── Every contact MUTATION closes when the visit starts, and the test is the persisted status
        //    alone — mirrors EnsureProfileUpdateAllowed. A campus that is running, finished or closed
        //    has a contact record the visit was received against; correcting it afterwards is editing
        //    history, not the plan. Cleanup of an outstanding invitation is a different thing and
        //    survives below. ──
        var contactMutable = instance.Status is
            VisitInstanceStatuses.WaitingContactConfirmation
            or VisitInstanceStatuses.WaitingRequestApproval
            or VisitInstanceStatuses.Assigned
            or VisitInstanceStatuses.BeforeVisit;

        // ── Correcting the contact's DETAILS, to the registrant and to the person currently holding
        //    the campus — mirrors EnsureProfileUpdateAllowed + EnsureMayManageContact(allowCurrentContact:
        //    true). Listed first because it is the common case: most contact edits are a corrected phone
        //    number, not a change of person. ──
        if (contactMutable)
            actions.Add(VisitFormActions.UpdateOperationalContactProfile);

        // ── No confirmed holder → REPLACE territory (registrant only; EnsureReplaceWindowOpen). Whether
        //    a campus has a confirmed holder — not its decision status — is what separates REPLACE from
        //    TRANSFER: a WAITING_REQUEST_APPROVAL campus already has a real holder (that is how it got
        //    there), so it is handover territory exactly like an ASSIGNED one. ──
        var hasConfirmedHolder = instance.OperationalContactUserId is not null;
        var replaceable = instance.Status is VisitInstanceStatuses.WaitingContactConfirmation
            or VisitInstanceStatuses.WaitingRequestApproval;
        if (isRegistrant && replaceable && !hasConfirmedHolder)
            actions.Add(VisitFormActions.ReplaceOperationalContact);

        // ── Confirmed holder, not yet started → TRANSFER territory (registrant or the current holder).
        //    Mirrors EnsureTransferWindowOpen: lifecycle ONLY, no clock, and no requirement that the
        //    campus be decided. A handover a minute before the start is offered while the campus still
        //    reads BEFORE_VISIT, because the handler accepts it. Deliberately NOT
        //    VisitInstanceStatuses.DecidedNotStarted — that constant is shared with the unrelated
        //    Host-transfer feature (TRANSFER_HOST), and widening it would change host-transfer
        //    eligibility too. ──
        var transferable = instance.Status is VisitInstanceStatuses.WaitingRequestApproval
            or VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit;
        if (hasConfirmedHolder && transferable && pending is null)
            actions.Add(VisitFormActions.InitiateOperationalContactTransfer);

        if (pending is null)
        {
            // ── No live invitation, and nobody holds the campus: the registrant has to send a NEW one.
            //    This is the state a cancel leaves behind, and it used to offer nothing at all — the
            //    contact form re-saved with the same address is classified as an unchanged address, so
            //    it mints no token and sends no mail, and the registrant was stuck with a campus that
            //    could never confirm. Offered only where ReinviteOperationalContactConfirmation would
            //    accept the call: registrant, replace window open, no confirmed contact. ──
            if (isRegistrant && replaceable && !hasConfirmedHolder)
                actions.Add(VisitFormActions.ReinviteOperationalContactConfirmation);
            return actions;
        }

        // Resend stops at the cap rather than offering a button that returns RATE_LIMITED, and never
        // resurrects an invitation that has already expired. For a TRANSFER it also asks the lifecycle
        // question again, because resending one renews its expiry and mints a fresh link — the handler
        // refuses that once the campus has started, so offering it would be a button that 409s.
        var mayResend = pending.ExpiresAt > now
            && pending.ResendCount < MaxContactResends
            && (pending.Kind != IdentityChangeKinds.Transfer || transferable);
        if (mayResend)
            actions.Add(VisitFormActions.ResendOperationalContactConfirmation);
        // Cancel is CLEANUP, not mutation: it settles the pending invitation and leaves whoever holds
        // the campus holding it. So it stays available after the mutation window closes — a campus that
        // started with a stale handover in flight must still be able to close it.
        actions.Add(VisitFormActions.CancelOperationalContactChange);
        return actions;
    }

    /// <summary>
    /// Gate progress over the campuses the caller may see. "Pending" counts campuses with no confirmed
    /// contact whose invitation is still live; "expired" those whose invitation has run out; "declined"
    /// is carried by the change rows and only shows once nothing is outstanding — a campus that was
    /// declined and re-invited is pending again, which is the truthful reading.
    /// </summary>
    /// <summary>
    /// The request-wide verdict, over EVERY campus. Only ever called for a caller with full-request
    /// scope — see <see cref="ResolvedVisitFormDto.RequestOutcome"/> for why that matters.
    /// </summary>
    private static ResolvedRequestOutcomeDto BuildRequestOutcome(
        VisitRequest request, IReadOnlyList<VisitRequestCampus> all)
    {
        int Count(params string[] statuses) => all.Count(c => statuses.Contains(c.Status));
        var accepted = Count(VisitInstanceStatuses.Assigned);
        var inProgress = Count(
            VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit, VisitInstanceStatuses.AfterVisit);
        // A campus still at the contact gate has not been put to a Staff Leader yet, but from the
        // request's point of view it is the same answer: nobody has decided it.
        var waiting = Count(
            VisitInstanceStatuses.WaitingContactConfirmation, VisitInstanceStatuses.WaitingRequestApproval);
        var rejected = Count(VisitInstanceStatuses.Rejected);
        var cancelled = Count(VisitInstanceStatuses.Cancelled);
        var closed = Count(VisitInstanceStatuses.Closed);

        string code;
        if (all.Count == 0) code = "NO_CAMPUS";
        else if (request.Status == VisitRequestStatuses.Cancelled) code = "ALL_CANCELLED";
        else if (rejected == all.Count) code = "ALL_REJECTED";
        else if (waiting == all.Count) code = "ALL_WAITING";
        else if (rejected + cancelled > 0) code = "MIXED";
        else code = "IN_PROGRESS";

        return new ResolvedRequestOutcomeDto
        {
            Code = code,
            Total = all.Count,
            Accepted = accepted,
            InProgress = inProgress,
            Waiting = waiting,
            Rejected = rejected,
            Cancelled = cancelled,
            Closed = closed,
        };
    }

    private static ResolvedConfirmationSummaryDto BuildConfirmationSummary(
        VisitRequest request,
        IReadOnlyList<VisitRequestCampus> visible,
        IReadOnlyDictionary<ulong, PendingContactChangeRow> pendingByInstance,
        DateTime now)
    {
        var active = visible.Where(c => c.Status != VisitInstanceStatuses.Cancelled).ToList();
        var confirmed = active.Count(c => c.OperationalContactUserId is not null);

        var pending = 0;
        var expired = 0;
        foreach (var c in active.Where(c => c.OperationalContactUserId is null))
        {
            if (pendingByInstance.TryGetValue(c.VisitInstanceId, out var change) && change.ExpiresAt > now)
                pending++;
            else
                expired++;
        }

        return new ResolvedConfirmationSummaryDto
        {
            Total = active.Count,
            Confirmed = confirmed,
            Pending = pending,
            Expired = expired,
            // A campus with no live invitation and no contact is stuck rather than declined; the
            // difference matters to the registrant, who has to act on it either way.
            Declined = 0,
            GateOpen = !VisitRequestStatuses.IsBehindContactGate(request.Status),
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<ulong, VisitCampusFormContent>> ResolveCampusFormContentAsync(
        VisitRequest request, IReadOnlyList<ulong> visibleInstanceIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<ulong, VisitCampusFormContent>();
        if (visibleInstanceIds.Count == 0)
            return result;

        // Pure V2: read ONLY the per-campus detail + instance-member links, and ONLY for the visible
        // instances. Two batched queries irrespective of campus/member count (no per-campus N+1).
        // Each instance keeps its OWN operational contact and its OWN member links — there is no
        // request-level content to share across campuses any more.
        var details = await _db.VisitInstanceFormDetails.AsNoTracking()
            .Where(d => visibleInstanceIds.Contains(d.VisitInstanceId))
            .ToListAsync(cancellationToken);
        var detailByInstance = details.ToDictionary(d => d.VisitInstanceId);

        var links = await _db.VisitInstanceGuestMembers.AsNoTracking()
            .Where(l => visibleInstanceIds.Contains(l.VisitInstanceId))
            .Join(_db.VisitGuestMembers.AsNoTracking(),
                l => l.GuestMemberId, m => m.GuestMemberId,
                (l, m) => new { l.VisitInstanceId, l.DisplayOrder, Member = m })
            .ToListAsync(cancellationToken);
        var membersByInstance = links
            .GroupBy(x => x.VisitInstanceId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.DisplayOrder).Select(x => x.Member).ToList());

        foreach (var instanceId in visibleInstanceIds)
        {
            if (!detailByInstance.TryGetValue(instanceId, out var d))
            {
                // Pure V2: every campus instance MUST have exactly one detail row, and there is no global
                // snapshot left to fall back to — fail loudly instead of returning borrowed content.
                _logger.LogError(
                    "Pure V2 consistency error: visit_instance {InstanceId} of request {RequestId} " +
                    "has no visit_instance_form_details row.",
                    instanceId, request.VisitRequestId);
                throw new ConflictException(
                    "Dữ liệu chuyến thăm theo cơ sở đang thiếu, không thể hiển thị.",
                    VisitFormV2ErrorCodes.VisitFormDetailMissing);
            }

            var linked = membersByInstance.TryGetValue(instanceId, out var ms) ? ms : new List<VisitGuestMember>();

            // Same rule as ResolveAsync's ResolvedOperationalContactDto.IsOrganizationInSystem: the
            // contact must BE a linked delegation member of THIS instance, and that member must carry
            // an OrganizationPartnerId. Reuses `linked` — no extra query, no name matching.
            var contactMember = d.OperationalContactGuestMemberId is ulong contactGmId
                ? linked.FirstOrDefault(m => m.GuestMemberId == contactGmId)
                : null;

            result[instanceId] = new VisitCampusFormContent
            {
                DelegationName = d.DelegationName,
                VisitType = d.VisitType,
                VisitTypeOther = d.VisitTypeOther,
                Purpose = d.Purpose,
                WorkingContent = d.WorkingContent,
                WorkingLanguage = d.WorkingLanguage,
                MediaConsentStatus = d.MediaConsentStatus,
                Notes = d.Notes,
                TransportationNote = d.TransportationNote,
                OperationalContact = new VisitFormOperationalContact
                {
                    FullName = d.OperationalContactFullName,
                    Organization = d.OperationalContactOrganization,
                    JobTitle = d.OperationalContactJobTitle,
                    Phone = d.OperationalContactPhone,
                    Email = d.OperationalContactEmail,
                    IsOrganizationInSystem = contactMember?.OrganizationPartnerId is not null,
                },
                Visitors = linked.Where(m => m.MemberType != ExternalSupport).Select(ToRow).ToList(),
                SupportMembers = linked.Where(m => m.MemberType == ExternalSupport).Select(ToRow).ToList(),
                FormRevision = d.FormRevision,
                ApprovalRevision = d.ApprovalRevision,
                RowVersion = d.RowVersion
            };
        }
        return result;
    }

    private static VisitFormMemberRow ToRow(VisitGuestMember m) => new(
        (long)m.GuestMemberId, m.MemberType, m.FullName, m.Organization, m.JobTitle, m.Nationality,
        (int)m.DisplayOrder, m.OrganizationPartnerId);

    private static ResolvedMemberDto MapMember(VisitGuestMember m) => new()
    {
        GuestMemberId = (long)m.GuestMemberId,
        MemberType = m.MemberType,
        FullName = m.FullName,
        Organization = m.Organization,
        OrganizationPartnerId = m.OrganizationPartnerId,
        JobTitle = m.JobTitle,
        Nationality = m.Nationality,
        DisplayOrder = (int)m.DisplayOrder
    };

    private sealed record VisitFormScope(
        HashSet<ulong> AuthorizedInstanceIds,
        string Relation,
        bool CanViewAllCampuses,
        bool IsReadOnly);

    /// <summary>
    /// Batched scope computation (§6). Two extra queries at most (participants + assigned logistics),
    /// regardless of the number of campuses. Never loads all campuses to filter on the client.
    /// </summary>
    private async Task<VisitFormScope> ComputeScopeAsync(
        VisitRequest request, List<VisitRequestCampus> instances, List<ulong> instanceIds,
        ulong userId, CancellationToken cancellationToken)
    {
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;
        var primaryCampusId = _currentUser.PrimaryCampusId;

        var isHo = roleCode == RoleCodes.Ho;
        var isAdmin = roleCode == RoleCodes.Admin;
        var isStaffLeader = roleCode == RoleCodes.Staff
            && string.Equals(subRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase);
        var isRegistrant = VisitRequestOwnership.IsRegistrant(request, userId);

        // Admin has no visit business access, ever.
        if (isAdmin)
            return new VisitFormScope(new HashSet<ulong>(), VisitInstanceAccess.None, false, false);

        // Whole-request viewers. The registrant is the only non-HO one left: holding a campus as its
        // operational contact shows THAT campus and nothing else, which is handled below.
        if (isHo)
            return new VisitFormScope(instanceIds.ToHashSet(), VisitInstanceAccess.Ho, true, true);
        if (isRegistrant)
            return new VisitFormScope(instanceIds.ToHashSet(), "REGISTRANT", true, false);

        var authorized = new HashSet<ulong>();
        string relation = VisitInstanceAccess.None;

        // Operational contact → only the campuses they actually confirmed. An unconfirmed invitee has
        // operational_contact_user_id NULL and therefore matches nothing here: until they accept, the
        // masked landing page is all they get.
        var operated = VisitRequestOwnership.OperatedCampuses(request, userId)
            .Select(c => c.VisitInstanceId).ToList();
        foreach (var id in operated) authorized.Add(id);
        if (operated.Count > 0) relation = VisitInstanceAccess.OperationalContact;

        // Staff Leader → only their own-campus instance(s). Campus responsibility is the whole rule:
        // a leader of another campus is still a stranger to this request and gets nothing here.
        //
        // The GLOBAL confirmation gate is deliberately NOT asked. It used to be ANDed on, which made
        // the detail 403 for a leader whose own campus had already confirmed just because a sibling
        // had not — so a leader could neither see nor decide. The gate withholds the DECISION only:
        // it is asked where approve/reject are OFFERED (the instanceActions block further up, on the
        // same four conditions as the list) and where they are EXECUTED (CampusApprovalExecutor,
        // RejectCampusInstanceCommandHandler). Reading a request that is still collecting its
        // operational contacts changes nothing and is exactly what a leader needs in order to know
        // one is coming.
        //
        // The registrant branch returned above, so a Staff Leader who submitted this request still
        // gets the whole request through REGISTRANT rather than this one-campus reviewer scope.
        if (isStaffLeader && primaryCampusId.HasValue)
        {
            foreach (var c in instances.Where(c => c.CampusId == primaryCampusId.Value))
                authorized.Add(c.VisitInstanceId);
            if (authorized.Count > 0) relation = VisitInstanceAccess.StaffLeader;
        }

        // Host → only instances they host.
        var hosted = instances.Where(c => c.CurrentHostUserId == userId).Select(c => c.VisitInstanceId).ToList();
        foreach (var id in hosted) authorized.Add(id);
        if (hosted.Count > 0 && relation == VisitInstanceAccess.None) relation = VisitInstanceAccess.Host;

        // Participant → invited/accepted/assigned rows grant visibility of that instance.
        var participantRows = new List<ParticipantRow>();
        if (instanceIds.Count > 0)
        {
            var raw = await _db.VisitParticipants.AsNoTracking()
                .Where(p => instanceIds.Contains(p.VisitInstanceId)
                            && p.UserId == userId
                            && (p.Status == ParticipantStatuses.Invited
                                || p.Status == ParticipantStatuses.Accepted
                                || p.Status == ParticipantStatuses.Assigned))
                .Select(p => new { p.VisitInstanceId, p.ParticipantRole, p.Status, p.IsHost })
                .ToListAsync(cancellationToken);
            participantRows = raw
                .Select(p => new ParticipantRow(p.VisitInstanceId, p.ParticipantRole, p.Status, p.IsHost))
                .ToList();
        }
        foreach (var p in participantRows) authorized.Add(p.InstanceId);

        // Department staff → instances where they hold an assigned logistics item.
        var isDepartmentStaff = roleCode == RoleCodes.Department
            || (roleCode == RoleCodes.Staff && string.Equals(subRole, UserSubRoles.Staff, StringComparison.OrdinalIgnoreCase));
        if (isDepartmentStaff && instanceIds.Count > 0)
        {
            var logisticsInstanceIds = await _db.VisitLogisticsItems.AsNoTracking()
                .Where(l => instanceIds.Contains(l.VisitInstanceId) && l.AssignedToUserId == userId)
                .Select(l => l.VisitInstanceId)
                .ToListAsync(cancellationToken);
            foreach (var id in logisticsInstanceIds) authorized.Add(id);
        }

        // Relation label from the highest supporting participant role (accepted, non-host).
        if (relation == VisitInstanceAccess.None)
        {
            var accepted = participantRows.FirstOrDefault(p => p.Status == ParticipantStatuses.Accepted && !p.IsHost);
            relation = accepted?.Role switch
            {
                ParticipantRoles.IcSupport => VisitInstanceAccess.IcSupport,
                ParticipantRoles.DeptSupport => VisitInstanceAccess.DeptSupport,
                ParticipantRoles.Student => VisitInstanceAccess.Student,
                _ => authorized.Count > 0 ? VisitInstanceAccess.DeptSupport : VisitInstanceAccess.None
            };
        }

        return new VisitFormScope(authorized, relation, CanViewAllCampuses: false, IsReadOnly: false);
    }

    private sealed record ParticipantRow(ulong InstanceId, string Role, string Status, bool IsHost);
}
