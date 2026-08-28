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
            .AsSplitQuery()
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
            // ── Request-level Registrant fields are REGISTRANT-ONLY (plan CanhIter3FixBug, decision S).
            //    The loop below only proves a non-registrant holds every CAMPUS named in the patch — it
            //    says nothing about request.Patch.Registrant, so a campus's operational contact could
            //    otherwise craft a payload naming their own campus's instance (passing the loop) plus a
            //    populated Registrant block, and silently edit request-level fields they have no
            //    authority over. Checked first, unconditionally — whether or not Instances is also
            //    populated or empty. ──
            if (request.Patch?.Registrant is not null)
                throw new ForbiddenException("Chỉ người đăng ký mới được sửa thông tin người đăng ký.");

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
            // ── Only Safe/PrivacyUrgent-classed changes are notification-worthy (plan CanhIter3FixBug,
            //    decision E) — Contact-classed entries (same-person operational-contact metadata/relation)
            //    are filtered out here, same precedent as the pre-existing standalone
            //    UpdateOperationalContactProfileCommandHandler, which has always sent zero notifications
            //    for this kind of correction. A call whose ONLY changes are contact ends up with
            //    `notifiable.Count == 0` and returns before touching the database at all — silent. ──
            var notifiable = result.AppliedChanges
                .Where(c => c.ChangeClass is AmendmentChangeClasses.Safe or AmendmentChangeClasses.PrivacyUrgent)
                .ToList();
            if (notifiable.Count == 0) return;

            var urgent = notifiable.Any(c => c.ChangeClass == AmendmentChangeClasses.PrivacyUrgent);
            var touchedInstanceIds = notifiable
                .Where(c => c.VisitInstanceId is not null)
                .Select(c => c.VisitInstanceId!.Value)
                .Distinct().ToList();
            // Whether the call ALSO carries a request-level notifiable change (registrant fields) — a
            // Contact entry is always instance-scoped, so this is unaffected by contact edits.
            var hasRequestLevel = notifiable.Any(c => c.VisitInstanceId is null);

            var recipients = new HashSet<ulong>();
            // Exact instance target (plan continuation §17): only when the edit touched EXACTLY ONE
            // campus AND has no request-level component is this unambiguous — every recipient below
            // (its leaders + its Host) is being told about that one instance, so naming it lets the
            // frontend focus that exact campus instead of falling back to the safe-but-generic
            // request-level detail. A request-level component present alongside instance-level ones
            // (plan CanhIter3FixBug, decision E — request-level and instance-level scopes UNION rather
            // than being mutually exclusive) targets the whole request instead of fabricating one
            // instance id for a change that also touched the request as a whole.
            ulong? exactInstanceId = !hasRequestLevel && touchedInstanceIds.Count == 1 ? touchedInstanceIds[0] : null;
            ulong? exactCampusId = null;
            if (touchedInstanceIds.Count > 0)
            {
                var rows = await _db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => touchedInstanceIds.Contains(c.VisitInstanceId))
                    .Select(c => new { c.CampusId, c.CurrentHostUserId })
                    .ToListAsync(ct);
                var campusIds = rows.Select(r => r.CampusId).Distinct().ToList();
                if (exactInstanceId.HasValue) exactCampusId = campusIds.FirstOrDefault();
                var leaders = await _db.Users.AsNoTracking()
                    .Where(u => u.Role.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Leader
                                && u.Status == UserStatuses.Active && u.PrimaryCampusId.HasValue
                                && campusIds.Contains(u.PrimaryCampusId.Value))
                    .Select(u => u.UserId).ToListAsync(ct);
                foreach (var id in leaders) recipients.Add(id);
                foreach (var host in rows.Where(r => r.CurrentHostUserId.HasValue))
                    recipients.Add(host.CurrentHostUserId!.Value);
            }
            // Request-level recipients (leaders of every still-active campus) are added whenever a
            // request-level notifiable change is present — UNIONED with any instance-level recipients
            // just computed above, deduplicated by the HashSet, rather than the two being an if/else
            // (plan CanhIter3FixBug, decision E — a registrant-field edit alongside a Notes edit on one
            // campus must not lose the request-wide leaders in favor of only that campus's).
            if (hasRequestLevel)
            {
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
                VisitInstanceId: exactInstanceId,
                CampusId: exactCampusId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                ActionUrl: exactInstanceId.HasValue
                    ? $"/dashboard/visit?visitRequestId={visitRequestId}&visitInstanceId={exactInstanceId}"
                    : $"/dashboard/visit?visitRequestId={visitRequestId}",
                MetadataJson: urgent
                    ? PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                        PEMS.Application.Notifications.Common.NotificationEventKeys.VisitPrivacyConsentWithdrawn,
                        new { requestCode })
                    : PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                        PEMS.Application.Notifications.Common.NotificationEventKeys.VisitRequestUpdatedPending,
                        new { requestCode }))).ToList();
            await _notificationService.CreateManyAsync(notifications, ct);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "safe-edit post-commit notification failed for {VisitRequestId}", visitRequestId);
        }
    }
}
