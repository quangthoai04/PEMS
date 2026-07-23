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
using PEMS.Application.Notifications.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

public sealed class CreateVisitRequestV2CommandHandler
    : IRequestHandler<CreateVisitRequestV2Command, CreateVisitRequestV2Response>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitRequestV2CreateService _createService;
    private readonly INotificationService _notificationService;
    private readonly IVisitContactClaimService _contactClaimService;
    private readonly IUserProvisionService _userProvisionService;
    private readonly ILogger<CreateVisitRequestV2CommandHandler> _logger;
    private readonly IVisitRequestAggregateStatusService _aggregateStatus;
    private readonly PerCampusFormV2Options _readFlag;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public CreateVisitRequestV2CommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitRequestV2CreateService createService,
        INotificationService notificationService,
        IVisitContactClaimService contactClaimService, IUserProvisionService userProvisionService,
        ILogger<CreateVisitRequestV2CommandHandler> logger,
        PerCampusFormV2Options readFlag, PerCampusFormV2WriteOptions writeFlag,
        IVisitRequestAggregateStatusService aggregateStatus)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _createService = createService;
        _notificationService = notificationService;
        _contactClaimService = contactClaimService;
        _userProvisionService = userProvisionService;
        _logger = logger;
        _aggregateStatus = aggregateStatus;
        _readFlag = readFlag;
        _writeFlag = writeFlag;
    }

    public async Task<CreateVisitRequestV2Response> Handle(
        CreateVisitRequestV2Command request, CancellationToken cancellationToken)
    {
        // ── Flag gate ──
        // Write OFF → the endpoint is inert (404); only the v1 create flow exists.
        if (!_writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        // Write ON but read OFF → invalid config: we would create v2 records that no read path can surface.
        if (!_readFlag.Enabled)
            throw new ConflictException(
                "Cấu hình không hợp lệ: bật ghi v2 nhưng chưa bật đọc v2.",
                CreateVisitRequestV2ErrorCodes.ReadRequired);

        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var registrantUserId = _currentUser.UserId.Value;

        var actor = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == registrantUserId, cancellationToken)
            ?? throw new ForbiddenException();

        if (!string.Equals(actor.Status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Tài khoản của bạn hiện không hoạt động.");

        var isVisitor      = actor.Role.RoleCode == RoleCodes.Visitor;
        var isRegularStaff = actor.Role.RoleCode == RoleCodes.Staff && actor.SubRole == UserSubRoles.Staff;
        var isStaffLeader  = actor.Role.RoleCode == RoleCodes.Staff && actor.SubRole == UserSubRoles.Leader;

        if (!isVisitor && !isRegularStaff && !isStaffLeader)
            throw new ForbiddenException("Vai trò của bạn không được tạo đoàn khách.");

        var isInternal = isRegularStaff || isStaffLeader;
        var createdSource = isInternal ? "STAFF_CREATED" : "VISITOR_SUBMITTED";

        var form = request.Form;
        if (string.IsNullOrWhiteSpace(form.SubmissionId))
            throw new BusinessRuleException("Thiếu submissionId.", "SUBMISSION_ID_REQUIRED");

        var contactEmail = form.PrimaryContact.Email.Trim().ToLowerInvariant();
        var registrantEmail = actor.Email!.ToLowerInvariant();

        if (isInternal && (contactEmail == registrantEmail))
            throw new BusinessRuleException(
                "Nhân sự nội bộ không thể là đầu mối liên hệ của đoàn khách. Vui lòng nhập một người khác (tài khoản VISITOR).",
                VisitRequestErrorCodes.InternalRegistrantCannotBeContact);

        if (isInternal && contactEmail != registrantEmail)
        {
            await _userProvisionService.ValidateContactEmailCanBeUsedForVisitorAsync(contactEmail, cancellationToken);
        }

        // ── Per-campus processing (authenticated create only; default = route to the campus Staff Leader).
        //    Authorization is the SAME role × mode × campus matrix as the v1 authenticated create, kept in
        //    one pure, unit-tested place (V2CampusProcessingRules) so the two flows can never drift. Every
        //    decision/host/audit column below is derived server-side — the client only states an intent. ──
        var selectedCodes = form.CampusVisits
            .Select(s => s.CampusId?.Trim().ToUpperInvariant() ?? string.Empty)
            .Where(c => c.Length > 0)
            .ToHashSet();

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

        var processingActor = new V2ProcessingActor(
            IsVisitor: isVisitor,
            IsRegularStaff: isRegularStaff,
            IsStaffLeader: isStaffLeader,
            OwnCampusCode: actorCampusCode,
            OwnDepartmentIsIc: actorDepartmentType == "IC",
            ActorUserId: registrantUserId);

        var planList = V2CampusProcessingRules.BuildPlans(form);
        V2CampusProcessingRules.ValidateShape(processingActor, planList);

        var plans = planList.ToDictionary(p => p.CampusCode, StringComparer.OrdinalIgnoreCase);
        var hasDirectMode = planList.Count > 0;

        // ASSIGN_HOST candidate: re-queried and re-authorized from the DB — the client dropdown is never
        // trusted (a disabled / other-campus / non-IC / Leader pick is rejected here, not in the UI).
        foreach (var plan in planList)
        {
            if (plan.Mode != CampusSubmissionModes.AssignHost) continue;

            var candidateId = plan.HostUserId!.Value;
            if (candidateId == registrantUserId)
                continue; // Leader picked themself — equivalent to SELF_HOST, already validated above.

            var candidate = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == candidateId, cancellationToken)
                ?? throw new NotFoundException("User", candidateId);

            var candidateDeptType = await _db.Departments
                .Where(d => d.DepartmentId == candidate.DepartmentId)
                .Select(d => d.DepartmentType)
                .FirstOrDefaultAsync(cancellationToken);

            var candidateOk = candidate.Role.RoleCode == RoleCodes.Staff
                && candidate.SubRole == UserSubRoles.Staff
                && candidate.PrimaryCampusId == actor.PrimaryCampusId
                && candidate.Status == UserStatuses.Active
                && candidateDeptType == "IC";

            if (!candidateOk)
                throw new BusinessRuleException(
                    "Host được chọn phải là IC Staff đang hoạt động thuộc đúng cơ sở của bạn.",
                    VisitRequestErrorCodes.InvalidHostCandidate);
        }

        // ── Idempotency (sequential): a retry with the same submissionId returns the same request. ──
        var existing = await FindBySubmissionAsync(form.SubmissionId, cancellationToken);
        if (existing is not null)
            return await ToResponseAsync(existing.Value.RequestId, cancellationToken, idempotent: true);

        var now = _clock.VietnamNow;
        VisitRequest created;

        ulong? notifyAssignedHostId = null;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            try
            {

                var initializers = new System.Collections.Generic.Dictionary<string, System.Action<PEMS.Domain.Entities.Delegations.VisitRequestCampus>>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in plans)
                {
                    var decision = V2CampusProcessingRules.Derive(processingActor, kvp.Value);
                    if (decision.IsLeaderAssignOther)
                        notifyAssignedHostId = decision.HostUserId;

                    initializers[kvp.Key] = instance =>
                    {
                        instance.Status = VisitInstanceStatus.Assigned;
                        instance.DecidedBy = registrantUserId;
                        instance.DecidedAt = now;
                        instance.DecisionActorRole = decision.DecisionActorRole;
                        instance.DecisionSource = decision.DecisionSource;
                        instance.CurrentHostUserId = decision.HostUserId;
                        instance.HostAssignedBy = registrantUserId;
                        instance.HostAssignedAt = now;
                        instance.Participants.Add(new VisitParticipant
                        {
                            UserId = decision.HostUserId,
                            ParticipantRole = ParticipantRoles.IcHost,
                            IsHost = true,
                            Status = ParticipantStatuses.Assigned,
                            AssignedBy = registrantUserId,
                            AssignedAt = now,
                            CreatedAt = now,
                            CreatedBy = registrantUserId,
                        });
                    };
                }

                created = await _createService.CreateV2Async(
                    form, registrantUserId, createdSource, now, cancellationToken, initializers);

                // Aggregate status — single source that mirrors the SQL aggregate trigger.
                _aggregateStatus.Apply(created);

                _db.AuditLogs.Add(new PEMS.Domain.Entities.Users.AuditLog
                {
                    ActorUserId = registrantUserId,
                    Action = hasDirectMode
                        ? "CREATE_VISIT_REQUEST_AUTHENTICATED_WITH_DIRECT_PROCESSING"
                        : "CREATE_VISIT_REQUEST_AUTHENTICATED",
                    EntityType = "VisitRequest",
                    EntityId = created.VisitRequestId,
                    CreatedAt = now
                });

                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Concurrent same-submission may have lost the unique-index race
                // (uq_visit_requests_submission_id): roll our own attempt back and, if a winner now exists,
                // return it — never two requests. Any other DB error is NOT swallowed.
                await tx.RollbackAsync(cancellationToken);
                var dup = await FindBySubmissionAsync(form.SubmissionId, cancellationToken);
                if (dup is not null)
                    return await ToResponseAsync(dup.Value.RequestId, cancellationToken, idempotent: true);
                throw;
            }
        }

        // ── Post-commit notifications + INITIAL_CLAIM invitation (only on the first successful create).
        //    Dispatched AFTER commit so a rollback never notifies/invites; a same-submission replay takes
        //    the idempotent return paths above and never re-sends. Best-effort; see V2CreateNotifier. ──
        await V2CreateNotifier.NotifyStaffLeadersAfterCommitAsync(
            _db, _notificationService, _logger, created, cancellationToken);
        await V2CreateNotifier.SendContactClaimInvitationAfterCommitAsync(
            _db, _contactClaimService, _logger, created, cancellationToken);

        if (notifyAssignedHostId is { } assignedHostId)
        {
            var assignedInstance = created.CampusInstances.First(c => c.CurrentHostUserId == assignedHostId);
            // The host is assigned to ONE campus, so name the delegation from THAT instance's own detail.
            var assignedDelegationName = await _db.VisitInstanceFormDetails.AsNoTracking()
                .Where(d => d.VisitInstanceId == assignedInstance.VisitInstanceId)
                .Select(d => d.DelegationName)
                .FirstOrDefaultAsync(cancellationToken) ?? created.RequestCode;
            await _notificationService.CreateManyAsync(new[]
            {
                new CreateNotificationRequest(
                    RecipientUserId: assignedHostId,
                    Title: "Bạn được gán phụ trách đoàn khách",
                    Message: $"Bạn được phân công làm host chính cho đoàn {assignedDelegationName}. Vui lòng vào Setup đoàn khách để chuẩn bị.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.HostAssigned,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                    RelatedId: assignedInstance.VisitInstanceId,
                    ActorUserId: registrantUserId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    IsActionRequired: true,
                    VisitRequestId: created.VisitRequestId,
                    VisitInstanceId: assignedInstance.VisitInstanceId,
                    CampusId: assignedInstance.CampusId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit/process/{assignedInstance.VisitInstanceId}")
            }, cancellationToken);
        }

        return await ToResponseAsync(created.VisitRequestId, cancellationToken, idempotent: false);
    }

    private async Task<(ulong RequestId, string RequestCode)?> FindBySubmissionAsync(
        string submissionId, CancellationToken ct)
    {
        var row = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.SubmissionId == submissionId)
            .Select(v => new { v.VisitRequestId, v.RequestCode })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : (row.VisitRequestId, row.RequestCode ?? string.Empty);
    }

    private async Task<CreateVisitRequestV2Response> ToResponseAsync(
        ulong visitRequestId, CancellationToken ct, bool idempotent)
    {
        var head = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == visitRequestId)
            .Select(v => new { v.RequestCode, v.VisitScope, v.HasMixedCampusDetails, v.PrimaryContactAccessStatus, v.VisitorUserId })
            .FirstAsync(ct);
        var instances = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == visitRequestId)
            .OrderBy(c => c.CampusId)
            .Select(c => new CreateVisitRequestV2CampusRef(c.VisitInstanceId, c.CampusId, c.Status))
            .ToListAsync(ct);
        return new CreateVisitRequestV2Response(
            visitRequestId, head.RequestCode ?? string.Empty, head.VisitScope, head.HasMixedCampusDetails,
            head.PrimaryContactAccessStatus,
            ContactClaimPending: head.VisitorUserId is null,
            instances, idempotent);
    }
}
