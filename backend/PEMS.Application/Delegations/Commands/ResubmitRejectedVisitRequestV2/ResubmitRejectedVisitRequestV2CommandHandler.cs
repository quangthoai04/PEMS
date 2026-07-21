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
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequestV2;

public sealed class ResubmitRejectedVisitRequestV2CommandHandler
    : IRequestHandler<ResubmitRejectedVisitRequestV2Command, ResubmitRejectedVisitRequestV2Response>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitRequestV2EditService _editService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ResubmitRejectedVisitRequestV2CommandHandler> _logger;
    private readonly PerCampusFormV2Options _readFlag;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public ResubmitRejectedVisitRequestV2CommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitRequestV2EditService editService,
        INotificationService notificationService,
        ILogger<ResubmitRejectedVisitRequestV2CommandHandler> logger,
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

    public async Task<ResubmitRejectedVisitRequestV2Response> Handle(
        ResubmitRejectedVisitRequestV2Command request, CancellationToken cancellationToken)
    {
        // ── Flag gate (identical to create-v2 / pending-edit-v2) ──
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

        if (visit.FormSchemaVersion < FormSchemaVersions.PerCampus)
            throw new ConflictException(
                "Đơn này dùng biểu mẫu phiên bản cũ. Vui lòng dùng chức năng gửi lại đơn hiện tại.",
                VisitRequestErrorCodes.NotPerCampusV2);

        // ── Editor policy — same as pending-edit v2 (registrant OR ACTIVE primary contact). ──
        var isRegistrant = visit.RegistrantUserId == actorId;
        var isActiveContact = visit.VisitorUserId == actorId
                              && string.Equals(visit.PrimaryContactAccessStatus, "ACTIVE", System.StringComparison.OrdinalIgnoreCase);
        if (!isRegistrant && !isActiveContact)
            throw new ForbiddenException("Bạn không có quyền gửi lại đơn này.");

        // ── Resubmittable gate (pre-check; the service re-checks in-transaction under the row lock) ──
        if (visit.Status != VisitRequestStatuses.Rejected
            || visit.CampusInstances.Count == 0
            || visit.CampusInstances.Any(c => c.Status != VisitInstanceStatuses.Rejected))
            throw new BusinessRuleException(
                "Chỉ có thể gửi lại đơn khi toàn bộ yêu cầu đã bị từ chối ở tất cả các cơ sở.",
                VisitRequestErrorCodes.VisitRequestNotResubmittable);

        V2EditResult result;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            result = await _editService.ApplyResubmitAsync(visit, request.Edit, actorId, now, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        // ── Post-commit: notify the CURRENT Staff Leader of every campus to re-process (best-effort). ──
        await NotifyLeadersAfterCommitAsync(visit, actorId, cancellationToken);

        var instances = visit.CampusInstances
            .OrderBy(c => c.CampusId)
            .Select(c => new CreateVisitRequestV2CampusRef(c.VisitInstanceId, c.CampusId, c.Status))
            .ToList();
        return new ResubmitRejectedVisitRequestV2Response(
            visit.VisitRequestId, visit.Status, (int)visit.ResubmissionCount,
            result.VisitScope, result.HasMixed, result.RequestRowVersion, instances,
            "Đơn đã được gửi lại thành công và đang chờ các cơ sở xem xét lại.");
    }

    private async Task NotifyLeadersAfterCommitAsync(
        PEMS.Domain.Entities.Delegations.VisitRequest visit, ulong actorId, CancellationToken cancellationToken)
    {
        try
        {
            var leaders = visit.CampusInstances
                .Where(c => c.CoordinatorUserId.HasValue)
                .Select(c => c.CoordinatorUserId!.Value)
                .Distinct()
                .ToList();
            if (leaders.Count == 0) return;

            await _notificationService.CreateManyAsync(
                leaders.Select(id => new CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Visitor đã gửi lại đơn bị từ chối",
                    Message: $"Visitor đã chỉnh sửa và gửi lại đơn {visit.RequestCode} ({(visit.FormSchemaVersion >= FormSchemaVersions.PerCampus ? (visit.HasMixedCampusDetails ? "Khác nhau theo cơ sở" : visit.CampusInstances.FirstOrDefault()!.FormDetail!.DelegationName) : visit.DelegationName)}). Vui lòng xử lý lại tại cơ sở của bạn.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    RelatedId: visit.VisitRequestId,
                    ActorUserId: actorId,
                    Category: NotificationCategories.Visit,
                    IsActionRequired: true,
                    VisitRequestId: visit.VisitRequestId,
                    ActionType: NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}")).ToList(),
                cancellationToken);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex,
                "resubmit-v2 post-commit notification dispatch failed for visit request {VisitRequestId}",
                visit.VisitRequestId);
        }
    }
}
