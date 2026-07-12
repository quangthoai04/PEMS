using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Commands.PrepareVisitLogistics;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.SignVisitLogisticsHandover;

public sealed class SignVisitLogisticsHandoverCommandHandler
    : IRequestHandler<SignVisitLogisticsHandoverCommand, SignVisitLogisticsHandoverResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public SignVisitLogisticsHandoverCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock, PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notificationService = notificationService;
    }

    public async Task<SignVisitLogisticsHandoverResponse> Handle(
        SignVisitLogisticsHandoverCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var actorId = _currentUser.UserId.Value;

        var handoverType = request.HandoverType?.Trim().ToUpperInvariant();
        if (!LogisticsHandoverTypes.All.Contains(handoverType))
            throw new BusinessRuleException("Loại biên bản không hợp lệ.");

        var condition = string.IsNullOrWhiteSpace(request.ItemCondition)
            ? null : request.ItemCondition!.Trim().ToUpperInvariant();
        if (condition != null && !HandoverItemConditions.All.Contains(condition))
            throw new BusinessRuleException("Tình trạng tài sản không hợp lệ.");
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (condition != null && HandoverItemConditions.RequireNote.Contains(condition) && string.IsNullOrEmpty(note))
            throw new BusinessRuleException("Vui lòng nhập ghi chú tình trạng tài sản khi tài sản không ở trạng thái tốt.");

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Only the campus instance Host may sign the borrower side from VisitProcess.
        if (instance.CurrentHostUserId != actorId)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được ký biên bản (bên nhận).");

        var item = await _db.VisitLogisticsItems
            .FirstOrDefaultAsync(l => l.LogisticsItemId == request.LogisticsItemId
                                      && l.VisitInstanceId == instance.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitLogisticsItem", request.LogisticsItemId);

        // Closed / not-yet-granted items cannot have a borrower handover signed.
        if (item.Status is LogisticsItemStatus.Cancelled or LogisticsItemStatus.Rejected or LogisticsItemStatus.Declined)
            throw new ConflictException("Yêu cầu hậu cần không còn ở trạng thái có thể ký biên bản.");
        if (item.CoordinationMode == LogisticsCoordinationModes.OfflineCoordinated)
            throw new ConflictException("Hạng mục đã trao đổi bên ngoài hệ thống — không ký biên bản trong hệ thống.");

        var now = _clock.VietnamNow;

        if (handoverType == LogisticsHandoverTypes.Borrow)
        {
            // The resource must have been granted by the department before the borrower can receive it.
            if (item.Status != LogisticsItemStatus.Accepted && item.Status != LogisticsItemStatus.InProgress)
                throw new ConflictException("Chỉ ký nhận (mượn) khi phòng ban đã chấp nhận và bàn giao tài sản.");
        }
        else // RETURN
        {
            // RETURN requires a fully-signed BORROW (both borrower & provider) — handover rule.
            var borrow = await _db.VisitLogisticsItemHandovers
                .FirstOrDefaultAsync(h => h.LogisticsItemId == item.LogisticsItemId
                                          && h.HandoverType == LogisticsHandoverTypes.Borrow, cancellationToken);
            if (borrow is null || borrow.BorrowerSignedAt is null || borrow.ProviderSignedAt is null)
                throw new ConflictException("Cần hoàn tất ký biên bản mượn (đủ hai bên) trước khi ký trả.");
        }

        var handover = await _db.VisitLogisticsItemHandovers
            .FirstOrDefaultAsync(h => h.LogisticsItemId == item.LogisticsItemId && h.HandoverType == handoverType, cancellationToken);
        if (handover is null)
        {
            handover = new VisitLogisticsItemHandover
            {
                LogisticsItemId = item.LogisticsItemId,
                HandoverType = handoverType!,
                ItemCondition = condition ?? HandoverItemConditions.Good,
                CreatedAt = now,
                CreatedBy = actorId,
            };
            _db.VisitLogisticsItemHandovers.Add(handover);
        }
        else if (condition != null)
        {
            handover.ItemCondition = condition;
        }

        if (handover.BorrowerSignedAt != null)
            throw new ConflictException("Bên nhận đã ký biên bản này rồi.");
        handover.BorrowerSignedBy = actorId;
        handover.BorrowerSignedAt = now;

        if (note != null)
        {
            var line = $"Bên nhận: {note}";
            handover.ConditionNote = string.IsNullOrWhiteSpace(handover.ConditionNote)
                ? line : $"{handover.ConditionNote}\n{line}";
        }

        // Borrower signing RETURN closes the item (mirrors SignLogisticsHandoverCommand).
        if (handoverType == LogisticsHandoverTypes.Return)
        {
            item.Status = LogisticsItemStatus.Done;
            item.CompletedAt = now;
        }
        item.UpdatedBy = actorId;
        item.UpdatedAt = now;
        item.RowVersion += 1;

        var actorName = await _db.Users.Where(u => u.UserId == actorId).Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Host";

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = instance.CampusId,
            Action = handoverType == LogisticsHandoverTypes.Borrow ? "LOGISTICS_HANDOVER_BORROW_BORROWER" : "LOGISTICS_HANDOVER_RETURN_BORROWER",
            EntityType = "VisitLogisticsItemHandover",
            EntityId = item.LogisticsItemId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        var deptHandoverUrl = item.RequestedToDepartmentId.HasValue
            ? $"/dashboard/departments/{item.RequestedToDepartmentId.Value}/tasks/{item.LogisticsItemId}"
            : null;

        if (item.AssignedToUserId.HasValue)
        {
            await _notificationService.CreateAsync(
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: item.AssignedToUserId.Value,
                    Title: "Ký biên bản bàn giao",
                    Message: $"Host {actorName} đã ký biên bản bàn giao (Bên nhận) cho nhiệm vụ \"{item.Title}\".",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsHandoverSigned,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsHandover,
                    RelatedId: handover.HandoverId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Handover,
                    VisitInstanceId: instance.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                    ActionUrl: deptHandoverUrl ?? $"/dashboard/visit/process/{instance.VisitInstanceId}"),
                cancellationToken
            );
        }

        return new SignVisitLogisticsHandoverResponse
        {
            LogisticsItemId = item.LogisticsItemId,
            HandoverId = handover.HandoverId,
            HandoverType = handover.HandoverType,
            Status = item.Status,
            SignedByName = actorName,
            SignedAt = now.ToString("yyyy-MM-ddTHH:mm:ss"),
            Message = handoverType == LogisticsHandoverTypes.Borrow
                ? "Đã ký nhận (mượn) tài sản."
                : "Đã ký trả tài sản. Hạng mục hoàn tất.",
        };
    }
}
