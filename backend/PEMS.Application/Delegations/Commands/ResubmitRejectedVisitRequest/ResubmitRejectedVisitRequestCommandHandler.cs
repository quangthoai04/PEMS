using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequest;

/// <summary>
/// Visitor resubmit after full rejection (spec §2.2/§7). Two-phase persistence inside one
/// transaction: the parent request flips REJECTED → PENDING_APPROVAL FIRST (EF flushes child
/// campus rows before the parent within a single SaveChanges, and the campus trigger only
/// allows REJECTED → WAITING_REQUEST_APPROVAL under a pending parent), then every campus
/// instance is reset to WAITING_REQUEST_APPROVAL with all decision/host/cancel fields cleared.
/// The old decisions are written to audit_log_changes BEFORE being cleared.
/// </summary>
public sealed class ResubmitRejectedVisitRequestCommandHandler
    : IRequestHandler<ResubmitRejectedVisitRequestCommand, ResubmitRejectedVisitRequestResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    private static readonly JsonSerializerOptions SnapshotJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ResubmitRejectedVisitRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notificationService = notificationService;
    }

    public async Task<ResubmitRejectedVisitRequestResponse> Handle(
        ResubmitRejectedVisitRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        if (_currentUser.RoleCode != RoleCodes.Visitor)
            throw new ForbiddenException("Chỉ khách (Visitor) mới được gửi lại đơn đăng ký tham quan.");

        var actorId = _currentUser.UserId.Value;
        var now = _clock.UtcNow;
        // planned_start_at is local wall-clock DATETIME → the 24h window uses VietnamNow.
        var vnNow = _clock.VietnamNow;

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .Include(v => v.GuestMembers)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);

        if (visit.VisitorUserId != actorId)
            throw new ForbiddenException("Bạn chỉ được gửi lại đơn của chính mình.");

        // ── Resubmittable gate (spec §2.2): request REJECTED + EVERY campus REJECTED ──
        if (visit.Status != VisitRequestStatuses.Rejected
            || visit.CampusInstances.Count == 0
            || visit.CampusInstances.Any(c => c.Status != VisitInstanceStatus.Rejected))
            throw new BusinessRuleException(
                "Chỉ có thể gửi lại đơn khi toàn bộ yêu cầu đã bị từ chối ở tất cả các cơ sở.",
                VisitRequestErrorCodes.VisitRequestNotResubmittable);

        // ── Campus set must be IDENTICAL to the original (phase rule: đổi campus ⇒ tạo đơn mới) ──
        var requestedCodes = request.CampusVisits
            .Select(s => s.CampusId?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var campuses = await _db.Campuses
            .Where(c => requestedCodes.Contains(c.CampusCode))
            .Select(c => new { c.CampusCode, c.CampusId, c.Name })
            .ToListAsync(cancellationToken);
        var campusByCode = campuses.ToDictionary(c => c.CampusCode, StringComparer.OrdinalIgnoreCase);

        foreach (var code in requestedCodes)
        {
            if (!campusByCode.ContainsKey(code))
                throw new BusinessRuleException($"Cơ sở '{code}' không tồn tại.", VisitRequestErrorCodes.CampusNotFound);
        }

        var requestedCampusIds = campuses.Select(c => c.CampusId).ToHashSet();
        var existingCampusIds = visit.CampusInstances.Select(c => c.CampusId).ToHashSet();
        if (!requestedCampusIds.SetEquals(existingCampusIds))
            throw new BusinessRuleException(
                "Không thể đổi danh sách cơ sở khi gửi lại đơn. Nếu muốn thăm cơ sở khác, vui lòng tạo đơn đăng ký mới.",
                VisitRequestErrorCodes.ResubmitCampusListChanged);

        // ── New schedule: every start ≥ now + 24h, end > start ──
        foreach (var slot in request.CampusVisits)
        {
            if (slot.EndDatetime <= slot.StartDatetime)
                throw new BusinessRuleException("Thời gian kết thúc phải sau thời gian bắt đầu.", VisitRequestErrorCodes.InvalidVisitTime);
            if (slot.StartDatetime < vnNow.AddHours(24))
                throw new BusinessRuleException(
                    "Thời gian bắt đầu mới phải cách hiện tại ít nhất 24 giờ.",
                    VisitRequestErrorCodes.InvalidVisitTime);
        }

        // ── Every campus must still have an ACTIVE Staff Leader to re-process the instance ──
        var campusIdList = requestedCampusIds.ToList();
        var staffLeadersByCampus = (await _db.Users
                .Where(u => u.Role.RoleCode == RoleCodes.Staff
                            && u.SubRole == UserSubRoles.Leader
                            && u.Status == UserStatuses.Active
                            && u.PrimaryCampusId.HasValue
                            && campusIdList.Contains(u.PrimaryCampusId.Value))
                .Select(u => new { u.UserId, CampusId = u.PrimaryCampusId!.Value })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.CampusId)
            .ToDictionary(g => g.Key, g => g.First().UserId);
        foreach (var campus in campuses)
        {
            if (!staffLeadersByCampus.ContainsKey(campus.CampusId))
                throw new BusinessRuleException(
                    $"Cơ sở {campus.Name} chưa có Staff Leader đang hoạt động nên chưa thể tiếp nhận lại yêu cầu.",
                    VisitRequestErrorCodes.CampusHasNoActiveStaffLeader);
        }

        // ── Immutable Fields Validation (spec §5.1) ──
        if (!string.Equals(visit.RegistrantFullName, request.RegistrantFullName, StringComparison.Ordinal) ||
            !string.Equals(visit.RegistrantNationality, request.RegistrantNationality, StringComparison.Ordinal) ||
            !string.Equals(visit.RegistrantJobTitle, request.RegistrantPosition, StringComparison.Ordinal) ||
            !string.Equals(visit.RegistrantPhone, request.RegistrantPhone, StringComparison.Ordinal) ||
            !string.Equals(visit.RegistrantEmail, request.RegistrantEmail, StringComparison.OrdinalIgnoreCase) ||
            visit.PartnerId != request.PartnerId)
        {
            throw new BusinessRuleException(
                "Thông tin người đăng ký không được phép thay đổi.",
                "IMMUTABLE_REGISTRANT_INFO");
        }

        if (!visit.PartnerId.HasValue && !string.Equals(visit.RegistrantOrganization, request.RegistrantOrganization, StringComparison.Ordinal))
        {
            throw new BusinessRuleException(
                "Thông tin người đăng ký không được phép thay đổi.",
                "IMMUTABLE_REGISTRANT_INFO");
        }

        var contactEmail = request.IsContactSelf ? request.RegistrantEmail : request.ContactPerson.Email;
        if (!string.Equals(visit.ContactPersonEmail, contactEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "Không được phép thay đổi email của đầu mối liên hệ.",
                "IMMUTABLE_CONTACT_IDENTITY");
        }

        var contactName = request.IsContactSelf ? request.RegistrantFullName : request.ContactPerson.FullName;
        var contactPhone = request.IsContactSelf ? request.RegistrantPhone : request.ContactPerson.Phone;
        var contactOrg = request.IsContactSelf ? request.RegistrantOrganization : request.ContactPerson.Organization;

        // ── Snapshot the old campus decisions BEFORE clearing them (spec §7.3) ──
        var deciderIds = visit.CampusInstances
            .Where(c => c.DecidedBy.HasValue).Select(c => c.DecidedBy!.Value).Distinct().ToList();
        var deciderNames = deciderIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users
                .Where(u => deciderIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);
        var campusNamesById = campuses.ToDictionary(c => c.CampusId, c => c.Name);

        var decisionSnapshot = visit.CampusInstances
            .OrderBy(c => c.PlannedStartAt)
            .Select(c => new
            {
                visitInstanceId = c.VisitInstanceId,
                campusId = c.CampusId,
                campusName = campusNamesById.TryGetValue(c.CampusId, out var cn) ? cn : null,
                oldStatus = c.Status,
                decidedBy = c.DecidedBy,
                decidedByName = c.DecidedBy is { } db && deciderNames.TryGetValue(db, out var dn) ? dn : null,
                decidedAt = c.DecidedAt,
                decisionActorRole = c.DecisionActorRole,
                decisionNote = c.DecisionNote,
            })
            .ToList();
        var snapshotJson = JsonSerializer.Serialize(decisionSnapshot, SnapshotJson);

        var oldCount = visit.ResubmissionCount;

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        // ── Audit FIRST (snapshot must exist even if a later step fails the transaction rolls back together) ──
        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = "RESUBMIT_REJECTED_VISIT_REQUEST",
            EntityType = "VisitRequest",
            EntityId = visit.VisitRequestId,
            CreatedAt = now
        };
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "request.status",
            OldValueText = VisitRequestStatuses.Rejected,
            NewValueText = VisitRequestStatuses.PendingApproval,
            CreatedAt = now
        });
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "resubmission_count",
            OldValueText = oldCount.ToString(),
            NewValueText = (oldCount + 1).ToString(),
            CreatedAt = now
        });
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "campus_decisions_before_resubmit_json",
            OldValueText = snapshotJson,
            NewValueText = "cleared_for_resubmission",
            CreatedAt = now
        });
        _db.AuditLogs.Add(audit);

        // ── Phase 1: request fields + status back to PENDING_APPROVAL ──
        visit.DelegationName = request.DelegationName;
        visit.VisitType = request.VisitType;
        visit.VisitTypeOther = request.VisitType == "OTHER" ? request.VisitTypeOther : null;
        visit.Purpose = request.Purpose;
        visit.WorkingContent = request.WorkingContent;
        visit.ContactPersonFullName = contactName;
        visit.ContactPersonOrganization = contactOrg;
        visit.ContactPersonPhone = contactPhone;
        visit.WorkingLanguage = request.WorkingLanguage;
        visit.TransportationNote = string.IsNullOrWhiteSpace(request.TransportationNote) ? null : request.TransportationNote.Trim();
        visit.MediaConsentStatus = request.MediaConsentStatus;
        visit.MediaConsentNote = request.MediaConsentNote;
        visit.NoteToFptu = request.Notes;
        visit.Status = VisitRequestStatuses.PendingApproval;
        visit.ResubmissionCount = oldCount + 1;
        visit.LastResubmittedAt = now;
        visit.LastResubmittedBy = actorId;
        // Stale cancellation leftovers (defensive — a REJECTED request should have none).
        visit.CancelledBy = null;
        visit.CancelledAt = null;
        visit.CancellationReason = null;
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;
        visit.RowVersion += 1;

        // ── Guest members: full replace ──
        _db.VisitGuestMembers.RemoveRange(visit.GuestMembers);
        visit.GuestMembers.Clear();
        uint order = 1;
        foreach (var v in request.Visitors)
        {
            visit.GuestMembers.Add(new VisitGuestMember
            {
                FullName = v.FullName,
                Organization = v.Organization,
                JobTitle = v.JobTitle,
                Nationality = v.Nationality,
                MemberType = "GUEST",
                DisplayOrder = order++,
                CreatedAt = now,
                CreatedBy = actorId
            });
        }
        foreach (var s in request.SupportMembers ?? Enumerable.Empty<PEMS.Application.Common.DTOs.SupportTeamMemberDto>())
        {
            visit.GuestMembers.Add(new VisitGuestMember
            {
                FullName = s.FullName,
                Organization = s.Organization,
                JobTitle = s.JobTitle,
                Nationality = s.Nationality,
                MemberType = "EXTERNAL_SUPPORT",
                DisplayOrder = order++,
                CreatedAt = now,
                CreatedBy = actorId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        // ── Phase 2: reset every campus instance to WAITING_REQUEST_APPROVAL (parent is
        //    already PENDING_APPROVAL, so the campus trigger accepts the transition) ──
        var slotsByCampusId = request.CampusVisits
            .GroupBy(s => campusByCode[s.CampusId!.Trim()].CampusId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var instance in visit.CampusInstances)
        {
            var slot = slotsByCampusId[instance.CampusId];
            instance.PlannedStartAt = slot.StartDatetime;
            instance.PlannedEndAt = slot.EndDatetime;
            instance.Status = VisitInstanceStatus.WaitingRequestApproval;
            // Clear the old decision (snapshotted above).
            instance.DecisionActorRole = null;
            instance.DecisionSource = null;
            instance.DecidedBy = null;
            instance.DecidedAt = null;
            instance.DecisionNote = null;
            // Clear host/cancel leftovers so the instance is a clean pending one.
            instance.CurrentHostUserId = null;
            instance.HostAssignedBy = null;
            instance.HostAssignedAt = null;
            instance.CancelledBy = null;
            instance.CancelledAt = null;
            instance.CancellationActorType = null;
            instance.CancellationSource = null;
            instance.CancellationReason = null;
            // Re-route to the CURRENT active Staff Leader of the campus.
            instance.CoordinatorUserId = staffLeadersByCampus[instance.CampusId];
            instance.CoordinatorAssignedBy = actorId;
            instance.CoordinatorAssignedAt = now;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            instance.RowVersion += 1;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // ── Notify the Staff Leaders of every campus to re-process ──
        var leadersToNotify = staffLeadersByCampus.Values.Distinct().ToList();
        if (leadersToNotify.Count > 0)
        {
            await _notificationService.CreateManyAsync(
                leadersToNotify.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Visitor đã gửi lại đơn bị từ chối",
                    Message: $"Visitor đã chỉnh sửa và gửi lại đơn {visit.RequestCode} ({visit.DelegationName}). Vui lòng xử lý lại tại cơ sở của bạn.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    RelatedId: visit.VisitRequestId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    IsActionRequired: true,
                    VisitRequestId: visit.VisitRequestId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: "/dashboard/visit")).ToList(),
                cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        return new ResubmitRejectedVisitRequestResponse(
            visit.VisitRequestId,
            visit.Status,
            (int)visit.ResubmissionCount,
            "Đơn đã được gửi lại thành công và đang chờ các cơ sở xem xét lại.");
    }
}
