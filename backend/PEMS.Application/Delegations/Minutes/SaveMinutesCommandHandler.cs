using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Minutes;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Minutes;

public sealed class SaveMinutesCommandHandler
    : IRequestHandler<SaveMinutesCommand, MinuteDto>
{
    private static readonly HashSet<string> AttendanceStatuses = new() { "PRESENT", "ABSENT", "EXCUSED" };
    private static readonly HashSet<string> ActionStatuses = new() { "TODO", "IN_PROGRESS", "DONE", "CANCELLED" };
    private const string ActionDone = "DONE";
    private const string ActionCancelled = "CANCELLED";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IEmailService _email;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public SaveMinutesCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IEmailService email, PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _email = email;
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
            await ReconcileParticipants(
                minute.MinutesId, instance.VisitRequestId, instance.VisitInstanceId,
                instance.OperationalContactUserId,
                request.Participants, userId, now, cancellationToken);

        List<MinuteActionItem> newlyAssignedItems = new();
        if (request.ActionItems != null)
        {
            var eligibleAssigneeIds = await ResponsibleCandidateEligibility.GetEligibleUserIdsAsync(
                _db, instance.VisitInstanceId, instance.CurrentHostUserId, cancellationToken);
            newlyAssignedItems = await ReconcileActionItems(
                minute.MinutesId, request.ActionItems, eligibleAssigneeIds, userId, now, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var notifications = new System.Collections.Generic.List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
        // Mixed per-campus v2: notification text uses THIS instance's detail name.
        string delegationName = (await Services.VisitFormRead.VisitInstanceEffectiveName
            .ForInstancesAsync(_db, new[] { instance.VisitInstanceId }, cancellationToken))
            .GetValueOrDefault(instance.VisitInstanceId) ?? "Đoàn khách";
        string title = request.Title.Trim();
        // Host xem/sửa biên bản trong trang "Quy trình tiếp khách" (tab Đang tiếp khách); mọi
        // participant khác (Student Buddy, IC Host Protocol, IT Lead...) chỉ có quyền vào trang
        // "Đóng góp kết quả" — route sai trang sẽ khiến participant vào 1 trang họ không thao tác
        // được. Mỗi recipient cần đúng URL theo vai trò của họ, không dùng chung 1 URL cứng.
        string ActionUrlFor(ulong recipientUserId) => recipientUserId == instance.CurrentHostUserId
            ? $"/dashboard/visit/process/{instance.VisitInstanceId}"
            : $"/dashboard/visit/contribution/{instance.VisitInstanceId}";

        // Notify + email the person newly assigned (or re-assigned) to an action item — only fires
        // for items whose assignee just changed, never on every re-save of an unrelated edit.
        if (newlyAssignedItems.Count > 0)
        {
            var assigneeIds = newlyAssignedItems.Select(a => a.AssignedToUserId!.Value).Distinct().ToList();
            var assigneeUsers = await _db.Users
                .Where(u => assigneeIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName, u.Email })
                .ToDictionaryAsync(u => u.UserId, cancellationToken);

            foreach (var item in newlyAssignedItems)
            {
                if (!assigneeUsers.TryGetValue(item.AssignedToUserId!.Value, out var assignee)) continue;

                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: assignee.UserId,
                    Title: "Bạn được giao đầu việc",
                    Message: $"Bạn được phân công phụ trách \"{item.Title}\" trong biên bản của đoàn {delegationName}.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.ActionItemAssigned,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.MinuteActionItem,
                    RelatedId: item.ActionItemId,
                    ActorUserId: userId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    VisitInstanceId: instance.VisitInstanceId,
                    CampusId: instance.CampusId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    // Khác với "Biên bản cập nhật" (trỏ về đúng trang biên bản theo role), thông báo
                    // giao việc trỏ thẳng tới "Quản lý việc sau tiếp khách" lọc đúng 1 đầu việc này.
                    ActionUrl: $"/dashboard/post-visit-tasks?actionItemId={item.ActionItemId}"
                ));

                if (!string.IsNullOrWhiteSpace(assignee.Email))
                    await SendActionItemAssignedEmailAsync(item, assignee.Email, assignee.FullName, delegationName, userId, now, cancellationToken);
            }
        }

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
                ActionUrl: ActionUrlFor(pId)
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
                ActionUrl: ActionUrlFor(instance.CurrentHostUserId.Value)
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
        ulong minutesId, ulong requestId, ulong visitInstanceId, ulong? contactUserId,
        IReadOnlyList<SaveMinuteParticipantInput> inputs, ulong userId, DateTime now, CancellationToken ct)
    {
        var existing = await _db.MinuteParticipants
            .Where(p => p.MinutesId == minutesId)
            .ToListAsync(ct);
        var byId = existing.ToDictionary(p => p.MinuteParticipantId);
        var processed = new HashSet<ulong>();

        // Duplicate protection counts only the rows that SURVIVE this save. A row the client dropped is
        // deleted at the end of this method, so treating it as "already in the list" would silently
        // swallow a re-added person: remove someone, sync them back, save — and they would vanish from
        // both the draft and the snapshot. The kept set is computed up front so it does not depend on
        // the order the inputs happen to arrive in.
        var keptIds = inputs
            .Where(i => i.MinuteParticipantId.HasValue)
            .Select(i => i.MinuteParticipantId!.Value)
            .ToHashSet();
        var kept = existing.Where(p => keptIds.Contains(p.MinuteParticipantId)).ToList();
        var liveUserIds = kept.Where(p => p.UserId != null).Select(p => p.UserId!.Value).ToHashSet();
        var liveGuestIds = kept.Where(p => p.GuestMemberId != null).Select(p => p.GuestMemberId!.Value).ToHashSet();
        uint maxOrder = existing.Count == 0 ? 0u : existing.Max(p => p.DisplayOrder);

        // Source-linked rows the client did NOT send back, indexed by the id that identifies the
        // person. These are no longer deleted — they become EXCLUDED — so re-adding that person is a
        // RESTORE, not an insert. Inserting instead would put the same guest_member_id in the biên
        // bản twice, which is exactly what `uq_minute_participants_minute_guest` refuses at the
        // database (MIN-01), and what the whole exclude/restore mechanism exists to make coherent.
        var droppedByGuestId = existing
            .Where(p => p.GuestMemberId != null && !keptIds.Contains(p.MinuteParticipantId))
            .GroupBy(p => p.GuestMemberId!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        var droppedByUserId = existing
            .Where(p => p.UserId != null && !keptIds.Contains(p.MinuteParticipantId))
            .GroupBy(p => p.UserId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        // Who the campus's contact is, so a row created here carries the same badge the sync would
        // have given it — and so the rows already present can be re-checked against it. The address
        // is fetched too: it is how a contact who is NOT a delegation member is recognised, having no
        // member id to be recognised by. Read from the instance, never from the client.
        var contact = await _db.VisitInstanceFormDetails
            .Where(d => d.VisitInstanceId == visitInstanceId)
            .Select(d => new { d.OperationalContactGuestMemberId, d.OperationalContactEmail })
            .FirstOrDefaultAsync(ct);
        var contactGuestMemberId = contact?.OperationalContactGuestMemberId;
        var contactEmail = contact?.OperationalContactEmail;

        // The badge describes the CURRENT arrangement, not the moment a row was created. Re-derived
        // on every save because the contact can change while a biên bản already exists.
        MinuteContactBadge.ApplyTo(existing, contactGuestMemberId);

        // Whether a surviving row already wears it, so a row created below cannot become a second.
        // The role belongs to the campus, not to a row: two rows wearing it is data that cannot say
        // who coordinated the visit, so it is refused here rather than written and reconciled later.
        var contactBadgeTaken = existing.Any(r => r.IsOperationalContact);

        // New rows created by this save, kept per source so the cross-source guard below can drop a
        // guest that duplicates an internal person (see the guard for why it runs after the loop).
        var addedInternal = new List<MinuteParticipant>();
        var addedGuests = new List<MinuteParticipant>();

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
                ApplySyncState(row, input.SyncState, isManual);

                if (isManual)
                {
                    // The one kind of row whose identity lives HERE and nowhere else, so this is the
                    // only place it can be corrected.
                    var fullName = (input.FullNameSnapshot ?? string.Empty).Trim();
                    if (fullName.Length == 0)
                        throw new BusinessRuleException("Họ tên người tham gia không được để trống.");
                    row.FullNameSnapshot = fullName;
                    row.RoleSnapshot = Clean(input.RoleSnapshot);
                    row.OrganizationSnapshot = Clean(input.OrganizationSnapshot);
                    row.EmailSnapshot = Clean(input.EmailSnapshot);
                }
                else
                {
                    // A source-linked row is a copy of a record kept elsewhere. Changing it here used
                    // to be accepted and dropped on the floor: the editor rendered inputs, the user
                    // typed a corrected job title, saved, was told it had worked, and reopened the
                    // biên bản to find the old value. Refusing says which row and what to do instead;
                    // silence taught the user their edit had been kept (MIN-04).
                    EnsureSourceIdentityUnchanged(row, input);
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
                // This person already HAS a row that this save was about to set aside. Bringing them
                // back is what the client asked for, so the existing row is restored rather than a
                // second one created for the same account.
                if (droppedByUserId.TryGetValue(newUserId, out var restoredInternal))
                {
                    RestoreDropped(restoredInternal, status, note, userId, now);
                    processed.Add(restoredInternal.MinuteParticipantId);
                    liveUserIds.Add(newUserId);
                    continue;
                }
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
                // The account's own unit wins, but an EXTERNAL account has neither a department nor a
                // campus — and blanking the column for them threw away the organisation the sync had
                // just supplied. The contact confirms with an outside address, so this is exactly the
                // row it happened to.
                newRow.OrganizationSnapshot =
                    u.Department?.Name ?? u.PrimaryCampus?.Name ?? Clean(input.OrganizationSnapshot);
                // A contact who is not in the delegation reaches the biên bản as an ACCOUNT row like
                // this one — the auto-fill carries their user_id — and the badge was set nowhere on
                // this path, so syncing them in and saving quietly dropped "· Đầu mối".
                newRow.IsOperationalContact = !contactBadgeTaken && contactGuestMemberId is null
                    && MinuteContactBadge.IsContactWithoutMemberId(
                        newUserId, newRow.EmailSnapshot, contactUserId, contactEmail);
                contactBadgeTaken |= newRow.IsOperationalContact;
                liveUserIds.Add(newUserId);
            }
            else if (input.GuestMemberId is ulong newGuestId)
            {
                // A guest synced from the official delegation list (visit_guest_members of THIS request).
                // This is in-system data (NOT a free-text/external person): the id is validated against the
                // request's guests and the snapshot is taken from the guest record — mirroring the create-time
                // auto-fill, never trusted from the client. This is what "Đồng bộ người mới" emits for guests.
                if (liveGuestIds.Contains(newGuestId)) continue; // already in the list → ignore duplicate
                // Same person, row already there but on its way out — restore it. Inserting a second
                // row for one guest_member_id is precisely the duplicate the unique index refuses.
                if (droppedByGuestId.TryGetValue(newGuestId, out var restoredGuest))
                {
                    RestoreDropped(restoredGuest, status, note, userId, now);
                    processed.Add(restoredGuest.MinuteParticipantId);
                    liveGuestIds.Add(newGuestId);
                    continue;
                }
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
                // Which KIND of member, recorded now rather than looked up at render time: an
                // interpreter travelling with the delegation is EXTERNAL_SUPPORT, and calling them a
                // guest in the record of the meeting is simply wrong (MIN-02). Taken from the guest
                // record the id resolved to, never from the client.
                newRow.SourceMemberType = guest.MemberType;
                newRow.IsOperationalContact = !contactBadgeTaken && contactGuestMemberId == newGuestId;
                contactBadgeTaken |= newRow.IsOperationalContact;
                liveGuestIds.Add(newGuestId);
            }
            else
            {
                var fullName = (input.FullNameSnapshot ?? string.Empty).Trim();
                if (fullName.Length == 0)
                    throw new BusinessRuleException("Họ tên người tham gia không được để trống.");
                newRow.FullNameSnapshot = fullName;
                newRow.RoleSnapshot = Clean(input.RoleSnapshot);
                newRow.OrganizationSnapshot = Clean(input.OrganizationSnapshot);
                newRow.EmailSnapshot = Clean(input.EmailSnapshot);
                // Same reason as the account branch above: a contact who is neither a delegation
                // member nor an account holder arrives here, matched on the address they were invited
                // at. A row the Host simply typed matches nothing and stays unbadged.
                newRow.IsOperationalContact = !contactBadgeTaken && contactGuestMemberId is null
                    && MinuteContactBadge.IsContactWithoutMemberId(
                        null, newRow.EmailSnapshot, contactUserId, contactEmail);
                contactBadgeTaken |= newRow.IsOperationalContact;
            }

            _db.MinuteParticipants.Add(newRow);
            if (newRow.UserId != null) addedInternal.Add(newRow);
            else if (newRow.GuestMemberId != null) addedGuests.Add(newRow);
        }

        // ── Cross-source duplicate guard ─────────────────────────────────────
        // The same person can sit in both source lists — as an internal participant and, separately, as
        // a member of the delegation — and the two rows carry different ids, so neither id check above
        // sees them as the same person. The sync now filters this out before it ever reaches the
        // editor, but a direct API call still could, so the create is refused here as well: identical
        // name + role + organisation means one person, and the internal row wins because a user_id is a
        // stronger identity than a guest_member_id. Only rows this save would CREATE are dropped —
        // already-persisted rows are the Host's snapshot and are never removed behind their back.
        var internalIdentityKeys = kept
            .Where(p => p.UserId != null)
            .Concat(addedInternal)
            .Select(p => PersonIdentity.Key(p.FullNameSnapshot, p.RoleSnapshot, p.OrganizationSnapshot))
            .Where(k => k.Length > 0)
            .ToHashSet();
        foreach (var guestRow in addedGuests)
        {
            var key = PersonIdentity.Key(
                guestRow.FullNameSnapshot, guestRow.RoleSnapshot, guestRow.OrganizationSnapshot);
            if (key.Length > 0 && internalIdentityKeys.Contains(key))
                _db.MinuteParticipants.Remove(guestRow); // Added → Detached; nothing is inserted.
        }

        // ── Rows the client left out ─────────────────────────────────────────────────────────
        // A MANUAL row exists only here, so dropping it from the list deletes it — there is nothing
        // else it could mean, and nothing regenerates it.
        //
        // A SOURCE-LINKED row is different (MIN-03). Deleting it made the person "not in the biên
        // bản" and therefore, to the next "đồng bộ người mới", somebody still on the official list
        // who was missing — so they came straight back and the Host's decision was quietly undone.
        // The row is kept and marked EXCLUDED instead: out of the biên bản, remembered by sync, and
        // restorable. Source tables are never touched either way.
        foreach (var row in existing)
        {
            if (processed.Contains(row.MinuteParticipantId)) continue;
            if (row.UserId == null && row.GuestMemberId == null)
                _db.MinuteParticipants.Remove(row);
            else
                row.SyncState = MinuteParticipantSyncStates.Excluded;
        }
    }

    /// <summary>
    /// Applies an explicit exclude/restore, refusing a state that does not exist.
    ///
    /// <para>Null means "leave it as it is", so a client that has never heard of the field cannot
    /// reset somebody's state simply by not sending it. A MANUAL row has no sync to be excluded from
    /// — it is deleted outright when dropped — so the field is ignored there rather than storing a
    /// state that means nothing.</para>
    /// </summary>
    private static void ApplySyncState(MinuteParticipant row, string? requested, bool isManual)
    {
        if (isManual || string.IsNullOrWhiteSpace(requested)) return;
        var state = requested.Trim().ToUpperInvariant();
        if (state != MinuteParticipantSyncStates.Active && state != MinuteParticipantSyncStates.Excluded)
            throw new BusinessRuleException(
                $"Trạng thái người tham gia không hợp lệ: '{requested}'.");
        row.SyncState = state;
    }

    /// <summary>
    /// Refuses an edit to the identity of a row whose identity belongs to another table.
    ///
    /// <para>Only a CHANGE is refused. A client that echoes back the values it was given — which is
    /// what every well-behaved save does — passes straight through; the refusal is reserved for an
    /// edit that would otherwise be accepted and thrown away. Null fields are treated as "not sent"
    /// for the same reason.</para>
    /// </summary>
    private static void EnsureSourceIdentityUnchanged(MinuteParticipant row, SaveMinuteParticipantInput input)
    {
        static bool Changed(string? incoming, string? stored) =>
            incoming is not null
            && !string.Equals(incoming.Trim(), (stored ?? string.Empty).Trim(), StringComparison.Ordinal);

        if (!Changed(input.FullNameSnapshot, row.FullNameSnapshot)
            && !Changed(input.RoleSnapshot, row.RoleSnapshot)
            && !Changed(input.OrganizationSnapshot, row.OrganizationSnapshot)
            && !Changed(input.EmailSnapshot, row.EmailSnapshot))
            return;

        throw new BusinessRuleException(
            $"Không thể sửa họ tên, vai trò hoặc đơn vị của \"{row.FullNameSnapshot}\" trong biên bản: "
            + "đây là người được lấy từ danh sách đoàn hoặc danh sách tham gia. "
            + "Vui lòng sửa ở thông tin đoàn / thông tin người tham gia, rồi đồng bộ lại. "
            + "Điểm danh và ghi chú vẫn sửa được ở đây.",
            SourceParticipantIdentityReadonly);
    }

    /// <summary>Refusal code for an attempt to edit a source-linked participant's identity (MIN-04).</summary>
    public const string SourceParticipantIdentityReadonly = "SOURCE_PARTICIPANT_IDENTITY_READONLY";

    /// <summary>
    /// Brings a source-linked row that this save had set aside back into the biên bản, carrying the
    /// attendance the client sent with it. The identity snapshot is left ALONE: it is the record of
    /// the person as they were when they entered the biên bản, and coming back is not a new arrival.
    /// </summary>
    private static void RestoreDropped(
        MinuteParticipant row, string status, string? note, ulong actorId, DateTime now)
    {
        row.SyncState = MinuteParticipantSyncStates.Active;
        if (row.AttendanceStatus != status || row.AttendanceNote != note)
        {
            row.CheckedBy = actorId;
            row.CheckedAt = now;
        }
        row.AttendanceStatus = status;
        row.AttendanceNote = note;
    }

    private async Task<List<MinuteActionItem>> ReconcileActionItems(
        ulong minutesId, IReadOnlyList<SaveMinuteActionItemInput> inputs, HashSet<ulong> eligibleAssigneeIds,
        ulong userId, DateTime now, CancellationToken ct)
    {
        var existing = await _db.MinuteActionItems
            .Where(a => a.MinutesId == minutesId)
            .ToListAsync(ct);
        var byId = existing.ToDictionary(a => a.ActionItemId);
        var processed = new HashSet<ulong>();
        // Items whose assignee is newly set or changed this save — the caller emails/notifies these.
        var newlyAssigned = new List<MinuteActionItem>();

        for (int i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var title = (input.Title ?? string.Empty).Trim();
            if (title.Length == 0)
                throw new BusinessRuleException("Tên đầu mục công việc không được để trống.");
            var status = (input.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (!ActionStatuses.Contains(status))
                throw new BusinessRuleException($"Trạng thái đầu mục công việc không hợp lệ: '{input.Status}'.");
            if (input.AssignedToUserId.HasValue && !eligibleAssigneeIds.Contains(input.AssignedToUserId.Value))
                throw new BusinessRuleException(
                    "Người phụ trách không hợp lệ hoặc chưa chấp nhận tham gia chuyến tiếp khách này.");
            var note = Clean(input.Note);
            int order = i + 1;

            if (input.ActionItemId is ulong id && byId.TryGetValue(id, out var row))
            {
                processed.Add(id);
                bool wasDone = row.Status == ActionDone;
                bool assigneeChanged = row.AssignedToUserId != input.AssignedToUserId;
                // Only re-arm the due-reminder when the due date or assignee actually changed — never
                // on an unrelated re-save, or an already-sent reminder would fire again next tick.
                if (assigneeChanged || row.DueDate != input.DueDate) row.DueReminderSentAt = null;
                row.Title = title;
                row.Note = note;
                row.AssignedToUserId = input.AssignedToUserId;
                row.DueDate = input.DueDate;
                row.Status = status;
                row.DisplayOrder = order;
                row.UpdatedAt = now;
                row.UpdatedBy = userId;
                if (status == ActionDone && !wasDone) row.CompletedAt = now;
                else if (status != ActionDone) row.CompletedAt = null;
                if (assigneeChanged && input.AssignedToUserId.HasValue) newlyAssigned.Add(row);
                continue;
            }

            var newItem = new MinuteActionItem
            {
                MinutesId = minutesId,
                Title = title,
                Note = note,
                AssignedToUserId = input.AssignedToUserId,
                DueDate = input.DueDate,
                Status = status,
                DisplayOrder = order,
                CompletedAt = status == ActionDone ? now : null,
                CreatedAt = now,
                CreatedBy = userId,
            };
            _db.MinuteActionItems.Add(newItem);
            if (input.AssignedToUserId.HasValue) newlyAssigned.Add(newItem);
        }

        // Rows the client dropped from the list: never hard-delete once persisted (an assignee may
        // already have been notified about it) — soft-cancel instead, so it still shows as "Đã hủy" on
        // the assignee's own task list. A DONE item cannot be dropped at all (unchanged rule).
        foreach (var row in existing)
        {
            if (processed.Contains(row.ActionItemId)) continue;
            if (row.Status == ActionDone)
                throw new BusinessRuleException("Không thể xóa đầu mục công việc đã hoàn thành.");
            if (row.Status == ActionCancelled) continue; // already cancelled — nothing to do
            row.Status = ActionCancelled;
            row.UpdatedAt = now;
            row.UpdatedBy = userId;
        }

        return newlyAssigned;
    }

    private async Task SendActionItemAssignedEmailAsync(
        MinuteActionItem item, string recipientEmail, string recipientName, string delegationName,
        ulong actorUserId, DateTime now, CancellationToken ct)
    {
        var dueDateText = item.DueDate?.ToString("HH:mm dd/MM/yyyy");
        var subject = ActionItemEmailContent.AssignedSubject(item.Title);
        var body = EmailComposition.BrandedShell(
            ActionItemEmailContent.AssignedBodyHtml(recipientName, item.Title, dueDateText, delegationName));

        ulong sentEmailId, sentEmailRecipientId;
        await using (var transaction = await _db.BeginTransactionAsync(ct))
        {
            var sentEmail = new SentEmail
            {
                RelatedType = PEMS.Application.Notifications.Common.NotificationRelatedTypes.MinuteActionItem,
                RelatedId = item.ActionItemId,
                Subject = subject,
                BodySnapshot = body,
                Status = "QUEUED",
                SentBy = actorUserId,
                CreatedAt = now,
            };
            _db.SentEmails.Add(sentEmail);
            await _db.SaveChangesAsync(ct);

            var sentRecipient = new SentEmailRecipient
            {
                SentEmailId = sentEmail.SentEmailId,
                RecipientEmail = recipientEmail,
                RecipientName = recipientName,
                RecipientType = "TO",
                DeliveryStatus = "QUEUED",
                CreatedAt = now,
            };
            _db.SentEmailRecipients.Add(sentRecipient);
            await _db.SaveChangesAsync(ct);

            sentEmailId = sentEmail.SentEmailId;
            sentEmailRecipientId = sentRecipient.SentEmailRecipientId;
            await transaction.CommitAsync(ct);
        }

        try
        {
            await _email.SendAsync(recipientEmail, subject, body, ct);
            await UpdateEmailStatusAsync(sentEmailId, sentEmailRecipientId, "SENT", actorUserId, now, null, ct);
        }
        catch (Exception ex)
        {
            await UpdateEmailStatusAsync(sentEmailId, sentEmailRecipientId, "FAILED", actorUserId, now, ex.Message, ct);
        }
    }

    private async Task UpdateEmailStatusAsync(
        ulong sentEmailId, ulong sentEmailRecipientId, string status, ulong actorId, DateTime now,
        string? error, CancellationToken ct)
    {
        var sentEmail = await _db.SentEmails.FirstOrDefaultAsync(e => e.SentEmailId == sentEmailId, ct);
        if (sentEmail != null)
        {
            sentEmail.Status = status;
            sentEmail.LastAttemptAt = now;
            sentEmail.RetryCount += 1;
            if (status == "SENT") { sentEmail.SentBy = actorId; sentEmail.SentAt = now; }
            else { sentEmail.ErrorMessage = Truncate(error, 1000); }
        }
        var rec = await _db.SentEmailRecipients.FirstOrDefaultAsync(r => r.SentEmailRecipientId == sentEmailRecipientId, ct);
        if (rec != null)
        {
            rec.DeliveryStatus = status;
            if (status == "SENT") rec.SentAt = now;
            else rec.ErrorMessage = Truncate(error, 1000);
        }
        await _db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
