using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.GetVisitProcessPermissions;

public sealed class GetVisitProcessPermissionsQueryHandler
    : IRequestHandler<GetVisitProcessPermissionsQuery, VisitProcessPermissionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitProcessPermissionsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<VisitProcessPermissionDto> Handle(
        GetVisitProcessPermissionsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var visit = instance.VisitRequest;

        // The signed-in user's relation to this campus instance (precedence: Host > Staff Leader of
        // the campus > HO > Visitor owner > accepted non-host participant).
        var acceptedParticipantRole = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                && p.UserId == userId
                && p.Status == ParticipantStatuses.Accepted
                && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        bool isHost = instance.CurrentHostUserId == userId;
        bool isStaffLeaderOfCampus = roleCode == RoleCodes.Staff
            && string.Equals(subRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
            && _currentUser.PrimaryCampusId == instance.CampusId;
        bool isHo = roleCode == RoleCodes.Ho;
        bool isVisitorOwner = roleCode == RoleCodes.Visitor && visit.VisitorUserId == userId;
        bool isAcceptedParticipant = acceptedParticipantRole != null;

        bool inScope = isHost || isStaffLeaderOfCampus || isHo || isVisitorOwner || isAcceptedParticipant;
        if (!inScope)
            throw new ForbiddenException("Bạn không có quyền xem chi tiết tiếp khách này.");

        var relation = isHost ? "HOST"
            : isStaffLeaderOfCampus ? "STAFF_LEADER"
            : isHo ? "HO"
            : isVisitorOwner ? "VISITOR_OWNER"
            : acceptedParticipantRole switch
            {
                ParticipantRoles.IcSupport => "IC_SUPPORT",
                ParticipantRoles.DeptSupport => "DEPT_SUPPORT",
                ParticipantRoles.Student => "STUDENT",
                _ => "NONE",
            };

        bool hostAssigned = instance.CurrentHostUserId != null;
        bool isClosed = instance.Status == VisitInstanceStatus.Closed;
        bool isCancelled = visit.Status == VisitRequestStatuses.Cancelled || instance.Status == VisitInstanceStatus.Cancelled;
        bool isApproved = visit.Status == VisitRequestStatuses.Approved;
        bool isLive = !isClosed && !isCancelled;

        // Only the Host edits the operational tabs (Trước/Trong/Sau), and only while the visit is live.
        bool hostCanEdit = isHost && isLive;
        // Minutes: Host OR an accepted (IC/Dept/Student) participant — never Visitor/HO/Staff Leader.
        bool minutesEditor = (isHost || isAcceptedParticipant) && isLive;
        // News: Host, accepted IC Staff, or accepted Student. Department participant does NOT create news.
        bool newsCreator = (isHost
            || (isAcceptedParticipant && (acceptedParticipantRole == ParticipantRoles.IcSupport
                                          || acceptedParticipantRole == ParticipantRoles.Student)))
            && isLive;

        // A pure Visitor owner must NEVER see the internal operational tabs (Before/During/After,
        // Minutes) of a CANCELLED campus — only the public-safe original request / cancellation reason.
        // Internal roles (Host / Staff Leader / HO / accepted participant) keep read-only visibility
        // to review what had been prepared before the cancellation.
        bool internalViewer = isHost || isStaffLeaderOfCampus || isHo || isAcceptedParticipant;
        bool visitorInternalBlocked = isVisitorOwner && !internalViewer && isCancelled;
        bool canViewInternalTabs = !visitorInternalBlocked;

        return new VisitProcessPermissionDto
        {
            VisitInstanceId = instance.VisitInstanceId,
            VisitRequestId = visit.VisitRequestId,
            RequestStatus = visit.Status,
            InstanceStatus = instance.Status,
            Relation = relation,
            HostAssigned = hostAssigned,

            // Original request + overview are public-safe (Visitor may see them even when cancelled).
            CanViewOriginalRequest = true,
            CanViewOverview = true,

            // Internal operational tabs: hidden from a Visitor on a CANCELLED campus.
            CanViewBeforeVisit = canViewInternalTabs,
            CanEditBeforeVisit = hostCanEdit,

            CanViewDuringVisit = canViewInternalTabs,
            CanEditDuringVisit = hostCanEdit,

            CanViewAfterVisit = canViewInternalTabs,
            CanEditAfterVisit = hostCanEdit,

            CanAssignHost = isStaffLeaderOfCampus && isApproved
                && instance.Status == VisitInstanceStatus.WaitingHostAssignment && !hostAssigned,

            CanViewMinutes = canViewInternalTabs,
            CanCreateMinutes = minutesEditor,
            CanEditMinutes = minutesEditor,

            CanViewNews = true,
            CanCreateNews = newsCreator,

            // Operational stage transitions — Host only, live instance (see CompleteVisitStageCommand).
            CanStartVisit = isHost && isLive
                && (instance.Status == VisitInstanceStatus.Assigned || instance.Status == VisitInstanceStatus.BeforeVisit),
            CanCompleteVisit = isHost && isLive && instance.Status == VisitInstanceStatus.DuringVisit,
            // Đóng đoàn nằm ở tab "Sau tiếp khách": Host đóng khi đoàn đã kết thúc (AFTER_VISIT).
            CanCloseVisit = isHost && isLive && instance.Status == VisitInstanceStatus.AfterVisit,
        };
    }
}
