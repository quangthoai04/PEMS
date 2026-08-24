using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.VisitNotifications;
using PEMS.Application.Emails.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The operational contact is frozen from the moment a campus starts — and cleanup of an invitation
/// left in flight is not, against a real MySQL database.
///
/// <para>
/// The bug this suite exists for is a race the previous rule could not even express. Whether a handover
/// was allowed used to be decided by a CLOCK (24 hours before <c>PlannedStartAt</c>), and it was decided
/// ONCE, when the invitation was written. A transfer proposed at 08:55 for a 09:00 visit was refused;
/// one proposed three days earlier and accepted at 09:02, with the delegation already in the building
/// and the campus reading DURING_VISIT, went through and moved the contact mid-visit. Both halves were
/// wrong, and no test could have caught the second, because the guard was never asked a second time.
/// </para>
/// <para>
/// So the rule is now: the persisted campus status decides, and it is re-asked at every point that
/// would MOVE the contact — initiate, accept, resend. The invitation's own 24-hour validity is a
/// different fact and still stands: a link may be perfectly fresh and still no longer applicable.
/// </para>
/// <para>
/// The four cleanup answers stay open. Cancel and decline settle a pending invitation without touching
/// who holds the campus, so blocking them would strand a started campus with a handover nobody can
/// close. That asymmetry — mutation locked, cleanup open — is the half of the rule most likely to be
/// "simplified" away later, so it is asserted here as loudly as the lock itself.
/// </para>
///
/// Each test creates its own committed request and cascade-deletes it in <c>finally</c>.
/// </summary>
public sealed class OperationalContactLifecycleLockTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    // ── Infrastructure ────────────────────────────────────────────────────────────

    private sealed class FakeUser : ICurrentUserService
    {
        private readonly ulong _id;
        private readonly string? _email;
        public FakeUser(ulong id, string? email = null) { _id = id; _email = email; }
        public bool IsAuthenticated => true;
        public ulong? UserId => _id;
        public string? Email => _email;
        public ulong? RoleId => null;
        public string? RoleCode => RoleCodes.Visitor;
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private sealed class NoopNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong r, string t, string? m, string nt, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest r, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>Captures what the expiry sweep would have sent, without rendering or delivering anything.</summary>
    private sealed class RecordingDispatcher : ISystemEmailDispatcher
    {
        public Task<SystemEmailDispatchResult> SendAsync(
            SystemEmailRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new SystemEmailDispatchResult(
                EmailDeliveryResult.Sent(), SentEmailId: 0, EmailTemplateId: 0));

        public Task<PreparedSystemEmail> PrepareAsync(
            SystemEmailRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EmailDeliveryResult> DeliverAsync(
            PreparedSystemEmail prepared, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    /// <summary>The claim always succeeds here: one sweep at a time, no cross-process race to narrow.</summary>
    private sealed class GrantingLock : IEmailRecoveryLock
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(string key, CancellationToken ct)
            => Task.FromResult<IAsyncDisposable?>(new Handle());

        private sealed class Handle : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>The real maintenance sweep, wired the way the container wires it.</summary>
    private static OperationalContactMaintenanceService Sweeper(ApplicationDbContext db, RecordingDispatcher mail)
        => new(db, NullLogger<OperationalContactMaintenanceService>.Instance,
            new ContactInvitationExpiryEmail(db),
            new RecoverableVisitEmailSender(
                db, mail, new GrantingLock(), new FixedClock(),
                NullLogger<RecoverableVisitEmailSender>.Instance));

    private sealed class FakeEmail : IEmailService
    {
        public List<(string To, string Subject, string Html)> Sent { get; } = new();

        /// <summary>
        /// The ACCEPT and DECLINE links of the last invitation sent, read back out of the rendered
        /// body. A handler that mints inside its own transaction never hands the raw tokens to its
        /// caller — and it must not, since the token is the secret — so the only place a test can
        /// obtain them is the same place the invited person does. Distinct and in order: the block
        /// prints the accept URL twice (button and plain text) before the decline URL.
        /// </summary>
        public (string Accept, string Decline) LastInvitationLinks()
        {
            var html = Sent.LastOrDefault().Html
                ?? throw new InvalidOperationException("No invitation email was sent.");
            var tokens = Regex.Matches(html, @"operational-contact-confirmation/([A-Za-z0-9_\-]+)")
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            Assert.Equal(2, tokens.Count);
            return (tokens[0], tokens[1]);
        }

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
        { Sent.Add((toEmail, subject, htmlBody)); return Task.CompletedTask; }

        public Task<EmailDeliveryResult> TrySendAsync(OutboundEmail message, CancellationToken ct = default)
        { Record(message); return Task.FromResult(EmailDeliveryResult.Sent()); }

        public Task SendAsync(OutboundEmail message, CancellationToken ct = default)
        { Record(message); return Task.CompletedTask; }

        private void Record(OutboundEmail message)
            => Sent.Add((message.To.Count > 0 ? message.To[0].Email : string.Empty,
                         message.Subject ?? string.Empty, message.Body ?? string.Empty));

        public Task<EmailDeliveryResult> TrySendAsync(
            string toEmail, string subject, string htmlBody, CancellationToken ct = default)
            => Task.FromResult(EmailDeliveryResult.Sent());
    }

    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static EmailActionTokenService Tokens() => new(EmptyConfig);

    private static OperationalContactInvitationService Invitations(ApplicationDbContext db, FakeEmail email)
        => new(db, Tokens(),
            new SystemEmailDispatcher(db, new EmailTemplateRenderer(db), email),
            new FixedClock(), NullLogger<OperationalContactInvitationService>.Instance, EmptyConfig);

    // ── Handlers under test ───────────────────────────────────────────────────────

    private static SaveOperationalContactCommandHandler Save(
        ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new InProcessSender(db, actor, email), new FakeUser(actor), WriteOn);

    /// <summary>
    /// The router's three destinations, wired to the REAL handlers. The point of going through the
    /// router is that it is the door the contact screen actually saves through: it classifies by
    /// address and delegates, and the refusal has to come from the destination's own guard rather than
    /// from a status test duplicated in the router.
    /// </summary>
    private sealed class InProcessSender : ISender
    {
        private readonly ApplicationDbContext _db;
        private readonly ulong _actor;
        private readonly FakeEmail _email;

        public InProcessSender(ApplicationDbContext db, ulong actor, FakeEmail email)
        { _db = db; _actor = actor; _email = email; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request switch
            {
                UpdateOperationalContactProfileCommand c => Cast<TResponse>(
                    new UpdateOperationalContactProfileCommandHandler(
                        _db, new FakeUser(_actor), new FixedClock(), Invitations(_db, _email),
                        new CanonicalContentRefresher(_db), WriteOn)
                        .Handle(c, ct)),
                ReplaceOperationalContactCommand c => Cast<TResponse>(
                    new ReplaceOperationalContactCommandHandler(
                        _db, new FakeUser(_actor), new FixedClock(), Invitations(_db, _email),
                        new VisitRequestAggregateStatusService(_db), new NoopNotifications(),
                        NullLogger<ReplaceOperationalContactCommandHandler>.Instance, WriteOn)
                        .Handle(c, ct)),
                InitiateOperationalContactTransferCommand c => Cast<TResponse>(
                    Transfer(_db, _actor, _email).Handle(c, ct)),
                _ => throw new NotSupportedException(request.GetType().Name),
            };

        private static async Task<TResponse> Cast<TResponse>(Task<OperationalContactManageResponse> task)
            => (TResponse)(object)await task;

        public Task<object?> Send(object request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default)
            where TRequest : IRequest
            => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private static InitiateOperationalContactTransferCommandHandler Transfer(
        ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new FakeUser(actor), new FixedClock(), Invitations(db, email), WriteOn);

    private static AcceptOperationalContactConfirmationCommandHandler Accept(
        ApplicationDbContext db, ulong actor, string actorEmail, FakeEmail email)
        => new(db, new FakeUser(actor, actorEmail), new FixedClock(), Tokens(), Invitations(db, email),
            new VisitRequestAggregateStatusService(db), new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)),
            new NoopNotifications(),
            NullLogger<AcceptOperationalContactConfirmationCommandHandler>.Instance, WriteOn);

    private static DeclineOperationalContactConfirmationCommandHandler Decline(
        ApplicationDbContext db, ulong actor, string actorEmail, FakeEmail email)
        => new(db, new FakeUser(actor, actorEmail), new FixedClock(), Tokens(), Invitations(db, email), WriteOn);

    private static ResendOperationalContactConfirmationCommandHandler Resend(
        ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new FakeUser(actor), new FixedClock(), Invitations(db, email), WriteOn);

    private static CancelOperationalContactChangeCommandHandler Cancel(
        ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new FakeUser(actor), new FixedClock(), Invitations(db, email), WriteOn);

    // ── Data helpers ──────────────────────────────────────────────────────────────

    /// <summary>An ACTIVE VISITOR account other than the registrant and any id already spoken for.</summary>
    private static async Task<(ulong UserId, string Email)> VisitorUserAsync(
        ApplicationDbContext db, params ulong[] exclude)
    {
        var taken = exclude.Append(Registrant).ToList();
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                        && !taken.Contains(u.UserId))
            .OrderBy(u => u.UserId)
            .Select(u => new { u.UserId, u.Email })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "This database needs at least two ACTIVE VISITOR accounts besides user 8.");
        return (row.UserId, row.Email!);
    }

    private static CampusVisitFormDto Campus(string code, string contactEmail)
    {
        var start = Now.AddDays(25);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(120), "Đoàn đầu mối", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Đầu mối " + code, "OrgB", "Trưởng phòng Hợp tác", "+84912345678", contactEmail),
            "EN", null, "DECLINED", null, null);
    }

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
            new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = true }, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));

        var form = new VisitRequestFormDataV2(
            "LL" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private sealed record CampusRow(ulong InstanceId, string Status, ulong? ContactUserId);

    private static async Task<CampusRow> CampusStateAsync(ulong requestId)
    {
        using var db = NewContext();
        var c = await db.VisitRequestCampuses.AsNoTracking()
            .Where(x => x.VisitRequestId == requestId)
            .OrderBy(x => x.VisitInstanceId)
            .FirstAsync();
        return new CampusRow(c.VisitInstanceId, c.Status, c.OperationalContactUserId);
    }

    private static async Task<PEMS.Domain.Entities.Delegations.VisitInstanceFormDetail> DetailAsync(ulong instanceId)
    {
        using var db = NewContext();
        return await db.VisitInstanceFormDetails.AsNoTracking().FirstAsync(d => d.VisitInstanceId == instanceId);
    }

    private static async Task<List<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange>> ChangesAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestIdentityChanges.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .OrderBy(c => c.IdentityChangeId).ToListAsync();
    }

    private static async Task<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange> PendingTransferAsync(ulong requestId)
        => (await ChangesAsync(requestId)).Single(c =>
            c.Status == IdentityChangeStatuses.Pending && c.ChangeKind == IdentityChangeKinds.Transfer);

    /// <summary>Links of this invitation a recipient could still click.</summary>
    private static async Task<int> LiveTokenCountAsync(ulong changeId)
    {
        using var db = NewContext();
        return await db.EmailActionTokens.AsNoTracking()
            .CountAsync(t => t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange
                             && t.TargetId == changeId
                             && t.UsedAt == null
                             && t.ResultStatus == EmailActionResultStatuses.Pending);
    }

    /// <summary>
    /// Issues the CREATE-time invitation the way every production caller does — mint, make the links
    /// durable, then send — and returns its ACCEPT link. Needed only because the create handler is
    /// wired to a recording invitation service in these fixtures, so nothing has been minted yet. The
    /// transfer path mints its own inside the handler's transaction and is read back from the email.
    /// </summary>
    private static async Task<string> IssueInvitationAsync(FakeEmail email, ulong identityChangeId)
    {
        using var db = NewContext();
        var invitations = Invitations(db, email);
        var tokens = await invitations.MintInvitationTokensAsync(identityChangeId, CancellationToken.None);
        Assert.NotNull(tokens);
        await db.SaveChangesAsync(CancellationToken.None);
        await invitations.DispatchInvitationEmailAsync(identityChangeId, tokens!, CancellationToken.None);
        return tokens!.AcceptToken;
    }

    /// <summary>
    /// Backdates the invitation's newest link past the one-minute resend cooldown. The rate limiter
    /// reads the last <c>email_action_tokens</c> row rather than a column on the invitation, so that
    /// is what has to move for a resend to be about the lifecycle rather than about the cooldown.
    /// </summary>
    private static async Task AgeLastTokenAsync(ulong changeId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE email_action_tokens SET created_at = {0} " +
            "WHERE target_type = 'VISIT_REQUEST_IDENTITY_CHANGE' AND target_id = {1}",
            Now.AddMinutes(-30), changeId);
    }

    private static SaveOperationalContactCommand SaveOf(
        ulong requestId, ulong instanceId, PEMS.Domain.Entities.Delegations.VisitInstanceFormDetail d,
        string? fullName = null, string? phone = null, string? email = null)
        => new(requestId, instanceId,
            fullName ?? d.OperationalContactFullName!,
            d.OperationalContactOrganization,
            d.OperationalContactJobTitle!,
            phone ?? d.OperationalContactPhone,
            email ?? d.OperationalContactEmail,
            Reason: null, ExpectedRowVersion: null);

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
        await Del("DELETE FROM email_action_tokens WHERE target_type = 'VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_agendas WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── Lifecycle drivers ─────────────────────────────────────────────────────────

    /// <summary>
    /// WAITING_REQUEST_APPROVAL → ASSIGNED, satisfying what the database insists on: a Staff Leader OF
    /// THIS CAMPUS as the decider, and an official host named in the same statement.
    /// </summary>
    private static async Task DriveToAssignedAsync(ulong instanceId)
    {
        using var db = NewContext();
        var campusId = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.CampusId).FirstAsync();

        var leaderId = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Staff
                        && u.SubRole == UserSubRoles.Leader
                        && u.Status == UserStatuses.Active
                        && u.PrimaryCampusId == campusId)
            .OrderBy(u => u.UserId).Select(u => (ulong?)u.UserId).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                $"Campus {campusId} has no ACTIVE Staff Leader, so no request could have been registered against it.");

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'ASSIGNED', decided_by = {1}, decided_at = {2}, " +
            "decision_actor_role = 'STAFF_LEADER', decision_note = 'test approval', " +
            "current_host_user_id = {1}, host_assigned_by = {1}, host_assigned_at = {2} " +
            "WHERE visit_instance_id = {0}",
            instanceId, leaderId, Now);
    }

    /// <summary>ASSIGNED → BEFORE_VISIT: the trigger only lets BEFORE_VISIT be entered from ASSIGNED.</summary>
    private static async Task DriveToBeforeVisitAsync(ulong instanceId)
    {
        await DriveToAssignedAsync(instanceId);
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'BEFORE_VISIT' WHERE visit_instance_id = {0}",
            instanceId);
    }

    /// <summary>
    /// Starts the visit. Two things the database requires and this workflow does not otherwise touch:
    /// DURING_VISIT may only be entered from BEFORE_VISIT, and a campus at DURING_VISIT or beyond must
    /// have at least one agenda item — a delegation cannot be "being received" against an empty
    /// programme. Both are checked by <c>trg_visit_campuses_*_bu</c>, so they are seeded rather than
    /// worked around.
    /// </summary>
    private static async Task StartVisitAsync(ulong instanceId, string status = VisitInstanceStatuses.DuringVisit)
    {
        using var db = NewContext();
        var hasAgenda = await db.VisitAgendas.AsNoTracking().AnyAsync(a => a.VisitInstanceId == instanceId);
        if (!hasAgenda)
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO visit_agendas (visit_instance_id, sequence_order, title, start_time, created_at) " +
                "VALUES ({0}, 1, 'Phiên làm việc', {1}, {1})",
                instanceId, Now);

        // DURING_VISIT first even when the destination is later: the trigger names BEFORE_VISIT as the
        // only door into it, and AFTER_VISIT/CLOSED are reached through it in production too.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'DURING_VISIT' WHERE visit_instance_id = {0}",
            instanceId);

        if (status != VisitInstanceStatuses.DuringVisit)
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE visit_request_campuses SET status = {1} WHERE visit_instance_id = {0}",
                instanceId, status);
    }

    /// <summary>
    /// A campus at <paramref name="status"/> whose contact is <c>contactId</c> and which has a PENDING
    /// transfer to <c>successor</c> — the state every "stale invitation" test below starts from.
    /// </summary>
    private static async Task<(ulong RequestId, ulong InstanceId, ulong ContactId, ulong ChangeId, string AcceptToken, string DeclineToken)>
        PendingTransferOnStartedCampusAsync(FakeEmail mail, string startedStatus)
    {
        ulong contactId, successorId;
        string contactEmail, successorEmail;
        using (var db = NewContext())
        {
            (contactId, contactEmail) = await VisitorUserAsync(db);
            (successorId, successorEmail) = await VisitorUserAsync(db, contactId);
        }

        var requestId = await CreateAsync(Campus("HN", contactEmail));
        var created = await CampusStateAsync(requestId);
        var invitation = Assert.Single(await ChangesAsync(requestId));

        // The invited person accepts, so the campus has a real confirmed contact to hand over.
        var acceptToken = await IssueInvitationAsync(mail, invitation.IdentityChangeId);
        using (var db = NewContext())
            await Accept(db, contactId, contactEmail, mail).Handle(
                new AcceptOperationalContactConfirmationCommand(acceptToken), CancellationToken.None);

        await DriveToBeforeVisitAsync(created.InstanceId);

        // A handover proposed while the campus was still legal. The handler mints and sends its own
        // links inside its transaction, so they are read back from the invitation that went out.
        using (var db = NewContext())
            await Transfer(db, Registrant, mail).Handle(
                new InitiateOperationalContactTransferCommand(
                    requestId, created.InstanceId, "Người nhận bàn giao", "OrgC", "Trưởng phòng",
                    "+84900000123", successorEmail, "Đầu mối cũ bận"),
                CancellationToken.None);

        var transfer = await PendingTransferAsync(requestId);
        Assert.Equal(successorEmail, mail.Sent.Last().To);
        var links = mail.LastInvitationLinks();

        // ...and then the visit starts before anybody answers it.
        await StartVisitAsync(created.InstanceId, startedStatus);

        var started = await CampusStateAsync(requestId);
        Assert.Equal(startedStatus, started.Status);
        Assert.Equal(contactId, started.ContactUserId);

        _ = successorId;
        return (requestId, created.InstanceId, contactId, transfer.IdentityChangeId, links.Accept, links.Decline);
    }

    /// <summary>
    /// A campus at WAITING_REQUEST_APPROVAL whose contact is <c>contactId</c> and which has a PENDING
    /// transfer to a successor — the state every "pre-approval handover" test below starts from. No
    /// decision has been made yet; this is exactly where the destructive REPLACE bug used to strike.
    /// </summary>
    private static async Task<(ulong RequestId, ulong InstanceId, ulong ContactId, ulong ChangeId, string AcceptToken, string DeclineToken)>
        PendingTransferOnUndecidedCampusAsync(FakeEmail mail)
    {
        ulong contactId, successorId;
        string contactEmail, successorEmail;
        using (var db = NewContext())
        {
            (contactId, contactEmail) = await VisitorUserAsync(db);
            (successorId, successorEmail) = await VisitorUserAsync(db, contactId);
        }

        var requestId = await CreateAsync(Campus("HN", contactEmail));
        var created = await CampusStateAsync(requestId);
        var invitation = Assert.Single(await ChangesAsync(requestId));

        var acceptToken = await IssueInvitationAsync(mail, invitation.IdentityChangeId);
        using (var db = NewContext())
            await Accept(db, contactId, contactEmail, mail).Handle(
                new AcceptOperationalContactConfirmationCommand(acceptToken), CancellationToken.None);

        Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, (await CampusStateAsync(requestId)).Status);

        // A handover proposed before any decision. The handler mints and sends its own links inside its
        // transaction, so they are read back from the invitation that went out.
        using (var db = NewContext())
            await Transfer(db, Registrant, mail).Handle(
                new InitiateOperationalContactTransferCommand(
                    requestId, created.InstanceId, "Người nhận bàn giao", "OrgC", "Trưởng phòng",
                    "+84900000123", successorEmail, "Đầu mối cũ bận"),
                CancellationToken.None);

        var transfer = await PendingTransferAsync(requestId);
        Assert.Equal(successorEmail, mail.Sent.Last().To);
        var links = mail.LastInvitationLinks();

        _ = successorId;
        return (requestId, created.InstanceId, contactId, transfer.IdentityChangeId, links.Accept, links.Decline);
    }

    // ── 1. The lock: nothing that moves the contact survives the start ────────────

    /// <summary>
    /// TC-LOCK-01. The everyday save — same address, corrected phone — through the same router the
    /// contact screen posts to. It reaches the profile handler, whose lifecycle guard refuses it, and
    /// no column moves.
    /// </summary>
    [Theory]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.AfterVisit)]
    [InlineData(VisitInstanceStatuses.Closed)]
    public async Task A_started_campus_refuses_a_contact_profile_correction(string status)
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactId;
            string contactEmail;
            using (var db = NewContext()) (contactId, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var created = await CampusStateAsync(requestId);
            var invitation = Assert.Single(await ChangesAsync(requestId));

            var mail = new FakeEmail();
            var token = await IssueInvitationAsync(mail, invitation.IdentityChangeId);
            using (var db = NewContext())
                await Accept(db, contactId, contactEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);

            await DriveToBeforeVisitAsync(created.InstanceId);
            await StartVisitAsync(created.InstanceId, status);

            var before = await DetailAsync(created.InstanceId);

            var refusal = await Assert.ThrowsAsync<ConflictException>(async () =>
            {
                using var db = NewContext();
                await Save(db, Registrant, new FakeEmail()).Handle(
                    SaveOf(requestId, created.InstanceId, before,
                        fullName: "Tên sửa sau khi bắt đầu", phone: "+84900000777"),
                    CancellationToken.None);
            });
            Assert.Equal(OperationalContactErrorCodes.ChangeConflict, refusal.ErrorCode);

            // Nothing was written on the way to the refusal.
            var after = await DetailAsync(created.InstanceId);
            Assert.Equal(before.OperationalContactFullName, after.OperationalContactFullName);
            Assert.Equal(before.OperationalContactPhone, after.OperationalContactPhone);
            Assert.Equal(before.FormRevision, after.FormRevision);
            Assert.Equal(contactId, (await CampusStateAsync(requestId)).ContactUserId);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-LOCK-02. A NEW address on a started campus routes to the transfer handler, which refuses it.
    /// The assertion that matters is the second one: no invitation row is left behind, because a
    /// PENDING change would occupy the campus and block every later contact operation on it.
    /// </summary>
    [Theory]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.Closed)]
    public async Task A_started_campus_refuses_a_new_handover_and_writes_no_invitation(string status)
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactId, successorId;
            string contactEmail, successorEmail;
            using (var db = NewContext())
            {
                (contactId, contactEmail) = await VisitorUserAsync(db);
                (successorId, successorEmail) = await VisitorUserAsync(db, contactId);
            }

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var created = await CampusStateAsync(requestId);
            var invitation = Assert.Single(await ChangesAsync(requestId));

            var mail = new FakeEmail();
            var token = await IssueInvitationAsync(mail, invitation.IdentityChangeId);
            using (var db = NewContext())
                await Accept(db, contactId, contactEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);

            await DriveToBeforeVisitAsync(created.InstanceId);
            await StartVisitAsync(created.InstanceId, status);

            var detail = await DetailAsync(created.InstanceId);
            var changesBefore = await ChangesAsync(requestId);
            var handoverMail = new FakeEmail();

            var refusal = await Assert.ThrowsAsync<ConflictException>(async () =>
            {
                using var db = NewContext();
                await Save(db, Registrant, handoverMail).Handle(
                    SaveOf(requestId, created.InstanceId, detail,
                        fullName: "Người nhận bàn giao", email: successorEmail),
                    CancellationToken.None);
            });
            Assert.Equal(OperationalContactErrorCodes.ChangeConflict, refusal.ErrorCode);

            Assert.Empty(handoverMail.Sent);
            Assert.Equal(changesBefore.Count, (await ChangesAsync(requestId)).Count);
            var after = await CampusStateAsync(requestId);
            Assert.Equal(contactId, after.ContactUserId);
            Assert.Equal(detail.OperationalContactEmail, (await DetailAsync(created.InstanceId)).OperationalContactEmail);
            _ = successorId;
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── 2. Lifecycle, not clock ──────────────────────────────────────────────────

    /// <summary>
    /// TC-CLOCK-01. The refusal the old lead time is remembered for. The campus starts in ONE MINUTE
    /// and has not started; the handover goes through. Under the 24-hour rule a registrant whose
    /// contact fell ill the night before was told to telephone FPTU.
    /// </summary>
    [Fact]
    public async Task A_handover_a_minute_before_the_start_succeeds_while_the_campus_has_not_started()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactId;
            string contactEmail, successorEmail;
            using (var db = NewContext())
            {
                (contactId, contactEmail) = await VisitorUserAsync(db);
                (_, successorEmail) = await VisitorUserAsync(db, contactId);
            }

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var created = await CampusStateAsync(requestId);
            var invitation = Assert.Single(await ChangesAsync(requestId));

            var mail = new FakeEmail();
            var token = await IssueInvitationAsync(mail, invitation.IdentityChangeId);
            using (var db = NewContext())
                await Accept(db, contactId, contactEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);

            await DriveToBeforeVisitAsync(created.InstanceId);

            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_campuses SET planned_start_at = {0}, planned_end_at = {1} " +
                    "WHERE visit_instance_id = {2}",
                    Now.AddMinutes(1), Now.AddMinutes(120), created.InstanceId);

            var handoverMail = new FakeEmail();
            using (var db = NewContext())
                await Transfer(db, Registrant, handoverMail).Handle(
                    new InitiateOperationalContactTransferCommand(
                        requestId, created.InstanceId, "Người nhận bàn giao", "OrgC", "Trưởng phòng",
                        "+84900000123", successorEmail, "Đầu mối cũ ốm đột xuất"),
                    CancellationToken.None);

            var transfer = await PendingTransferAsync(requestId);
            Assert.Equal(successorEmail, transfer.NewEmailNormalized);
            Assert.Equal(contactId, transfer.OldUserId);
            Assert.Equal(successorEmail, Assert.Single(handoverMail.Sent).To);

            // And nothing has moved yet — the handshake is still the handshake.
            var after = await CampusStateAsync(requestId);
            Assert.Equal(contactId, after.ContactUserId);
            Assert.Equal(VisitInstanceStatuses.BeforeVisit, after.Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-CLOCK-02. And the mirror image: a campus whose planned start is a fortnight out is refused
    /// anyway once the workflow has moved it to DURING_VISIT. Together with the test above this is the
    /// whole claim — the verdict follows the status, and the schedule is only what the campus is
    /// scheduled to do.
    /// </summary>
    [Fact]
    public async Task A_handover_is_refused_on_a_started_campus_even_with_a_fortnight_to_its_planned_start()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactId;
            string contactEmail, successorEmail;
            using (var db = NewContext())
            {
                (contactId, contactEmail) = await VisitorUserAsync(db);
                (_, successorEmail) = await VisitorUserAsync(db, contactId);
            }

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var created = await CampusStateAsync(requestId);
            var invitation = Assert.Single(await ChangesAsync(requestId));

            var mail = new FakeEmail();
            var token = await IssueInvitationAsync(mail, invitation.IdentityChangeId);
            using (var db = NewContext())
                await Accept(db, contactId, contactEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);

            await DriveToBeforeVisitAsync(created.InstanceId);
            await StartVisitAsync(created.InstanceId);

            // Comfortably outside any lead time the old rule would have measured.
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_campuses SET planned_start_at = {0}, planned_end_at = {1} " +
                    "WHERE visit_instance_id = {2}",
                    Now.AddDays(14), Now.AddDays(14).AddHours(2), created.InstanceId);

            var refusal = await Assert.ThrowsAsync<ConflictException>(async () =>
            {
                using var db = NewContext();
                await Transfer(db, Registrant, new FakeEmail()).Handle(
                    new InitiateOperationalContactTransferCommand(
                        requestId, created.InstanceId, "Người nhận bàn giao", "OrgC", "Trưởng phòng",
                        "+84900000123", successorEmail, null),
                    CancellationToken.None);
            });
            Assert.Equal(OperationalContactErrorCodes.ChangeConflict, refusal.ErrorCode);
            Assert.Single(await ChangesAsync(requestId));   // the accepted initial confirmation only
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-CLOCK-03. ASSIGNED is handover territory too — the campus has a decision and a Host, and the
    /// Host has simply not opened preparation yet. The guard used to demand BEFORE_VISIT while the read
    /// model offered the button on both, so this exact call was a button that 409'd.
    /// </summary>
    [Fact]
    public async Task A_handover_may_be_proposed_on_an_assigned_campus_before_preparation_starts()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactId;
            string contactEmail, successorEmail;
            using (var db = NewContext())
            {
                (contactId, contactEmail) = await VisitorUserAsync(db);
                (_, successorEmail) = await VisitorUserAsync(db, contactId);
            }

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var created = await CampusStateAsync(requestId);
            var invitation = Assert.Single(await ChangesAsync(requestId));

            var mail = new FakeEmail();
            var token = await IssueInvitationAsync(mail, invitation.IdentityChangeId);
            using (var db = NewContext())
                await Accept(db, contactId, contactEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);

            await DriveToAssignedAsync(created.InstanceId);
            Assert.Equal(VisitInstanceStatuses.Assigned, (await CampusStateAsync(requestId)).Status);

            using (var db = NewContext())
                await Transfer(db, Registrant, new FakeEmail()).Handle(
                    new InitiateOperationalContactTransferCommand(
                        requestId, created.InstanceId, "Người nhận bàn giao", "OrgC", "Trưởng phòng",
                        "+84900000123", successorEmail, null),
                    CancellationToken.None);

            var transfer = await PendingTransferAsync(requestId);
            Assert.Equal(successorEmail, transfer.NewEmailNormalized);
            Assert.Equal(VisitInstanceStatuses.Assigned, (await CampusStateAsync(requestId)).Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── 3. The stale invitation ──────────────────────────────────────────────────

    /// <summary>
    /// TC-STALE-01. The race this whole change exists for. A handover proposed while the campus was
    /// still BEFORE_VISIT is accepted after it has started: the link is valid, unused and inside its
    /// 24 hours, and the answer is still refused — because applying it would swap the contact of a
    /// campus that is currently receiving a delegation.
    /// </summary>
    [Theory]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.AfterVisit)]
    [InlineData(VisitInstanceStatuses.Closed)]
    public async Task A_transfer_pending_since_before_the_start_cannot_be_accepted_afterwards(string status)
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var mail = new FakeEmail();
            var setup = await PendingTransferOnStartedCampusAsync(mail, status);
            requestId = setup.RequestId;

            ulong successorId;
            string successorEmail;
            using (var db = NewContext())
                (successorId, successorEmail) = await VisitorUserAsync(db, setup.ContactId);

            var refusal = await Assert.ThrowsAsync<ConflictException>(async () =>
            {
                using var db = NewContext();
                await Accept(db, successorId, successorEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(setup.AcceptToken),
                    CancellationToken.None);
            });
            Assert.Equal(OperationalContactErrorCodes.ChangeConflict, refusal.ErrorCode);

            // The outgoing contact keeps the campus, and the invitation stays answerable — a refusal
            // is not a settlement, so cancel and decline below still have something to close.
            var after = await CampusStateAsync(requestId);
            Assert.Equal(setup.ContactId, after.ContactUserId);
            Assert.Equal(status, after.Status);

            var change = (await ChangesAsync(requestId)).Single(c => c.IdentityChangeId == setup.ChangeId);
            Assert.Equal(IdentityChangeStatuses.Pending, change.Status);
            Assert.Null(change.AppliedAt);
            Assert.Null(change.NewUserId);
            Assert.Equal(2, await LiveTokenCountAsync(setup.ChangeId));   // accept + decline, both intact
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-STALE-02. The same invitation, resent instead of accepted. Resending is not a neutral
    /// re-delivery: it kills the old links, bumps the version and pushes the expiry another day out,
    /// which is exactly how a handover that is no longer applicable would be kept alive. Refused —
    /// and refused BEFORE any of those four writes, which is what the assertions check.
    /// </summary>
    [Theory]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.AfterVisit)]
    public async Task A_transfer_pending_since_before_the_start_cannot_be_resent_afterwards(string status)
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var mail = new FakeEmail();
            var setup = await PendingTransferOnStartedCampusAsync(mail, status);
            requestId = setup.RequestId;

            // Past the cooldown, so what refuses below is the lifecycle and not the rate limiter.
            await AgeLastTokenAsync(setup.ChangeId);

            var before = (await ChangesAsync(requestId)).Single(c => c.IdentityChangeId == setup.ChangeId);
            var liveBefore = await LiveTokenCountAsync(setup.ChangeId);
            var resendMail = new FakeEmail();

            var refusal = await Assert.ThrowsAsync<ConflictException>(async () =>
            {
                using var db = NewContext();
                await Resend(db, Registrant, resendMail).Handle(
                    new ResendOperationalContactConfirmationCommand(requestId, setup.InstanceId),
                    CancellationToken.None);
            });
            Assert.Equal(OperationalContactErrorCodes.ChangeConflict, refusal.ErrorCode);

            var after = (await ChangesAsync(requestId)).Single(c => c.IdentityChangeId == setup.ChangeId);
            Assert.Equal(before.TokenVersion, after.TokenVersion);
            Assert.Equal(before.ResendCount, after.ResendCount);
            Assert.Equal(before.ExpiresAt, after.ExpiresAt);
            Assert.Equal(IdentityChangeStatuses.Pending, after.Status);
            // The old links were NOT killed on the way to the refusal, and no replacement was minted.
            Assert.Equal(liveBefore, await LiveTokenCountAsync(setup.ChangeId));
            Assert.Empty(resendMail.Sent);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-STALE-03. The control for the test above: the same resend, on the same invitation, while the
    /// campus is still BEFORE_VISIT. It must actually work — a lifecycle guard that refused everything
    /// would pass the test above for the wrong reason.
    /// </summary>
    [Fact]
    public async Task A_pending_transfer_can_still_be_resent_while_the_campus_has_not_started()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactId;
            string contactEmail, successorEmail;
            using (var db = NewContext())
            {
                (contactId, contactEmail) = await VisitorUserAsync(db);
                (_, successorEmail) = await VisitorUserAsync(db, contactId);
            }

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var created = await CampusStateAsync(requestId);
            var invitation = Assert.Single(await ChangesAsync(requestId));

            var mail = new FakeEmail();
            var token = await IssueInvitationAsync(mail, invitation.IdentityChangeId);
            using (var db = NewContext())
                await Accept(db, contactId, contactEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);

            await DriveToBeforeVisitAsync(created.InstanceId);
            using (var db = NewContext())
                await Transfer(db, Registrant, mail).Handle(
                    new InitiateOperationalContactTransferCommand(
                        requestId, created.InstanceId, "Người nhận bàn giao", "OrgC", "Trưởng phòng",
                        "+84900000123", successorEmail, null),
                    CancellationToken.None);

            var before = await PendingTransferAsync(requestId);
            await AgeLastTokenAsync(before.IdentityChangeId);

            var resendMail = new FakeEmail();
            using (var db = NewContext())
                await Resend(db, Registrant, resendMail).Handle(
                    new ResendOperationalContactConfirmationCommand(requestId, created.InstanceId),
                    CancellationToken.None);

            var after = await PendingTransferAsync(requestId);
            Assert.Equal(before.TokenVersion + 1u, after.TokenVersion);
            Assert.Equal(before.ResendCount + 1u, after.ResendCount);
            Assert.Equal(successorEmail, Assert.Single(resendMail.Sent).To);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── 4. Cleanup stays open ────────────────────────────────────────────────────

    /// <summary>
    /// TC-CLEANUP-01. Cancelling a stale handover from a started campus SUCCEEDS. It changes nothing
    /// about who runs the campus; it closes an invitation that can no longer be applied. Blocking it
    /// would leave the campus permanently occupied by a PENDING change — and one pending change per
    /// campus is a hard rule, so nothing else about the contact could ever be done again.
    /// </summary>
    [Theory]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.AfterVisit)]
    public async Task A_stale_transfer_can_still_be_cancelled_after_the_visit_starts(string status)
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var mail = new FakeEmail();
            var setup = await PendingTransferOnStartedCampusAsync(mail, status);
            requestId = setup.RequestId;

            using (var db = NewContext())
                await Cancel(db, Registrant, mail).Handle(
                    new CancelOperationalContactChangeCommand(
                        requestId, setup.InstanceId, "Chuyến thăm đã bắt đầu"),
                    CancellationToken.None);

            var change = (await ChangesAsync(requestId)).Single(c => c.IdentityChangeId == setup.ChangeId);
            Assert.Equal(IdentityChangeStatuses.Cancelled, change.Status);
            Assert.NotNull(change.CancelledAt);
            Assert.Equal(0, await LiveTokenCountAsync(setup.ChangeId));   // every link is dead

            // The current contact is exactly where it was.
            var after = await CampusStateAsync(requestId);
            Assert.Equal(setup.ContactId, after.ContactUserId);
            Assert.Equal(status, after.Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-CLEANUP-02. And the invited person may still say no. Declining settles the invitation from
    /// the other end and equally leaves the campus with the contact it already had — so the person who
    /// was asked is never trapped answering for a campus they never took on.
    /// </summary>
    [Fact]
    public async Task A_stale_transfer_can_still_be_declined_by_the_invited_person()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var mail = new FakeEmail();
            var setup = await PendingTransferOnStartedCampusAsync(mail, VisitInstanceStatuses.DuringVisit);
            requestId = setup.RequestId;

            ulong successorId;
            string successorEmail;
            using (var db = NewContext())
                (successorId, successorEmail) = await VisitorUserAsync(db, setup.ContactId);

            using (var db = NewContext())
                await Decline(db, successorId, successorEmail, mail).Handle(
                    new DeclineOperationalContactConfirmationCommand(setup.DeclineToken, "Không nhận được"),
                    CancellationToken.None);

            var change = (await ChangesAsync(requestId)).Single(c => c.IdentityChangeId == setup.ChangeId);
            Assert.Equal(IdentityChangeStatuses.Declined, change.Status);
            Assert.Equal(0, await LiveTokenCountAsync(setup.ChangeId));

            var after = await CampusStateAsync(requestId);
            Assert.Equal(setup.ContactId, after.ContactUserId);
            Assert.Equal(VisitInstanceStatuses.DuringVisit, after.Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── 5. Pre-approval handover: the defect this fix closes ──────────────────────
    //
    // A campus reaches WAITING_REQUEST_APPROVAL only through a confirmed contact (the database
    // enforces it), so it always has a real holder to hand over — whether or not a Staff Leader has
    // acted on it yet. Before the fix, this exact state routed through REPLACE instead: the holder was
    // cleared, the snapshot was overwritten, and the campus was forced back to WAITING_CONTACT_
    // CONFIRMATION before anybody had accepted anything. Every outcome below (initiate, accept, cancel,
    // decline, expire) must leave the ORIGINAL holder in place unless and until somebody actually
    // accepts the handover — and approval, if it happens in between, must neither block nor settle it.

    /// <summary>
    /// TC-PRE-01. The defect this whole change fixes: a campus with a confirmed contact but no decision
    /// yet is handover territory too, not replace territory.
    /// </summary>
    [Fact]
    public async Task A_handover_may_be_proposed_on_an_undecided_campus_once_it_has_a_confirmed_contact()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var mail = new FakeEmail();
            var setup = await PendingTransferOnUndecidedCampusAsync(mail);
            requestId = setup.RequestId;

            var state = await CampusStateAsync(requestId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, state.Status);
            Assert.Equal(setup.ContactId, state.ContactUserId);   // A still holds it — nothing moved yet

            var change = (await ChangesAsync(requestId)).Single(c => c.IdentityChangeId == setup.ChangeId);
            Assert.Equal(IdentityChangeKinds.Transfer, change.ChangeKind);
            Assert.Equal(IdentityChangeStatuses.Pending, change.Status);
            Assert.Equal(setup.ContactId, change.OldUserId);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-PRE-02 (spec Matrix B4). Accepting a handover proposed before any decision moves ONLY the
    /// contact. The campus must not be nudged toward ASSIGNED as a side effect — that would fabricate
    /// an approval nobody made.
    /// </summary>
    [Fact]
    public async Task A_pending_transfer_on_an_undecided_campus_can_be_accepted_and_the_campus_stays_waiting_for_approval()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var mail = new FakeEmail();
            var setup = await PendingTransferOnUndecidedCampusAsync(mail);
            requestId = setup.RequestId;

            ulong successorId;
            string successorEmail;
            using (var db = NewContext())
                (successorId, successorEmail) = await VisitorUserAsync(db, setup.ContactId);

            using (var db = NewContext())
                await Accept(db, successorId, successorEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(setup.AcceptToken), CancellationToken.None);

            var after = await CampusStateAsync(requestId);
            Assert.Equal(successorId, after.ContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, after.Status);   // no decision fabricated

            var change = (await ChangesAsync(requestId)).Single(c => c.IdentityChangeId == setup.ChangeId);
            Assert.Equal(IdentityChangeStatuses.Applied, change.Status);
            Assert.NotNull(change.AppliedAt);
            Assert.Equal(successorId, change.NewUserId);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>TC-PRE-03 (spec Matrix B1). Cancel settles the invitation; the original holder is untouched.</summary>
    [Fact]
    public async Task A_pending_transfer_on_an_undecided_campus_can_be_cancelled_and_leaves_the_holder_unchanged()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var mail = new FakeEmail();
            var setup = await PendingTransferOnUndecidedCampusAsync(mail);
            requestId = setup.RequestId;

            using (var db = NewContext())
                await Cancel(db, Registrant, mail).Handle(
                    new CancelOperationalContactChangeCommand(
                        requestId, setup.InstanceId, "Đổi ý trước khi duyệt"),
                    CancellationToken.None);

            var change = (await ChangesAsync(requestId)).Single(c => c.IdentityChangeId == setup.ChangeId);
            Assert.Equal(IdentityChangeStatuses.Cancelled, change.Status);
            Assert.Equal(0, await LiveTokenCountAsync(setup.ChangeId));

            var after = await CampusStateAsync(requestId);
            Assert.Equal(setup.ContactId, after.ContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, after.Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>TC-PRE-04 (spec Matrix B2). Decline settles the invitation; the original holder is untouched.</summary>
    [Fact]
    public async Task A_pending_transfer_on_an_undecided_campus_can_be_declined_and_leaves_the_holder_unchanged()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var mail = new FakeEmail();
            var setup = await PendingTransferOnUndecidedCampusAsync(mail);
            requestId = setup.RequestId;

            ulong successorId;
            string successorEmail;
            using (var db = NewContext())
                (successorId, successorEmail) = await VisitorUserAsync(db, setup.ContactId);

            using (var db = NewContext())
                await Decline(db, successorId, successorEmail, mail).Handle(
                    new DeclineOperationalContactConfirmationCommand(setup.DeclineToken, "Không nhận lời mời"),
                    CancellationToken.None);

            var change = (await ChangesAsync(requestId)).Single(c => c.IdentityChangeId == setup.ChangeId);
            Assert.Equal(IdentityChangeStatuses.Declined, change.Status);
            Assert.Equal(0, await LiveTokenCountAsync(setup.ChangeId));

            var after = await CampusStateAsync(requestId);
            Assert.Equal(setup.ContactId, after.ContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, after.Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-PRE-05 (spec Matrix B3). Letting a pre-approval handover lapse is cleanup, exactly like
    /// cancel and decline above — the real maintenance sweep settles the invitation and never touches
    /// who holds the campus.
    /// </summary>
    [Fact]
    public async Task A_pending_transfer_on_an_undecided_campus_expires_and_leaves_the_holder_unchanged()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var mail = new FakeEmail();
            var setup = await PendingTransferOnUndecidedCampusAsync(mail);
            requestId = setup.RequestId;

            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_identity_changes SET expires_at = {0} WHERE identity_change_id = {1}",
                    Now.AddMinutes(-1), setup.ChangeId);

            using (var db = NewContext())
                await Sweeper(db, new RecordingDispatcher()).RunOnceAsync(Now, 100, CancellationToken.None);

            var change = (await ChangesAsync(requestId)).Single(c => c.IdentityChangeId == setup.ChangeId);
            Assert.Equal(IdentityChangeStatuses.Expired, change.Status);
            Assert.Equal(0, await LiveTokenCountAsync(setup.ChangeId));

            var after = await CampusStateAsync(requestId);
            Assert.Equal(setup.ContactId, after.ContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, after.Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-PRE-06 (spec §11 / Matrix C1-C2, approval crossover). A Staff Leader may approve a campus
    /// while a handover is pending — the confirmed contact it has right now is real, so the decision is
    /// not blocked on somebody who has not even accepted a proposal yet, and approving must neither
    /// settle nor block the pending transfer. The decision must then survive the handover once it IS
    /// accepted: only the operational contact moves, never the host or the decision.
    /// </summary>
    [Fact]
    public async Task A_pending_transfer_survives_approval_and_the_decision_survives_the_transfer()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var mail = new FakeEmail();
            var setup = await PendingTransferOnUndecidedCampusAsync(mail);
            requestId = setup.RequestId;

            await DriveToAssignedAsync(setup.InstanceId);

            var decided = await CampusStateAsync(requestId);
            Assert.Equal(VisitInstanceStatuses.Assigned, decided.Status);
            Assert.Equal(setup.ContactId, decided.ContactUserId);   // approval did not touch the contact

            var stillPending = (await ChangesAsync(requestId)).Single(c => c.IdentityChangeId == setup.ChangeId);
            Assert.Equal(IdentityChangeStatuses.Pending, stillPending.Status);   // approval did not settle it

            async Task<(ulong? Host, ulong? DecidedBy, DateTime? DecidedAt, string? Note)> DecisionSnapshotAsync()
            {
                using var snap = NewContext();
                var row = await snap.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == setup.InstanceId)
                    .Select(c => new { c.CurrentHostUserId, c.DecidedBy, c.DecidedAt, c.DecisionNote })
                    .SingleAsync();
                return (row.CurrentHostUserId, row.DecidedBy, row.DecidedAt, row.DecisionNote);
            }
            var before = await DecisionSnapshotAsync();

            ulong successorId;
            string successorEmail;
            using (var db = NewContext())
                (successorId, successorEmail) = await VisitorUserAsync(db, setup.ContactId);

            using (var db = NewContext())
                await Accept(db, successorId, successorEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(setup.AcceptToken), CancellationToken.None);

            var after = await CampusStateAsync(requestId);
            Assert.Equal(successorId, after.ContactUserId);              // the contact moved
            Assert.Equal(VisitInstanceStatuses.Assigned, after.Status);  // the decision did not

            Assert.Equal(before, await DecisionSnapshotAsync());
        }
        finally { await CleanupAsync(requestId); }
    }
}
