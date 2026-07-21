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
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);

        // v2 endpoint only serves v2 requests — a v1 request keeps using its own pending-edit endpoint.
        if (visit.FormSchemaVersion < FormSchemaVersions.PerCampus)
            throw new ConflictException(
                "Đơn này dùng biểu mẫu phiên bản cũ. Vui lòng dùng chức năng sửa đơn hiện tại.",
                VisitRequestErrorCodes.NotPerCampusV2);

        // ── Editor policy (plan §6.4): the registrant, or the primary contact once ACTIVE. A contact still
        //    PENDING_CONFIRMATION has not accepted the request yet and cannot edit it; any other account
        //    (unrelated visitor, staff, host) is rejected — staff-side changes go through amendments. ──
        var isRegistrant = visit.RegistrantUserId == actorId;
        var isActiveContact = visit.VisitorUserId == actorId
                              && string.Equals(visit.PrimaryContactAccessStatus, "ACTIVE", System.StringComparison.OrdinalIgnoreCase);
        if (!isRegistrant && !isActiveContact)
            throw new ForbiddenException("Bạn không có quyền sửa đơn này.");

        // ── Editable-lifecycle gate (v1 parity §2.1): fully pending + ≥ 24h before earliest start ──
        if (visit.Status != VisitRequestStatuses.PendingApproval)
            throw new BusinessRuleException(
                "Chỉ có thể sửa đơn đang chờ xử lý (chưa cơ sở nào ra quyết định).",
                VisitRequestErrorCodes.VisitRequestNotEditable);
        if (visit.CampusInstances.Count == 0
            || visit.CampusInstances.Any(c => c.Status != VisitInstanceStatuses.WaitingRequestApproval))
            throw new BusinessRuleException(
                "Đơn đã có cơ sở được xử lý (duyệt/từ chối/hủy) nên không thể sửa.",
                VisitRequestErrorCodes.VisitRequestNotEditable);
        if (visit.CampusInstances.Min(c => c.PlannedStartAt) < now.AddHours(24))
            throw new BusinessRuleException(
                "Lịch thăm sắp diễn ra trong vòng 24 giờ nên không thể sửa. Vui lòng liên hệ FPTU để được hỗ trợ.",
                VisitRequestErrorCodes.VisitCancelWindowExpired);

        // Campuses involved BEFORE the edit (kept + removed) — their leaders are notified too.
        var campusIdsBefore = visit.CampusInstances.Select(c => c.CampusId).ToList();

        V2EditResult result;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            // The service re-checks concurrency/lifecycle/data rules in-transaction and applies everything.
            result = await _editService.ApplyPendingEditAsync(visit, request.Edit, actorId, now, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        // ── Post-commit notifications (best-effort; a rolled-back edit never notifies) ──
        // Mixed v2: the projection is not business content — the generic notification names the request
        // by code with the explicit mixed label (leaders read their own campus's content in the detail).
        var notifyName = visit.FormSchemaVersion >= FormSchemaVersions.PerCampus
            ? (visit.HasMixedCampusDetails ? "Khác nhau theo cơ sở" : visit.CampusInstances.FirstOrDefault()!.FormDetail!.DelegationName)
            : visit.DelegationName;
        await NotifyLeadersAfterCommitAsync(visit.VisitRequestId, visit.RequestCode, notifyName,
            campusIdsBefore.Concat(visit.CampusInstances.Select(c => c.CampusId)).Distinct().ToList(),
            actorId, cancellationToken);

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
                    ActionType: NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit?visitRequestId={visitRequestId}")).ToList(),
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
