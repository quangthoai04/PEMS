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
/// Returns the guest-submitted form snapshot for a visit request, enforcing canonical v10
/// visibility:
///   • HO            → MULTI_CAMPUS only (any status). SINGLE_CAMPUS ⇒ 403.
///   • Staff Leader  → SINGLE_CAMPUS of own campus (any status: pending/approved/rejected/cancelled);
///                     MULTI_CAMPUS of own campus ONLY after HO approval (status APPROVED or a
///                     post-approval terminal like CANCELLED). MULTI_CAMPUS PENDING_APPROVAL or
///                     REJECTED-by-HO ⇒ 403.
///   • Visitor       → only the request they own (any status / scope).
///   • Everyone else (Admin, regular Staff, Department, Student) ⇒ 403.
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
        var isVisitor = roleCode == RoleCodes.Visitor;

        // Admin / Department / Student never see the guest form. HO, Staff Leader, Visitor, and a
        // regular Staff who HOSTS an instance of this request may. The host relation needs the
        // request's campus instances, so it is verified after the request loads (below).
        var isStaff = roleCode == RoleCodes.Staff;
        if (!isHo && !isVisitor && !isStaff)
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

        // The Staff Leader's own-campus instance (if any). Used for both scope checks and flags.
        var primaryCampusId = _currentUser.PrimaryCampusId;
        var ownInstance = isStaffLeader && primaryCampusId.HasValue
            ? visitRequest.CampusInstances.FirstOrDefault(c => c.CampusId == primaryCampusId.Value)
            : null;

        // A regular Staff assigned as the official host of one (or more) of this request's campus
        // instances may view the guest form read-only to set up. (Staff Leader handled above.)
        var hostedInstances = visitRequest.CampusInstances.Where(c => c.CurrentHostUserId == userId).ToList();
        var isHost = !isStaffLeader && hostedInstances.Count > 0;

        // ── Scope enforcement ──
        if (isHo)
        {
            if (!isMulti)
                throw new ForbiddenException("HO chỉ được xem đơn liên cơ sở.");
        }
        else if (isStaffLeader)
        {
            if (!primaryCampusId.HasValue)
                throw new ForbiddenException("Tài khoản Staff Leader chưa được gán cơ sở.");

            if (isSingle)
            {
                if (ownInstance == null)
                    throw new ForbiddenException("Đơn không thuộc cơ sở của bạn.");
            }
            else if (isMulti)
            {
                // Before HO decides (PENDING) or after HO rejects (REJECTED), the request is
                // NOT released to the campus — Staff Leader has nothing to do with it.
                if (status == VisitRequestStatuses.PendingApproval)
                    throw new ForbiddenException("Staff Leader không được xem đơn liên cơ sở khi chưa được HO duyệt.");
                if (status == VisitRequestStatuses.Rejected)
                    throw new ForbiddenException("Đơn liên cơ sở đã bị HO từ chối, không thuộc phạm vi của bạn.");
                if (ownInstance == null)
                    throw new ForbiddenException("Đơn không có cơ sở thuộc phạm vi của bạn.");
            }
            else
            {
                throw new ForbiddenException("Phạm vi đơn không hợp lệ.");
            }
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
                : visitRequest.CampusInstances.ToList();

        // Resolve every "who decided / who cancelled" name in one round-trip (decision actor,
        // request-level canceller, and each visible campus-instance canceller).
        var actorIds = new List<ulong>();
        if (visitRequest.DecidedBy.HasValue) actorIds.Add(visitRequest.DecidedBy.Value);
        if (visitRequest.CancelledBy.HasValue) actorIds.Add(visitRequest.CancelledBy.Value);
        actorIds.AddRange(visibleInstances.Where(c => c.CancelledBy.HasValue).Select(c => c.CancelledBy!.Value));
        actorIds = actorIds.Distinct().ToList();

        var actorNames = actorIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _context.Users
                .Where(u => actorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        string? NameOf(ulong? id) =>
            id.HasValue && actorNames.TryGetValue(id.Value, out var n) ? n : null;

        var decidedByName = NameOf(visitRequest.DecidedBy);

        // ── Footer action flags ──
        bool canApprove = false, canReject = false, canAssignHost = false, canCancel = false;
        if (status == VisitRequestStatuses.PendingApproval)
        {
            if (isHo && isMulti)
            {
                canApprove = canReject = true;
            }
            else if (isStaffLeader && isSingle
                && ownInstance != null
                && ownInstance.Status == VisitInstanceStatuses.WaitingRequestApproval)
            {
                canApprove = canReject = true;
            }
        }
        else if (status == VisitRequestStatuses.Approved && isStaffLeader && ownInstance != null
            && ownInstance.Status == VisitInstanceStatus.WaitingHostAssignment)
        {
            canAssignHost = true;
        }

        // Cancel (UC-136) is offered on this read-only form only to the Visitor owner of an
        // approved request that still has an active (ASSIGNED/BEFORE_VISIT) instance. HO/Staff
        // Leader never cancel here (pre-approval ⇒ reject; post-approval cancel is out of scope).
        // The cancel command re-checks the time window and ownership — this flag is a UI hint only.
        if (isVisitor && status == VisitRequestStatuses.Approved
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
            TransportationType = visitRequest.TransportationType,
            TransportationDetail = visitRequest.TransportationDetail,
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
                    IsOwnCampus = primaryCampusId.HasValue && c.CampusId == primaryCampusId.Value,
                    CancelledByUserId = c.CancelledBy.HasValue ? (long)c.CancelledBy.Value : null,
                    CancelledByName = NameOf(c.CancelledBy),
                    CancelledAt = c.CancelledAt,
                    CancellationActorType = c.CancellationActorType,
                    CancellationSource = c.CancellationSource,
                    CancellationReason = c.CancellationReason,
                })
                .ToList(),

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

            DecidedByUserId = visitRequest.DecidedBy.HasValue ? (long)visitRequest.DecidedBy.Value : null,
            DecidedByName = decidedByName,
            DecisionActorRole = visitRequest.DecisionActorRole,
            DecidedAt = visitRequest.DecidedAt,
            DecisionNote = visitRequest.DecisionNote,

            IsCancelled = isCancelled,
            CancellationLevel = cancellationLevel,
            CancelledByUserId = cancelledByUserId,
            CancelledByName = cancelledByName,
            CancelledAt = cancelledAt,
            CancellationActorType = cancellationActorType,
            CancellationSource = cancellationSource,
            CancellationReason = cancellationReason,

            CanApprove = canApprove,
            CanReject = canReject,
            CanCancel = canCancel,
            CanAssignHost = canAssignHost,
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
