using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using INotificationService = PEMS.Application.Notifications.Common.INotificationService;

namespace PEMS.Application.Partners.Commands.RejectPartner;

public sealed class RejectPartnerCommandHandler : IRequestHandler<RejectPartnerCommand, RejectPartnerResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly INotificationService _notifications;

    public RejectPartnerCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        INotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notifications = notifications;
    }

    public async Task<RejectPartnerResponse> Handle(RejectPartnerCommand request, CancellationToken cancellationToken)
    {
        var partner = await _db.Partners
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanDecidePartner(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Chỉ Trưởng phòng IC cùng campus mới được từ chối đối tác này.", 403);

        if (partner.ProfileStatus != PartnerProfileStatuses.PendingApproval)
            throw new BusinessRuleException("Hồ sơ đối tác không ở trạng thái chờ duyệt.",
                PartnerErrorCodes.NotPending);

        var now = _clock.VietnamNow;
        partner.ProfileStatus = PartnerProfileStatuses.Rejected;
        partner.ReviewNote = request.Reason.Trim();
        partner.ReviewedBy = _currentUser.UserId;
        partner.ReviewedAt = now;
        // A rejected profile must never stay publicly visible.
        if (partner.Visibility == PartnerVisibilities.Public)
            partner.Visibility = PartnerVisibilities.Internal;
        partner.UpdatedAt = now;
        partner.UpdatedBy = _currentUser.UserId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = partner.OwnerCampusId,
            Action = "REJECT_PARTNER",
            EntityType = "Partner",
            EntityId = partner.PartnerId,
            Changes = new List<AuditLogChange>
            {
                new()
                {
                    FieldName = "ProfileStatus",
                    OldValueText = PartnerProfileStatuses.PendingApproval,
                    NewValueText = $"{PartnerProfileStatuses.Rejected}: {partner.ReviewNote}",
                },
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        if (partner.CreatedBy is { } creatorId && creatorId != _currentUser.UserId)
        {
            await _notifications.CreateAsync(
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: creatorId,
                    Title: "Đối tác bị từ chối",
                    Message: $"Hồ sơ đối tác \"{partner.Name}\" bị từ chối. Lý do: {partner.ReviewNote}",
                    NotificationType: NotificationTypes.PartnerReviewed,
                    RelatedType: "Partner",
                    RelatedId: partner.PartnerId,
                    ActorUserId: _currentUser.UserId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Partner,
                    CampusId: partner.OwnerCampusId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenPartnerDetail,
                    // Trang quản lý đối tác lọc đúng 1 hồ sơ (có nút "Xem tất cả"), không vào thẳng chi tiết.
                    ActionUrl: $"/dashboard/partners?partnerId={partner.PartnerId}"),
                cancellationToken);
        }

        return new RejectPartnerResponse
        {
            PartnerId = partner.PartnerId,
            ProfileStatus = partner.ProfileStatus,
        };
    }
}
