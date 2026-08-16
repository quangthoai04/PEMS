using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

using PEMS.Application.Delegations.Common;
namespace PEMS.Application.Delegations.Commands.CompleteVisitStage;

/// <summary>
/// Advances a campus instance one operational stage forward. Only the official current host of
/// the instance may do so. Transition guards return clean business errors (409) instead of letting
/// an invalid status or a DB trigger surface as a generic 500.
/// </summary>
public sealed class CompleteVisitStageCommandHandler
    : IRequestHandler<CompleteVisitStageCommand, CompleteVisitStageResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public CompleteVisitStageCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock, PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notificationService = notificationService;
    }

    public async Task<CompleteVisitStageResponse> Handle(
        CompleteVisitStageCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId
                                      && c.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Operational stage transitions are a HOST-only action (the official current host of THIS
        // instance). Staff Leader/HO monitor read-only; Visitor/Dept/Student have no process control.
        if (instance.CurrentHostUserId != actorId)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được cập nhật tiến độ tiếp khách.");

        if (instance.Status == VisitInstanceStatus.Cancelled)
            throw new ConflictException("Cơ sở này đã bị hủy nên không thể cập nhật tiến độ.");
        if (instance.Status == VisitInstanceStatus.Closed)
            throw new ConflictException("Cơ sở này đã đóng đoàn nên không thể cập nhật tiến độ.");

        var now = _clock.VietnamNow;
        string newStatus;
        string action;
        string message;

        switch (request.Stage)
        {
            case VisitStageKeys.Before:
                // Finish preparation → start the visit.
                if (instance.Status == VisitInstanceStatus.Assigned)
                    throw new ConflictException(
                        "Không thể bắt đầu tiếp khách. Bạn chưa bắt đầu giai đoạn chuẩn bị cho cơ sở này.",
                        VisitRequestErrorCodes.VisitPreparationNotStarted);
                if (instance.Status != VisitInstanceStatus.BeforeVisit)
                    throw new ConflictException("Không thể bắt đầu tiếp khách. Cơ sở chưa ở giai đoạn chuẩn bị.");

                // KHÔNG có điều kiện thời gian ở đây (NP-05). Mốc T-6h trong VisitStageTransitionPolicy
                // là mốc KHUYẾN NGHỊ, không phải điều kiện chặn: Host đã chuẩn bị xong được chuyển giai
                // đoạn bất cứ lúc nào. Permission DTO trả RecommendedStartVisitAt +
                // IsBeforeRecommendedStartWindow để màn hình ghi chú "sớm hơn dự kiến" trong hộp xác
                // nhận — chỉ là lời nhắc, không khóa nút. Mọi điều kiện chặn dưới đây đều về mức độ SẴN
                // SÀNG (agenda, lời mời, phiếu bàn giao), không về đồng hồ.

                // Precondition: no mandatory preparation item may still be blocking. Today the only
                // persisted blocking item is a participant invitation still awaiting a response
                // (status INVITED). When the agenda/logistics persistence APIs land, extend this
                // check (e.g. unconfirmed logistics, missing agenda) — same 409 surface.
                var missing = new List<string>();

                var pendingInvites = await _db.VisitParticipants
                    .CountAsync(p => p.VisitInstanceId == instance.VisitInstanceId
                                     && p.Status == ParticipantStatuses.Invited, cancellationToken);
                if (pendingInvites > 0)
                    missing.Add($"{pendingInvites} lời mời tham gia chưa được phản hồi");

                var agendaCount = await _db.VisitAgendas
                    .CountAsync(a => a.VisitInstanceId == instance.VisitInstanceId, cancellationToken);
                if (agendaCount == 0)
                    missing.Add("lịch trình (agenda) chưa có mục nào");

                // Mọi hạng mục hậu cần "gửi yêu cầu qua hệ thống" đã được chấp nhận (ACCEPTED/
                // IN_PROGRESS/DONE) phải có phiếu bàn giao (BORROW) đã ký đủ hai bên (bên giao +
                // bên nhận) trước khi bắt đầu tiếp khách — tránh đoàn khách đến mà tài sản/thiết bị
                // chưa thực sự được bàn giao xong.
                var beforeHandoverEligibleIds = await _db.VisitLogisticsItems
                    .Where(l => l.VisitInstanceId == instance.VisitInstanceId
                                && l.CoordinationMode == "SYSTEM_REQUEST"
                                && (l.Status == LogisticsItemStatus.Accepted
                                    || l.Status == LogisticsItemStatus.InProgress
                                    || l.Status == LogisticsItemStatus.Done))
                    .Select(l => l.LogisticsItemId)
                    .ToListAsync(cancellationToken);
                if (beforeHandoverEligibleIds.Count > 0)
                {
                    var fullySignedBorrowCount = await _db.VisitLogisticsItemHandovers
                        .CountAsync(h => beforeHandoverEligibleIds.Contains(h.LogisticsItemId)
                                         && h.HandoverType == LogisticsHandoverTypes.Borrow
                                         && h.ProviderSignedAt != null && h.BorrowerSignedAt != null,
                            cancellationToken);
                    var unsignedHandoverCount = beforeHandoverEligibleIds.Count - fullySignedBorrowCount;
                    if (unsignedHandoverCount > 0)
                        missing.Add($"{unsignedHandoverCount} phiếu bàn giao tài sản chưa ký nhận đủ hai bên");
                }

                if (missing.Count > 0)
                    throw new ConflictException(
                        "Chưa thể chuyển sang giai đoạn đang tiếp khách vì còn hạng mục chuẩn bị chưa hoàn tất: "
                        + string.Join("; ", missing) + ".");

                // Spec §9 (resubmit_agenda_cancel24): DURING_VISIT and beyond REQUIRE ≥ 1 agenda
                // row — mirrored by a DB rule; checked here so the user gets a clean 409.
                await EnsureAgendaExistsAsync(instance.VisitInstanceId, cancellationToken);

                newStatus = VisitInstanceStatus.DuringVisit;
                action = "COMPLETE_BEFORE_VISIT";
                message = "Đã hoàn thành chuẩn bị. Chuyển sang giai đoạn đang tiếp khách.";
                break;

            case VisitStageKeys.During:
                if (instance.Status != VisitInstanceStatus.DuringVisit)
                    throw new ConflictException("Không thể hoàn thành tiếp khách. Cơ sở chưa ở giai đoạn đang tiếp khách.");
                // AFTER_VISIT still requires an agenda (spec §9 — DB rule enforces the same).
                await EnsureAgendaExistsAsync(instance.VisitInstanceId, cancellationToken);
                newStatus = VisitInstanceStatus.AfterVisit;
                action = "COMPLETE_DURING_VISIT";
                message = "Đã hoàn thành tiếp khách. Chuyển sang giai đoạn sau tiếp khách.";
                break;

            case VisitStageKeys.After:
                if (instance.Status != VisitInstanceStatus.AfterVisit)
                    throw new ConflictException("Không thể đóng đoàn. Cơ sở chưa ở giai đoạn sau tiếp khách.");

                // CLOSED still requires an agenda (spec §9 — DB rule enforces the same).
                await EnsureAgendaExistsAsync(instance.VisitInstanceId, cancellationToken);

                // §10: điều kiện đóng đoàn — gom mọi hạng mục còn thiếu và trả 409 rõ ràng. Không
                // update status nếu chưa đủ điều kiện.
                var blockers = new List<string>();

                // 1. Đã qua thời điểm kết thúc theo kế hoạch. planned_end_at là DATETIME wall-clock
                //    nên so với VietnamNow (KHÔNG dùng UtcNow để tránh lệch múi giờ).
                if (_clock.VietnamNow < instance.PlannedEndAt)
                    blockers.Add("chưa qua thời điểm kết thúc theo kế hoạch");

                // 2. Không còn logistics item đang xử lý/chưa hoàn tất (terminal =
                //    DONE/REJECTED/DECLINED/CANCELLED).
                var activeLogistics = await _db.VisitLogisticsItems
                    .CountAsync(l => l.VisitInstanceId == instance.VisitInstanceId
                                     && l.Status != LogisticsItemStatus.Done
                                     && l.Status != LogisticsItemStatus.Rejected
                                     && l.Status != LogisticsItemStatus.Declined
                                     && l.Status != LogisticsItemStatus.Cancelled, cancellationToken);
                if (activeLogistics > 0)
                    blockers.Add($"{activeLogistics} yêu cầu hậu cần chưa hoàn tất/hủy");

                // 3. Mọi phiếu bàn giao (BORROW/RETURN) đã ký đủ hai bên (bên mượn + bên giao).
                //    Tách 2 truy vấn (ids → handovers) để Pomelo dịch an toàn, tránh subquery tương quan.
                var instanceLogisticsIds = await _db.VisitLogisticsItems
                    .Where(l => l.VisitInstanceId == instance.VisitInstanceId)
                    .Select(l => l.LogisticsItemId)
                    .ToListAsync(cancellationToken);
                if (instanceLogisticsIds.Count > 0)
                {
                    var unsignedHandovers = await _db.VisitLogisticsItemHandovers
                        .CountAsync(h => instanceLogisticsIds.Contains(h.LogisticsItemId)
                                         && (h.BorrowerSignedAt == null || h.ProviderSignedAt == null),
                            cancellationToken);
                    if (unsignedHandovers > 0)
                        blockers.Add($"{unsignedHandovers} phiếu bàn giao tài sản chưa ký đủ hai bên");
                }

                // 4. Mọi đầu mục công việc trong biên bản đã DONE/CANCELLED (không còn TODO/IN_PROGRESS).
                //    minute_action_items.status ENUM('TODO','IN_PROGRESS','DONE','CANCELLED').
                var instanceMinuteIds = await _db.Minutes
                    .Where(m => m.VisitInstanceId == instance.VisitInstanceId)
                    .Select(m => m.MinutesId)
                    .ToListAsync(cancellationToken);
                if (instanceMinuteIds.Count > 0)
                {
                    var openActionItems = await _db.MinuteActionItems
                        .CountAsync(ai => instanceMinuteIds.Contains(ai.MinutesId)
                                          && ai.Status != "DONE" && ai.Status != "CANCELLED", cancellationToken);
                    if (openActionItems > 0)
                        blockers.Add($"{openActionItems} đầu mục công việc trong biên bản chưa hoàn tất");
                }

                // 5. Có ít nhất 1 bài tin tức ĐÃ DUYỆT (PUBLISHED) HOẶC Host xác nhận không cần tin tức.
                //    Xác nhận của Host được ghi nhận (persist) lên instance để đóng đoàn lần sau không hỏi lại.
                if (request.ConfirmNoNews && !instance.NewsNotRequired)
                    instance.NewsNotRequired = true;
                if (!instance.NewsNotRequired)
                {
                    var hasPublishedNews = await _db.News
                        .AnyAsync(n => n.VisitInstanceId == instance.VisitInstanceId
                                       && n.Status == NewsConstants.Status.Published, cancellationToken);
                    if (!hasPublishedNews)
                        blockers.Add("chưa có bài tin tức đã được đăng — vui lòng chờ Staff Leader duyệt bài hoặc xác nhận chuyến này không cần tin tức");
                }

                if (blockers.Count > 0)
                    throw new ConflictException("Chưa thể đóng đoàn vì: " + string.Join("; ", blockers) + ".");

                newStatus = VisitInstanceStatus.Closed;
                action = "CLOSE_VISIT_INSTANCE";
                message = "Đã đóng đoàn. Hồ sơ tiếp khách được lưu trữ.";
                instance.ClosedBy = actorId;
                instance.ClosedAt = now;
                break;

            default:
                throw new BusinessRuleException("Giai đoạn không hợp lệ.");
        }

        instance.Status = newStatus;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;
        instance.RowVersion += 1;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = action,
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        // --- Notifications ---
        var notifications = new List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
        var visitDetailUrl = $"/dashboard/visit/process/{instance.VisitInstanceId}";

        // G3 (spec §5 Visitor/Host): mời gửi feedback NGAY khi Host bấm "Xác nhận kết thúc tiếp
        // khách" (DURING_VISIT → AFTER_VISIT) — đánh giá là hoạt động của giai đoạn Sau tiếp khách,
        // không đợi tới lúc đóng đoàn. DedupeKey chống gửi trùng nếu transition bị gọi lại.
        if (newStatus == VisitInstanceStatus.AfterVisit)
        {
            var feedbackUrl = $"/dashboard/visit?visitRequestId={instance.VisitRequestId}&feedbackVisitInstanceId={instance.VisitInstanceId}";
            // The feedback invitation asks about THIS campus, so it goes to the person who was
            // here — its operational contact — and to the registrant.
            var visitorRecipients = instance.VisitRequest is null
                ? Enumerable.Empty<ulong>()
                : VisitRequestOwnership.GuestSideRecipients(instance.VisitRequest, instance);

            foreach (var recipientId in visitorRecipients)
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: recipientId,
                    Title: "Mời bạn đánh giá chuyến thăm",
                    Message: $"Chuyến thăm {instance.VisitRequest?.RequestCode} đã kết thúc tiếp khách. Hãy dành chút thời gian đánh giá trải nghiệm của bạn.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                    RelatedId: instance.VisitInstanceId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Feedback,
                    IsActionRequired: true,
                    VisitRequestId: instance.VisitRequestId,
                    VisitInstanceId: instance.VisitInstanceId,
                    CampusId: instance.CampusId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: feedbackUrl,
                    DedupeKey: $"FEEDBACK_INVITE_VISITOR_{instance.VisitInstanceId}_{recipientId}",
                    MetadataJson: PEMS.Application.Notifications.Common.NotificationMessageKeys.BuildMetadata(
                        PEMS.Application.Notifications.Common.NotificationMessageKeys.FeedbackInviteVisitor,
                        new { requestCode = instance.VisitRequest?.RequestCode })
                ));
            }
            if (instance.CurrentHostUserId.HasValue)
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: instance.CurrentHostUserId.Value,
                    Title: "Mời bạn đánh giá thành phần tham gia",
                    Message: $"Đoàn {(instance.VisitRequest is { } cvr ? Services.VisitFormRead.VisitInstanceEffectiveName.Of(cvr, instance.FormDetail) : null)} đã kết thúc tiếp khách. Hãy đánh giá đoàn khách và các bên đã hỗ trợ bạn.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                    RelatedId: instance.VisitInstanceId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Feedback,
                    IsActionRequired: true,
                    VisitInstanceId: instance.VisitInstanceId,
                    CampusId: instance.CampusId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: feedbackUrl,
                    DedupeKey: $"FEEDBACK_INVITE_HOST_{instance.VisitInstanceId}"
                ));
            }
        }

        if (newStatus == VisitInstanceStatus.Closed && instance.VisitRequest is not null)
        {
            // Closing is news about THIS campus, so the campus's contact and the registrant both hear
            // it. The registrant used to be left out entirely: on a request they submitted for
            // somebody else, nothing ever told them it had finished.
            foreach (var recipientId in
                     VisitRequestOwnership.GuestSideRecipients(instance.VisitRequest, instance))
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: recipientId,
                    Title: "Kết thúc chuyến thăm",
                    Message: $"Chuyến thăm {instance.VisitRequest.RequestCode} của bạn đã hoàn tất. Cảm ơn bạn!",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitClosed,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                    RelatedId: instance.VisitInstanceId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    VisitInstanceId: instance.VisitInstanceId,
                    CampusId: instance.CampusId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: visitDetailUrl,
                    MetadataJson: PEMS.Application.Notifications.Common.NotificationMessageKeys.BuildMetadata(
                        PEMS.Application.Notifications.Common.NotificationMessageKeys.VisitClosed,
                        new { requestCode = instance.VisitRequest.RequestCode })
                ));
            }
        }

        if (notifications.Any())
        {
            await _notificationService.CreateManyAsync(notifications, cancellationToken);
        }

        return new CompleteVisitStageResponse(
            instance.VisitRequestId, instance.VisitInstanceId, instance.Status, message);
    }

    /// <summary>
    /// Spec §9: a campus instance may only be in DURING_VISIT / AFTER_VISIT / CLOSED when it
    /// has at least one agenda row. Enforced before every such transition (the DB carries the
    /// same rule; this check turns it into a clean 409 instead of a raw trigger 500).
    /// </summary>
    private async Task EnsureAgendaExistsAsync(ulong visitInstanceId, CancellationToken cancellationToken)
    {
        var hasAgenda = await _db.VisitAgendas
            .AnyAsync(a => a.VisitInstanceId == visitInstanceId, cancellationToken);
        if (!hasAgenda)
            throw new ConflictException(
                "Cần có agenda trước khi bắt đầu tiếp khách.",
                VisitRequestErrorCodes.VisitAgendaRequiredBeforeStart);
    }
}
