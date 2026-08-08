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
    private readonly IOperationalContactInvitationService _contactInvitations;
    private readonly IUserProvisionService _userProvisionService;
    private readonly ILogger<CreateVisitRequestV2CommandHandler> _logger;
    private readonly IVisitRequestAggregateStatusService _aggregateStatus;
    private readonly IProposedHostActivationService _activationService;
    private readonly PerCampusFormV2Options _readFlag;
    private readonly PerCampusFormV2WriteOptions _writeFlag;
    private readonly IUserMutationLockService _lockService;

    public CreateVisitRequestV2CommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitRequestV2CreateService createService,
        INotificationService notificationService,
        IOperationalContactInvitationService contactInvitations, IUserProvisionService userProvisionService,
        ILogger<CreateVisitRequestV2CommandHandler> logger,
        PerCampusFormV2Options readFlag, PerCampusFormV2WriteOptions writeFlag,
        IVisitRequestAggregateStatusService aggregateStatus,
        IProposedHostActivationService activationService,
        IUserMutationLockService lockService)
    {
        _activationService = activationService;
        _lockService = lockService;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _createService = createService;
        _notificationService = notificationService;
        _contactInvitations = contactInvitations;
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

        // Every campus names its own contact; one person may hold several, so grouped by normalized
        // address before checking — one lookup per distinct contact, and any violation is still
        // reported on EVERY campus card naming it. There is no single request-level address left to
        // check instead.
        var contactsByEmail = form.CampusVisits
            .Select((c, i) => (Index: i, Email: c.OperationalContact.Email))
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .GroupBy(x => RegistrantIdentityRules.Normalize(x.Email), StringComparer.OrdinalIgnoreCase)
            .ToList();
        var registrantEmail = RegistrantIdentityRules.Normalize(actor.Email);

        // ── Registrant identity (plan §5.3/§8.1) ──
        // This endpoint is SELF-registration only: the JWT is what stands in for the OTP, so it can only
        // vouch for the caller's OWN mailbox. A form naming somebody else as registrant is routed to the
        // delegated OTP flow instead — otherwise registrant_user_id and the persisted registrant snapshot
        // would describe two different people, with nothing having verified the second one.
        RegistrantIdentityRules.EnsureDirectCreateIsSelfRegistration(actor.Email, form.Registrant.Email);

        // An internal creator may not appoint THEMSELF as any campus's operational contact: the whole
        // point of the gate is that somebody outside FPTU confirms they will run the visit, and a
        // Staff Leader self-appointing would clear it without anyone confirming anything.
        //
        // Reported as a field error on the campus cards that actually named that address — the same
        // shape as the contact check below. It used to be one banner sentence for the whole form, so
        // on a three-campus request the user was told "somebody internal cannot be the contact" with
        // nothing saying which card to look at, and the sentence named the account type on top of it.
        if (isInternal)
        {
            var selfContactCampuses = contactsByEmail
                .Where(g => string.Equals(g.Key, registrantEmail, StringComparison.OrdinalIgnoreCase))
                .SelectMany(g => g)
                .Select(c => c.Index)
                .ToList();
            if (selfContactCampuses.Count > 0)
                throw new ValidationException(
                    selfContactCampuses.ToDictionary(
                        index => $"CampusVisits[{index}].OperationalContact.Email",
                        _ => new[] { VisitRequestErrorMessages.ContactEmailNotEligible }),
                    VisitRequestErrorCodes.InternalRegistrantCannotBeContact);
        }

        if (isInternal)
        {
            // Collected into field-shaped errors (CampusVisits[i].OperationalContact.Email) rather than
            // thrown as a plain message: a bare ConflictException/BusinessRuleException here used to
            // surface as one generic banner at the bottom of a long, multi-campus form, leaving the user
            // to guess which contact email was the problem. A ValidationException lands red on the exact
            // input instead (see mapServerFieldPathToFormPath on the frontend).
            Dictionary<string, string[]>? contactFieldErrors = null;
            // Representative code for the WHOLE response (first violation found) — the client uses it
            // to show a localized message (errors:api.<CODE>) instead of this raw Vietnamese text
            // verbatim, which is the only wording available when two different violations land in one
            // response.
            string? contactErrorCode = null;
            foreach (var group in contactsByEmail)
            {
                try
                {
                    await _userProvisionService.ValidateContactEmailCanBeUsedForVisitorAsync(
                        group.Key, cancellationToken);
                }
                catch (Exception ex) when (ex is ConflictException or BusinessRuleException)
                {
                    contactFieldErrors ??= new Dictionary<string, string[]>();
                    contactErrorCode ??= (ex as ConflictException)?.ErrorCode ?? (ex as BusinessRuleException)?.ErrorCode;
                    foreach (var contact in group)
                        contactFieldErrors[$"CampusVisits[{contact.Index}].OperationalContact.Email"] = new[] { ex.Message };
                }
            }
            if (contactFieldErrors is not null)
                throw new ValidationException(contactFieldErrors, contactErrorCode);
        }

        // ── Per-campus reception-host arrangement (authenticated create only; default = wait for the
        //    campus Staff Leader to assign). Authorization lives in one pure, unit-tested place
        //    (V2HostProposalRules). Nothing here names a CURRENT host: a proposal is stored and only
        //    becomes an assignment when the confirmation gate opens. ──
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

        var proposalActor = new V2ProposalActor(
            IsVisitor: isVisitor,
            IsRegularStaff: isRegularStaff,
            IsStaffLeader: isStaffLeader,
            OwnCampusCode: actorCampusCode,
            OwnDepartmentIsIc: actorDepartmentType == "IC",
            ActorUserId: registrantUserId);

        var proposalList = V2HostProposalRules.Authorize(
            proposalActor, V2HostProposalRules.BuildProposals(form));

        var proposals = proposalList
            .Where(p => HostSelectionModes.IsProposal(p.Mode))
            .ToDictionary(p => p.CampusCode, StringComparer.OrdinalIgnoreCase);

        // The proposed person is re-queried and re-authorized from the DB — the client dropdown is never
        // trusted (a disabled / other-campus / non-IC / other-Leader pick is rejected here, not in the UI).
        // Runs twice: once here to fail fast, once more under the row lock inside the transaction. It runs
        // AGAIN at activation, because between submit and the gate opening anything may have changed.
        await EnsureProposedHostsEligibleAsync(
            proposals.Values, registrantUserId, actor.PrimaryCampusId, cancellationToken);

        // ── Idempotency (sequential): a retry with the same submissionId returns the same request. ──
        var existing = await FindBySubmissionAsync(form.SubmissionId, cancellationToken);
        if (existing is not null)
            return await ToResponseAsync(existing.Value.RequestId, cancellationToken, idempotent: true);

        var now = _clock.VietnamNow;
        VisitRequest created;
        IReadOnlyList<ProposedHostActivation> activations = System.Array.Empty<ProposedHostActivation>();

        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                // ── A proposal can be activated inside this very transaction (every contact
                //    self-matched, so the gate never closes), so the accounts involved join the shared
                //    lock protocol (see IUserMutationLockService) before their eligibility is trusted.
                //    The candidate loop above ran unlocked and is deliberately re-run here against
                //    committed state (spec §13.6/§14). ──
                var proposedHostIds = proposals.Values
                    .Select(p => p.ProposedHostUserId!.Value)
                    .Distinct()
                    .ToList();
                if (proposedHostIds.Count > 0)
                {
                    await _lockService.LockUsersAsync(proposedHostIds, cancellationToken);
                    await EnsureProposedHostsEligibleAsync(
                        proposals.Values, registrantUserId, actor.PrimaryCampusId, cancellationToken);
                }

                var seeds = proposals.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new CampusHostProposalSeed(
                        kvp.Value.Mode, kvp.Value.ProposedHostUserId, registrantUserId),
                    StringComparer.OrdinalIgnoreCase);

                created = await _createService.CreateV2Async(
                    form, registrantUserId, createdSource, now, cancellationToken, seeds);

                // Aggregate status — single source that mirrors the SQL aggregate trigger.
                _aggregateStatus.Apply(created);

                _db.AuditLogs.Add(new PEMS.Domain.Entities.Users.AuditLog
                {
                    ActorUserId = registrantUserId,
                    Action = proposals.Count > 0
                        ? "CREATE_VISIT_REQUEST_AUTHENTICATED_WITH_PROPOSED_HOST"
                        : "CREATE_VISIT_REQUEST_AUTHENTICATED",
                    EntityType = "VisitRequest",
                    EntityId = created.VisitRequestId,
                    CreatedAt = now
                });

                await _db.SaveChangesAsync(cancellationToken);

                // ── Branch C: every campus's contact was the registrant themself, so nobody has to
                //    confirm anything and the gate is already open. Activate now — but only after the
                //    flush above, because the DB refuses a campus reaching ASSIGNED while its request
                //    row still reads PENDING_CONTACT_CONFIRMATION, and EF writes campuses first. ──
                if (!VisitRequestStatuses.IsBehindContactGate(created.Status))
                {
                    activations = await _activationService.ActivateAsync(created, now, cancellationToken);
                    if (activations.Count > 0)
                    {
                        _aggregateStatus.Apply(created);
                        WriteActivationAudits(created, activations, registrantUserId, now);
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }

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
        await V2CreateNotifier.SendOperationalContactInvitationsAfterCommitAsync(
            _db, _contactInvitations, _logger, created, cancellationToken);

        // Proposals that were stored but NOT activated (the gate is still shut) are announced as
        // proposals — never as "you have been assigned". §7.3 is explicit that the two must not read
        // the same, because until a contact confirms there is nothing to prepare for.
        await ProposedHostNotifier.AnnounceOutcomeAsync(
            _db, _notificationService, _logger, created, proposals.Values.Count > 0, activations,
            registrantUserId, cancellationToken);

        return await ToResponseAsync(created.VisitRequestId, cancellationToken, idempotent: false);
    }

    /// <summary>
    /// Files one audit row per activated campus. The activation is a real campus decision made by
    /// somebody who is not present at the time, so it needs the same per-campus audit trail an
    /// approve gets — otherwise a host appears on a campus with nothing recording how.
    /// </summary>
    private void WriteActivationAudits(
        VisitRequest visit, IReadOnlyList<ProposedHostActivation> activations, ulong actorId, DateTime now)
    {
        foreach (var activation in activations)
        {
            _db.AuditLogs.Add(new PEMS.Domain.Entities.Users.AuditLog
            {
                ActorUserId = activation.ProposerUserId ?? actorId,
                Action = activation.Activated
                    ? "PROPOSED_HOST_ACTIVATED"
                    : "PROPOSED_HOST_NEEDS_RESELECTION",
                EntityType = "VisitRequestCampus",
                EntityId = activation.VisitInstanceId,
                CampusId = activation.CampusId,
                VisitRequestId = visit.VisitRequestId,
                VisitInstanceId = activation.VisitInstanceId,
                SourceType = CampusDecisionAudit.SourceType,
                Reason = activation.Activated
                    ? $"mode={activation.SelectionMode};host={activation.ProposedHostUserId}"
                    : $"mode={activation.SelectionMode};reason={activation.RejectionReason}",
                CreatedAt = now,
            });
        }
    }

    /// <summary>
    /// Re-authorizes every proposed host straight from the database: an active IC Staff of the
    /// proposer's own campus, or the proposing Staff Leader themself — never another Leader and never
    /// another campus. Called once before the transaction to fail fast, and again inside it under the
    /// users row lock, because between those two points a role change may have committed.
    /// </summary>
    private async Task EnsureProposedHostsEligibleAsync(
        IEnumerable<V2HostProposal> proposals, ulong registrantUserId,
        ulong? actorCampusId, CancellationToken ct)
    {
        foreach (var proposal in proposals)
        {
            var candidateId = proposal.ProposedHostUserId!.Value;
            if (candidateId == registrantUserId)
                continue; // Proposing themself — the role × mode matrix already settled this.

            var candidate = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == candidateId, ct)
                ?? throw new NotFoundException("User", candidateId);

            var candidateDeptType = await _db.Departments
                .Where(d => d.DepartmentId == candidate.DepartmentId)
                .Select(d => d.DepartmentType)
                .FirstOrDefaultAsync(ct);

            var candidateOk = candidate.Role.RoleCode == RoleCodes.Staff
                && candidate.SubRole == UserSubRoles.Staff
                && candidate.PrimaryCampusId == actorCampusId
                && candidate.Status == UserStatuses.Active
                && candidateDeptType == "IC";

            if (!candidateOk)
                throw new BusinessRuleException(
                    "Người phụ trách dự kiến phải là IC Staff đang hoạt động thuộc đúng cơ sở của bạn.",
                    VisitRequestErrorCodes.InvalidHostCandidate);
        }
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
            .Select(v => new { v.RequestCode, v.VisitScope, v.HasMixedCampusDetails, v.Status, v.SubmittedAt })
            .FirstAsync(ct);
        var rows = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == visitRequestId)
            .OrderBy(c => c.CampusId)
            .Select(c => new { c.VisitInstanceId, c.CampusId, c.Status, c.OperationalContactUserId })
            .ToListAsync(ct);
        var instances = rows
            .Select(c => new CreateVisitRequestV2CampusRef(c.VisitInstanceId, c.CampusId, c.Status))
            .ToList();
        // Counted from the campuses themselves, not from a request-level flag: the count IS the
        // per-campus fact, and a cancelled campus is nobody's outstanding confirmation.
        var pendingConfirmations = rows.Count(c =>
            c.Status != VisitInstanceStatus.Cancelled && c.OperationalContactUserId is null);

        return new CreateVisitRequestV2Response(
            visitRequestId, head.RequestCode ?? string.Empty, head.VisitScope, head.HasMixedCampusDetails,
            pendingConfirmations,
            instances, idempotent,
            head.Status, head.SubmittedAt.ToString("yyyy-MM-ddTHH:mm:ss"), instances.Count);
    }
}
