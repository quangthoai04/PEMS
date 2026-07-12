using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.CreateAuthenticatedVisitRequest;

/// <summary>
/// Authenticated visit-request create for VISITOR / IC Staff (STAFF+STAFF) / Staff Leader
/// (STAFF+LEADER). No OTP — the JWT session is the registrant identity, revalidated from
/// the DB. Reuses the SAME services as the public flow (form creation, account provision,
/// fingerprint idempotency, aggregate status) so the two flows can never drift.
///
/// Campus processing (narrow deliberate exception to "submit never approves"):
///   - Visitor: every campus SEND_FOR_REVIEW; any direct mode/host payload is rejected.
///   - Regular IC Staff: may SELF_HOST their OWN primary campus inside this create only.
///   - Staff Leader: may SELF_HOST or ASSIGN_HOST (same-campus ACTIVE IC Staff) on their
///     OWN campus; other campuses always go to that campus's Staff Leader.
/// Direct processing mirrors ApproveCampusInstanceCommandHandler semantics exactly
/// (decision + host + IC_HOST participant + audit + aggregate status in one transaction).
/// </summary>
public sealed class CreateAuthenticatedVisitRequestCommandHandler
    : IRequestHandler<CreateAuthenticatedVisitRequestCommand, CreateAuthenticatedVisitRequestResponse>
{
    private const int DuplicateWindowMinutes = 15;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVisitRequestService _visitRequestService;
    private readonly IUserProvisionService _userProvisionService;
    private readonly IVisitRequestAggregateStatusService _aggregateStatus;
    private readonly IEmailService _emailService;
    private readonly IDateTimeService _clock;
    private readonly ILogger<CreateAuthenticatedVisitRequestCommandHandler> _logger;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public CreateAuthenticatedVisitRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IVisitRequestService visitRequestService,
        IUserProvisionService userProvisionService,
        IVisitRequestAggregateStatusService aggregateStatus,
        IEmailService emailService,
        IDateTimeService clock,
        ILogger<CreateAuthenticatedVisitRequestCommandHandler> logger,
        PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db                   = db;
        _currentUser          = currentUser;
        _visitRequestService  = visitRequestService;
        _userProvisionService = userProvisionService;
        _aggregateStatus      = aggregateStatus;
        _emailService         = emailService;
        _clock                = clock;
        _logger               = logger;
        _notificationService  = notificationService;
    }

    private sealed record CampusPlan(string Mode, ulong? HostUserId);

    public async Task<CreateAuthenticatedVisitRequestResponse> Handle(
        CreateAuthenticatedVisitRequestCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.VietnamNow;

        // ── 1. Actor: authenticated claims revalidated against the DB (never the payload). ──
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;
        var actor = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == actorId, cancellationToken)
            ?? throw new ForbiddenException();

        if (!string.Equals(actor.Status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Tài khoản của bạn hiện không hoạt động.");

        var isVisitor      = actor.Role.RoleCode == RoleCodes.Visitor;
        var isRegularStaff = actor.Role.RoleCode == RoleCodes.Staff && actor.SubRole == UserSubRoles.Staff;
        var isStaffLeader  = actor.Role.RoleCode == RoleCodes.Staff && actor.SubRole == UserSubRoles.Leader;

        if (!isVisitor && !isRegularStaff && !isStaffLeader)
            throw new ForbiddenException("Vai trò của bạn không được tạo đoàn khách.");

        // ── 2. Registrant identity — server-side, payload identity fields are ignored. ──
        var registrantEmail    = VisitRequestFingerprintBuilder.NormalizeEmail(actor.Email);
        var registrantFullName = actor.FullName;

        // ── 3. Contact rules by actor kind. ──
        var isInternal = isRegularStaff || isStaffLeader;
        var contactEmailInput = request.IsContactSelf && !isInternal
            ? registrantEmail
            : (request.ContactPerson?.Email ?? string.Empty);
        var contactEmail = VisitRequestFingerprintBuilder.NormalizeEmail(contactEmailInput);

        if (isInternal && (request.IsContactSelf || contactEmail == registrantEmail))
            throw new BusinessRuleException(
                "Nhân sự nội bộ không thể là đầu mối liên hệ của đoàn khách. Vui lòng nhập một người khác (tài khoản VISITOR).",
                VisitRequestErrorCodes.InternalRegistrantCannotBeContact);

        var isContactSelf = isVisitor && contactEmail == registrantEmail;

        var contactName  = isContactSelf ? registrantFullName : request.ContactPerson.FullName;
        var contactPhone = isContactSelf
            ? (string.IsNullOrWhiteSpace(request.RegistrantPhone) ? (actor.Phone ?? string.Empty) : request.RegistrantPhone)
            : request.ContactPerson.Phone;

        // ── 4. Per-campus processing plan (default SEND_FOR_REVIEW). ──
        var selectedCodes = request.CampusVisits
            .Select(s => s.CampusId?.Trim().ToUpperInvariant() ?? string.Empty)
            .Where(c => c.Length > 0)
            .ToHashSet();

        var plans = new Dictionary<string, CampusPlan>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in request.CampusProcessing ?? new List<CampusProcessingDto>())
        {
            var code = p.CampusId?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!selectedCodes.Contains(code))
                throw new BusinessRuleException(
                    "Lựa chọn xử lý tham chiếu cơ sở không nằm trong danh sách cơ sở đã chọn.",
                    VisitRequestErrorCodes.DirectModeCampusNotSelected);
            plans[code] = new CampusPlan(p.Mode, p.HostUserId);
        }

        var hasDirectMode = plans.Values.Any(p => p.Mode != CampusSubmissionModes.SendForReview);

        if (isVisitor && (hasDirectMode || plans.Values.Any(p => p.HostUserId.HasValue)))
            throw new BusinessRuleException(
                "Đơn của Visitor luôn chờ Staff Leader từng cơ sở duyệt — không thể tự duyệt hoặc gán host.",
                VisitRequestErrorCodes.InvalidCampusSubmissionMode);

        // Resolve the actor's own campus CODE for direct-mode checks.
        string? actorCampusCode = null;
        if (isInternal && actor.PrimaryCampusId.HasValue)
        {
            actorCampusCode = await _db.Campuses
                .Where(c => c.CampusId == actor.PrimaryCampusId.Value)
                .Select(c => c.CampusCode)
                .FirstOrDefaultAsync(cancellationToken);
        }

        string? actorDepartmentType = null;
        if (isInternal && actor.DepartmentId.HasValue)
        {
            actorDepartmentType = await _db.Departments
                .Where(d => d.DepartmentId == actor.DepartmentId.Value)
                .Select(d => d.DepartmentType)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // ── 5. Role × mode × campus matrix (shape; DB-dependent parts rechecked in tx). ──
        User? assignedHostCandidate = null;
        foreach (var (code, plan) in plans)
        {
            if (plan.Mode == CampusSubmissionModes.SendForReview)
                continue;

            var isOwnCampus = actorCampusCode != null
                && string.Equals(code, actorCampusCode, StringComparison.OrdinalIgnoreCase);
            if (!isOwnCampus)
                throw new ForbiddenException(
                    "Bạn chỉ được xử lý trực tiếp cơ sở của chính mình; cơ sở khác luôn chờ Staff Leader cơ sở đó duyệt.");

            if (plan.Mode == CampusSubmissionModes.AssignHost)
            {
                if (!isStaffLeader)
                    throw new ForbiddenException("Chỉ Staff Leader mới được gán host cho người khác.");

                var candidateId = plan.HostUserId
                    ?? throw new BusinessRuleException(
                        "Chế độ gán host phải chọn host cụ thể.", VisitRequestErrorCodes.InvalidHostCandidate);

                if (candidateId == actorId)
                    continue; // Leader picked themself — equivalent to SELF_HOST, validated below.

                assignedHostCandidate = await _db.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserId == candidateId, cancellationToken)
                    ?? throw new NotFoundException("User", candidateId);

                var candidateDeptType = await _db.Departments
                    .Where(d => d.DepartmentId == assignedHostCandidate.DepartmentId)
                    .Select(d => d.DepartmentType)
                    .FirstOrDefaultAsync(cancellationToken);

                var candidateOk = assignedHostCandidate.Role.RoleCode == RoleCodes.Staff
                    && assignedHostCandidate.SubRole == UserSubRoles.Staff
                    && assignedHostCandidate.PrimaryCampusId == actor.PrimaryCampusId
                    && assignedHostCandidate.Status == UserStatuses.Active
                    && candidateDeptType == "IC";

                if (!candidateOk)
                    throw new BusinessRuleException(
                        "Host được chọn phải là IC Staff đang hoạt động thuộc đúng cơ sở của bạn.",
                        VisitRequestErrorCodes.InvalidHostCandidate);
            }
            else // SELF_HOST
            {
                if (plan.HostUserId.HasValue && plan.HostUserId.Value != actorId)
                    throw new ForbiddenException("Bạn không được gán host là người khác trong chế độ tự nhận host.");

                // Self-host eligibility: an ACTIVE IC-department STAFF of that campus, or the
                // Staff Leader of that campus themself (the DB trigger enforces the same).
                if (isRegularStaff && actorDepartmentType != "IC")
                    throw new BusinessRuleException(
                        "Chỉ IC Staff thuộc phòng IC của cơ sở mới được tự nhận host.",
                        VisitRequestErrorCodes.SelfHostNotEligible);
            }
        }

        // ── 6. Idempotency: same contract as the public flow. Fingerprint is built from
        //       SERVER-side identity (payload identity fields never participate). ──
        var fingerprint = VisitRequestFingerprintBuilder.Build(
            registrantEmail,
            contactEmail,
            request.DelegationName,
            request.VisitScope == VisitScopes.MultiCampus ? VisitScopes.MultiCampus : VisitScopes.SingleCampus,
            request.VisitType,
            request.VisitTypeOther,
            request.CampusVisits.Select(s => (s.CampusId, s.StartDatetime, s.EndDatetime)));

        var replay = await CheckIdempotentReplayAsync(request.SubmissionId, registrantEmail, fingerprint, cancellationToken);
        if (replay is not null)
            return replay;

        // ── 7. Transaction: duplicate guard → provision contact → create → direct-process →
        //       aggregate → audit/notifications. All-or-nothing. ──
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var committed = false;

        VisitRequest visitRequest;
        bool hasHostingConflict = false;
        ulong? notifyAssignedHostId = null;
        try
        {
            var duplicateWindowStart = now.AddMinutes(-DuplicateWindowMinutes);
            var duplicate = await _db.VisitRequests.AsNoTracking()
                .Where(r => r.BusinessFingerprint == fingerprint
                            && r.SubmittedAt >= duplicateWindowStart
                            && r.Status != VisitRequestStatuses.Rejected
                            && r.Status != VisitRequestStatuses.Cancelled)
                .OrderByDescending(r => r.SubmittedAt)
                .Select(r => new { r.VisitRequestId, r.RequestCode, r.Status, r.SubmittedAt })
                .FirstOrDefaultAsync(cancellationToken);

            if (duplicate is not null)
            {
                throw new ConflictException(
                    "Một đơn đăng ký với nội dung tương tự vừa được gửi trước đó. Không có đơn mới nào được tạo.",
                    VisitRequestErrorCodes.DuplicateVisitRequest,
                    new
                    {
                        existingVisitRequestId = duplicate.VisitRequestId,
                        existingRequestCode    = duplicate.RequestCode,
                        existingStatus         = duplicate.Status,
                        existingSubmittedAt    = duplicate.SubmittedAt
                    });
            }

            // Contact owner account: reuse the actor when a Visitor is their own contact;
            // otherwise link/create a VISITOR (internal-email conflicts throw inside).
            var visitorUserId = isContactSelf
                ? actorId
                : await _userProvisionService.EnsureVisitorAccountAsync(
                    contactEmail, contactName, contactPhone, now, cancellationToken);

            var formData = new VisitRequestFormData(
                registrantFullName,
                request.RegistrantNationality,
                request.RegistrantOrganization,
                request.RegistrantPosition,
                request.RegistrantPhone,
                registrantEmail,
                request.DelegationName,
                request.VisitScope,
                request.VisitType,
                request.VisitTypeOther,
                request.CampusVisits,
                request.Purpose,
                request.WorkingContent,
                request.Visitors,
                request.SupportMembers,
                new ContactPointDto(contactName, request.ContactPerson.Organization, contactPhone, contactEmail),
                isContactSelf,
                request.WorkingLanguage,
                request.TransportationNote,
                request.MediaConsentStatus,
                request.MediaConsentNote,
                request.PartnerId,
                request.Notes);

            var createdSource = isInternal ? "STAFF_CREATED" : "VISITOR_SUBMITTED";
            visitRequest = await _visitRequestService.CreateAsync(
                formData, visitorUserId, actorId, createdSource, now, cancellationToken);

            visitRequest.SubmissionId        = request.SubmissionId;
            visitRequest.BusinessFingerprint = fingerprint;
            // The registrant identity is the authenticated session — treated as verified.
            visitRequest.EmailVerifiedAt     = now;

            // ── Direct processing of the actor's own campus (mirrors ApproveCampusInstance). ──
            var campusCodesById = await _db.Campuses
                .Where(c => selectedCodes.Contains(c.CampusCode))
                .Select(c => new { c.CampusId, c.CampusCode })
                .ToDictionaryAsync(c => c.CampusId, c => c.CampusCode, cancellationToken);

            foreach (var instance in visitRequest.CampusInstances)
            {
                var code = campusCodesById.TryGetValue(instance.CampusId, out var cc) ? cc : null;
                var plan = code != null && plans.TryGetValue(code, out var pl) ? pl : null;
                if (plan is null || plan.Mode == CampusSubmissionModes.SendForReview)
                    continue;

                var isLeaderAssignOther = plan!.Mode == CampusSubmissionModes.AssignHost
                    && plan.HostUserId.HasValue && plan.HostUserId.Value != actorId;
                var hostUserId = isLeaderAssignOther ? plan.HostUserId!.Value : actorId;

                // Non-blocking hosting-overlap warning — same policy as ApproveCampusInstance,
                // but here the user must confirm BEFORE the direct assignment happens.
                var conflict = await _db.VisitRequestCampuses.AnyAsync(c =>
                    c.CurrentHostUserId == hostUserId
                    && (c.Status == VisitInstanceStatus.Assigned
                        || c.Status == VisitInstanceStatus.BeforeVisit
                        || c.Status == VisitInstanceStatus.DuringVisit)
                    && c.PlannedStartAt < instance.PlannedEndAt
                    && c.PlannedEndAt > instance.PlannedStartAt,
                    cancellationToken);
                if (conflict)
                {
                    hasHostingConflict = true;
                    if (!request.ConfirmedHostConflict)
                        throw new ConflictException(
                            "Host được chọn đã phụ trách một đoàn khác trùng khung giờ. Vui lòng xác nhận trước khi tiếp tục.",
                            "HOST_SCHEDULE_CONFLICT_CONFIRMATION_REQUIRED");
                }

                instance.Status            = VisitInstanceStatus.Assigned;
                instance.DecidedBy         = actorId;
                instance.DecidedAt         = now;
                instance.DecisionActorRole = isStaffLeader ? DecisionActorRole.StaffLeader : DecisionActorRole.Staff;
                instance.DecisionSource    = isLeaderAssignOther
                    ? DecisionSources.InternalLeaderAssign
                    : DecisionSources.InternalSelfHost;
                instance.CurrentHostUserId = hostUserId;
                instance.HostAssignedBy    = actorId;
                instance.HostAssignedAt    = now;

                if (isLeaderAssignOther)
                    notifyAssignedHostId = hostUserId;

                // Official IC_HOST participant row — same semantics as the approve flow.
                instance.Participants.Add(new VisitParticipant
                {
                    UserId          = hostUserId,
                    ParticipantRole = ParticipantRoles.IcHost,
                    IsHost          = true,
                    Status          = ParticipantStatuses.Assigned,
                    AssignedBy      = actorId,
                    AssignedAt      = now,
                    CreatedAt       = now,
                    CreatedBy       = actorId,
                });
            }

            // Aggregate status — single source that mirrors the SQL aggregate trigger.
            _aggregateStatus.Apply(visitRequest);

            await _db.SaveChangesAsync(cancellationToken);

            // Audit AFTER the first save so the DB-generated id is available.
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                Action = hasDirectMode
                    ? "CREATE_VISIT_REQUEST_AUTHENTICATED_WITH_DIRECT_PROCESSING"
                    : "CREATE_VISIT_REQUEST_AUTHENTICATED",
                EntityType = "VisitRequest",
                EntityId = visitRequest.VisitRequestId,
                CreatedAt = now
            });
            await _db.SaveChangesAsync(cancellationToken);

            // ── In-app notifications (inside the tx — same convention as the public flow). ──
            var notifications = new List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();

            var pendingCampusIds = visitRequest.CampusInstances
                .Where(c => c.Status == VisitInstanceStatus.WaitingRequestApproval)
                .Select(c => c.CampusId)
                .Distinct()
                .ToList();

            if (pendingCampusIds.Count > 0)
            {
                var staffLeaders = await _db.Users
                    .Where(u => u.Role.RoleCode == RoleCodes.Staff
                                && u.SubRole == UserSubRoles.Leader
                                && u.PrimaryCampusId.HasValue
                                && pendingCampusIds.Contains(u.PrimaryCampusId.Value)
                                && u.Status == UserStatuses.Active
                                && u.UserId != actorId)
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken);

                notifications.AddRange(staffLeaders.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Có yêu cầu tiếp khách mới",
                    Message: $"{visitRequest.DelegationName} đang chờ xử lý tại cơ sở của bạn. Vui lòng xem chi tiết, duyệt/từ chối và chọn host nếu duyệt.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    RelatedId: visitRequest.VisitRequestId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    IsActionRequired: true,
                    VisitRequestId: visitRequest.VisitRequestId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: "/dashboard/visit")));
            }

            // Own campus direct-processed: the campus Staff Leader gets an INFORMATIONAL
            // notice (no fake pending action) unless they are the actor themself.
            var directInstances = visitRequest.CampusInstances
                .Where(c => c.DecisionSource == DecisionSources.InternalSelfHost
                            || c.DecisionSource == DecisionSources.InternalLeaderAssign)
                .ToList();
            if (directInstances.Count > 0)
            {
                var directCampusIds = directInstances.Select(c => c.CampusId).Distinct().ToList();
                var monitoringLeaders = await _db.Users
                    .Where(u => u.Role.RoleCode == RoleCodes.Staff
                                && u.SubRole == UserSubRoles.Leader
                                && u.PrimaryCampusId.HasValue
                                && directCampusIds.Contains(u.PrimaryCampusId.Value)
                                && u.Status == UserStatuses.Active
                                && u.UserId != actorId)
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken);

                notifications.AddRange(monitoringLeaders.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Đoàn khách mới đã được xử lý trực tiếp",
                    Message: $"{visitRequest.DelegationName} ({visitRequest.RequestCode}) đã được tạo và xử lý trực tiếp tại cơ sở của bạn.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    RelatedId: visitRequest.VisitRequestId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    VisitRequestId: visitRequest.VisitRequestId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: "/dashboard/visit")));
            }

            // Leader assigned another host → host-assignment notification (actor knows already).
            if (notifyAssignedHostId is { } assignedHostId)
            {
                var assignedInstance = directInstances.First(c => c.CurrentHostUserId == assignedHostId);
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: assignedHostId,
                    Title: "Bạn được gán phụ trách đoàn khách",
                    Message: $"Bạn được phân công làm host chính cho đoàn {visitRequest.DelegationName}. Vui lòng vào Setup đoàn khách để chuẩn bị.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.HostAssigned,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                    RelatedId: assignedInstance.VisitInstanceId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    IsActionRequired: true,
                    VisitRequestId: visitRequest.VisitRequestId,
                    VisitInstanceId: assignedInstance.VisitInstanceId,
                    CampusId: assignedInstance.CampusId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit/process/{assignedInstance.VisitInstanceId}"));
            }

            // Contact owner (different account from the actor): ownership notice in-app.
            if (visitRequest.VisitorUserId is { } ownerId && ownerId != actorId)
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: ownerId,
                    Title: "Bạn là đầu mối liên hệ của đoàn khách",
                    Message: $"Bạn được ghi nhận là đầu mối liên hệ của đoàn {visitRequest.DelegationName} ({visitRequest.RequestCode}). Đăng nhập Visitor Portal để theo dõi và thao tác đơn.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    RelatedId: visitRequest.VisitRequestId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    VisitRequestId: visitRequest.VisitRequestId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: "/dashboard/visit"));
            }

            if (notifications.Count > 0)
                await _notificationService.CreateManyAsync(notifications, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            committed = true;
        }
        catch (DbUpdateException dbEx) when (!committed)
        {
            // uq_visit_requests_submission_id / unique-email race: replay idempotently.
            await transaction.RollbackAsync(cancellationToken);

            var racedReplay = await CheckIdempotentReplayAsync(
                request.SubmissionId, registrantEmail, fingerprint, cancellationToken);
            if (racedReplay is not null)
                return racedReplay;

            _logger.LogError(dbEx,
                "Authenticated create failed with DbUpdateException and no idempotent row for submission {SubmissionId}.",
                request.SubmissionId);
            throw;
        }
        catch
        {
            if (!committed)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // ── 8. External emails AFTER commit (failure never rolls back business data). ──
        var delegationName = request.DelegationName;
        var requestCode = visitRequest.RequestCode;
        var scope = request.VisitScope;
        var slots = request.CampusVisits.ToList();
        _ = Task.Run(async () =>
        {
            try
            {
                var minTime = slots.Min(s => s.StartDatetime);
                var maxTime = slots.Max(s => s.EndDatetime);
                var plannedTimeText = minTime.Date == maxTime.Date
                    ? $"{minTime:dd/MM/yyyy} ({minTime:HH:mm} - {maxTime:HH:mm})"
                    : $"{minTime:dd/MM/yyyy HH:mm} - {maxTime:dd/MM/yyyy HH:mm}";

                await _emailService.SendVisitorAccountCreatedOrLinkedEmailAsync(
                    contactEmail, contactName, delegationName, requestCode, scope, plannedTimeText,
                    CancellationToken.None);

                if (!string.Equals(registrantEmail, contactEmail, StringComparison.OrdinalIgnoreCase))
                {
                    await _emailService.SendRegistrantConfirmationAsync(
                        registrantEmail, registrantFullName, contactName, contactEmail,
                        delegationName, requestCode, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send authenticated-create emails for visit request {RequestCode} to {ContactEmail}",
                    requestCode, contactEmail);
            }
        });

        var message = visitRequest.Status switch
        {
            VisitRequestStatuses.Approved => "Đơn đã được tạo và xử lý trực tiếp thành công.",
            VisitRequestStatuses.PartiallyApproved => "Đơn đã được tạo; cơ sở của bạn đã xử lý, các cơ sở khác đang chờ Staff Leader duyệt.",
            _ => "Đơn đăng ký tham quan đã được gửi thành công và đang chờ phê duyệt."
        };

        return new CreateAuthenticatedVisitRequestResponse(
            visitRequest.VisitRequestId,
            visitRequest.RequestCode,
            visitRequest.Status,
            message,
            hasHostingConflict);
    }

    /// <summary>Same-submission-intent replay: same contract as the public verify flow.</summary>
    private async Task<CreateAuthenticatedVisitRequestResponse?> CheckIdempotentReplayAsync(
        string submissionId, string normalizedEmail, string fingerprint, CancellationToken cancellationToken)
    {
        var existing = await _db.VisitRequests.AsNoTracking()
            .Where(r => r.SubmissionId == submissionId)
            .Select(r => new { r.VisitRequestId, r.RequestCode, r.Status, r.RegistrantEmail, r.BusinessFingerprint })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
            return null;

        if (!string.Equals(existing.RegistrantEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.BusinessFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new ConflictException(
                "Phiên gửi đơn này đã được dùng cho một nội dung khác. Vui lòng tải lại trang và gửi lại.",
                VisitRequestErrorCodes.IdempotencyKeyReused);
        }

        return new CreateAuthenticatedVisitRequestResponse(
            existing.VisitRequestId,
            existing.RequestCode,
            existing.Status,
            "Đơn đăng ký này đã được ghi nhận trước đó. Không có đơn mới nào được tạo.",
            false);
    }
}
