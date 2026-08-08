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

using PEMS.Application.Delegations.Common;
namespace PEMS.Application.Delegations.Commands.RejectCampusInstance;

public sealed class RejectCampusInstanceCommandHandler
    : IRequestHandler<RejectCampusInstanceCommand, RejectCampusInstanceResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitRequestAggregateStatusService _aggregateStatus;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
    private readonly IVisitFormReadService _formReadService;
    private readonly PEMS.Application.Delegations.VisitNotifications.CampusRejectionEmail _rejectionEmail;
    private readonly PEMS.Application.Delegations.VisitNotifications.RecoverableVisitEmailSender _mailer;

    public RejectCampusInstanceCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitRequestAggregateStatusService aggregateStatus,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        IVisitFormReadService formReadService,
        PEMS.Application.Delegations.VisitNotifications.CampusRejectionEmail rejectionEmail,
        PEMS.Application.Delegations.VisitNotifications.RecoverableVisitEmailSender mailer)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _aggregateStatus = aggregateStatus;
        _notificationService = notificationService;
        _formReadService = formReadService;
        _rejectionEmail = rejectionEmail;
        _mailer = mailer;
    }

    public async Task<RejectCampusInstanceResponse> Handle(
        RejectCampusInstanceCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        // Only the campus Staff Leader rejects their own campus instance (HO never decides).
        if (!(_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader))
            throw new ForbiddenException("Chỉ Staff Leader của cơ sở mới được từ chối cơ sở này.");

        var actorId = _currentUser.UserId.Value;

        // Reason is mandatory (also enforced by the validator; re-checked for internal callers —
        // the DB trigger requires a non-empty decision_note on REJECTED as well).
        var reason = request.DecisionNote?.Trim();
        if (string.IsNullOrEmpty(reason))
            throw new BusinessRuleException("Vui lòng nhập lý do từ chối.");

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == request.VisitInstanceId)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        if (_currentUser.PrimaryCampusId != instance.CampusId)
            throw new ForbiddenException("Cơ sở này không thuộc phạm vi phụ trách của bạn.");

        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new ConflictException("Đơn đã bị hủy nên không thể từ chối.");

        // The same global gate as approve (spec §6.1). Reject is a decision too, and a decision taken
        // while a sibling campus still has no confirmed contact is taken on a request nobody was
        // supposed to be able to see yet.
        if (VisitRequestStatuses.IsBehindContactGate(visit.Status))
            throw new ConflictException(
                "Đơn chưa đủ đầu mối vận hành xác nhận nên chưa thể từ chối.",
                OperationalContactErrorCodes.ContactConfirmationRequired);

        if (instance.Status != VisitInstanceStatus.WaitingRequestApproval)
            throw new ConflictException("Cơ sở này đã được xử lý hoặc trạng thái đã thay đổi.");

        var now = _clock.VietnamNow;

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        // Reject records the decision on the instance only — never a host, never participants,
        // never logistics/minutes/calendar.
        instance.Status = VisitInstanceStatus.Rejected;
        instance.DecidedBy = actorId;
        instance.DecidedAt = now;
        instance.DecisionActorRole = DecisionActorRole.StaffLeader;
        instance.DecisionNote = reason;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;
        instance.RowVersion += 1;

        // Rejecting ONE campus never rejects the whole request while other campuses are still
        // pending/assigned — the aggregate service (mirroring the DB trigger) decides the total.
        var previousVisitStatus = visit.Status;
        _aggregateStatus.Apply(visit);
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;
        visit.RowVersion += 1;

        // Same per-campus audit context as the approve decision — see CampusDecisionAudit. The free-text
        // reason is NOT copied here: it already lives on the instance, and this column is a short
        // structured summary, not a second copy of staff-authored prose.
        //
        // It is also THE identity of this rejection for notification purposes: the same campus may be
        // rejected again after a resubmit, and each rejection owes the registrant its own email. The row
        // is never updated and never deleted, so its id stays the right answer for as long as the
        // recovery sweep might ask (plan §37).
        var rejectionEvent = new AuditLog
        {
            ActorUserId = actorId,
            Action = PEMS.Application.Delegations.VisitNotifications.CampusRejectionEmail.RejectionAuditAction,
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED",
            CreatedAt = now
        };
        _db.AuditLogs.Add(rejectionEvent);

        await _db.SaveChangesAsync(cancellationToken);

        // --- Notification: the Visitor sees which campus declined and why ---
        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"#{instance.CampusId}";

        // This command acts on ONE campus instance, so the delegation name comes from THAT instance's own
        // detail — never a sibling's, and never a request-level value, which does not exist. A missing
        // detail fails loudly rather than going blank.
        var formContent = await _formReadService.ResolveCampusFormContentAsync(
            visit, new[] { instance.VisitInstanceId }, cancellationToken);
        var delegationName = formContent[instance.VisitInstanceId].DelegationName;

        var notifications = new System.Collections.Generic.List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();

        // This campus’s own contact plus the registrant — a sibling campus’s contact is not told.
        var visitorRecipients = VisitRequestOwnership.GuestSideRecipients(visit, instance);

        foreach (var recipientId in visitorRecipients)
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: recipientId,
                Title: "Cơ sở từ chối tiếp nhận",
                Message: $"Cơ sở {campusName} đã từ chối tiếp nhận yêu cầu tham quan {visit.RequestCode}. Lý do: {reason}",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestRejected,
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

        // G8/G9/G10 (spec §5 HO): visibility on multi-campus per-campus decisions + aggregate
        // status transitions — same logic as ApproveCampusInstanceCommandHandler.
        if (visit.VisitScope == VisitScopes.MultiCampus)
        {
            var hoUsers = await _db.Users
                .Where(u => u.Role.RoleCode == RoleCodes.Ho && u.Status == "ACTIVE")
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken);

            notifications.AddRange(hoUsers.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: id,
                Title: "Cơ sở đã từ chối đơn liên cơ sở",
                Message: $"Cơ sở {campusName} đã từ chối đơn {visit.RequestCode} ({delegationName}). Lý do: {reason}",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                RelatedId: visit.VisitRequestId,
                ActorUserId: actorId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                VisitRequestId: visit.VisitRequestId,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                ActionUrl: $"/dashboard/visit/process/{instance.VisitInstanceId}"
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

        if (notifications.Count > 0)
            await _notificationService.CreateManyAsync(notifications, cancellationToken);

        await tx.CommitAsync(cancellationToken);

        // ── The registrant's email, sent only after the decision is committed ────────────────────
        //
        // Strictly after the commit, for two reasons: a message must never describe a rejection a
        // rollback then undid, and the dispatcher shares this DbContext and saves — inside the
        // transaction it would have written whatever else was still tracked.
        //
        // Sent through the recoverable sender rather than directly, which is what makes a failure here
        // survivable. A rejection cannot be retried by rejecting again: this handler is the only writer
        // of REJECTED and it refuses unless the campus is still WAITING_REQUEST_APPROVAL, so a second
        // call conflicts long before reaching this line. The sender keys "already notified" off the
        // email history instead, so the recovery sweep can finish the job later without the decision
        // being replayed — and cannot send a second message once one has succeeded.
        //
        // Keyed on THIS rejection's audit row, so an earlier rejection of the same campus that was
        // notified successfully cannot mark this one as already done (plan §37).
        //
        // It never throws. The campus IS rejected and the response says so; a mail-server problem is
        // recorded on its own rows and does not turn a completed decision into an error the Staff
        // Leader would retry into a conflict (plan §11).
        await _mailer.SendOnceAsync(_rejectionEmail, rejectionEvent.AuditLogId, cancellationToken);

        return new RejectCampusInstanceResponse(
            visit.VisitRequestId,
            instance.VisitInstanceId,
            visit.Status,
            instance.Status,
            "Đã từ chối tiếp nhận tại cơ sở này.");
    }

}
