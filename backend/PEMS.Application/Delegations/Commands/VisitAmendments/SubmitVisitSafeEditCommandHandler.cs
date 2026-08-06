using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Domain.Constants;
using PEMS.Domain.Enums;

using PEMS.Application.Delegations.Common;
namespace PEMS.Application.Delegations.Commands.VisitAmendments;

/// <summary>
/// SAFE-EDIT endpoint handler (plan §16.6): flags gate + editor policy (registrant or ACTIVE primary
/// contact), then <see cref="IVisitSafeEditService"/> applies the classified patch in one transaction.
/// A PRIVACY_URGENT media withdrawal gets HIGH-priority post-commit notifications to the affected
/// campus Staff Leaders and current Hosts; normal safe edits notify the leaders normally.
/// </summary>
public sealed class SubmitVisitSafeEditCommandHandler
    : IRequestHandler<SubmitVisitSafeEditCommand, VisitRequestSafeEditResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitSafeEditService _safeEditService;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
    private readonly ILogger<SubmitVisitSafeEditCommandHandler> _logger;
    private readonly PerCampusFormV2Options _readFlag;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public SubmitVisitSafeEditCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitSafeEditService safeEditService,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        ILogger<SubmitVisitSafeEditCommandHandler> logger,
        PerCampusFormV2Options readFlag, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _safeEditService = safeEditService;
        _notificationService = notificationService;
        _logger = logger;
        _readFlag = readFlag;
        _writeFlag = writeFlag;
    }

    public async Task<VisitRequestSafeEditResponse> Handle(
        SubmitVisitSafeEditCommand request, CancellationToken cancellationToken)
    {
        if (!_writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!_readFlag.Enabled)
            throw new ConflictException(
                "Cấu hình không hợp lệ: bật ghi v2 nhưng chưa bật đọc v2.",
                CreateVisitRequestV2.CreateVisitRequestV2ErrorCodes.ReadRequired);
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

        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new BusinessRuleException(
                "Đơn đã bị hủy nên không thể sửa.", VisitRequestErrorCodes.VisitRequestNotEditable);

        // Editor policy. The registrant may correct any campus of their request; anybody else must
        // hold EVERY campus the patch touches. A sparse patch names its campuses explicitly, so this
        // is checkable exactly — and it closes the case where the holder of one campus sent a patch
        // listing three and had all three applied.
        var isRegistrant = VisitRequestOwnership.IsRegistrant(visit, actorId);
        if (!isRegistrant)
        {
            var patched = request.Patch?.Instances ?? new List<SafeInstancePatchDto>();
            if (patched.Count == 0)
                throw new ForbiddenException("Bạn không có quyền sửa đơn này.");

            foreach (var p in patched)
            {
                var target = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == p.VisitInstanceId)
                    ?? throw new NotFoundException("Lịch thăm tại cơ sở", p.VisitInstanceId);
                if (!VisitRequestOwnership.IsOperationalContact(target, actorId))
                    throw new ForbiddenException("Bạn không có quyền sửa cơ sở này trong đơn.");
            }
        }

        VisitRequestSafeEditResponse result;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            result = await _safeEditService.ApplySafeEditAsync(
                visit, request.Patch, actorId, now, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        await NotifyAfterCommitAsync(visit.VisitRequestId, visit.RequestCode, result, actorId, cancellationToken);
        return result;
    }

    private async Task NotifyAfterCommitAsync(
        ulong visitRequestId, string? requestCode, VisitRequestSafeEditResponse result,
        ulong actorId, CancellationToken ct)
    {
        try
        {
            var urgent = result.AppliedChanges.Any(c => c.ChangeClass == AmendmentChangeClasses.PrivacyUrgent);
            var touchedInstanceIds = result.AppliedChanges
                .Where(c => c.VisitInstanceId is not null)
                .Select(c => c.VisitInstanceId!.Value)
                .Distinct().ToList();

            var recipients = new HashSet<ulong>();
            if (touchedInstanceIds.Count > 0)
            {
                var rows = await _db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => touchedInstanceIds.Contains(c.VisitInstanceId))
                    .Select(c => new { c.CampusId, c.CurrentHostUserId })
                    .ToListAsync(ct);
                var campusIds = rows.Select(r => r.CampusId).Distinct().ToList();
                var leaders = await _db.Users.AsNoTracking()
                    .Where(u => u.Role.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Leader
                                && u.Status == UserStatuses.Active && u.PrimaryCampusId.HasValue
                                && campusIds.Contains(u.PrimaryCampusId.Value))
                    .Select(u => u.UserId).ToListAsync(ct);
                foreach (var id in leaders) recipients.Add(id);
                foreach (var host in rows.Where(r => r.CurrentHostUserId.HasValue))
                    recipients.Add(host.CurrentHostUserId!.Value);
            }
            else
            {
                // Request-level-only change → the leaders of every still-active campus.
                var campusIds = await _db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == visitRequestId)
                    .Select(c => c.CampusId).Distinct().ToListAsync(ct);
                var leaders = await _db.Users.AsNoTracking()
                    .Where(u => u.Role.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Leader
                                && u.Status == UserStatuses.Active && u.PrimaryCampusId.HasValue
                                && campusIds.Contains(u.PrimaryCampusId.Value))
                    .Select(u => u.UserId).ToListAsync(ct);
                foreach (var id in leaders) recipients.Add(id);
            }
            if (recipients.Count == 0) return;

            var notifications = recipients.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: id,
                Title: urgent ? "Khách rút quyền sử dụng hình ảnh/truyền thông" : "Thông tin đơn tham quan được cập nhật",
                Message: urgent
                    ? $"Đơn {requestCode}: khách đã RÚT đồng ý truyền thông. Vui lòng dừng ghi hình/sử dụng hình ảnh liên quan."
                    : $"Đơn {requestCode}: một số thông tin liên hệ/ghi chú đã được cập nhật (không ảnh hưởng phê duyệt).",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                RelatedId: visitRequestId,
                ActorUserId: actorId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                Priority: urgent ? NotificationPriority.URGENT : NotificationPriority.NORMAL,
                IsActionRequired: urgent,
                VisitRequestId: visitRequestId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                ActionUrl: $"/dashboard/visit?visitRequestId={visitRequestId}")).ToList();
            await _notificationService.CreateManyAsync(notifications, ct);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "safe-edit post-commit notification failed for {VisitRequestId}", visitRequestId);
        }
    }
}
