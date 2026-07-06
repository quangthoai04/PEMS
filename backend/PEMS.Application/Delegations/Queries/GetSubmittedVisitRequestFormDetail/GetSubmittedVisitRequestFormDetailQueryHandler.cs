using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.GetSubmittedVisitRequestFormDetail;

/// <summary>
/// Returns the guest-submitted form snapshot for a visit request, enforcing campus-independent
/// approval visibility (SQL v10):
///   • HO            → monitor/read-only on every request (never any decision action).
///   • Staff Leader  → any request with an instance of THEIR campus (single or multi, any status,
///                     immediately after submit); only their own-campus instance is surfaced on
///                     multi-campus requests.
///   • Visitor       → only the request they own (any status / scope).
///   • Everyone else (Admin, regular Staff, Department, Student) ⇒ 403 unless host/participant.
///
/// Strictly read-only: only visit_requests / visit_request_campuses / visit_guest_members are
/// touched. Host-created data (agendas, participants, logistics, minutes) is never read here.
/// </summary>
public sealed class GetSubmittedVisitRequestFormDetailQueryHandler
    : IRequestHandler<GetSubmittedVisitRequestFormDetailQuery, SubmittedVisitRequestFormDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetSubmittedVisitRequestFormDetailQueryHandler> _logger;

    public GetSubmittedVisitRequestFormDetailQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<GetSubmittedVisitRequestFormDetailQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<SubmittedVisitRequestFormDetailDto> Handle(
        GetSubmittedVisitRequestFormDetailQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("Current user is not authenticated.");

        var userId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;

        var isHo = roleCode == RoleCodes.Ho;
        var isStaffLeader = roleCode == RoleCodes.Staff
            && string.Equals(subRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase);
        var isDepartmentStaff = roleCode == RoleCodes.Department
            && string.Equals(subRole, UserSubRoles.Staff, StringComparison.OrdinalIgnoreCase);
        var isVisitor = roleCode == RoleCodes.Visitor;

        // Admin / Department / Student never see the guest form. HO, Staff Leader, Visitor, and a
        // regular Staff who HOSTS an instance of this request may. The host relation needs the
        // request's campus instances, so it is verified after the request loads (below).
        var isStaff = roleCode == RoleCodes.Staff;
        var isStudent = roleCode == RoleCodes.Student;
        if (!isHo && !isVisitor && !isStaff && !isDepartmentStaff && !isStudent)
            throw new ForbiddenException("Bạn không có quyền xem chi tiết đơn này.");

        var visitRequest = await _context.VisitRequests
            .Include(vr => vr.CampusInstances)
            .Include(vr => vr.GuestMembers)
            .FirstOrDefaultAsync(vr => vr.VisitRequestId == request.VisitRequestId, cancellationToken);

        if (visitRequest == null)
        {
            var matchingInstance = await _context.VisitRequestCampuses
                .AnyAsync(c => c.VisitInstanceId == request.VisitRequestId, cancellationToken);
            _logger.LogWarning(
                "SubmittedFormDetail not found. InputId = {InputId}. MatchesAVisitInstanceId = {MatchesInstance}. " +
                "Possible cause: frontend passed visitInstanceId instead of visitRequestId.",
                request.VisitRequestId, matchingInstance);
            throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);
        }

        var isMulti = visitRequest.VisitScope == VisitScopes.MultiCampus;
        var isSingle = visitRequest.VisitScope == VisitScopes.SingleCampus;
        var status = visitRequest.Status;
        var instanceIds = visitRequest.CampusInstances.Select(c => c.VisitInstanceId).ToList();
        var departmentStaffAssignedInstanceIds = new HashSet<ulong>();
        if (isDepartmentStaff)
        {
            var logisticsInstanceIds = await _context.VisitLogisticsItems
                .Where(l => instanceIds.Contains(l.VisitInstanceId) && l.AssignedToUserId == userId)
                .Select(l => l.VisitInstanceId)
                .ToListAsync(cancellationToken);
            var participantInstanceIds = await _context.VisitParticipants
                .Where(p => instanceIds.Contains(p.VisitInstanceId)
                    && p.UserId == userId
                    && p.AssignedBy != null
                    && p.Status != ParticipantStatuses.Removed)
                .Select(p => p.VisitInstanceId)
                .ToListAsync(cancellationToken);
            departmentStaffAssignedInstanceIds = logisticsInstanceIds.Concat(participantInstanceIds).ToHashSet();
        }

        // The Staff Leader's own-campus instance (if any). Used for both scope checks and flags.
        var primaryCampusId = _currentUser.PrimaryCampusId;
        var ownInstance = isStaffLeader && primaryCampusId.HasValue
            ? visitRequest.CampusInstances.FirstOrDefault(c => c.CampusId == primaryCampusId.Value)
            : null;

        // A regular Staff assigned as the official host of one (or more) of this request's campus
        // instances may view the guest form read-only to set up. (Staff Leader handled above.)
        var hostedInstances = visitRequest.CampusInstances.Where(c => c.CurrentHostUserId == userId).ToList();
        var isHost = !isStaffLeader && hostedInstances.Count > 0;

        var isParticipant = await _context.VisitParticipants
            .AnyAsync(p => instanceIds.Contains(p.VisitInstanceId)
                        && p.UserId == userId
                        && (p.Status == ParticipantStatuses.Invited
                            || p.Status == ParticipantStatuses.Accepted
                            || p.Status == ParticipantStatuses.Assigned
                            || p.Status == ParticipantStatuses.Declined),
                      cancellationToken);

        // ── Scope enforcement ──
        if (isHo)
        {
            // HO has global read-only access to all requests for audit purposes.
        }
        else if (isStaffLeader)
        {
            if (!primaryCampusId.HasValue)
                throw new ForbiddenException("Tài khoản Staff Leader chưa được gán cơ sở.");

            // Campus-independent approval: the Staff Leader sees any request that has an
            // instance of THEIR campus — single or multi, any status, right after submit.
            if (ownInstance == null)
                throw new ForbiddenException("Đơn không có cơ sở thuộc phạm vi của bạn.");
        }
        else if (isVisitor)
        {
            var owns = visitRequest.VisitorUserId == userId || visitRequest.CreatedBy == userId;
            if (!owns)
                throw new ForbiddenException("Bạn chỉ được xem đơn của chính mình.");
        }
        else if (isHost)
        {
            // Verified above: caller is the official host of ≥1 campus instance of this request.
            // Allowed read-only; only their hosted instance(s) are surfaced (no other-campus leak).
        }
        else if (isDepartmentStaff && departmentStaffAssignedInstanceIds.Count > 0)
        {
            // Allowed read-only for assigned logistics/participants.
        }
        else if (isParticipant)
        {
            // Allowed read-only.
        }
        else
        {
            throw new ForbiddenException("Bạn không có quyền xem chi tiết đơn này.");
        }

        // ── Resolve display names: campuses + decided-by user ──
        var campusIds = visitRequest.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
        var campuses = campusIds.Count == 0
            ? new Dictionary<ulong, (string Code, string Name)>()
            : await _context.Campuses
                .Where(c => campusIds.Contains(c.CampusId))
                .Select(c => new { c.CampusId, c.CampusCode, c.Name })
                .ToDictionaryAsync(c => c.CampusId, c => (Code: c.CampusCode, Name: c.Name), cancellationToken);

        // For a Staff Leader on a MULTI_CAMPUS request we only surface their own campus instance
        // (they have no business with the other campuses). HO sees every campus; Visitor (owner)
        // sees all; Staff Leader on SINGLE_CAMPUS sees the single instance.
        var visibleInstances = (isStaffLeader && isMulti)
            ? visitRequest.CampusInstances.Where(c => c.CampusId == primaryCampusId).ToList()
            : isHost
                ? hostedInstances
                : isDepartmentStaff && !isParticipant
                    ? visitRequest.CampusInstances.Where(c => departmentStaffAssignedInstanceIds.Contains(c.VisitInstanceId)).ToList()
                    : visitRequest.CampusInstances.ToList();

        // Resolve every "who decided / who cancelled" name in one round-trip (per-instance
        // decision actors, request-level canceller, and each visible campus-instance canceller).
        var actorIds = new List<ulong>();
        if (visitRequest.CancelledBy.HasValue) actorIds.Add(visitRequest.CancelledBy.Value);
        if (visitRequest.LastResubmittedBy.HasValue) actorIds.Add(visitRequest.LastResubmittedBy.Value);
        actorIds.AddRange(visibleInstances.Where(c => c.CancelledBy.HasValue).Select(c => c.CancelledBy!.Value));
        actorIds.AddRange(visibleInstances.Where(c => c.DecidedBy.HasValue).Select(c => c.DecidedBy!.Value));
        actorIds.AddRange(visibleInstances.Where(c => c.CurrentHostUserId.HasValue).Select(c => c.CurrentHostUserId!.Value));
        actorIds = actorIds.Distinct().ToList();

        var actorNames = actorIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _context.Users
                .Where(u => actorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        string? NameOf(ulong? id) =>
            id.HasValue && actorNames.TryGetValue(id.Value, out var n) ? n : null;

        // Request-level decision mirror (campus-independent approval): the decision fields live
        // on each instance; surface the caller-relevant one (Staff Leader's own instance, or the
        // sole instance of a single-campus request).
        var decisionInstance = ownInstance
            ?? (visibleInstances.Count == 1 ? visibleInstances[0] : null);
        var decidedByName = NameOf(decisionInstance?.DecidedBy);

        // ── Footer action flags (campus-independent approval): only the campus Staff Leader
        // decides, per instance, while it awaits their decision. Approve = approve + assign host. ──
        bool canApprove = false, canReject = false, canCancel = false;
        if (status != VisitRequestStatuses.Cancelled
            && isStaffLeader && ownInstance != null
            && ownInstance.Status == VisitInstanceStatuses.WaitingRequestApproval)
        {
            canApprove = canReject = true;
        }

        // Cancel (UC-136) is offered on this read-only form only to the Visitor owner of an
        // approved/partially-approved request that still has an active (ASSIGNED/BEFORE_VISIT)
        // instance. The cancel command re-checks the time window and ownership — UI hint only.
        if (isVisitor
            && (status == VisitRequestStatuses.Approved || status == VisitRequestStatuses.PartiallyApproved)
            && visibleInstances.Any(c => c.Status == VisitInstanceStatus.Assigned
                || c.Status == VisitInstanceStatus.BeforeVisit))
        {
            canCancel = true;
        }

        // ── Cancellation info (request-level vs campus-instance-level) ──
        bool isCancelled = false;
        string? cancellationLevel = null;
        long? cancelledByUserId = null;
        string? cancelledByName = null;
        DateTime? cancelledAt = null;
        string? cancellationActorType = null;
        string? cancellationSource = null;
        string? cancellationReason = null;

        if (status == VisitRequestStatuses.Cancelled)
        {
            // Whole request was cancelled. Actor type / source live on the cancelled instances
            // (the request row only stores who/when/reason); borrow them from any cancelled one.
            var sourceInstance = visibleInstances.FirstOrDefault(c => c.Status == VisitInstanceStatus.Cancelled);
            isCancelled = true;
            cancellationLevel = "REQUEST";
            cancelledByUserId = visitRequest.CancelledBy.HasValue ? (long)visitRequest.CancelledBy.Value : null;
            cancelledByName = NameOf(visitRequest.CancelledBy);
            cancelledAt = visitRequest.CancelledAt;
            cancellationReason = visitRequest.CancellationReason;
            cancellationActorType = sourceInstance?.CancellationActorType;
            cancellationSource = sourceInstance?.CancellationSource;
        }
        else
        {
            // Request still active but one of the visible campus instances may be cancelled
            // (partial multi-campus cancel, or a host instance-cancel).
            var cancelledInstance = visibleInstances
                .Where(c => c.Status == VisitInstanceStatus.Cancelled)
                .OrderByDescending(c => c.CancelledAt)
                .FirstOrDefault();
            if (cancelledInstance != null)
            {
                isCancelled = true;
                cancellationLevel = "CAMPUS_INSTANCE";
                cancelledByUserId = cancelledInstance.CancelledBy.HasValue ? (long)cancelledInstance.CancelledBy.Value : null;
                cancelledByName = NameOf(cancelledInstance.CancelledBy);
                cancelledAt = cancelledInstance.CancelledAt;
                cancellationActorType = cancelledInstance.CancellationActorType;
                cancellationSource = cancelledInstance.CancellationSource;
                cancellationReason = cancelledInstance.CancellationReason;
            }
        }

        var dto = new SubmittedVisitRequestFormDetailDto
        {
            VisitRequestId = (long)visitRequest.VisitRequestId,
            RequestCode = visitRequest.RequestCode,
            CreatedSource = visitRequest.CreatedSource,
            SubmittedAt = visitRequest.SubmittedAt,
            EmailVerifiedAt = visitRequest.EmailVerifiedAt,
            RequestStatus = status,
            VisitScope = visitRequest.VisitScope,

            DelegationName = visitRequest.DelegationName,
            VisitType = visitRequest.VisitType,
            VisitTypeOther = visitRequest.VisitTypeOther,
            Purpose = visitRequest.Purpose,
            WorkingContent = visitRequest.WorkingContent,

            Registrant = new SubmittedRegistrantDto
            {
                FullName = visitRequest.RegistrantFullName,
                Organization = visitRequest.RegistrantOrganization,
                JobTitle = visitRequest.RegistrantJobTitle,
                Phone = visitRequest.RegistrantPhone,
                Email = visitRequest.RegistrantEmail,
                Nationality = visitRequest.RegistrantNationality,
            },
            ContactPerson = new SubmittedContactPersonDto
            {
                FullName = visitRequest.ContactPersonFullName,
                Organization = visitRequest.ContactPersonOrganization,
                Phone = visitRequest.ContactPersonPhone,
                Email = visitRequest.ContactPersonEmail,
            },

            WorkingLanguage = visitRequest.WorkingLanguage,
            MediaConsentStatus = visitRequest.MediaConsentStatus,
            MediaConsentNote = visitRequest.MediaConsentNote,
            TransportationNote = visitRequest.TransportationNote,
            NoteToFptu = visitRequest.NoteToFptu,

            Campuses = visibleInstances
                .OrderBy(c => c.PlannedStartAt)
                .Select(c => new SubmittedCampusScheduleDto
                {
                    VisitInstanceId = (long)c.VisitInstanceId,
                    CampusId = (long)c.CampusId,
                    CampusCode = campuses.TryGetValue(c.CampusId, out var ci) ? ci.Code : "",
                    CampusName = campuses.TryGetValue(c.CampusId, out var cn) ? cn.Name : "",
                    PlannedStartAt = c.PlannedStartAt,
                    PlannedEndAt = c.PlannedEndAt,
                    InstanceStatus = c.Status,
                    CoordinatorUserId = c.CoordinatorUserId.HasValue ? (long)c.CoordinatorUserId.Value : null,
                    CurrentHostUserId = c.CurrentHostUserId.HasValue ? (long)c.CurrentHostUserId.Value : null,
                    CurrentHostName = NameOf(c.CurrentHostUserId),
                    IsOwnCampus = primaryCampusId.HasValue && c.CampusId == primaryCampusId.Value,
                    DecidedByUserId = c.DecidedBy.HasValue ? (long)c.DecidedBy.Value : null,
                    DecidedByName = NameOf(c.DecidedBy),
                    DecidedAt = c.DecidedAt,
                    DecisionActorRole = c.DecisionActorRole,
                    DecisionNote = c.DecisionNote,
                    CancelledByUserId = c.CancelledBy.HasValue ? (long)c.CancelledBy.Value : null,
                    CancelledByName = NameOf(c.CancelledBy),
                    CancelledAt = c.CancelledAt,
                    CancellationActorType = c.CancellationActorType,
                    CancellationSource = c.CancellationSource,
                    CancellationReason = c.CancellationReason,
                })
                .ToList(),

            CampusDecisionSummary = new CampusDecisionSummaryDto
            {
                Total = visitRequest.CampusInstances.Count,
                Pending = visitRequest.CampusInstances.Count(c => c.Status == VisitInstanceStatus.WaitingRequestApproval),
                Approved = visitRequest.CampusInstances.Count(c =>
                    c.Status == VisitInstanceStatus.Assigned
                    || c.Status == VisitInstanceStatus.BeforeVisit
                    || c.Status == VisitInstanceStatus.DuringVisit
                    || c.Status == VisitInstanceStatus.AfterVisit
                    || c.Status == VisitInstanceStatus.Closed),
                Rejected = visitRequest.CampusInstances.Count(c => c.Status == VisitInstanceStatus.Rejected),
                Cancelled = visitRequest.CampusInstances.Count(c => c.Status == VisitInstanceStatus.Cancelled),
            },

            GuestMembers = visitRequest.GuestMembers
                .Where(m => m.MemberType != "EXTERNAL_SUPPORT")
                .OrderBy(m => m.DisplayOrder)
                .Select(MapMember)
                .ToList(),
            ExternalSupportMembers = visitRequest.GuestMembers
                .Where(m => m.MemberType == "EXTERNAL_SUPPORT")
                .OrderBy(m => m.DisplayOrder)
                .Select(MapMember)
                .ToList(),

            DecidedByUserId = decisionInstance?.DecidedBy is { } dby ? (long)dby : null,
            DecidedByName = decidedByName,
            DecisionActorRole = decisionInstance?.DecisionActorRole,
            DecidedAt = decisionInstance?.DecidedAt,
            DecisionNote = decisionInstance?.DecisionNote,

            IsCancelled = isCancelled,
            CancellationLevel = cancellationLevel,
            CancelledByUserId = cancelledByUserId,
            CancelledByName = cancelledByName,
            CancelledAt = cancelledAt,
            CancellationActorType = cancellationActorType,
            CancellationSource = cancellationSource,
            CancellationReason = cancellationReason,

            ResubmissionCount = (int)visitRequest.ResubmissionCount,
            LastResubmittedAt = visitRequest.LastResubmittedAt,
            LastResubmittedBy = visitRequest.LastResubmittedBy.HasValue ? (long)visitRequest.LastResubmittedBy.Value : null,
            LastResubmittedByName = NameOf(visitRequest.LastResubmittedBy),

            CanApprove = canApprove,
            CanReject = canReject,
            CanCancel = canCancel,
        };

        return dto;
    }

    private static SubmittedGuestMemberDto MapMember(Domain.Entities.Delegations.VisitGuestMember m) => new()
    {
        GuestMemberId = (long)m.GuestMemberId,
        MemberType = m.MemberType,
        FullName = m.FullName,
        Organization = m.Organization,
        JobTitle = m.JobTitle,
        Nationality = m.Nationality,
        DisplayOrder = (int)m.DisplayOrder,
    };
}
