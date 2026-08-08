using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;
// Two `NotificationTypes` are in scope (Domain constants and the notification pipeline's own). The
// pipeline's is the one CreateNotificationRequest expects, so it is named explicitly rather than left to
// whichever using happens to come first.
using NotificationTypes = PEMS.Application.Notifications.Common.NotificationTypes;
using NotificationRelatedTypes = PEMS.Application.Notifications.Common.NotificationRelatedTypes;

namespace PEMS.Application.Delegations.Services;

/// <summary>What the approval did, for the caller's response.</summary>
public sealed record CampusApprovalOutcome(
    string RequestStatus,
    string InstanceStatus,
    ulong HostUserId,
    bool IsSelfHost,
    bool HasHostingConflict,
    string Message);

/// <summary>
/// Approving ONE campus and naming its Host — the single implementation, so every path that can produce
/// an approved campus produces the same one.
///
/// <para>
/// It exists because approval is not only reachable from the approve button. A Staff Leader who is also
/// the registrant may fix the schedule and approve in one gesture ("Lưu và duyệt"), and that has to be
/// ONE transaction: an edit that commits and an approval that then fails would leave the campus holding
/// a schedule nobody agreed to. Having the second path re-implement approval is how the two would drift
/// — one of them forgetting the participant row, or the aggregate recompute, or the audit.
/// </para>
/// <para>
/// The service runs inside the CALLER'S open transaction and flushes but never commits, mirroring
/// <c>IVisitRequestV2EditService</c>. Notifications are deliberately a separate call, made by the caller
/// after its commit: a notification failure must never roll back an approval that really happened.
/// </para>
/// </summary>
public interface ICampusApprovalExecutor
{
    /// <summary>
    /// Approves <paramref name="instance"/> and assigns <paramref name="hostUserId"/>, in that one act.
    /// Throws (and leaves the caller's transaction to roll back) on any refusal.
    /// </summary>
    Task<CampusApprovalOutcome> ApproveAndAssignHostAsync(
        VisitRequest visit, VisitRequestCampus instance, ulong hostUserId, string? decisionNote,
        ulong actorId, System.DateTime now, CancellationToken cancellationToken);

