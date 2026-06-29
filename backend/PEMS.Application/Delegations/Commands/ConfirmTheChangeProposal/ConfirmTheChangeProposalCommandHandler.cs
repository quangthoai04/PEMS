using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Delegations.Commands.ConfirmTheChangeProposal;

public sealed class ConfirmTheChangeProposalCommandHandler : IRequestHandler<ConfirmTheChangeProposalCommand, ConfirmTheChangeProposalResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public ConfirmTheChangeProposalCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _notificationService = notificationService;
    }

    public async Task<ConfirmTheChangeProposalResponse> Handle(ConfirmTheChangeProposalCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is null)
            throw new UnauthorizedAccessException("Bạn cần đăng nhập để phản hồi đề xuất.");

        var item = await _db.VisitLogisticsItems
            .Include(x => x.VisitInstance)
                .ThenInclude(x => x.VisitRequest)
            .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

        if (item == null)
            throw new InvalidOperationException("Không tìm thấy đơn yêu cầu.");

        if (item.Status != "CHANGE_PROPOSED")
            throw new InvalidOperationException("Đơn yêu cầu không ở trạng thái đang đề xuất.");

        var now = DateTime.UtcNow;
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

        var notifications = new System.Collections.Generic.List<PEMS.Application.Notifications.Common.CreateNotificationItem>();
        ulong? assigneeId = item.AssignedToUserId;
        ulong? assignedBy = item.AssignedBy;

        string actionText = request.Accepted ? "ĐÃ CHẤP NHẬN" : "ĐÃ TỪ CHỐI";
        string msgText = request.Accepted 
            ? $"Host đã chấp nhận đề xuất thay đổi cho nhiệm vụ \"{item.Title}\"."
            : $"Host đã từ chối đề xuất thay đổi cho nhiệm vụ \"{item.Title}\".";

        if (assigneeId.HasValue && assigneeId.Value != _currentUser.UserId.Value)
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationItem(
                assigneeId.Value,
                "Phản hồi đề xuất hậu cần",
                msgText,
                PEMS.Application.Notifications.Common.NotificationTypes.LogisticsProposalResponded,
                PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                item.LogisticsItemId
            ));
        }

        if (assignedBy.HasValue && assignedBy.Value != _currentUser.UserId.Value && (!assigneeId.HasValue || assignedBy.Value != assigneeId.Value))
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationItem(
                assignedBy.Value,
                "Phản hồi đề xuất hậu cần",
                msgText,
                PEMS.Application.Notifications.Common.NotificationTypes.LogisticsProposalResponded,
                PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                item.LogisticsItemId
            ));
        }
        else if (item.RequestedToDepartmentId.HasValue)
        {
            var dept = await _db.Departments.FirstOrDefaultAsync(d => d.DepartmentId == item.RequestedToDepartmentId.Value, cancellationToken);
            if (dept?.HeadUserId != null && dept.HeadUserId.Value != _currentUser.UserId.Value && (!assigneeId.HasValue || dept.HeadUserId.Value != assigneeId.Value))
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationItem(
                    dept.HeadUserId.Value,
                    "Phản hồi đề xuất hậu cần",
                    msgText,
                    PEMS.Application.Notifications.Common.NotificationTypes.LogisticsProposalResponded,
                    PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                    item.LogisticsItemId
                ));
            }
        }

        if (notifications.Count > 0)
        {
            await _notificationService.CreateManyAsync(notifications, cancellationToken);
        }

        return new ConfirmTheChangeProposalResponse
        {
            LogisticsItemId = item.LogisticsItemId,
            Status = item.Status,
            Message = request.Accepted ? "Đã chấp nhận đề xuất thay đổi." : "Đã từ chối đề xuất thay đổi."
        };
    }
}
