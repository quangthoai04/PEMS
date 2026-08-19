using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Feedbacks.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Feedbacks;

namespace PEMS.Application.Feedbacks.Commands.SubmitVisitFeedback;

public sealed class SubmitVisitFeedbackCommandHandler
    : IRequestHandler<SubmitVisitFeedbackCommand, SubmitVisitFeedbackResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public SubmitVisitFeedbackCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notificationService = notificationService;
    }

    public async Task<SubmitVisitFeedbackResponse> Handle(
        SubmitVisitFeedbackCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var userId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);
        var visitRequest = await _db.VisitRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.VisitRequestId == instance.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", instance.VisitRequestId);

        var actorType = FeedbackEligibility.ActorTypeOf(userId, visitRequest, instance)
            ?? throw new ForbiddenException("Bạn không có quyền đánh giá chuyến thăm này.");
        if (!FeedbackEligibility.IsInstanceStatusEligible(instance.Status))
            throw new BusinessRuleException("Chỉ đánh giá được sau khi Host xác nhận kết thúc tiếp khách.");

        // Actor may only submit feedback types of their role (chk_feedbacks_flow).
        foreach (var item in request.Items)
        {
            if (FeedbackRules.SubmitterRoleOf(item.FeedbackType) != actorType)
                throw new ForbiddenException(actorType == FeedbackSubmitterRoles.Visitor
                    ? "Visitor chỉ được gửi đánh giá chung chuyến thăm."
                    : "Host chỉ được gửi đánh giá cho bên tham gia và bên hậu cần.");
        }

        // In-batch duplicate targets.
        var duplicateInBatch = request.Items
            .GroupBy(i => (i.TargetType, i.TargetUserId, i.TargetParticipantId, i.TargetGuestMemberId,
                           i.TargetLogisticsItemId, i.TargetHandoverId, i.TargetDepartmentId))
            .Any(g => g.Count() > 1);
        if (duplicateInBatch)
            throw new ConflictException("Danh sách gửi có mục đánh giá bị trùng đối tượng.");

        var myExisting = await _db.Feedbacks.AsNoTracking()
            .Where(f => f.VisitInstanceId == instance.VisitInstanceId && f.SubmittedByUserId == userId)
            .ToListAsync(cancellationToken);

        var submitterName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId).Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Người dùng";

        var lookups = await LoadTargetLookupsAsync(request.Items, instance.VisitInstanceId, visitRequest.VisitRequestId, cancellationToken);

        // Feedback is INSTANCE-scoped → THIS instance's own detail name for target snapshots and
        // notifications. There is no request-level name to fall back to.
        var effectiveDelegationName = (await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
            .ForInstancesAsync(_db, new[] { instance.VisitInstanceId }, cancellationToken))
            .GetValueOrDefault(instance.VisitInstanceId) ?? visitRequest.RequestCode;

        var now = _clock.VietnamNow;
        var toAdd = new List<Feedback>();
        foreach (var item in request.Items)
        {
            if (myExisting.Any(f => FeedbackRules.SameTarget(
                    f, item.TargetType, item.TargetUserId, item.TargetParticipantId, item.TargetGuestMemberId,
                    item.TargetLogisticsItemId, item.TargetHandoverId, item.TargetDepartmentId)))
                throw new ConflictException("Bạn đã đánh giá mục này rồi.");

            var (targetName, targetRole, targetContext) = ResolveTarget(item, visitRequest, instance, effectiveDelegationName, lookups);

            toAdd.Add(new Feedback
            {
                VisitRequestId = visitRequest.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                FeedbackType = item.FeedbackType,
                SubmittedByUserId = userId,
                SubmitterRole = actorType,
                SubmitterContext = actorType == FeedbackSubmitterRoles.Host ? "Host phụ trách" : "Người tạo đoàn",
                SubmitterNameSnapshot = submitterName,
                TargetType = item.TargetType,
                TargetUserId = item.TargetUserId,
                TargetParticipantId = item.TargetParticipantId,
                TargetGuestMemberId = item.TargetGuestMemberId,
                TargetLogisticsItemId = item.TargetLogisticsItemId,
                TargetHandoverId = item.TargetHandoverId,
                TargetDepartmentId = item.TargetDepartmentId,
                TargetRole = targetRole,
                TargetContext = targetContext,
                TargetNameSnapshot = targetName,
                Rating = item.Rating,
                Comment = string.IsNullOrWhiteSpace(item.Comment) ? null : item.Comment.Trim(),
                SubmittedAt = now,
            });
        }

        _db.Feedbacks.AddRange(toAdd);
        await _db.SaveChangesAsync(cancellationToken);

        // G4/G5 (spec §5): notify về việc feedback đã được ghi nhận.
        var feedbackNotifs = new List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
        var visitDetailUrl = $"/dashboard/visit/process/{instance.VisitInstanceId}";

        if (actorType == FeedbackSubmitterRoles.Visitor)
        {
            // G4: Visitor đánh giá chung chuyến thăm — Host hiện tại + Staff Leader (coordinator)
            // campus đó được biết feedback đã được ghi nhận. Bấm vào phải mở modal xem NỘI DUNG
            // đánh giá (rating/comment) — không điều hướng sang trang setup đoàn.
            var recipientIds = new HashSet<ulong>();
            if (instance.CurrentHostUserId.HasValue) recipientIds.Add(instance.CurrentHostUserId.Value);
            if (instance.CoordinatorUserId.HasValue) recipientIds.Add(instance.CoordinatorUserId.Value);
            recipientIds.Remove(userId);

            foreach (var recipientId in recipientIds)
            {
                feedbackNotifs.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: recipientId,
                    Title: "Visitor đã gửi đánh giá",
                    Message: $"Visitor đã gửi đánh giá cho chuyến thăm {visitRequest.RequestCode} ({effectiveDelegationName}).",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                    RelatedId: instance.VisitInstanceId,
                    ActorUserId: userId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Feedback,
                    VisitRequestId: visitRequest.VisitRequestId,
                    VisitInstanceId: instance.VisitInstanceId,
                    CampusId: instance.CampusId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitorFeedbackModal,
                    ActionUrl: visitDetailUrl,
                    MetadataJson: PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                        PEMS.Application.Notifications.Common.NotificationEventKeys.VisitorFeedbackReceived,
                        new { delegationName = effectiveDelegationName })
                ));
            }
        }
        else if (actorType == FeedbackSubmitterRoles.Host)
        {
            // G5: Host chấm feedback về chính 1 user có tài khoản (Student/Dept Leader/Dept
            // Staff/Staff) — chỉ khi TargetType==USER (GuestMember không có tài khoản, bỏ qua).
            foreach (var f in toAdd.Where(f => f.TargetType == FeedbackTargetTypes.User && f.TargetUserId.HasValue))
            {
                feedbackNotifs.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: f.TargetUserId!.Value,
                    Title: "Host đã đánh giá bạn",
                    Message: $"Host đã gửi đánh giá về bạn trong chuyến thăm {visitRequest.RequestCode} ({effectiveDelegationName}).",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                    RelatedId: instance.VisitInstanceId,
                    ActorUserId: userId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Feedback,
                    VisitRequestId: visitRequest.VisitRequestId,
                    VisitInstanceId: instance.VisitInstanceId,
                    CampusId: instance.CampusId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenHostFeedbackModal,
                    ActionUrl: visitDetailUrl,
                    MetadataJson: PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                        PEMS.Application.Notifications.Common.NotificationEventKeys.HostFeedbackReceived,
                        new { delegationName = effectiveDelegationName, feedbackId = f.FeedbackId }),
                    DedupeKey: $"FEEDBACK_{f.FeedbackId}"
                ));
            }
        }

        if (feedbackNotifs.Count > 0)
            await _notificationService.CreateManyAsync(feedbackNotifs, cancellationToken);

        return new SubmitVisitFeedbackResponse
        {
            SavedCount = toAdd.Count,
            AlreadySubmittedAllRequired = true,
            Message = "Đã gửi đánh giá. Cảm ơn bạn!",
        };
    }

    private sealed class TargetLookups
    {
        public Dictionary<ulong, (ulong UserId, string FullName, string Role)> Participants { get; init; } = new();
        public Dictionary<ulong, (string FullName, string Organization)> GuestMembers { get; init; } = new();
        public Dictionary<ulong, (string Title, ulong? DeptId)> LogisticsItems { get; init; } = new();
        public Dictionary<ulong, ulong> HandoverItemIds { get; init; } = new();   // handoverId → logisticsItemId
        public Dictionary<ulong, string> Departments { get; init; } = new();
        public Dictionary<ulong, string> Users { get; init; } = new();
        public HashSet<ulong> InstanceParticipantUserIds { get; init; } = new();
        public HashSet<ulong> InstanceLogisticsDeptIds { get; init; } = new();
    }

    /// <summary>Batch-load every referenced target row, scoped to this instance/request.</summary>
    private async Task<TargetLookups> LoadTargetLookupsAsync(
        List<SubmitVisitFeedbackItem> items, ulong visitInstanceId, ulong visitRequestId, CancellationToken ct)
    {
        var participantIds = items.Where(i => i.TargetParticipantId.HasValue).Select(i => i.TargetParticipantId!.Value).Distinct().ToList();
        var guestIds = items.Where(i => i.TargetGuestMemberId.HasValue).Select(i => i.TargetGuestMemberId!.Value).Distinct().ToList();
        var logisticsIds = items.Where(i => i.TargetLogisticsItemId.HasValue).Select(i => i.TargetLogisticsItemId!.Value).Distinct().ToList();
        var handoverIds = items.Where(i => i.TargetHandoverId.HasValue).Select(i => i.TargetHandoverId!.Value).Distinct().ToList();
        var deptIds = items.Where(i => i.TargetDepartmentId.HasValue).Select(i => i.TargetDepartmentId!.Value).Distinct().ToList();
        var userIds = items.Where(i => i.TargetUserId.HasValue).Select(i => i.TargetUserId!.Value).Distinct().ToList();

        var participants = participantIds.Count == 0
            ? new Dictionary<ulong, (ulong, string, string)>()
            : await (from p in _db.VisitParticipants.AsNoTracking()
                     join u in _db.Users.AsNoTracking() on p.UserId equals u.UserId
                     where participantIds.Contains(p.ParticipantId) && p.VisitInstanceId == visitInstanceId
                     select new { p.ParticipantId, p.UserId, u.FullName, p.ParticipantRole })
                .ToDictionaryAsync(x => x.ParticipantId, x => (x.UserId, x.FullName, x.ParticipantRole), ct);

        var guests = guestIds.Count == 0
            ? new Dictionary<ulong, (string, string)>()
            : await _db.VisitGuestMembers.AsNoTracking()
                .Where(m => guestIds.Contains(m.GuestMemberId) && m.VisitRequestId == visitRequestId)
                .ToDictionaryAsync(m => m.GuestMemberId, m => (m.FullName, m.Organization), ct);

        var logistics = logisticsIds.Count == 0
            ? new Dictionary<ulong, (string, ulong?)>()
            : await _db.VisitLogisticsItems.AsNoTracking()
                .Where(l => logisticsIds.Contains(l.LogisticsItemId) && l.VisitInstanceId == visitInstanceId)
                .ToDictionaryAsync(l => l.LogisticsItemId, l => (l.Title, l.RequestedToDepartmentId), ct);

        var handoverItems = handoverIds.Count == 0
            ? new Dictionary<ulong, ulong>()
            : await (from h in _db.VisitLogisticsItemHandovers.AsNoTracking()
                     join l in _db.VisitLogisticsItems.AsNoTracking() on h.LogisticsItemId equals l.LogisticsItemId
                     where handoverIds.Contains(h.HandoverId) && l.VisitInstanceId == visitInstanceId
                     select new { h.HandoverId, h.LogisticsItemId })
                .ToDictionaryAsync(x => x.HandoverId, x => x.LogisticsItemId, ct);

        var departments = deptIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Departments.AsNoTracking()
                .Where(d => deptIds.Contains(d.DepartmentId))
                .ToDictionaryAsync(d => d.DepartmentId, d => d.Name, ct);

        var users = userIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        var instanceParticipantUserIds = (await _db.VisitParticipants.AsNoTracking()
            .Where(p => p.VisitInstanceId == visitInstanceId)
            .Select(p => p.UserId).ToListAsync(ct)).ToHashSet();

        var instanceLogisticsDeptIds = (await _db.VisitLogisticsItems.AsNoTracking()
            .Where(l => l.VisitInstanceId == visitInstanceId && l.RequestedToDepartmentId != null)
            .Select(l => l.RequestedToDepartmentId!.Value).ToListAsync(ct)).ToHashSet();

        return new TargetLookups
        {
            Participants = participants,
            GuestMembers = guests,
            LogisticsItems = logistics,
            HandoverItemIds = handoverItems,
            Departments = departments,
            Users = users,
            InstanceParticipantUserIds = instanceParticipantUserIds,
            InstanceLogisticsDeptIds = instanceLogisticsDeptIds,
        };
    }

    /// <summary>
    /// Verifies the target row exists AND belongs to this visit, then returns display snapshots.
    /// Throws BusinessRuleException otherwise — never stores a feedback pointing outside the visit.
    /// </summary>
    private (string Name, string? Role, string Context) ResolveTarget(
        SubmitVisitFeedbackItem item,
        Domain.Entities.Delegations.VisitRequest visitRequest,
        Domain.Entities.Delegations.VisitRequestCampus instance,
        string effectiveDelegationName,
        TargetLookups lookups)
    {
        switch (item.TargetType)
        {
            case FeedbackTargetTypes.VisitRequest:
            case FeedbackTargetTypes.VisitInstance:
                return (effectiveDelegationName, "Đoàn tiếp đón", "Đánh giá chung chuyến thăm");

            case FeedbackTargetTypes.VisitParticipant:
                if (!lookups.Participants.TryGetValue(item.TargetParticipantId!.Value, out var p))
                    throw new BusinessRuleException("Người tham gia được đánh giá không thuộc chuyến thăm này.");
                return (p.FullName, p.Role, "Setup");

            case FeedbackTargetTypes.GuestMember:
                if (!lookups.GuestMembers.TryGetValue(item.TargetGuestMemberId!.Value, out var g))
                    throw new BusinessRuleException("Khách được đánh giá không thuộc đoàn này.");
                return (g.FullName, "Khách", "Đoàn khách");

            case FeedbackTargetTypes.LogisticsItem:
                if (!lookups.LogisticsItems.TryGetValue(item.TargetLogisticsItemId!.Value, out var l))
                    throw new BusinessRuleException("Hạng mục hậu cần được đánh giá không thuộc chuyến thăm này.");
                return (l.Title, null, "Detail setup");

            case FeedbackTargetTypes.LogisticsHandover:
                if (!lookups.HandoverItemIds.ContainsKey(item.TargetHandoverId!.Value))
                    throw new BusinessRuleException("Biên bản ký mượn/trả được đánh giá không thuộc chuyến thăm này.");
                return ($"Biên bản bàn giao #{item.TargetHandoverId}", null, "Detail setup");

            case FeedbackTargetTypes.Department:
                if (!lookups.Departments.TryGetValue(item.TargetDepartmentId!.Value, out var deptName))
                    throw new BusinessRuleException("Phòng ban được đánh giá không tồn tại.");
                if (!lookups.InstanceLogisticsDeptIds.Contains(item.TargetDepartmentId!.Value))
                    throw new BusinessRuleException("Phòng ban được đánh giá không hỗ trợ hậu cần cho chuyến thăm này.");
                return (deptName, "Phòng ban", "Detail setup");

            case FeedbackTargetTypes.User:
                if (!lookups.Users.TryGetValue(item.TargetUserId!.Value, out var userName))
                    throw new BusinessRuleException("Người được đánh giá không tồn tại.");
                // A USER target must relate to this visit: the request creator or an internal participant.
                // “Creator” here means the guest side of the visit being rated: this campus’s
                // operational contact, or the registrant who submitted the request.
                var isCreator = instance.OperationalContactUserId == item.TargetUserId
                    || visitRequest.RegistrantUserId == item.TargetUserId;
                var isParticipant = lookups.InstanceParticipantUserIds.Contains(item.TargetUserId!.Value);
                if (!isCreator && !isParticipant)
                    throw new BusinessRuleException("Người được đánh giá không liên quan đến chuyến thăm này.");
                return (userName, isCreator ? "Người tạo đoàn" : "Người tham gia",
                        isCreator ? "Người tạo đoàn khách" : "Setup");

            default:
                throw new BusinessRuleException("Loại đối tượng đánh giá không hợp lệ.");
        }
    }
}
