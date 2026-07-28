using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Domain.Policies;

// Both namespaces define these names; the notification pipeline expects the Application ones.
using NotificationTypes = PEMS.Application.Notifications.Common.NotificationTypes;
using NotificationRelatedTypes = PEMS.Application.Notifications.Common.NotificationRelatedTypes;

namespace PEMS.Application.Delegations.Commands.TransferVisitHost;

/// <summary>
/// Post-approval Host handover for ONE campus instance.
///
/// What it deliberately does NOT touch: the campus decision, its status, its approval or form revision,
/// and any pending amendment. Changing who runs the visit is not a reason to re-decide whether the
/// visit happens — re-opening the approval would send a settled request back through a queue and
/// invalidate work the campus has already done.
/// </summary>
public sealed class TransferVisitHostCommandHandler
    : IRequestHandler<TransferVisitHostCommand, TransferVisitHostResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly INotificationService _notificationService;
    private readonly IVisitFormReadService _formReadService;
    private readonly ILogger<TransferVisitHostCommandHandler> _logger;
    private readonly IUserMutationLockService _lockService;

    public TransferVisitHostCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        INotificationService notificationService, IVisitFormReadService formReadService,
        ILogger<TransferVisitHostCommandHandler> logger,
        IUserMutationLockService lockService)
    {
        _lockService = lockService;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notificationService = notificationService;
        _formReadService = formReadService;
        _logger = logger;
    }

    public async Task<TransferVisitHostResponse> Handle(
        TransferVisitHostCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var actorId = _currentUser.UserId.Value;
        var now = _clock.VietnamNow;

        if (!(_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader))
            throw new ForbiddenException("Chỉ Staff Leader mới được chuyển Host.");

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == command.VisitInstanceId, ct)
            ?? throw new NotFoundException("VisitRequestCampus", command.VisitInstanceId);

        // Own campus only — the same bar as approving it.
        if (_currentUser.PrimaryCampusId != instance.CampusId)
            throw new ForbiddenException("Cơ sở này không thuộc phạm vi phụ trách của bạn.");

        var visit = await _db.VisitRequests
            .FirstOrDefaultAsync(v => v.VisitRequestId == instance.VisitRequestId, ct)
            ?? throw new NotFoundException("VisitRequest", instance.VisitRequestId);

        var campusNames = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == instance.CampusId)
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, ct);

        // Lifecycle + window, from the shared policy — the same call the read model made when it decided
        // whether to offer TRANSFER_HOST. Scoped to this campus alone.
        VisitMutationGuard.EnsureAllowed(
            VisitMutationAction.TransferHost, visit.Status, instance, now,
            VisitViewerRelations.CampusLeader, VisitRequestErrorCodes.VisitRequestNotEditable,
            campusNames);

        if (instance.CurrentHostUserId is not { } previousHostId)
            throw new BusinessRuleException(
                "Cơ sở này chưa có Host để chuyển giao. Host đầu tiên được gán khi duyệt đơn.",
                VisitHostTransferErrorCodes.NoCurrentHost);

        if (previousHostId == command.NewHostUserId)
            throw new BusinessRuleException(
                "Người được chọn đang là Host hiện tại của cơ sở này.",
                VisitHostTransferErrorCodes.SameHost);

        // Optimistic concurrency BEFORE any write: two leaders picking different Hosts from the same
        // stale screen must not both succeed, leaving the second one's notification pointing at a Host
        // who no longer holds the role.
        if (command.ExpectedRowVersion != instance.RowVersion)
            throw new ConflictException(
                "Cơ sở này đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.",
                VisitFormV2ErrorCodes.VisitFormConcurrencyConflict);

        // ── Transaction + shared lock BEFORE eligibility (see IUserMutationLockService). A handover
        //    hands the incoming Host a live responsibility, so it must serialize against a role
        //    change on that same account. Both ids are handed to the lock service together, which
        //    orders them ascending — locking "outgoing then incoming" here and the reverse elsewhere
        //    is exactly the deadlock spec §13.4 rules out. ──
        await using var tx = await _db.BeginTransactionAsync(ct);
        await _lockService.LockUsersAsync(new[] { previousHostId, command.NewHostUserId }, ct);

        var (eligibility, newHost) = await VisitHostEligibility.EvaluateAsync(
            _db, command.NewHostUserId, instance.CampusId, actorId, ct);
        if (eligibility == HostEligibility.NotFound || newHost is null)
            throw new NotFoundException("User", command.NewHostUserId);
        if (eligibility != HostEligibility.Eligible)
            throw new BusinessRuleException(
                VisitHostEligibility.MessageFor(eligibility),
                VisitHostTransferErrorCodes.HostNotEligible);

        var previousHostName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == previousHostId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct) ?? $"#{previousHostId}";

        var reason = command.Reason?.Trim();
        var campusName = campusNames.TryGetValue(instance.CampusId, out var cn) ? cn : $"#{instance.CampusId}";

        // ── The handover itself. Status, decision and revisions are untouched by construction. ──
        instance.CurrentHostUserId = command.NewHostUserId;
        instance.HostAssignedBy = actorId;
        instance.HostAssignedAt = now;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;
        instance.RowVersion += 1;

        // ── Participant rows. The outgoing Host STAYS on the visit as ordinary IC support rather than
        //    being removed: they have been preparing this visit, may hold logistics items, and dropping
        //    them would revoke their access to the very request they were handing over. ──
        var participants = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                        && p.Status != ParticipantStatuses.Removed)
            .ToListAsync(ct);

        foreach (var p in participants.Where(p => p.UserId == previousHostId))
        {
            p.IsHost = false;
            p.ParticipantRole = ParticipantRoles.IcSupport;
            p.UpdatedAt = now;
            p.UpdatedBy = actorId;
        }

        var incoming = participants.FirstOrDefault(p => p.UserId == command.NewHostUserId);
        if (incoming is null)
        {
            _db.VisitParticipants.Add(new VisitParticipant
            {
                VisitInstanceId = instance.VisitInstanceId,
                UserId = command.NewHostUserId,
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
            incoming.ParticipantRole = ParticipantRoles.IcHost;
            incoming.IsHost = true;
            incoming.Status = ParticipantStatuses.Assigned;
            incoming.AssignedBy = actorId;
            incoming.AssignedAt = now;
            incoming.UpdatedAt = now;
            incoming.UpdatedBy = actorId;
        }

        // ── Audit: the before/after the history detail drawer reads. ──
        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = VisitAuditActions.HostTransferred,
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            SourceType = VisitAuditActions.HostTransferSourceType,
            Reason = reason,
            CreatedAt = now,
        };
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "currentHostUserId",
            OldValueText = previousHostId.ToString(),
            NewValueText = command.NewHostUserId.ToString(),
            CreatedAt = now,
        });
        // Ids are what the system needs; names are what a person reading the timeline needs, and the
        // account may be renamed or deactivated long before anyone opens this entry.
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "currentHostName",
            OldValueText = previousHostName,
            NewValueText = newHost.FullName,
            CreatedAt = now,
        });
        _db.AuditLogs.Add(audit);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await NotifyAfterCommitAsync(
            visit.VisitRequestId, visit.RequestCode, visit.VisitorUserId, instance.VisitInstanceId,
            instance.CampusId, campusName, previousHostId, previousHostName,
            command.NewHostUserId, newHost.FullName, reason, actorId, ct);

        return new TransferVisitHostResponse(
            instance.VisitInstanceId, previousHostId, previousHostName,
            command.NewHostUserId, newHost.FullName, instance.RowVersion,
            $"Đã chuyển Host của {campusName} sang {newHost.FullName}.");
    }

    /// <summary>
    /// Post-commit, best-effort. Four audiences, each told the part that concerns them: the outgoing
    /// Host (you are no longer running this), the incoming Host (you now are — action required), the
    /// visitor (the name you were given has changed), and the campus's other Staff Leaders.
    /// A transfer that committed must never be undone because a notification failed.
    /// </summary>
    private async Task NotifyAfterCommitAsync(
        ulong visitRequestId, string? requestCode, ulong? visitorUserId, ulong visitInstanceId,
        ulong campusId, string campusName, ulong previousHostId, string previousHostName,
        ulong newHostId, string newHostName, string? reason, ulong actorId, CancellationToken ct)
    {
        try
        {
            var visitProcessUrl = $"/dashboard/visit/process/{visitInstanceId}";
            var detailUrl = $"/dashboard/visit?visitRequestId={visitRequestId}";
            var because = string.IsNullOrWhiteSpace(reason) ? "" : $" Lý do: {reason}";
            var notifications = new List<CreateNotificationRequest>();

            CreateNotificationRequest Build(
                ulong recipient, string title, string message, string type,
                bool actionRequired, string url) => new(
                    RecipientUserId: recipient,
                    Title: title,
                    Message: message,
                    NotificationType: type,
                    RelatedType: NotificationRelatedTypes.VisitInstance,
                    RelatedId: visitInstanceId,
                    ActorUserId: actorId,
                    Category: NotificationCategories.Visit,
                    IsActionRequired: actionRequired,
                    VisitRequestId: visitRequestId,
                    VisitInstanceId: visitInstanceId,
                    CampusId: campusId,
                    ActionType: NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: url);

            // The leader who made the change already knows; telling them is noise.
            if (previousHostId != actorId)
                notifications.Add(Build(
                    previousHostId,
                    "Bạn không còn phụ trách đoàn khách này",
                    $"Bạn đã được chuyển giao vai trò Host của đoàn khách tại {campusName} (đơn {requestCode}) cho {newHostName}.{because}",
                    NotificationTypes.HostAssigned, actionRequired: false, visitProcessUrl));

            if (newHostId != actorId)
                notifications.Add(Build(
                    newHostId,
                    "Bạn được chuyển làm Host phụ trách đoàn khách",
                    $"Bạn được phân công làm Host chính cho đoàn khách tại {campusName} (đơn {requestCode}), thay cho {previousHostName}. Vui lòng vào Setup đoàn khách để tiếp nhận.{because}",
                    NotificationTypes.HostAssigned, actionRequired: true, visitProcessUrl));

            if (visitorUserId is { } visitor)
                notifications.Add(Build(
                    visitor,
                    "Host phụ trách chuyến thăm đã thay đổi",
                    $"Cơ sở {campusName} đã đổi Host phụ trách đơn {requestCode} sang {newHostName}. Lịch và nội dung chuyến thăm giữ nguyên.",
                    NotificationTypes.VisitStatusChanged, actionRequired: false, detailUrl));

            var leaders = await _db.Users.AsNoTracking()
                .Where(u => u.Role.RoleCode == RoleCodes.Staff
                            && u.SubRole == UserSubRoles.Leader
                            && u.Status == UserStatuses.Active
                            && u.PrimaryCampusId == campusId
                            && u.UserId != actorId)
                .Select(u => u.UserId)
                .ToListAsync(ct);
            notifications.AddRange(leaders.Select(id => Build(
                id,
                "Host của một đoàn khách đã được chuyển",
                $"Host của đoàn khách tại {campusName} (đơn {requestCode}) đã chuyển từ {previousHostName} sang {newHostName}.{because}",
                NotificationTypes.VisitStatusChanged, actionRequired: false, detailUrl)));

            if (notifications.Count > 0)
                await _notificationService.CreateManyAsync(notifications, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "host-transfer post-commit notification failed for instance {VisitInstanceId}", visitInstanceId);
        }
    }
}
