using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Services.VisitFormRead;

/// <inheritdoc cref="IVisitFormReadService"/>
public sealed class VisitFormReadService : IVisitFormReadService
{
    private const string ExternalSupport = "EXTERNAL_SUPPORT";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<VisitFormReadService> _logger;
    private readonly IDateTimeService? _clock;

    /// <summary>Self-service amendment cutoff — mirrors VisitAmendmentService.SelfServiceCutoffHours.</summary>
    private const int SelfServiceCutoffHours = 24;

    public VisitFormReadService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<VisitFormReadService> logger,
        IDateTimeService? clock = null)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
        _clock = clock;
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

        // Action computation (viewer.allowedActions + per-instance allowedActions). These MIRROR the
        // authorization each command handler enforces — the handlers still re-authorize independently.
        var now = _clock?.VietnamNow ?? DateTime.Now;
        var requesterSide = request.RegistrantUserId == userId
            || (request.VisitorUserId == userId
                && request.PrimaryContactAccessStatus == PrimaryContactAccessStatuses.Active);
        bool IsCurrentCampusLeader(ulong campusId) =>
            _currentUser.RoleCode == RoleCodes.Staff
            && _currentUser.SubRole == UserSubRoles.Leader
            && _currentUser.PrimaryCampusId == campusId;
        // SUBMIT_AMENDMENT: requester side, ASSIGNED/BEFORE_VISIT, ≥24h out, no pending amendment
        // (VisitAmendmentService.SubmitAsync rules). APPROVE/REJECT: current campus leader + pending.
        // WITHDRAW: requester side + pending.
        List<string> InstanceActions(VisitRequestCampus c, bool hasPendingAmendment)
        {
            var actions = new List<string>();
            if (requesterSide)
            {
                var amendable = c.Status is VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit;
                if (amendable && !hasPendingAmendment
                    && c.PlannedStartAt >= now.AddHours(SelfServiceCutoffHours))
                    actions.Add(VisitFormActions.SubmitAmendment);
                if (hasPendingAmendment)
                    actions.Add(VisitFormActions.WithdrawAmendment);
            }
            if (hasPendingAmendment && IsCurrentCampusLeader(c.CampusId))
            {
                actions.Add(VisitFormActions.ApproveAmendment);
                actions.Add(VisitFormActions.RejectAmendment);
            }
            return actions;
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

        // 4. Project each visible campus from ITS OWN per-campus detail.
        var campusVisits = new List<ResolvedCampusVisitDto>();
        foreach (var c in visibleInstances)
        {
            string code = "", name = "";
            if (campuses.TryGetValue(c.CampusId, out var ci)) { code = ci.Code; name = ci.Name; }

            string delegationName, visitType, purpose, workingLanguage, mediaStatus;
            string? visitTypeOther, workingContent, transportationNote, mediaNote, noteToFptu;
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
                mediaNote = d.MediaConsentNote;
                noteToFptu = d.NoteToFptu;
                opContact = new ResolvedOperationalContactDto
                {
                    FullName = d.OperationalContactFullName,
                    // Organization + email are optional (nullable in the DB). Surface "" not null so the
                    // read DTO/JSON contract stays a non-null string for the client.
                    Organization = d.OperationalContactOrganization ?? string.Empty,
                    Phone = d.OperationalContactPhone,
                    Email = d.OperationalContactEmail ?? string.Empty
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
                MediaConsentNote = mediaNote,
                NoteToFptu = noteToFptu,
                FormRevision = formRevision,
                ApprovalRevision = approvalRevision,
                RowVersion = rowVersion,
                ActiveAmendment = activeAmendmentByInstance.TryGetValue(c.VisitInstanceId, out var am) ? am : null,
                AllowedActions = InstanceActions(c, activeAmendmentByInstance.ContainsKey(c.VisitInstanceId))
            });
        }

        // EDIT_PENDING: fully pending + ≥24h out. RESUBMIT: rejected. SUBMIT_SAFE_EDIT: v2 + not cancelled.
        // (Mirrors UpdatePendingVisitRequestV2 / ResubmitRejectedVisitRequestV2 / SubmitVisitSafeEdit handlers.)
        List<string> BuildRequestActions()
        {
            var actions = new List<string> { VisitFormActions.View };
            if (!requesterSide) return actions;
            var fullyPending = instances.Count > 0
                && request.Status == VisitRequestStatuses.PendingApproval
                && instances.All(c => c.Status == VisitInstanceStatuses.WaitingRequestApproval);
            if (fullyPending && instances.Min(c => c.PlannedStartAt) >= now.AddHours(SelfServiceCutoffHours))
                actions.Add(VisitFormActions.EditPendingRequest);
            if (request.Status == VisitRequestStatuses.Rejected)
                actions.Add(VisitFormActions.ResubmitRejectedRequest);
            if (request.Status != VisitRequestStatuses.Cancelled)
                actions.Add(VisitFormActions.SubmitSafeEdit);
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
            PrimaryContact = new ResolvedPrimaryContactDto
            {
                FullName = request.ContactPersonFullName,
                Organization = request.ContactPersonOrganization,
                Phone = request.ContactPersonPhone,
                Email = request.ContactPersonEmail,
                AccessStatus = request.PrimaryContactAccessStatus,
                VerifiedAt = request.PrimaryContactVerifiedAt
            },
            CampusVisits = campusVisits,
            Viewer = new ResolvedViewerContextDto
            {
                Relation = scope.Relation,
                CanViewAllCampuses = scope.CanViewAllCampuses,
                IsReadOnly = scope.IsReadOnly,
                AllowedActions = BuildRequestActions()
            }
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
            result[instanceId] = new VisitCampusFormContent
            {
                DelegationName = d.DelegationName,
                VisitType = d.VisitType,
                VisitTypeOther = d.VisitTypeOther,
                Purpose = d.Purpose,
                WorkingContent = d.WorkingContent,
                WorkingLanguage = d.WorkingLanguage,
                MediaConsentStatus = d.MediaConsentStatus,
                MediaConsentNote = d.MediaConsentNote,
                TransportationNote = d.TransportationNote,
                NoteToFptu = d.NoteToFptu,
                OperationalContact = new VisitFormOperationalContact
                {
                    FullName = d.OperationalContactFullName,
                    Organization = d.OperationalContactOrganization,
                    Phone = d.OperationalContactPhone,
                    Email = d.OperationalContactEmail
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
        (long)m.GuestMemberId, m.MemberType, m.FullName, m.Organization, m.JobTitle, m.Nationality, (int)m.DisplayOrder);

    private static ResolvedMemberDto MapMember(VisitGuestMember m) => new()
    {
        GuestMemberId = (long)m.GuestMemberId,
        MemberType = m.MemberType,
        FullName = m.FullName,
        Organization = m.Organization,
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
        var isRegistrant = request.RegistrantUserId == userId;
        var isOwner = roleCode == RoleCodes.Visitor && request.VisitorUserId == userId;

        // Admin has no visit business access, ever.
        if (isAdmin)
            return new VisitFormScope(new HashSet<ulong>(), VisitInstanceAccess.None, false, false);

        // Whole-request viewers.
        if (isHo)
            return new VisitFormScope(instanceIds.ToHashSet(), VisitInstanceAccess.Ho, true, true);
        if (isOwner)
            return new VisitFormScope(instanceIds.ToHashSet(), VisitInstanceAccess.VisitorOwner, true, false);
        if (isRegistrant)
            return new VisitFormScope(instanceIds.ToHashSet(), "REGISTRANT", true, false);

        var authorized = new HashSet<ulong>();
        string relation = VisitInstanceAccess.None;

        // Staff Leader → only their own-campus instance(s).
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