    /// <summary>Post-commit, best-effort notifications for an approval this service just performed.</summary>
    Task NotifyApprovedAsync(
        VisitRequest visit, VisitRequestCampus instance, CampusApprovalOutcome outcome,
        string previousRequestStatus, ulong actorId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="ICampusApprovalExecutor" />
public sealed class CampusApprovalExecutor : ICampusApprovalExecutor
{
    private readonly IApplicationDbContext _db;
    private readonly IVisitRequestAggregateStatusService _aggregateStatus;
    private readonly IUserMutationLockService _lockService;
    private readonly INotificationService _notificationService;
    private readonly IVisitFormReadService _formReadService;
    private readonly ILogger<CampusApprovalExecutor> _logger;

    public CampusApprovalExecutor(
        IApplicationDbContext db,
        IVisitRequestAggregateStatusService aggregateStatus,
        IUserMutationLockService lockService,
        INotificationService notificationService,
        IVisitFormReadService formReadService,
        ILogger<CampusApprovalExecutor> logger)
    {
        _db = db;
        _aggregateStatus = aggregateStatus;
        _lockService = lockService;
        _notificationService = notificationService;
        _formReadService = formReadService;
        _logger = logger;
    }

    public async Task<CampusApprovalOutcome> ApproveAndAssignHostAsync(
        VisitRequest visit, VisitRequestCampus instance, ulong hostUserId, string? decisionNote,
        ulong actorId, System.DateTime now, CancellationToken cancellationToken)
    {
        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new ConflictException("Đơn đã bị hủy nên không thể duyệt.");

        // ── The global confirmation gate (spec §3.4). It is a property of the WHOLE request, so it does
        //    not follow from the campus check: this campus can be at WAITING_REQUEST_APPROVAL with its
        //    own contact confirmed while a SIBLING still has nobody, and the request as a whole is then
        //    invisible to every Staff Leader. The queue filters those rows out, but the queue is not
        //    authorization — a direct call has to be refused here. ──
        if (VisitRequestStatuses.IsBehindContactGate(visit.Status))
            throw new ConflictException(
                "Đơn chưa đủ đầu mối vận hành xác nhận nên chưa thể duyệt.",
                OperationalContactErrorCodes.ContactConfirmationRequired);

        if (instance.Status != VisitInstanceStatuses.WaitingRequestApproval)
            throw new ConflictException("Cơ sở này đã được xử lý hoặc trạng thái đã thay đổi.");

        // Host is one-time: never overwrite an existing assignment. Handing the campus to somebody else
        // afterwards is a transfer, which keeps its own record of who held it and when.
        if (instance.CurrentHostUserId is not null)
            throw new BusinessRuleException(
                "Không thể gán lại host sau khi campus đã được duyệt.", "HOST_ALREADY_ASSIGNED");

        // ── The invariant the whole post-approval model rests on: an ASSIGNED campus HAS a Host. Every
        //    right that exists after approval — deciding amendments, running setup — is the Host's, so a
        //    campus approved without one would be a campus nobody can act on. There is no
        //    "approve now, assign later"; refusing here is what keeps that row from existing. ──
        if (hostUserId == 0)
            throw new BusinessRuleException(
                "Khi duyệt yêu cầu, bạn phải chọn host chính thức.",
                VisitRequestErrorCodes.HostRequiredOnApproval);

        // Lock the candidate's users row first (see IUserMutationLockService): approving assigns a Host,
        // which is exactly the responsibility a concurrent role change must not step over. Whichever
        // transaction locks first wins, and the loser re-reads the committed state.
        await _lockService.LockUsersAsync(new[] { hostUserId }, cancellationToken);

        // Host eligibility — the SHARED rule, so the transfer path that runs after approval cannot admit
        // somebody this path would refuse.
        var (eligibility, host) = await VisitHostEligibility.EvaluateAsync(
            _db, hostUserId, instance.CampusId, actorId, cancellationToken);
        if (eligibility == HostEligibility.NotFound || host is null)
            throw new NotFoundException("User", hostUserId);
        if (eligibility != HostEligibility.Eligible)
            throw new BusinessRuleException(VisitHostEligibility.MessageFor(eligibility));
        var isSelfHost = host.UserId == actorId;

        // Calendar-event overlaps are surfaced as warnings by the host-candidate API and stay
        // selectable; hosting two delegations at once is not blocked, only reported.
        var hasHostingConflict = await _db.VisitRequestCampuses.AnyAsync(c =>
            c.CurrentHostUserId == hostUserId
            && c.VisitInstanceId != instance.VisitInstanceId
            && (c.Status == VisitInstanceStatuses.Assigned
                || c.Status == VisitInstanceStatuses.BeforeVisit
                || c.Status == VisitInstanceStatuses.DuringVisit)
            && c.PlannedStartAt < instance.PlannedEndAt
            && c.PlannedEndAt > instance.PlannedStartAt,
            cancellationToken);

        var note = string.IsNullOrWhiteSpace(decisionNote) ? null : decisionNote.Trim();

        // Approve + assign in ONE campus-instance update (the DB trigger requires decision and host
        // fields to be present together when the row moves to ASSIGNED).
        instance.Status = VisitInstanceStatuses.Assigned;
        instance.DecidedBy = actorId;
        instance.DecidedAt = now;
        instance.DecisionActorRole = DecisionActorRole.StaffLeader;
        instance.DecisionNote = note;
        instance.CurrentHostUserId = hostUserId;
        instance.HostAssignedBy = actorId;
        instance.HostAssignedAt = now;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;
        instance.RowVersion += 1;

        // ── Host participant row (IC_HOST). One active participant row per user per instance: update an
        //    existing non-removed row instead of inserting a duplicate. ──
        var existingParticipant = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                        && p.UserId == hostUserId
                        && p.Status != ParticipantStatuses.Removed)
            .OrderBy(p => p.ParticipantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingParticipant is null)
        {
            _db.VisitParticipants.Add(new VisitParticipant
            {
                VisitInstanceId = instance.VisitInstanceId,
                UserId = hostUserId,
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

        // Aggregate request status, computed here to mirror the DB aggregate trigger exactly, so EF's
        // later visit_requests UPDATE writes the same value the trigger already produced.
        _aggregateStatus.Apply(visit);
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;
        visit.RowVersion += 1;

        // The decision belongs to ONE campus instance, so it is filed under that instance's own audit
        // context — audit_logs indexes campus_id / visit_request_id / visit_instance_id precisely for
        // this, and the admin audit surface filters by campus.
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = hasHostingConflict
                ? "APPROVE_CAMPUS_INSTANCE_AND_ASSIGN_HOST_WITH_SCHEDULE_WARNING"
                : "APPROVE_CAMPUS_INSTANCE_AND_ASSIGN_HOST",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            SourceType = CampusDecisionAudit.SourceType,
            Reason = $"decision=ASSIGNED;host={hostUserId}",
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new CampusApprovalOutcome(
            visit.Status, instance.Status, hostUserId, isSelfHost, hasHostingConflict,
            isSelfHost
                ? "Đã duyệt cơ sở và bạn là host chính phụ trách."
                : "Đã duyệt cơ sở và gán host phụ trách.");
    }

    public async Task NotifyApprovedAsync(
        VisitRequest visit, VisitRequestCampus instance, CampusApprovalOutcome outcome,
        string previousRequestStatus, ulong actorId, CancellationToken cancellationToken)
    {
        try
        {
            await BuildAndSendAsync(visit, instance, outcome, previousRequestStatus, actorId, cancellationToken);
        }
        catch (System.Exception ex)
        {
            // The approval has already committed. Losing a notification is a smaller failure than
            // reporting an error for a decision that was in fact taken, so it is logged and swallowed.
            _logger.LogError(ex,
                "campus approval {VisitInstanceId} committed but its notifications failed",
                instance.VisitInstanceId);
        }
    }

    private async Task BuildAndSendAsync(
        VisitRequest visit, VisitRequestCampus instance, CampusApprovalOutcome outcome,
        string previousRequestStatus, ulong actorId, CancellationToken cancellationToken)
    {
        var campusName = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"#{instance.CampusId}";

        // Delegation name: this acts on ONE campus instance, so it comes from THAT instance's own
        // detail — never a sibling's, and never a request-level value, since visit_requests carries no
        // delegation name.
        var formContent = await _formReadService.ResolveCampusFormContentAsync(
            visit, new[] { instance.VisitInstanceId }, cancellationToken);
        var delegationName = formContent[instance.VisitInstanceId].DelegationName;

        var hostName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == outcome.HostUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var notifications = new List<CreateNotificationRequest>();
        var visitProcessUrl = $"/dashboard/visit/process/{instance.VisitInstanceId}";
        var detailUrl = $"/dashboard/visit?visitRequestId={visit.VisitRequestId}";

        // This campus's own contact plus the registrant — a sibling campus's contact is not told.
        foreach (var recipientId in VisitRequestOwnership.GuestSideRecipients(visit, instance))
        {
            notifications.Add(new CreateNotificationRequest(
                RecipientUserId: recipientId,
                Title: "Cơ sở đã tiếp nhận yêu cầu",
                Message: $"Cơ sở {campusName} đã tiếp nhận yêu cầu tham quan {visit.RequestCode} của bạn. Host phụ trách: {hostName}.",
                NotificationType: NotificationTypes.VisitRequestApproved,
                RelatedType: NotificationRelatedTypes.VisitInstance,
                RelatedId: instance.VisitInstanceId,
                ActorUserId: actorId,
                Category: NotificationCategories.Visit,
                VisitRequestId: visit.VisitRequestId,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: NotificationActionTypes.OpenVisitDetail,
                ActionUrl: detailUrl));
        }

        // Self-host: the approver already knows — skip the "you were assigned" notification.
        if (!outcome.IsSelfHost)
        {
            notifications.Add(new CreateNotificationRequest(
                RecipientUserId: outcome.HostUserId,
                Title: "Bạn được gán phụ trách đoàn khách",
                Message: $"Bạn được phân công làm host chính cho đoàn {delegationName} tại {campusName}. Vui lòng vào Setup đoàn khách để chuẩn bị.",
                NotificationType: NotificationTypes.HostAssigned,
                RelatedType: NotificationRelatedTypes.VisitInstance,
                RelatedId: instance.VisitInstanceId,
                ActorUserId: actorId,
                Category: NotificationCategories.Visit,
                IsActionRequired: true,
                VisitRequestId: visit.VisitRequestId,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: NotificationActionTypes.OpenVisitDetail,
                ActionUrl: visitProcessUrl));
        }

        // HO visibility on multi-campus per-campus decisions + aggregate transitions. HO doesn't act on
        // these — Category=Visit, not action-required.
        if (visit.VisitScope == VisitScopes.MultiCampus)
        {
            var hoUsers = await _db.Users.AsNoTracking()
                .Where(u => u.Role.RoleCode == RoleCodes.Ho && u.Status == UserStatuses.Active)
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken);

            notifications.AddRange(hoUsers.Select(id => new CreateNotificationRequest(
                RecipientUserId: id,
                Title: "Cơ sở đã duyệt đơn liên cơ sở",
                Message: $"Cơ sở {campusName} đã duyệt đơn {visit.RequestCode} ({delegationName}).",
                NotificationType: NotificationTypes.VisitStatusChanged,
                RelatedType: NotificationRelatedTypes.VisitRequest,
                RelatedId: visit.VisitRequestId,
                ActorUserId: actorId,
                Category: NotificationCategories.Visit,
                VisitRequestId: visit.VisitRequestId,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: NotificationActionTypes.OpenVisitDetail,
                ActionUrl: visitProcessUrl)));

            if (previousRequestStatus != visit.Status)
            {
                var (title, message) = visit.Status switch
                {
                    VisitRequestStatuses.PartiallyApproved => (
                        "Đơn liên cơ sở được duyệt một phần",
                        $"Đơn {visit.RequestCode} ({delegationName}) hiện đã được duyệt một phần."),
                    VisitRequestStatuses.Approved or VisitRequestStatuses.Rejected => (
                        "Đơn liên cơ sở đã xử lý xong",
                        $"Tất cả cơ sở của đơn {visit.RequestCode} ({delegationName}) đã xử lý xong."),
                    _ => (string.Empty, string.Empty),
                };
                if (title.Length > 0)
                    notifications.AddRange(hoUsers.Select(id => new CreateNotificationRequest(
                        RecipientUserId: id,
                        Title: title,
                        Message: message,
                        NotificationType: NotificationTypes.VisitStatusChanged,
                        RelatedType: NotificationRelatedTypes.VisitRequest,
                        RelatedId: visit.VisitRequestId,
                        ActorUserId: actorId,
                        Category: NotificationCategories.Visit,
                        VisitRequestId: visit.VisitRequestId,
                        ActionType: NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: detailUrl)));
            }
        }

        if (notifications.Count > 0)
            await _notificationService.CreateManyAsync(notifications, cancellationToken);
    }
}
