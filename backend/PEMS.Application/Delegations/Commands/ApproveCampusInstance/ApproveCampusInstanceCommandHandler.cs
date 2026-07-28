using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.ApproveCampusInstance;

public sealed class ApproveCampusInstanceCommandHandler
    : IRequestHandler<ApproveCampusInstanceCommand, ApproveCampusInstanceResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitRequestAggregateStatusService _aggregateStatus;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
    private readonly IVisitFormReadService _formReadService;
    private readonly IUserMutationLockService _lockService;

    public ApproveCampusInstanceCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitRequestAggregateStatusService aggregateStatus,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        IVisitFormReadService formReadService,
        IUserMutationLockService lockService)
    {
        _lockService = lockService;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _aggregateStatus = aggregateStatus;
        _notificationService = notificationService;
        _formReadService = formReadService;
    }

    public async Task<ApproveCampusInstanceResponse> Handle(
        ApproveCampusInstanceCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        // Only the campus Staff Leader approves + assigns the host (also enforced by RBAC).
        if (!(_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader))
            throw new ForbiddenException("Chỉ Staff Leader mới được duyệt và gán host.");

        var actorId = _currentUser.UserId.Value;

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == request.VisitInstanceId)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Staff Leader may only act on their own campus instance.
        if (_currentUser.PrimaryCampusId != instance.CampusId)
            throw new ForbiddenException("Cơ sở này không thuộc phạm vi phụ trách của bạn.");

        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new ConflictException("Đơn đã bị hủy nên không thể duyệt.");

        // Only a still-pending instance can be approved (ASSIGNED/REJECTED/CANCELLED/CLOSED → 409).
        if (instance.Status != VisitInstanceStatus.WaitingRequestApproval)
            throw new ConflictException("Cơ sở này đã được xử lý hoặc trạng thái đã thay đổi.");

        // Host is one-time: never overwrite an existing assignment.
        if (instance.CurrentHostUserId != null)
            throw new BusinessRuleException(
                "Không thể gán lại host sau khi campus đã được duyệt.",
                "HOST_ALREADY_ASSIGNED");

        if (request.HostUserId == 0)
            throw new BusinessRuleException(
                "Khi duyệt yêu cầu, bạn phải chọn host chính thức.",
                VisitRequestErrorCodes.HostRequiredOnApproval);

        // ── The transaction opens BEFORE host eligibility is evaluated, and the first thing it does
        //    is lock the candidate's users row (see IUserMutationLockService). Approving assigns a
        //    Host, which is exactly the responsibility a concurrent role change must not be able to
        //    step over: whichever transaction locks first wins, and the loser re-reads the committed
        //    state — either this approval refuses an ineligible candidate, or the role change
        //    returns 409 because the account now hosts a visit (spec §13.6). ──
        await using var tx = await _db.BeginTransactionAsync(cancellationToken);
        await _lockService.LockUsersAsync(new[] { request.HostUserId }, cancellationToken);

        // ── Host eligibility — the SHARED rule (VisitHostEligibility), so the transfer path that runs
        //    after approval cannot admit somebody this path would refuse. ──
        var (eligibility, host) = await VisitHostEligibility.EvaluateAsync(
            _db, request.HostUserId, instance.CampusId, actorId, cancellationToken);
        if (eligibility == HostEligibility.NotFound || host is null)
            throw new NotFoundException("User", request.HostUserId);
        if (eligibility != HostEligibility.Eligible)
            throw new BusinessRuleException(VisitHostEligibility.MessageFor(eligibility));
        var isSelfHost = host.UserId == actorId;

        //    this window. Calendar-event overlaps are surfaced as warnings by the host-candidate
        //    API and stay selectable; hosting two delegations at once is not blocked anymore,
        //    we just log it as a warning.
        var hasHostingConflict = await _db.VisitRequestCampuses.AnyAsync(c =>
            c.CurrentHostUserId == request.HostUserId
            && c.VisitInstanceId != instance.VisitInstanceId
            && (c.Status == VisitInstanceStatus.Assigned
                || c.Status == VisitInstanceStatus.BeforeVisit
                || c.Status == VisitInstanceStatus.DuringVisit)
            && c.PlannedStartAt < instance.PlannedEndAt
            && c.PlannedEndAt > instance.PlannedStartAt,
            cancellationToken);

        var now = _clock.VietnamNow;
        var decisionNote = string.IsNullOrWhiteSpace(request.DecisionNote) ? null : request.DecisionNote.Trim();

        // Approve + assign in ONE campus-instance update (the DB trigger requires decision and
        // host fields to be present together when the row moves to ASSIGNED).
        instance.Status = VisitInstanceStatus.Assigned;
        instance.DecidedBy = actorId;
        instance.DecidedAt = now;
        instance.DecisionActorRole = DecisionActorRole.StaffLeader;
        instance.DecisionNote = decisionNote;
        instance.CurrentHostUserId = request.HostUserId;
        instance.HostAssignedBy = actorId;
        instance.HostAssignedAt = now;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;
        instance.RowVersion += 1;

        // ── Host participant row (IC_HOST). One active participant row per user per instance:
        //    update an existing non-removed row instead of inserting a duplicate. ──
        var existingParticipant = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                        && p.UserId == request.HostUserId
                        && p.Status != ParticipantStatuses.Removed)
            .OrderBy(p => p.ParticipantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingParticipant is null)
        {
            _db.VisitParticipants.Add(new VisitParticipant
            {
                VisitInstanceId = instance.VisitInstanceId,
                UserId = request.HostUserId,
                ParticipantRole = ParticipantRoles.IcHost,
                IsHost = true,
                Status = ParticipantStatuses.Assigned,
                AssignedBy = actorId,
                AssignedAt = now,
                CreatedAt = now,
                CreatedBy = actorId,
            });
        }
        else
        {
            existingParticipant.ParticipantRole = ParticipantRoles.IcHost;
            existingParticipant.IsHost = true;
            existingParticipant.Status = ParticipantStatuses.Assigned;
            existingParticipant.AssignedBy = actorId;
            existingParticipant.AssignedAt = now;
            existingParticipant.UpdatedAt = now;
            existingParticipant.UpdatedBy = actorId;
        }

        // ── Aggregate request status (PENDING/PARTIALLY_APPROVED/APPROVED/REJECTED). Computed
        //    here to mirror the DB aggregate trigger exactly, so EF's later visit_requests UPDATE
        //    writes the same value the trigger already produced. ──
        var previousVisitStatus = visit.Status;
        _aggregateStatus.Apply(visit);
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;
        visit.RowVersion += 1;

        var auditAction = hasHostingConflict 
            ? "APPROVE_CAMPUS_INSTANCE_AND_ASSIGN_HOST_WITH_SCHEDULE_WARNING" 
            : "APPROVE_CAMPUS_INSTANCE_AND_ASSIGN_HOST";

        // The decision belongs to ONE campus instance, so it is filed under that instance's own audit
        // context. audit_logs indexes campus_id / visit_request_id / visit_instance_id precisely for this,
        // and the admin audit surface filters by campus — leaving them null hid every per-campus decision
        // from exactly the query that is scoped the way Pure V2 is scoped.
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = auditAction,
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            SourceType = CampusDecisionAudit.SourceType,
            Reason = $"decision=ASSIGNED;host={request.HostUserId}",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        // --- Notifications (in-app only — không gửi email mời host) ---
        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"#{instance.CampusId}";

        // Delegation name for the notification text: this whole command acts on ONE campus instance, so it
        // comes from THAT instance's own detail — never a sibling's, and never a request-level value, since
        // visit_requests carries no delegation name. A missing detail fails loudly rather than going blank.
        var formContent = await _formReadService.ResolveCampusFormContentAsync(
            visit, new[] { instance.VisitInstanceId }, cancellationToken);
        var delegationName = formContent[instance.VisitInstanceId].DelegationName;

        var notifications = new List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
        var visitProcessUrl = $"/dashboard/visit/process/{instance.VisitInstanceId}";

        if (visit.VisitorUserId.HasValue)
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: visit.VisitorUserId.Value,
                Title: "Cơ sở đã tiếp nhận yêu cầu",
                Message: $"Cơ sở {campusName} đã tiếp nhận yêu cầu tham quan {visit.RequestCode} của bạn. Host phụ trách: {host.FullName}.",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestApproved,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                RelatedId: instance.VisitInstanceId,
                ActorUserId: actorId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                VisitRequestId: visit.VisitRequestId,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}"
            ));
        }

        // Self-host: the approver already knows — skip the "you were assigned" notification.
        if (!isSelfHost)
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: request.HostUserId,
                Title: "Bạn được gán phụ trách đoàn khách",
                Message: $"Bạn được phân công làm host chính cho đoàn {delegationName} tại {campusName}. Vui lòng vào Setup đoàn khách để chuẩn bị.",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.HostAssigned,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                RelatedId: instance.VisitInstanceId,
                ActorUserId: actorId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                IsActionRequired: true,
                VisitRequestId: visit.VisitRequestId,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                ActionUrl: visitProcessUrl
            ));
        }

        // G8/G9/G10 (spec §5 HO): visibility on multi-campus per-campus decisions + aggregate
        // status transitions. HO doesn't act on these — Category=Visit, not action-required.
        if (visit.VisitScope == VisitScopes.MultiCampus)
        {
            var hoUsers = await _db.Users
                .Where(u => u.Role.RoleCode == RoleCodes.Ho && u.Status == "ACTIVE")
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken);

            notifications.AddRange(hoUsers.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: id,
                Title: "Cơ sở đã duyệt đơn liên cơ sở",
                Message: $"Cơ sở {campusName} đã duyệt đơn {visit.RequestCode} ({delegationName}).",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                RelatedId: visit.VisitRequestId,
                ActorUserId: actorId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                VisitRequestId: visit.VisitRequestId,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                ActionUrl: visitProcessUrl
            )));

            if (previousVisitStatus != visit.Status)
            {
                if (visit.Status == VisitRequestStatuses.PartiallyApproved)
                {
                    notifications.AddRange(hoUsers.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: id,
                        Title: "Đơn liên cơ sở được duyệt một phần",
                        Message: $"Đơn {visit.RequestCode} ({delegationName}) hiện đã được duyệt một phần.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                        RelatedId: visit.VisitRequestId,
                        ActorUserId: actorId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                        VisitRequestId: visit.VisitRequestId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}"
                    )));
                }
                else if (visit.Status == VisitRequestStatuses.Approved || visit.Status == VisitRequestStatuses.Rejected)
                {
                    notifications.AddRange(hoUsers.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: id,
                        Title: "Đơn liên cơ sở đã xử lý xong",
                        Message: $"Tất cả cơ sở của đơn {visit.RequestCode} ({delegationName}) đã xử lý xong.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                        RelatedId: visit.VisitRequestId,
                        ActorUserId: actorId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                        VisitRequestId: visit.VisitRequestId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}"
                    )));
                }
            }
        }

        if (notifications.Any())
            await _notificationService.CreateManyAsync(notifications, cancellationToken);

        await tx.CommitAsync(cancellationToken);

        return new ApproveCampusInstanceResponse(
            visit.VisitRequestId,
            instance.VisitInstanceId,
            visit.Status,
            instance.Status,
            request.HostUserId,
            isSelfHost
                ? "Đã duyệt cơ sở và bạn là host chính phụ trách."
                : "Đã duyệt cơ sở và gán host phụ trách.",
            hasHostingConflict);
    }
}
