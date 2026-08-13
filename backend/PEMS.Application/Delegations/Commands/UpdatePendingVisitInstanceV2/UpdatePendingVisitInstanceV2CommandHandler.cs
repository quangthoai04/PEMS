using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Policies;
// Disambiguated for the same reason as in CampusApprovalExecutor: the notification pipeline has its own
// constants, and CreateNotificationRequest takes those.
using NotificationTypes = PEMS.Application.Notifications.Common.NotificationTypes;
using NotificationRelatedTypes = PEMS.Application.Notifications.Common.NotificationRelatedTypes;

namespace PEMS.Application.Delegations.Commands.UpdatePendingVisitInstanceV2;

/// <summary>
/// Applies a per-campus pending edit, and — when asked and entitled — approves the campus in the same
/// transaction.
///
/// <para>
/// Authorization is by RELATION to THIS campus, resolved in one place —
/// <see cref="VisitRequestOwnership.ResolvePendingCampusEdit"/> — which the read model asks too:
/// </para>
/// <list type="bullet">
/// <item><b>Registrant</b> — owns the request, so owns every campus of it.</item>
/// <item><b>Operational contact of this campus</b> — runs it. Holding a SIBLING campus grants nothing
/// here, which is the whole reason the check takes the instance rather than the request.</item>
/// <item><b>Staff Leader of this campus who ALSO filed the request</b> — the only actor who may file a
/// schedule inside the 72-hour registration floor, and the only one for whom "Lưu và duyệt" exists. A
/// Staff Leader who did NOT file it is this campus's DECIDER, not its author: they approve or reject
/// through their own commands, and neither of those is touched by the rule here.</item>
/// </list>
/// </summary>
public sealed class UpdatePendingVisitInstanceV2CommandHandler
    : IRequestHandler<UpdatePendingVisitInstanceV2Command, UpdatePendingVisitInstanceV2Response>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitRequestV2EditService _editService;
    private readonly ICampusApprovalExecutor _approval;
    private readonly INotificationService _notifications;
    private readonly ILogger<UpdatePendingVisitInstanceV2CommandHandler> _logger;
    private readonly PerCampusFormV2Options _readFlag;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public UpdatePendingVisitInstanceV2CommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitRequestV2EditService editService, ICampusApprovalExecutor approval,
        INotificationService notifications,
        ILogger<UpdatePendingVisitInstanceV2CommandHandler> logger,
        PerCampusFormV2Options readFlag, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _editService = editService;
        _approval = approval;
        _notifications = notifications;
        _logger = logger;
        _readFlag = readFlag;
        _writeFlag = writeFlag;
    }

    public async Task<UpdatePendingVisitInstanceV2Response> Handle(
        UpdatePendingVisitInstanceV2Command request, CancellationToken cancellationToken)
    {
        if (!_writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!_readFlag.Enabled)
            throw new ConflictException(
                "Cấu hình không hợp lệ: bật ghi v2 nhưng chưa bật đọc v2.",
                CreateVisitRequestV2ErrorCodes.ReadRequired);
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;
        var now = _clock.VietnamNow;

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .Include(v => v.CampusInstances).ThenInclude(c => c.GuestMemberLinks)
            .Include(v => v.GuestMembers)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);

        // Naming a campus of somebody else's request fails HERE (IDOR): the instance must belong to the
        // request in the route, and the lookup is scoped to that request's own collection so it cannot
        // resolve any other.
        var instance = visit.CampusInstances
            .FirstOrDefault(c => c.VisitInstanceId == request.VisitInstanceId)
            ?? throw new NotFoundException("Lịch thăm tại cơ sở", request.VisitInstanceId);

        // WHO is asking, decided once. The read model builds its EDIT_PENDING_CAMPUS capability from the
        // very same call, so a button that appeared is a call this admits — and a Staff Leader who is
        // not the registrant is refused here whether or not a client offers them the screen.
        var relation = VisitRequestOwnership.ResolvePendingCampusEdit(visit, instance, _currentUser);
        if (!relation.CanEdit)
            throw new ForbiddenException(relation.ActorIsStaffLeader
                // Their own campus, somebody else's request. Say what they CAN do, because they can
                // still decide it — sending them away with a bare "no permission" would read as if the
                // campus had stopped being theirs to answer.
                ? "Staff Leader chỉ được sửa cơ sở đang chờ duyệt khi chính mình là người đăng ký đơn. "
                  + "Với đơn của người khác, bạn vẫn duyệt hoặc từ chối cơ sở này như bình thường."
                : "Chỉ người đăng ký hoặc đầu mối vận hành của cơ sở này mới được sửa.");

        // The same policy call the read model made when it decided whether to OFFER the button, so a
        // capability can never promise what this refuses. Scoped to this campus only — a sibling that is
        // under way says nothing about a campus still waiting for its answer. The relation is the
        // resolver's, never re-derived here: CAMPUS_LEADER reaches the policy only for a leader who is
        // also the registrant, which is what keeps the policy's leader branch from being a second door.
        VisitMutationGuard.EnsureAllowed(
            VisitMutationAction.EditPendingCampus, visit.Status, instance, now,
            relation.ViewerRelation!,
            VisitRequestErrorCodes.PendingCampusNotEditable);

        // "Lưu và duyệt" needs BOTH halves: the campus's Staff Leader (approving is their act) AND the
        // registrant (editing this request is theirs). A leader deciding somebody else's request keeps
        // the ordinary approve/reject commands, which ask for neither.
        if (request.ApproveAfterSave is not null && !relation.CanSaveAndApprove)
            throw new ForbiddenException(
                "Chỉ Staff Leader phụ trách cơ sở này và đồng thời là người đăng ký đơn mới được lưu và duyệt.");

        var previousRequestStatus = visit.Status;
        CampusApprovalOutcome? approval = null;
        int requestRowVersion;

        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var result = await _editService.ApplyInstancePendingEditAsync(
                visit, instance, request.Content, actorId, now,
                // The 72-hour override is a leader-registrant privilege, not a role's: a leader who did
                // not file the request never reaches this line, and one editing somebody else's campus
                // is not acting as its leader here at all.
                actorIsCampusLeader: relation.ActsAsCampusLeader,
                overrideLeadTimeConfirmed: request.OverrideLeadTimeConfirmed,
                cancellationToken);
            requestRowVersion = result.RequestRowVersion;

            // One transaction covers both, so a refused approval takes the edit down with it (§57). The
            // alternative — commit the edit, then approve — can leave the campus rewritten and still
            // waiting, with the leader believing they approved it.
            if (request.ApproveAfterSave is { } approve)
                approval = await _approval.ApproveAndAssignHostAsync(
                    visit, instance, approve.HostUserId, approve.DecisionNote, actorId, now, cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }

        // ── Post-commit, best-effort. An approval speaks for itself through its own notifications; a
        //    plain save tells the campus's Staff Leader there is something new to look at — and ONLY
        //    that campus's leader. A sibling's leader has nothing to reprocess. ──
        if (approval is not null)
            await _approval.NotifyApprovedAsync(
                visit, instance, approval, previousRequestStatus, actorId, cancellationToken);
        else
            await NotifyLeaderAsync(visit, instance, actorId, cancellationToken);

        return new UpdatePendingVisitInstanceV2Response(
            visit.VisitRequestId,
            instance.VisitInstanceId,
            visit.Status,
            instance.Status,
            instance.RowVersion,
            requestRowVersion,
            approval is not null,
            approval?.HostUserId,
            approval?.Message ?? "Đã cập nhật thông tin cho cơ sở này. Cơ sở vẫn đang chờ duyệt.");
    }

    /// <summary>
    /// Tells the Staff Leader of THIS campus, and nobody else's. Skipped when the leader IS the editor —
    /// nobody needs to be told about their own change.
    /// </summary>
    private async Task NotifyLeaderAsync(
        VisitRequest visit, VisitRequestCampus instance, ulong actorId, CancellationToken cancellationToken)
    {
        try
        {
            if (instance.CoordinatorUserId is not { } leaderId || leaderId == actorId) return;

            // Never while the request is behind the GLOBAL confirmation gate. This campus's own
            // contact may already have confirmed — that is precisely the case this guards — but a
            // sibling has not, so no Staff Leader may see the request yet and this notification
            // would deep-link its leader into a detail page that refuses them. The one canonical
            // "there is something to review" announcement is sent when the LAST contact confirms
            // (OperationalContactNotifier.AnnounceApprovalReady, deduped on the gate revision).
            if (VisitRequestStatuses.IsBehindContactGate(visit.Status)) return;

            var campusName = await _db.Campuses.AsNoTracking()
                .Where(c => c.CampusId == instance.CampusId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{instance.CampusId}";

            await _notifications.CreateAsync(new CreateNotificationRequest(
                RecipientUserId: leaderId,
                Title: "Thông tin cơ sở chờ duyệt đã được cập nhật",
                Message: $"Đơn {visit.RequestCode} đã được cập nhật tại cơ sở {campusName}. Vui lòng xem thông tin mới nhất trước khi xử lý.",
                NotificationType: NotificationTypes.VisitRequestSubmitted,
                RelatedType: NotificationRelatedTypes.VisitInstance,
                RelatedId: instance.VisitInstanceId,
                ActorUserId: actorId,
                Category: NotificationCategories.Visit,
                IsActionRequired: true,
                VisitRequestId: visit.VisitRequestId,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: NotificationActionTypes.OpenVisitDetail,
                ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}"
            ), cancellationToken);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex,
                "pending campus edit {VisitInstanceId} committed but its notification failed",
                instance.VisitInstanceId);
        }
    }
}
