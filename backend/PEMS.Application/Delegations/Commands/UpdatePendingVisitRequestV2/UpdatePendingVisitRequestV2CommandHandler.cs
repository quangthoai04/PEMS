using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Policies;

using PEMS.Application.Delegations.Common;
namespace PEMS.Application.Delegations.Commands.UpdatePendingVisitRequestV2;

public sealed class UpdatePendingVisitRequestV2CommandHandler
    : IRequestHandler<UpdatePendingVisitRequestV2Command, UpdatePendingVisitRequestV2Response>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitRequestV2EditService _editService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<UpdatePendingVisitRequestV2CommandHandler> _logger;
    private readonly PerCampusFormV2Options _readFlag;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public UpdatePendingVisitRequestV2CommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitRequestV2EditService editService,
        INotificationService notificationService,
        ILogger<UpdatePendingVisitRequestV2CommandHandler> logger,
        PerCampusFormV2Options readFlag, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _editService = editService;
        _notificationService = notificationService;
        _logger = logger;
        _readFlag = readFlag;
        _writeFlag = writeFlag;
    }

    public async Task<UpdatePendingVisitRequestV2Response> Handle(
        UpdatePendingVisitRequestV2Command request, CancellationToken cancellationToken)
    {
        // ── Flag gate (identical to create-v2) ──
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
            .AsSplitQuery()
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);

        // ── Editor policy (plan §6.4): the REGISTRANT alone. This edit rewrites the request as a
        //    whole — it can add and drop campuses — so it belongs to the person who owns the request,
        //    not to somebody who confirmed one of its campuses. A campus holder changes their own
        //    campus through a safe edit or an amendment. Staff-side changes go through amendments. ──
        if (!VisitRequestOwnership.IsRegistrant(visit, actorId))
            throw new ForbiddenException("Chỉ người đăng ký mới được sửa toàn bộ đơn này.");

        // ── Editable-lifecycle gate — the SAME policy call the read model made when it decided whether
        //    to offer the button, so a capability can never promise what this refuses. ──
        if (visit.CampusInstances.Count == 0)
            throw new BusinessRuleException(
                "Đơn không có cơ sở nào nên không thể sửa.",
                VisitRequestErrorCodes.VisitRequestNotEditable);
        // Both pre-decision stages qualify: a request whose campuses are still waiting for their
        // operational contacts is as un-decided as one waiting for approval, and the registrant may
        // correct it in either. Same predicate the read model uses to offer the action, and the same
        // one VisitMutationPolicy applies underneath.
        VisitMutationGuard.EnsureRequestLevelAllowed(
            VisitMutationAction.EditPendingRequest, visit, now,
            c => c.Status is VisitInstanceStatuses.WaitingContactConfirmation
                          or VisitInstanceStatuses.WaitingRequestApproval,
            VisitRequestErrorCodes.VisitRequestNotEditable);

        // Campuses involved BEFORE the edit (kept + removed) — their leaders are notified too.
        var campusIdsBefore = visit.CampusInstances.Select(c => c.CampusId).ToList();

        // ── Short-notice capability (PEMS_SHORT_NOTICE_72H_ALL_REGISTRANT_MUTATIONS plan) ──
        // The registrant guard above already proved actorId IS visit.RegistrantUserId, so the only
        // remaining question is the actor's role. An internal Staff/Staff Leader account editing THEIR
        // OWN request may move a campus's schedule under the 72-hour floor; a Visitor/Guest registrant
        // keeps it exactly as before. Never derived from the payload — only from the actor's own role
        // claims and the ownership check already performed.
        var allowShortNotice = VisitMutationPolicy.IsShortNoticeEligible(
            VisitRequestOwnership.IsInternalActor(_currentUser),
            isRegistrant: true);

        V2EditResult result;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            // The service re-checks concurrency/lifecycle/data rules in-transaction and applies everything.
            result = await _editService.ApplyPendingEditAsync(
                visit, request.Edit, actorId, now, cancellationToken, allowShortNotice);
            await tx.CommitAsync(cancellationToken);
        }

        // ── Post-commit notifications (best-effort; a rolled-back edit never notifies) ──
        // Mixed v2: the projection is not business content — the generic notification names the request
        // by code with the explicit mixed label (leaders read their own campus's content in the detail).
        //
        // BUT NOT while the request is behind the global confirmation gate. A leader who cannot see
        // the request must not be told to go and review it: the notification carries the request's
        // code, its delegation name and a deep link into a detail page that will refuse them. The
        // canonical moment to announce a request to its leaders is the FINAL contact acceptance —
        // OperationalContactNotifier.AnnounceApprovalReady, keyed on the gate revision so a retry
        // cannot mail twice. An edit made before that point changes what they will eventually read,
        // not whether they should read it yet.
        if (!VisitRequestStatuses.IsBehindContactGate(visit.Status))
        {
            var notifyName = visit.HasMixedCampusDetails ? "Khác nhau theo cơ sở" : (visit.CampusInstances.FirstOrDefault()?.FormDetail?.DelegationName ?? visit.RequestCode);
            await NotifyLeadersAfterCommitAsync(visit.VisitRequestId, visit.RequestCode, notifyName,
                campusIdsBefore.Concat(visit.CampusInstances.Select(c => c.CampusId)).Distinct().ToList(),
                actorId, cancellationToken);
        }

        var instances = visit.CampusInstances
            .OrderBy(c => c.CampusId)
            .Select(c => new CreateVisitRequestV2CampusRef(c.VisitInstanceId, c.CampusId, c.Status))
            .ToList();
        return new UpdatePendingVisitRequestV2Response(
            visit.VisitRequestId, visit.Status, result.VisitScope, result.HasMixed, result.RequestRowVersion,
            instances,
            "Đơn đăng ký đã được cập nhật. Staff Leader các cơ sở sẽ xem thông tin mới nhất.");
    }

    private async Task NotifyLeadersAfterCommitAsync(
        ulong visitRequestId, string? requestCode, string? delegationName,
        IReadOnlyCollection<ulong> campusIds, ulong actorId, CancellationToken cancellationToken)
    {
        try
        {
            var leaders = await _db.Users
                .Where(u => u.Role.RoleCode == RoleCodes.Staff
                            && u.SubRole == UserSubRoles.Leader
                            && u.Status == UserStatuses.Active
                            && u.PrimaryCampusId.HasValue
                            && campusIds.Contains(u.PrimaryCampusId.Value))
                .Select(u => u.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (leaders.Count == 0) return;

            await _notificationService.CreateManyAsync(
                leaders.Select(id => new CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Visitor đã cập nhật đơn đăng ký tham quan",
                    Message: $"Visitor đã cập nhật thông tin đơn {requestCode} ({delegationName}). Vui lòng xem lại thông tin mới nhất trước khi xử lý.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    RelatedId: visitRequestId,
                    ActorUserId: actorId,
                    Category: NotificationCategories.Visit,
                    IsActionRequired: true,
                    VisitRequestId: visitRequestId,
                    // OpenVisitHistory (not the generic OpenVisitDetail): this notification means "data
                    // changed, go read it" — never "a decision is waiting for you". The frontend's
                    // navigation-intent resolver must never escalate this into the live approve/reject
                    // control even if the campus is still genuinely PENDING and the viewer still holds
                    // APPROVE_AND_ASSIGN_HOST (plan §5/§7/§14 — this IS the reported bug).
                    ActionType: NotificationActionTypes.OpenVisitHistory,
                    ActionUrl: $"/dashboard/visit?visitRequestId={visitRequestId}",
                    MetadataJson: NotificationEventKeys.BuildMetadata(
                        NotificationEventKeys.VisitRequestUpdatedPending,
                        new { delegationName, requestCode }))).ToList(),
                cancellationToken);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex,
                "pending-edit-v2 post-commit notification dispatch failed for visit request {VisitRequestId}",
                visitRequestId);
        }
    }
}
