using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Policies;
using PEMS.Shared;

using PEMS.Application.Delegations.Common;
namespace PEMS.Application.Delegations.Queries.GetVisitProcessPermissions;

public sealed class GetVisitProcessPermissionsQueryHandler
    : IRequestHandler<GetVisitProcessPermissionsQuery, VisitProcessPermissionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    // The earliest-start gate is a TIME rule, so this handler needs a clock. It used to have none,
    // which is precisely why CanStartVisit could not express the rule and the button was offered days
    // early — the frontend then asked for a transition the command refuses.
    private readonly IDateTimeService _clock;

    public GetVisitProcessPermissionsQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
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
            // Media consent lives on the per-campus detail, and the news eligibility verdict below
            // reads it. Without the include it is null, which the evaluator correctly treats as
            // "consent not agreed" — and every campus would lose its create-news flag.
            .Include(c => c.FormDetail)
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
        bool isGuestSide = VisitRequestOwnership.IsGuestSide(visit, instance, userId);
        bool isAcceptedParticipant = acceptedParticipantRole != null;

        bool inScope = isHost || isStaffLeaderOfCampus || isHo || isGuestSide || isAcceptedParticipant;
        if (!inScope)
            throw new ForbiddenException("Bạn không có quyền truy cập trang thông tin này.");

        var relation = isHost ? "HOST"
            : isStaffLeaderOfCampus ? "STAFF_LEADER"
            : isHo ? "HO"
            : isGuestSide ? VisitInstanceAccess.OperationalContact
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
        bool isRejected = instance.Status == VisitInstanceStatus.Rejected;
        bool isLive = !isClosed && !isCancelled && !isRejected;

        // Only the Host edits the operational tabs (Trước/Trong/Sau), and only while the visit is live.
        bool hostCanEdit = isHost && isLive;
        // Rule (đặc tả 1.5 / 8.4): ONLY the "Trước tiếp khách" tab locks once preparation is done.
        // It is editable while BEFORE_VISIT; the moment the instance advances to DURING_VISIT (or
        // beyond) the prep tab becomes read-only — while Trong/Sau stay editable until the instance
        // is CLOSED (handled by isLive). We never lock all three tabs at once.
        //
        // ASSIGNED is NOT open either, and that is the newer half of the rule: the campus has a Host
        // but preparation has not begun, so the tab renders read-only until that Host presses
        // "Bắt đầu chuẩn bị". CanStartPreparation below is what offers them that button, and the two
        // flags are mutually exclusive by construction.
        bool beforeTabOpen = instance.Status == VisitInstanceStatus.BeforeVisit;
        bool canStartPreparation = isHost && isLive
            && instance.Status == VisitInstanceStatus.Assigned
            && hostAssigned;
        // Minutes: Host OR an accepted (IC/Dept/Student) participant — never Visitor/HO/Staff Leader.
        bool minutesEditor = (isHost || isAcceptedParticipant) && isLive;
        // News: the SAME canonical verdict the process list, the Create-News preset and the create
        // command use. This flag used to be its own rule — Host / accepted IC / Student AND isLive,
        // with no writing window and no media-consent or news-required gate — so it could read true
        // at BEFORE_VISIT for a visit whose guest had refused media coverage, on a campus where the
        // create endpoint refuses outright.
        bool newsCreator = PEMS.Application.Delegations.News.VisitNewsAccess.Evaluate(
            instance, visit, _currentUser, acceptedParticipantRole).CanCreate;

        // A pure Visitor owner must NEVER see the internal operational tabs (Before/During/After,
        // Minutes) of a CANCELLED campus — only the public-safe original request / cancellation reason.
        // Internal roles (Host / Staff Leader / HO / accepted participant) keep read-only visibility
        // to review what had been prepared before the cancellation.
        bool internalViewer = isHost || isStaffLeaderOfCampus || isHo || isAcceptedParticipant;
        bool visitorInternalBlocked = isGuestSide && !internalViewer && isCancelled;
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
            // Before tab is locked once the visit has started (DURING_VISIT+); see beforeTabOpen.
            CanEditBeforeVisit = hostCanEdit && beforeTabOpen,

            CanViewDuringVisit = canViewInternalTabs,
            CanEditDuringVisit = hostCanEdit,

            CanViewAfterVisit = canViewInternalTabs,
            CanEditAfterVisit = hostCanEdit,

            // Campus-independent approval: there is no separate assign-host step anymore —
            // the host is assigned inside the Staff Leader's approve action.
            CanAssignHost = false,

            CanViewMinutes = canViewInternalTabs,
            CanCreateMinutes = minutesEditor,
            CanEditMinutes = minutesEditor,

            CanViewNews = true,
            CanCreateNews = newsCreator,

            // ASSIGNED → BEFORE_VISIT, current Host only (see StartVisitPreparationCommand).
            CanStartPreparation = canStartPreparation,

            // Operational stage transitions — Host only, live instance (see CompleteVisitStageCommand).
            // BEFORE_VISIT only: "finish preparation" presupposes preparation started. And not before
            // T-6h — the same policy the command enforces, so the button is disabled rather than
            // clickable-then-409.
            CanStartVisit = isHost && isLive
                && instance.Status == VisitInstanceStatus.BeforeVisit
                && VisitStageTransitionPolicy.CanAdvanceBeforeToDuring(
                    _clock.VietnamNow, instance.PlannedStartAt),
            // Returned throughout BEFORE_VISIT, including while the window is shut, because that is
            // exactly when the UI needs it: a disabled button that says WHEN it opens is far better
            // than one that only says no. Derived from the CURRENT schedule, so an amendment that
            // moves the visit moves this with it.
            StartVisitAvailableAt = instance.Status == VisitInstanceStatus.BeforeVisit
                ? VisitStageTransitionPolicy.StartVisitAvailableAt(instance.PlannedStartAt)
                : null,
            StartVisitDisabledReasonCode =
                isHost && isLive
                && instance.Status == VisitInstanceStatus.BeforeVisit
                && !VisitStageTransitionPolicy.CanAdvanceBeforeToDuring(
                    _clock.VietnamNow, instance.PlannedStartAt)
                    ? VisitRequestErrorCodes.VisitStartWindowNotOpen
                    : null,
            CanCompleteVisit = isHost && isLive && instance.Status == VisitInstanceStatus.DuringVisit,
            // Đóng đoàn nằm ở tab "Sau tiếp khách": Host đóng khi đoàn đã kết thúc (AFTER_VISIT).
            CanCloseVisit = isHost && isLive && instance.Status == VisitInstanceStatus.AfterVisit,

            // "Gửi cập nhật chuẩn bị" — current Host only, and only while the prep tab is still open.
            // The send command re-checks this at send time: a host handover or a stage change between
            // composing the draft and pressing send must refuse, and a flag read minutes earlier
            // cannot know about either.
            CanSendSetupProgressEmail = hostCanEdit && beforeTabOpen,
        };
    }
}
