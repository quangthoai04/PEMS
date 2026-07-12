using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Minutes;

namespace PEMS.Application.Delegations.Minutes;

public sealed class SaveMinutesCommandHandler
    : IRequestHandler<SaveMinutesCommand, MinuteDto>
{
    private static readonly HashSet<string> AttendanceStatuses = new() { "PRESENT", "ABSENT", "EXCUSED" };
    private static readonly HashSet<string> ActionStatuses = new() { "TODO", "IN_PROGRESS", "DONE", "CANCELLED" };
    private const string ActionDone = "DONE";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public SaveMinutesCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock, PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notificationService = notificationService;
    }

    public async Task<MinuteDto> Handle(SaveMinutesCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new BusinessRuleException("Tiêu đề biên bản không được để trống.");

        var minute = await _db.Minutes.FirstOrDefaultAsync(m => m.MinutesId == request.MinutesId, cancellationToken)
            ?? throw new NotFoundException("Minute", request.MinutesId);

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == minute.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", minute.VisitInstanceId);

        var acceptedRole = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.UserId == userId
                && p.Status == ParticipantStatuses.Accepted && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        var (inScope, canEdit) = MinuteAccess.Evaluate(instance, instance.VisitRequest, _currentUser, acceptedRole);
        if (!inScope)
            throw new ForbiddenException("Bạn không có quyền xem biên bản của chuyến thăm này.");
        if (!canEdit)
            throw new ForbiddenException("Bạn không có quyền chỉnh sửa biên bản chuyến thăm này.");

        var now = _clock.VietnamNow;

        // The caller must currently hold the lock (same token, not expired, same user).
        bool holdsLock = minute.EditLockedBy == userId
            && minute.EditLockToken == request.EditLockToken
            && minute.EditLockExpiresAt.HasValue && minute.EditLockExpiresAt.Value > now;
        if (!holdsLock)
            throw new ConflictException("Phiên chỉnh sửa biên bản đã hết hạn hoặc đang do người khác giữ. Vui lòng mở lại để chỉnh sửa.");

        // Optimistic concurrency: reject if the record changed since it was opened.
        if (minute.RowVersion != request.RowVersion)
            throw new ConflictException("Biên bản đã được cập nhật bởi người khác. Vui lòng tải lại nội dung mới nhất.");

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        minute.Title = request.Title.Trim();
        minute.Content = request.Content;
        minute.Status = request.IsDraft ? MinuteAccess.StatusDraft : MinuteAccess.StatusSaved;
        minute.RowVersion += 1;
        minute.UpdatedAt = now;
        minute.UpdatedBy = userId;
        // Save releases the lock so others can edit next.
        minute.EditLockedBy = null;
        minute.EditLockedAt = null;
        minute.EditLockExpiresAt = null;
        minute.EditLockToken = null;

        if (request.Participants != null)
            await ReconcileParticipants(minute.MinutesId, instance.VisitRequestId, request.Participants, userId, now, cancellationToken);

        if (request.ActionItems != null)
            await ReconcileActionItems(minute.MinutesId, request.ActionItems, userId, now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var notifications = new System.Collections.Generic.List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
        string delegationName = instance.VisitRequest?.DelegationName ?? "Đoàn khách";
        string title = request.Title.Trim();
        var minutesActionUrl = $"/dashboard/visit/process/{instance.VisitInstanceId}";

        // Notify Accepted participants
        var notifyParticipantIds = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.Status == ParticipantStatuses.Accepted && p.UserId != userId)
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        foreach (var pId in notifyParticipantIds)
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: pId,
                Title: "Biên bản cuộc họp cập nhật",
                Message: $"Biên bản cuộc họp \"{title}\" của đoàn {delegationName} đã được lưu.",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.MinutesUpdated,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                RelatedId: instance.VisitInstanceId,
                ActorUserId: userId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                ActionUrl: minutesActionUrl
            ));
        }

        // The current Host must also learn about it when they aren't the one editing (e.g. a
        // participant saved the minutes) — but never anyone else's campus Staff Leader, per the
        // host-scope rule (Staff/Staff Leader only get delegation detail notifications for
        // delegations they actually host).
        if (instance.CurrentHostUserId.HasValue && instance.CurrentHostUserId.Value != userId
            && !notifyParticipantIds.Contains(instance.CurrentHostUserId.Value))
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: instance.CurrentHostUserId.Value,
                Title: "Biên bản cuộc họp cập nhật",
                Message: $"Biên bản cuộc họp \"{title}\" của đoàn {delegationName} đã được lưu.",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.MinutesUpdated,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                RelatedId: instance.VisitInstanceId,
                ActorUserId: userId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                ActionUrl: minutesActionUrl
            ));
        }

        if (notifications.Count > 0)
        {
            await _notificationService.CreateManyAsync(notifications, cancellationToken);
        }

        var dto = new MinuteDto
        {
            Exists = true,
            MinutesId = minute.MinutesId,
            VisitInstanceId = minute.VisitInstanceId,
            Title = minute.Title,
            Content = minute.Content,
            Status = minute.Status,
            RowVersion = minute.RowVersion,
            UpdatedAt = minute.UpdatedAt,
            IsLockedByMe = false,
            IsLockedByOther = false,
            CanView = true,
            CanEdit = canEdit,
            CanCreate = false,
        };
        await MinuteChildren.LoadInto(_db, dto, minute.MinutesId, cancellationToken);
        return dto;
    }

    private async Task ReconcileParticipants(
        ulong minutesId, ulong requestId, IReadOnlyList<SaveMinuteParticipantInput> inputs, ulong userId, DateTime now, CancellationToken ct)
    {
        var existing = await _db.MinuteParticipants
            .Where(p => p.MinutesId == minutesId)
            .ToListAsync(ct);
        var byId = existing.ToDictionary(p => p.MinuteParticipantId);
        var processed = new HashSet<ulong>();
        var liveUserIds = existing.Where(p => p.UserId != null).Select(p => p.UserId!.Value).ToHashSet();
        var liveGuestIds = existing.Where(p => p.GuestMemberId != null).Select(p => p.GuestMemberId!.Value).ToHashSet();
        uint maxOrder = existing.Count == 0 ? 0u : existing.Max(p => p.DisplayOrder);

        foreach (var input in inputs)
        {
            var status = (input.AttendanceStatus ?? string.Empty).Trim().ToUpperInvariant();
            if (!AttendanceStatuses.Contains(status))
                throw new BusinessRuleException($"Trạng thái điểm danh không hợp lệ: '{input.AttendanceStatus}'.");
            var note = string.IsNullOrWhiteSpace(input.AttendanceNote) ? null : input.AttendanceNote.Trim();

            if (input.MinuteParticipantId is ulong id && byId.TryGetValue(id, out var row))
            {
                processed.Add(id);
                bool isManual = row.UserId == null && row.GuestMemberId == null;

                // Record who/when only when attendance actually changes.
                if (row.AttendanceStatus != status || row.AttendanceNote != note)
                {
                    row.CheckedBy = userId;
                    row.CheckedAt = now;
                }
                row.AttendanceStatus = status;
                row.AttendanceNote = note;

                // Snapshot is editable ONLY for manual rows (if any still exist)
                if (isManual)
                {
                    var fullName = (input.FullNameSnapshot ?? string.Empty).Trim();
                    if (fullName.Length == 0)
                        throw new BusinessRuleException("Họ tên người tham gia không được để trống.");
                    row.FullNameSnapshot = fullName;
                    row.RoleSnapshot = Clean(input.RoleSnapshot);
                    row.OrganizationSnapshot = Clean(input.OrganizationSnapshot);
                    row.EmailSnapshot = Clean(input.EmailSnapshot);
                }
                continue;
            }

            // ── New row ──────────────────────────────────────────────────────
            var newRow = new MinuteParticipant
            {
                MinutesId = minutesId,
                GuestMemberId = null, // set below only for a validated synced delegation guest
                AttendanceStatus = status,
                AttendanceNote = note,
                DisplayOrder = ++maxOrder,
                CreatedAt = now,
            };
            if (status != "ABSENT" || note != null)
            {
                newRow.CheckedBy = userId;
                newRow.CheckedAt = now;
            }

            if (input.UserId is ulong newUserId)
            {
                if (liveUserIds.Contains(newUserId)) continue; // already in the list → ignore duplicate
                var u = await _db.Users
                    .Include(x => x.Department).Include(x => x.PrimaryCampus).Include(x => x.Role)
                    .FirstOrDefaultAsync(x => x.UserId == newUserId, ct);
                if (u == null)
                    throw new BusinessRuleException("Người dùng được chọn không tồn tại hoặc không còn hoạt động.");
                if (u.Status != "ACTIVE")
                    throw new BusinessRuleException("Người dùng được chọn đã bị vô hiệu hóa.");
                newRow.UserId = newUserId;
                newRow.FullNameSnapshot = u.FullName;
                newRow.EmailSnapshot = u.Email;
                newRow.RoleSnapshot = Clean(input.RoleSnapshot);
                newRow.OrganizationSnapshot = u.Department?.Name ?? u.PrimaryCampus?.Name;
                liveUserIds.Add(newUserId);
            }
            else if (input.GuestMemberId is ulong newGuestId)
            {
                // A guest synced from the official delegation list (visit_guest_members of THIS request).
                // This is in-system data (NOT a free-text/external person): the id is validated against the
                // request's guests and the snapshot is taken from the guest record — mirroring the create-time
                // auto-fill, never trusted from the client. This is what "Đồng bộ người mới" emits for guests.
                if (liveGuestIds.Contains(newGuestId)) continue; // already in the list → ignore duplicate
                // Scope check: the guest must belong to THIS minutes' visit_request. A non-existent id or
                // an id from another request is a reference/scope error — NOT a free-text/external person,
                // so it gets its own message + code (never the "ngoài hệ thống" one).
                var guest = await _db.VisitGuestMembers
                    .FirstOrDefaultAsync(g => g.GuestMemberId == newGuestId && g.VisitRequestId == requestId, ct);
                if (guest == null)
                    throw new BusinessRuleException(
                        "Khách tham gia không thuộc đoàn hiện tại hoặc đã không còn hợp lệ. Vui lòng đồng bộ lại danh sách người tham gia.",
                        "MINUTE_GUEST_NOT_IN_CURRENT_REQUEST");
                newRow.GuestMemberId = newGuestId;
                newRow.FullNameSnapshot = guest.FullName;
                newRow.RoleSnapshot = guest.JobTitle;
                newRow.OrganizationSnapshot = guest.Organization;
                newRow.EmailSnapshot = null;
                liveGuestIds.Add(newGuestId);
            }
            else
            {
                throw new BusinessRuleException("Không hỗ trợ thêm người tham gia ngoài hệ thống. Vui lòng chọn một người dùng có sẵn.");
            }

            _db.MinuteParticipants.Add(newRow);
        }

        // Rows omitted by the client are removed from the snapshot (never from the source tables).
        foreach (var row in existing)
            if (!processed.Contains(row.MinuteParticipantId))
                _db.MinuteParticipants.Remove(row);
    }

    private async Task ReconcileActionItems(
        ulong minutesId, IReadOnlyList<SaveMinuteActionItemInput> inputs, ulong userId, DateTime now, CancellationToken ct)
    {
        var existing = await _db.MinuteActionItems
            .Where(a => a.MinutesId == minutesId)
            .ToListAsync(ct);
        var byId = existing.ToDictionary(a => a.ActionItemId);
        var processed = new HashSet<ulong>();

        for (int i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var title = (input.Title ?? string.Empty).Trim();
            if (title.Length == 0)
                throw new BusinessRuleException("Tên đầu mục công việc không được để trống.");
            var status = (input.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (!ActionStatuses.Contains(status))
                throw new BusinessRuleException($"Trạng thái đầu mục công việc không hợp lệ: '{input.Status}'.");
            var note = Clean(input.Note);
            int order = i + 1;

            if (input.ActionItemId is ulong id && byId.TryGetValue(id, out var row))
            {
                processed.Add(id);
                bool wasDone = row.Status == ActionDone;
                row.Title = title;
                row.Note = note;
                row.DueDate = input.DueDate;
                row.Status = status;
                row.DisplayOrder = order;
                row.UpdatedAt = now;
                row.UpdatedBy = userId;
                if (status == ActionDone && !wasDone) row.CompletedAt = now;
                else if (status != ActionDone) row.CompletedAt = null;
                continue;
            }

            var newItem = new MinuteActionItem
            {
                MinutesId = minutesId,
                Title = title,
                Note = note,
                DueDate = input.DueDate,
                Status = status,
                DisplayOrder = order,
                CompletedAt = status == ActionDone ? now : null,
                CreatedAt = now,
                CreatedBy = userId,
            };
            _db.MinuteActionItems.Add(newItem);
        }

        foreach (var row in existing)
        {
            if (processed.Contains(row.ActionItemId)) continue;
            if (row.Status == ActionDone)
                throw new BusinessRuleException("Không thể xóa đầu mục công việc đã hoàn thành.");
            _db.MinuteActionItems.Remove(row);
        }
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
