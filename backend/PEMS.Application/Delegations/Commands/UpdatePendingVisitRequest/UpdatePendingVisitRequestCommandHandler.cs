using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.UpdatePendingVisitRequest;

/// <summary>
/// Visitor pending edit (spec §2.1/§6): rewrites the form fields, guest members and campus
/// schedules of a still-fully-pending request. Campus list may change (no campus decided
/// anything yet) — removed instances are deleted, new ones start WAITING_REQUEST_APPROVAL
/// with the campus Staff Leader as coordinator. Status stays PENDING_APPROVAL and
/// resubmission_count is NOT touched.
/// </summary>
public sealed class UpdatePendingVisitRequestCommandHandler
    : IRequestHandler<UpdatePendingVisitRequestCommand, UpdatePendingVisitRequestResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public UpdatePendingVisitRequestCommandHandler(
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

    public async Task<UpdatePendingVisitRequestResponse> Handle(
        UpdatePendingVisitRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        if (_currentUser.RoleCode != RoleCodes.Visitor)
            throw new ForbiddenException("Chỉ khách (Visitor) mới được sửa đơn đăng ký tham quan.");

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
            throw new ForbiddenException("Bạn chỉ được sửa đơn của chính mình.");

        // ── Editable-state gate (spec §2.1) ──
        if (visit.Status != VisitRequestStatuses.PendingApproval)
            throw new BusinessRuleException(
                "Chỉ có thể sửa đơn đang chờ xử lý (chưa cơ sở nào ra quyết định).",
                VisitRequestErrorCodes.VisitRequestNotEditable);
        if (visit.CampusInstances.Count == 0
            || visit.CampusInstances.Any(c => c.Status != VisitInstanceStatus.WaitingRequestApproval))
            throw new BusinessRuleException(
                "Đơn đã có cơ sở được xử lý (duyệt/từ chối/hủy) nên không thể sửa.",
                VisitRequestErrorCodes.VisitRequestNotEditable);
        if (visit.CampusInstances.Min(c => c.PlannedStartAt) < vnNow.AddHours(24))
            throw new BusinessRuleException(
                "Lịch thăm sắp diễn ra trong vòng 24 giờ nên không thể sửa. Vui lòng liên hệ FPTU để được hỗ trợ.",
                VisitRequestErrorCodes.VisitCancelWindowExpired);

        // ── Resolve + validate the (possibly changed) campus list ──
        var requestedCodes = request.CampusVisits
            .Select(s => s.CampusId?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (requestedCodes.Count == 0)
            throw new BusinessRuleException("Phải chọn ít nhất 1 cơ sở.", VisitRequestErrorCodes.InvalidVisitScope);

        var campuses = await _db.Campuses
            .Where(c => requestedCodes.Contains(c.CampusCode))
            .Select(c => new { c.CampusCode, c.CampusId, c.Status, c.Name })
            .ToListAsync(cancellationToken);
        var campusByCode = campuses.ToDictionary(c => c.CampusCode, StringComparer.OrdinalIgnoreCase);

        foreach (var code in requestedCodes)
        {
            if (!campusByCode.TryGetValue(code, out var campus))
                throw new BusinessRuleException($"Cơ sở '{code}' không tồn tại.", VisitRequestErrorCodes.CampusNotFound);
            if (!string.Equals(campus.Status, EntityStatuses.Active, StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException($"Cơ sở '{code}' hiện không hoạt động.", VisitRequestErrorCodes.CampusInactive);
        }

        // Every chosen campus must still have an ACTIVE Staff Leader (routing target/coordinator).
        var requestedCampusIds = campuses.Select(c => c.CampusId).ToList();
        var staffLeadersByCampus = (await _db.Users
                .Where(u => u.Role.RoleCode == RoleCodes.Staff
                            && u.SubRole == UserSubRoles.Leader
                            && u.Status == UserStatuses.Active
                            && u.PrimaryCampusId.HasValue
                            && requestedCampusIds.Contains(u.PrimaryCampusId.Value))
                .Select(u => new { u.UserId, CampusId = u.PrimaryCampusId!.Value })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.CampusId)
            .ToDictionary(g => g.Key, g => g.First().UserId);
        foreach (var campus in campuses)
        {
            if (!staffLeadersByCampus.ContainsKey(campus.CampusId))
                throw new BusinessRuleException(
                    $"Cơ sở {campus.Name} chưa có Staff Leader đang hoạt động nên chưa thể tiếp nhận yêu cầu.",
                    VisitRequestErrorCodes.CampusHasNoActiveStaffLeader);
        }

        // ── Validate the new schedule (validator already ran; re-check the DB-facing rules) ──
        foreach (var slot in request.CampusVisits)
        {
            if (slot.EndDatetime <= slot.StartDatetime)
                throw new BusinessRuleException("Thời gian kết thúc phải sau thời gian bắt đầu.", VisitRequestErrorCodes.InvalidVisitTime);
            if (slot.StartDatetime < vnNow.AddHours(24))
                throw new BusinessRuleException(
                    "Thời gian bắt đầu mới phải cách hiện tại ít nhất 24 giờ.",
                    VisitRequestErrorCodes.InvalidVisitTime);
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

        var visitScope = request.VisitScope == VisitScopes.MultiCampus
            ? VisitScopes.MultiCampus
            : VisitScopes.SingleCampus;

        var contactName = request.IsContactSelf ? request.RegistrantFullName : request.ContactPerson.FullName;
        var contactPhone = request.IsContactSelf ? request.RegistrantPhone : request.ContactPerson.Phone;
        var contactOrg = request.IsContactSelf ? request.RegistrantOrganization : request.ContactPerson.Organization;

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        // ── visit_requests scalar fields ──
        visit.DelegationName = request.DelegationName;
        visit.VisitScope = visitScope;
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
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;
        visit.RowVersion += 1;

        // ── Guest members: full replace (pending request has no dependent rows yet) ──
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

        // ── Campus instances: diff update by campus_id (all still WAITING — safe to add/remove) ──
        var slotsByCampusId = request.CampusVisits
            .GroupBy(s => campusByCode[s.CampusId!.Trim()].CampusId)
            .ToDictionary(g => g.Key, g => g.First());

        var removedInstances = visit.CampusInstances
            .Where(c => !slotsByCampusId.ContainsKey(c.CampusId))
            .ToList();
        foreach (var gone in removedInstances)
        {
            visit.CampusInstances.Remove(gone);
            _db.VisitRequestCampuses.Remove(gone);
        }

        var keptCampusIds = new HashSet<ulong>();
        foreach (var instance in visit.CampusInstances)
        {
            var slot = slotsByCampusId[instance.CampusId];
            keptCampusIds.Add(instance.CampusId);
            instance.PlannedStartAt = slot.StartDatetime;
            instance.PlannedEndAt = slot.EndDatetime;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            instance.RowVersion += 1;
        }

        foreach (var (campusId, slot) in slotsByCampusId)
        {
            if (keptCampusIds.Contains(campusId))
                continue;
            visit.CampusInstances.Add(new VisitRequestCampus
            {
                CampusId = campusId,
                PlannedStartAt = slot.StartDatetime,
                PlannedEndAt = slot.EndDatetime,
                Status = VisitInstanceStatus.WaitingRequestApproval,
                CoordinatorUserId = staffLeadersByCampus[campusId],
                CoordinatorAssignedBy = actorId,
                CoordinatorAssignedAt = now,
                RowVersion = 0,
                CreatedAt = now,
                CreatedBy = actorId
            });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "UPDATE_PENDING_VISIT_REQUEST",
            EntityType = "VisitRequest",
            EntityId = visit.VisitRequestId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        // ── Notify the Staff Leaders of every involved campus (kept + added + removed) ──
        var involvedCampusIds = slotsByCampusId.Keys
            .Concat(removedInstances.Select(r => r.CampusId))
            .Distinct()
            .ToList();
        var leadersToNotify = await _db.Users
            .Where(u => u.Role.RoleCode == RoleCodes.Staff
                        && u.SubRole == UserSubRoles.Leader
                        && u.PrimaryCampusId.HasValue
                        && involvedCampusIds.Contains(u.PrimaryCampusId.Value)
                        && u.Status == UserStatuses.Active)
            .Select(u => u.UserId)
            .ToListAsync(cancellationToken);
        if (leadersToNotify.Count > 0)
        {
            await _notificationService.CreateManyAsync(
                leadersToNotify.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Visitor đã cập nhật đơn đăng ký tham quan",
                    Message: $"Visitor đã cập nhật thông tin đơn {visit.RequestCode} ({visit.DelegationName}). Vui lòng xem lại thông tin mới nhất trước khi xử lý.",
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

        return new UpdatePendingVisitRequestResponse(
            visit.VisitRequestId,
            visit.Status,
            "Đơn đăng ký đã được cập nhật. Staff Leader các cơ sở sẽ xem thông tin mới nhất.");
    }
}
