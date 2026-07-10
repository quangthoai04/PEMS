using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.SignLogisticsHandover;

public class SignLogisticsHandoverCommand : IRequest<SignLogisticsHandoverResponse>
{
    public ulong LogisticsItemId { get; set; }
    public string HandoverType { get; set; } = default!;
    public string SignerSide { get; set; } = default!;
    public string? Note { get; set; }
}

public sealed class SignLogisticsHandoverResponse
{
    public ulong LogisticsItemId { get; set; }
    public ulong HandoverId { get; set; }
    public string HandoverType { get; set; } = default!;
    public string SignerSide { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string SignedByName { get; set; } = default!;
    public string SignedAt { get; set; } = default!;
}

public class SignLogisticsHandoverCommandHandler : IRequestHandler<SignLogisticsHandoverCommand, SignLogisticsHandoverResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public SignLogisticsHandoverCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
    }

    public async Task<SignLogisticsHandoverResponse> Handle(SignLogisticsHandoverCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new Exception("Bạn cần đăng nhập để ký biên bản.");
        var handoverType = request.HandoverType?.Trim().ToUpperInvariant();
        var signerSide = request.SignerSide?.Trim().ToUpperInvariant();

        if (!LogisticsHandoverTypes.All.Contains(handoverType))
            throw new Exception("Loại biên bản không hợp lệ.");
        if (!HandoverSignerSides.All.Contains(signerSide))
            throw new Exception("Bên ký không hợp lệ.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw new Exception("Không tìm thấy người ký.");

        var item = await _context.VisitLogisticsItems
            .Include(x => x.VisitInstance)
            .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken)
            ?? throw new Exception("Không tìm thấy đơn yêu cầu.");

        if (item.Status is "CANCELLED" or "REJECTED" or "DECLINED")
            throw new Exception("Đơn yêu cầu không còn ở trạng thái có thể ký biên bản.");

        if (item.RequestedToDepartmentId.HasValue && user.DepartmentId != item.RequestedToDepartmentId)
            throw new Exception("Bạn không có quyền ký biên bản của phòng ban khác.");

        var isDepartmentStaff = string.Equals(_currentUserService.RoleCode, RoleCodes.Department, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_currentUserService.SubRole, UserSubRoles.Staff, StringComparison.OrdinalIgnoreCase);
        if (isDepartmentStaff && item.AssignedToUserId != userId)
            throw new Exception("Ban chi co the ky bien ban cua don yeu cau duoc giao cho minh.");

        var now = DateTime.UtcNow;
        var handover = await _context.VisitLogisticsItemHandovers
            .FirstOrDefaultAsync(h => h.LogisticsItemId == request.LogisticsItemId && h.HandoverType == handoverType, cancellationToken);

        if (handover == null)
        {
            handover = new VisitLogisticsItemHandover
            {
                LogisticsItemId = request.LogisticsItemId,
                HandoverType = handoverType!,
                ItemCondition = HandoverItemConditions.Good,
                CreatedAt = now,
                CreatedBy = userId
            };
            _context.VisitLogisticsItemHandovers.Add(handover);
        }

        if (signerSide == HandoverSignerSides.Borrower)
        {
            if (handover.BorrowerSignedAt != null) throw new Exception("Bên nhận đã ký biên bản này.");
            handover.BorrowerSignedBy = userId;
            handover.BorrowerSignedAt = now;
        }
        else
        {
            if (handover.ProviderSignedAt != null) throw new Exception("Bên giao đã ký biên bản này.");
            handover.ProviderSignedBy = userId;
            handover.ProviderSignedAt = now;
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
            handover.ConditionNote = MergeNote(handover.ConditionNote, signerSide!, request.Note.Trim());

        if (handoverType == LogisticsHandoverTypes.Borrow && signerSide == HandoverSignerSides.Provider)
        {
            item.Status = "IN_PROGRESS";
        }
        else if (handoverType == LogisticsHandoverTypes.Return && signerSide == HandoverSignerSides.Borrower)
        {
            item.Status = "DONE";
            item.CompletedAt = now;
        }

        item.UpdatedBy = userId;
        item.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        if (item.VisitInstance?.CurrentHostUserId != null)
        {
            string label = signerSide == HandoverSignerSides.Borrower ? "Bên nhận" : "Bên giao";
            await _notificationService.CreateAsync(
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: item.VisitInstance.CurrentHostUserId.Value,
                    Title: "Ký biên bản bàn giao",
                    Message: $"Nhân sự phòng ban ({user.FullName}) đã ký biên bản bàn giao ({label}) cho nhiệm vụ \"{item.Title}\".",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsHandoverSigned,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsHandover,
                    RelatedId: handover.HandoverId,
                    ActorUserId: userId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Handover,
                    VisitInstanceId: item.VisitInstance.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenHandoverDetail,
                    ActionUrl: $"/dashboard/visit/process/{item.VisitInstance.VisitInstanceId}"),
                cancellationToken
            );
        }

        return new SignLogisticsHandoverResponse
        {
            LogisticsItemId = item.LogisticsItemId,
            HandoverId = handover.HandoverId,
            HandoverType = handover.HandoverType,
            SignerSide = signerSide!,
            Status = item.Status,
            SignedByName = user.FullName,
            SignedAt = now.ToString("O")
        };
    }

    private static string MergeNote(string? existing, string signerSide, string note)
    {
        var label = signerSide == HandoverSignerSides.Borrower ? "Bên nhận" : "Bên giao";
        var line = $"{label}: {note}";
        return string.IsNullOrWhiteSpace(existing) ? line : $"{existing}\n{line}";
    }
}
