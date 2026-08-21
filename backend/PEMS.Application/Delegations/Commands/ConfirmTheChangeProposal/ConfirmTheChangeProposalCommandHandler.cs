using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentReceptionTasks.Common;

using PEMS.Application.Common;
namespace PEMS.Application.Delegations.Commands.ConfirmTheChangeProposal;

public sealed class ConfirmTheChangeProposalCommandHandler : IRequestHandler<ConfirmTheChangeProposalCommand, ConfirmTheChangeProposalResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
    private readonly IUserMutationLockService _lockService;

    public ConfirmTheChangeProposalCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        IUserMutationLockService lockService)
    {
        _db = db;
        _currentUser = currentUser;
        _notificationService = notificationService;
        _lockService = lockService;
    }

    public async Task<ConfirmTheChangeProposalResponse> Handle(ConfirmTheChangeProposalCommand request, CancellationToken cancellationToken)
    {
        // Patch 7 (P7.1): these three used to throw bare framework exceptions, which
        // ExceptionHandlingMiddleware's `default` case can only treat as an unhandled fault — a real
        // user hitting a stale link (item deleted/moved) or double-clicking Accept/Reject got "Đã xảy
        // ra lỗi hệ thống" for what is, in every case, an ordinary and expected outcome.
        if (_currentUser.UserId is null)
            throw new ForbiddenException("Bạn cần đăng nhập để phản hồi đề xuất.");

        var visitInstanceId = await _db.VisitLogisticsItems
            .Where(x => x.LogisticsItemId == request.LogisticsItemId)
            .Select(x => (ulong?)x.VisitInstanceId)
            .FirstOrDefaultAsync(cancellationToken);
        if (visitInstanceId is null)
            throw new NotFoundException("Không tìm thấy đơn yêu cầu.", LogisticsTaskErrorCodes.RequestNotFound);
        var visitRequestId = await _db.VisitRequestCampuses
            .Where(c => c.VisitInstanceId == visitInstanceId)
            .Select(c => (ulong?)c.VisitRequestId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến tiếp khách.");

        // Lock hierarchy (see IUserMutationLockService) — this handler previously took no lock and no
        // transaction at all; a Portal proposal decision is the last remaining channel for this item
        // (spec BUG-07), so a double-click here needs the same protection every other response gets.
        await using var transaction = await _db.BeginSerializedTransactionAsync(cancellationToken);
        await _lockService.LockVisitRequestsAsync(new[] { visitRequestId }, cancellationToken);
        await _lockService.LockVisitRequestCampusesAsync(new[] { visitInstanceId.Value }, cancellationToken);
        await _lockService.LockVisitLogisticsItemsAsync(new[] { request.LogisticsItemId }, cancellationToken);

        var item = await _db.VisitLogisticsItems
            .Include(x => x.VisitInstance)
                .ThenInclude(x => x.VisitRequest)
            .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

        if (item == null)
            throw new NotFoundException("Không tìm thấy đơn yêu cầu.", LogisticsTaskErrorCodes.RequestNotFound);

        // Not a defect: a normal double-click, or a second person acting on the same item after it was
        // already answered/withdrawn, reaches here just as easily as a genuinely stale link.
        if (item.Status != "CHANGE_PROPOSED")
            throw new ConflictException(
                "Đơn yêu cầu không ở trạng thái đang đề xuất.", LogisticsTaskErrorCodes.ProposalNotAwaitingConfirmation);

        var now = VietnamTime.Now();
        item.ProposalResponse = request.Accepted ? "ACCEPTED" : "REJECTED";
        item.ProposalRespondedBy = _currentUser.UserId.Value;
        item.ProposalRespondedAt = now;
        item.ProposalResponseNote = request.Note?.Trim();
        item.UpdatedBy = _currentUser.UserId.Value;
        item.UpdatedAt = now;

        if (request.Accepted)
        {
            if (item.ProposedUsageStartAt.HasValue) item.UsageStartAt = item.ProposedUsageStartAt.Value;
            if (item.ProposedUsageEndAt.HasValue) item.UsageEndAt = item.ProposedUsageEndAt.Value;
            if (!string.IsNullOrWhiteSpace(item.ProposedDescription)) item.Description = item.ProposedDescription;
            item.Status = "ACCEPTED";
        }
        else
        {
            item.Status = "REJECTED";
            item.DecisionNote = request.Note?.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);

        var notifications = new System.Collections.Generic.List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
        ulong? assigneeId = item.AssignedToUserId;
        ulong? assignedBy = item.AssignedBy;
        ulong? proposedBy = item.ProposedBy;
        var currentUserId = _currentUser.UserId.Value;
        var deptTaskUrl = item.RequestedToDepartmentId.HasValue
            ? $"/dashboard/departments/{item.RequestedToDepartmentId.Value}/tasks/{item.LogisticsItemId}"
            : null;

        string actionText = request.Accepted ? "ĐÃ CHẤP NHẬN" : "ĐÃ TỪ CHỐI";
        string msgText = request.Accepted
            ? $"Host đã chấp nhận đề xuất thay đổi cho nhiệm vụ \"{item.Title}\"."
            : $"Host đã từ chối đề xuất thay đổi cho nhiệm vụ \"{item.Title}\".";
        var delegationName = (await PEMS.Application.Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
            .ForInstancesAsync(_db, new[] { item.VisitInstanceId }, cancellationToken))
            .GetValueOrDefault(item.VisitInstanceId) ?? item.Title;
        string proposalMetadataJson = PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
            request.Accepted
                ? PEMS.Application.Notifications.Common.NotificationEventKeys.LogisticsProposalAccepted
                : PEMS.Application.Notifications.Common.NotificationEventKeys.LogisticsProposalRejected,
            new { delegationName });

        if (assigneeId.HasValue && assigneeId.Value != currentUserId)
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: assigneeId.Value,
                Title: "Phản hồi đề xuất hậu cần",
                Message: msgText,
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsProposalResponded,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                RelatedId: item.LogisticsItemId,
                ActorUserId: currentUserId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                VisitInstanceId: item.VisitInstanceId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                ActionUrl: deptTaskUrl ?? $"/dashboard/visit/process/{item.VisitInstanceId}",
                MetadataJson: proposalMetadataJson
            ));
        }

        if (assignedBy.HasValue && assignedBy.Value != currentUserId && (!assigneeId.HasValue || assignedBy.Value != assigneeId.Value))
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: assignedBy.Value,
                Title: "Phản hồi đề xuất hậu cần",
                Message: msgText,
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsProposalResponded,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                RelatedId: item.LogisticsItemId,
                ActorUserId: currentUserId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                VisitInstanceId: item.VisitInstanceId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                ActionUrl: deptTaskUrl ?? $"/dashboard/visit/process/{item.VisitInstanceId}",
                MetadataJson: proposalMetadataJson
            ));
        }

        // The person who actually authored the proposal must learn the outcome too — they may be
        // neither the assignee nor the assignedBy (e.g. a Dept Leader proposing on their own item).
        if (proposedBy.HasValue && proposedBy.Value != currentUserId
            && (!assigneeId.HasValue || proposedBy.Value != assigneeId.Value)
            && (!assignedBy.HasValue || proposedBy.Value != assignedBy.Value))
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: proposedBy.Value,
                Title: "Phản hồi đề xuất hậu cần",
                Message: msgText,
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsProposalResponded,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                RelatedId: item.LogisticsItemId,
                ActorUserId: currentUserId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                VisitInstanceId: item.VisitInstanceId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                ActionUrl: deptTaskUrl ?? $"/dashboard/visit/process/{item.VisitInstanceId}",
                MetadataJson: proposalMetadataJson
            ));
        }

        if (notifications.Count > 0)
        {
            await _notificationService.CreateManyAsync(notifications, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new ConfirmTheChangeProposalResponse
        {
            LogisticsItemId = item.LogisticsItemId,
            Status = item.Status,
            Message = request.Accepted ? "Đã chấp nhận đề xuất thay đổi." : "Đã từ chối đề xuất thay đổi."
        };
    }
}
