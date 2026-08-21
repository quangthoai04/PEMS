using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Emails.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Shared;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The per-campus operational-contact confirmation workflow (plan §3.2/§3.3/§5.2), against a real
/// MySQL database.
///
/// <para>
/// It replaces <c>VisitContactClaimWorkflowTests</c> and <c>VisitContactTransferWorkflowTests</c>,
/// which tested a request-level claim/transfer that no longer exists. The difference those two could
/// not express is the whole point of this suite: an invitation belongs to ONE campus, answering it
/// decides ONE campus, and the global gate stays shut until every campus has been answered — so a
/// person who confirms campus A gets nothing at all on campus B.
/// </para>
///
/// Each test creates its own committed request and cascade-deletes it in <c>finally</c>, so the
/// database is left exactly as it was found. Actors: registrant = user 8; the invited contact and a
/// bystander are ACTIVE VISITOR accounts looked up at run time.
/// </summary>
public sealed class OperationalContactConfirmationWorkflowTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;

    // ── Infrastructure ────────────────────────────────────────────────────────────

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
        Assert.True(_dbUp!.Value,
            "pems_pr3_test is not reachable — import the canonical SQL to run these tests.");
    }

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

    /// <summary>Records outbound mail and exposes the confirmation token from the last body sent.</summary>
    private sealed class FakeEmail : IEmailService
    {
        public List<(string To, string Subject, string Html)> Sent { get; } = new();

        public string? LastConfirmationToken
        {
            get
            {
                var html = Sent.LastOrDefault().Html;
                if (html is null) return null;
                var m = Regex.Match(html, @"operational-contact-confirmation/([A-Za-z0-9_\-]+)");
                return m.Success ? m.Groups[1].Value : null;
            }
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

    private sealed class NoopNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType,
            string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    /// <summary>Mirrors OperationalContactGuards.MaxResends, which is internal to the Application layer.</summary>
    private const int MaxResends = 5;

    private static EmailActionTokenService Tokens() => new(EmptyConfig);

    private static OperationalContactInvitationService Invitations(
        ApplicationDbContext db, FakeEmail? email = null)
        => new(db, Tokens(),
            new SystemEmailDispatcher(db, new EmailTemplateRenderer(db), email ?? new FakeEmail()),
            new FixedClock(), NullLogger<OperationalContactInvitationService>.Instance, EmptyConfig);

    // ── Handlers under test ───────────────────────────────────────────────────────

    private static AcceptOperationalContactConfirmationCommandHandler Accept(
        ApplicationDbContext db, ulong actor, string? actorEmail = null)
        => new(db, new FakeUser(actor, actorEmail), new FixedClock(), Tokens(), Invitations(db),
            new VisitRequestAggregateStatusService(db), new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)),
            new NoopNotifications(),
            NullLogger<AcceptOperationalContactConfirmationCommandHandler>.Instance, WriteOn);

    private static DeclineOperationalContactConfirmationCommandHandler DeclineHandler(
        ApplicationDbContext db, ulong actor, string? actorEmail = null)
        => new(db, new FakeUser(actor, actorEmail), new FixedClock(), Tokens(), Invitations(db), WriteOn);

    private static ResendOperationalContactConfirmationCommandHandler ResendHandler(
        ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new FakeUser(actor), new FixedClock(), Invitations(db, email), WriteOn);

    private static ReplaceOperationalContactCommandHandler ReplaceHandler(
        ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new FakeUser(actor), new FixedClock(), Invitations(db, email),
            new VisitRequestAggregateStatusService(db), new NoopNotifications(),
            NullLogger<ReplaceOperationalContactCommandHandler>.Instance, WriteOn);

    // ── Data helpers ──────────────────────────────────────────────────────────────

    private static async Task<(ulong UserId, string Email)> VisitorUserAsync(
        ApplicationDbContext db, params ulong[] exclude)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                        && u.UserId != Registrant && !exclude.Contains(u.UserId))
            .OrderBy(u => u.UserId)
            .Select(u => new { u.UserId, u.Email })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "This database needs at least two ACTIVE VISITOR users besides user 8.");
        return (row.UserId, row.Email!);
    }

    private static CampusVisitFormDto Campus(string campusCode, string contactEmail, int dayOffset)
    {
        var start = Now.AddDays(20 + dayOffset);
        return new CampusVisitFormDto(
            campusCode, start, start.AddMinutes(120),
            "Đoàn xác nhận", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Đầu mối " + campusCode, "OrgB", "Trưởng phòng Hợp tác", "+8492", contactEmail),
            "EN", null, "DECLINED", null, null);
    }

    /// <summary>
    /// The same campus, with a reception-host arrangement attached. SELF resolves to the caller on
    /// the backend, so the id is only ever passed for SELECTED.
    /// </summary>
    private static CampusVisitFormDto CampusWithProposal(
        string campusCode, string contactEmail, int dayOffset, string mode, ulong? proposedHostUserId = null)
    {
        var basic = Campus(campusCode, contactEmail, dayOffset);
        return basic with { HostSelection = new CampusHostSelectionV2Dto(mode, proposedHostUserId) };
    }

    /// <summary>
    /// The same form, registered by somebody other than the default seed registrant. This endpoint is
    /// SELF-registration only, so the form's registrant email has to be the caller's own.
    /// </summary>
    private static VisitRequestFormDataV2 FormFor(string registrantEmail, params CampusVisitFormDto[] campuses)
        => new(
            "OC" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", registrantEmail),
            null,
            campuses.ToList());

    /// <summary>Creates a request AS an internal actor, so a host proposal is authorized at all.</summary>
    private static async Task<ulong> CreateAsInternalAsync(VisitRequestFormDataV2 form, ulong actorId)
    {
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(actorId), new FixedClock(), new VisitRequestV2CreateService(db),
            new NoopNotifications(),
            new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = true }, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));

        var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);
        return created.VisitRequestId;
    }

    /// <summary>
    /// An ACTIVE IC Staff / Staff Leader of the given campus, or null when the seed has none. The
    /// gate-activation tests skip rather than fail in that case: they are about the activation rule,
    /// not about which people a particular database happens to contain.
    /// </summary>
    private static async Task<(ulong UserId, string Email, string Campus)?> InternalHostAsync(
        ApplicationDbContext db, string subRole)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Staff
                        && u.SubRole == subRole
                        && u.Status == UserStatuses.Active
                        && u.PrimaryCampusId != null
                        && u.Department != null
                        && u.Department.DepartmentType == "IC"
                        && u.Department.Status == "ACTIVE"
                        && u.Department.CampusId == u.PrimaryCampusId)
            .OrderBy(u => u.UserId)
            .Select(u => new { u.UserId, u.Email, u.PrimaryCampusId })
            .FirstOrDefaultAsync();
        if (row is null) return null;

        var code = await db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == row.PrimaryCampusId!.Value)
            .Select(c => c.CampusCode)
            .FirstOrDefaultAsync();
        return code is null ? null : (row.UserId, row.Email!, code);
    }

    /// <summary>Reads one campus back with everything the activation writes.</summary>
    private static async Task<PEMS.Domain.Entities.Delegations.VisitRequestCampus> InstanceAsync(ulong instanceId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking().FirstAsync(c => c.VisitInstanceId == instanceId);
    }

    private static VisitRequestFormDataV2 Form(params CampusVisitFormDto[] campuses)
        => new(
            "OC" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null,
            campuses.ToList());

    /// <summary>Creates + commits a request and returns its id.</summary>
    private static async Task<ulong> CreateAsync(VisitRequestFormDataV2 form)
    {
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
            new NoopNotifications(),
            new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = true }, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));

        var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);
        return created.VisitRequestId;
    }

    private static async Task<(ulong InstanceId, ulong ChangeId)> PendingInvitationAsync(
        ulong requestId, int index = 0)
    {
        using var db = NewContext();
        var row = await db.VisitRequestIdentityChanges.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId && c.Status == IdentityChangeStatuses.Pending)
            .OrderBy(c => c.VisitInstanceId)
            .Select(c => new { c.VisitInstanceId, c.IdentityChangeId })
            .Skip(index).FirstAsync();
        return (row.VisitInstanceId, row.IdentityChangeId);
    }

    /// <summary>
    /// Sends the invitation and returns its ACCEPT link.
    ///
    /// <para>
    /// An invitation now carries TWO links, one per answer, so that the confirmation page learns which
    /// button was pressed from the token rather than from a query parameter a mail scanner could flip.
    /// This helper keeps returning the accept link because that is what most callers here are testing;
    /// a decline needs <see cref="MintDeclineTokenAsync"/>, and posting one where the other belongs is
    /// refused on purpose.
    /// </para>
    /// </summary>
    private static async Task<string> MintTokenAsync(ulong changeId, FakeEmail? email = null)
    {
        using var db = NewContext();
        var invitations = Invitations(db, email);
        // The two halves in the order every production caller uses them: mint, make the links durable,
        // then send. There is no mint-and-send convenience any more, on purpose — see the service docs.
        var tokens = await invitations.MintInvitationTokensAsync(changeId, CancellationToken.None);
        Assert.NotNull(tokens);
        await db.SaveChangesAsync(CancellationToken.None);
        await invitations.DispatchInvitationEmailAsync(changeId, tokens!, CancellationToken.None);
        return tokens!.AcceptToken;
    }

    /// <summary>The DECLINE link of the invitation's newest token group.</summary>
    private static async Task<string> MintDeclineTokenAsync(ulong changeId, FakeEmail? email = null)
    {
        using var db = NewContext();
        var tokens = await Invitations(db, email)
            .MintInvitationTokensAsync(changeId, CancellationToken.None);
        Assert.NotNull(tokens);
        await db.SaveChangesAsync(CancellationToken.None);
        return tokens!.DeclineToken;
    }

    /// <summary>
    /// How many links ONE send produces — an accept and a decline. Named so the assertions below read
    /// as "one invitation" rather than as a bare 2.
    /// </summary>
    private const int LinksPerInvitation = 2;

    /// <summary>
    /// Backdates this invitation’s newest link past the resend cooldown. The guard reads the last
    /// email_action_tokens row rather than a column on the invitation, so that is what has to move.
    /// </summary>
    private static async Task AgeLastTokenAsync(ulong changeId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE email_action_tokens SET created_at = {0} " +
            "WHERE target_type = 'VISIT_REQUEST_IDENTITY_CHANGE' AND target_id = {1}",
            Now.AddMinutes(-30), changeId);
    }

    /// <summary>Links of this invitation that a recipient could still click.</summary>
    private static async Task<int> LiveTokenCountAsync(ulong changeId)
    {
        using var db = NewContext();
        return await db.EmailActionTokens.AsNoTracking()
            .CountAsync(t => t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange
                             && t.TargetId == changeId
                             && t.UsedAt == null
                             && t.ResultStatus == EmailActionResultStatuses.Pending);
    }

    private static async Task<int> TotalTokenCountAsync(ulong changeId)
    {
        using var db = NewContext();
        return await db.EmailActionTokens.AsNoTracking()
            .CountAsync(t => t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange
                             && t.TargetId == changeId);
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM email_action_tokens WHERE target_type='VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        // Activating a proposal creates the IC_HOST participant row, so the cleanup has to know
        // about it: without this the campus delete fails on the FK and takes the whole test with it.
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── Self-match ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The registrant names their own verified address as the contact. Nothing to confirm: the campus
    /// is linked inside the create transaction, no invitation exists, and the gate never closes.
    /// </summary>
    [Fact]
    public async Task Registrant_self_match_links_the_campus_with_no_invitation()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Form(Campus("HN", V2SeedActor.Email(Registrant), 0)));

            using var db = NewContext();
            var instance = await db.VisitRequestCampuses.AsNoTracking()
                .SingleAsync(c => c.VisitRequestId == requestId);

            Assert.Equal(Registrant, instance.OperationalContactUserId);
            Assert.Equal(OperationalContactSources.RegistrantSelfMatch, instance.OperationalContactConfirmationSource);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, instance.Status);

            var request = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
            Assert.Equal(VisitRequestStatuses.PendingApproval, request.Status);

            Assert.Empty(await db.VisitRequestIdentityChanges.AsNoTracking()
                .Where(c => c.VisitRequestId == requestId).ToListAsync());
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Accept ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_links_the_invited_account_to_that_campus_and_opens_the_gate()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using (var seed = NewContext())
            {
                var (contactId, contactEmail) = await VisitorUserAsync(seed);
                requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));

                var (instanceId, changeId) = await PendingInvitationAsync(requestId);
                var token = await MintTokenAsync(changeId);

                using var db = NewContext();
                var result = await Accept(db, contactId, contactEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);

                Assert.Equal(instanceId, result.VisitInstanceId);
                Assert.Equal(IdentityChangeStatuses.Applied, result.ChangeStatus);
                Assert.False(result.Idempotent);

                using var check = NewContext();
                var instance = await check.VisitRequestCampuses.AsNoTracking()
                    .SingleAsync(c => c.VisitInstanceId == instanceId);
                Assert.Equal(contactId, instance.OperationalContactUserId);
                Assert.Equal(OperationalContactSources.EmailConfirmation, instance.OperationalContactConfirmationSource);
                Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, instance.Status);

                // The only campus is confirmed, so the gate opens and the Staff Leader can finally see it.
                var request = await check.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Equal(VisitRequestStatuses.PendingApproval, request.Status);
                Assert.True(request.ContactGateRevision >= 1);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// The heart of the cutover. Two campuses, two different invited people: answering one must move
    /// that campus alone, and the request stays behind the gate until the other answers too.
    /// </summary>
    [Fact]
    public async Task Accepting_one_campus_moves_only_that_campus_and_the_gate_stays_shut()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var (contactA, emailA) = await VisitorUserAsync(seed);
            var (_, emailB) = await VisitorUserAsync(seed, contactA);

            requestId = await CreateAsync(Form(
                Campus("HN", emailA, 0),
                Campus("HCM", emailB, 1)));

            var (firstInstance, firstChange) = await PendingInvitationAsync(requestId);
            var token = await MintTokenAsync(firstChange);

            using (var db = NewContext())
            {
                await Accept(db, contactA, emailA).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);
            }

            using var check = NewContext();
            var instances = await check.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitRequestId == requestId)
                .OrderBy(c => c.VisitInstanceId)
                .ToListAsync();

            var answered = instances.Single(c => c.VisitInstanceId == firstInstance);
            var sibling = instances.Single(c => c.VisitInstanceId != firstInstance);

            Assert.Equal(contactA, answered.OperationalContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, answered.Status);

            // The sibling is untouched: no owner, still waiting, and its own invitation is still open.
            Assert.Null(sibling.OperationalContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, sibling.Status);

            // And the WHOLE request is still behind the gate, so no Staff Leader sees any of it —
            // including the campus that is otherwise ready.
            var request = await check.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
            Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, request.Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Accepting_twice_from_the_same_account_is_an_idempotent_success()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var (contactId, contactEmail) = await VisitorUserAsync(seed);
            requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));

            var (_, changeId) = await PendingInvitationAsync(requestId);
            var token = await MintTokenAsync(changeId);

            using (var first = NewContext())
            {
                await Accept(first, contactId, contactEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);
            }

            using var second = NewContext();
            var replay = await Accept(second, contactId, contactEmail).Handle(
                new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);

            // A double-submitted form must not look like a failure to the person who succeeded.
            Assert.True(replay.Idempotent);
            Assert.Equal(IdentityChangeStatuses.Applied, replay.ChangeStatus);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_bystander_cannot_take_a_campus_with_someone_elses_link()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var (contactId, contactEmail) = await VisitorUserAsync(seed);
            var (bystanderId, bystanderEmail) = await VisitorUserAsync(seed, contactId);

            requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));
            var (instanceId, changeId) = await PendingInvitationAsync(requestId);
            var token = await MintTokenAsync(changeId);

            using var db = NewContext();
            await Assert.ThrowsAnyAsync<Exception>(() =>
                Accept(db, bystanderId, bystanderEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None));

            using var check = NewContext();
            var instance = await check.VisitRequestCampuses.AsNoTracking()
                .SingleAsync(c => c.VisitInstanceId == instanceId);
            Assert.Null(instance.OperationalContactUserId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Decline ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Decline_closes_the_invitation_and_leaves_the_campus_unowned()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var (contactId, contactEmail) = await VisitorUserAsync(seed);
            requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));

            var (instanceId, changeId) = await PendingInvitationAsync(requestId);
            // The DECLINE link — an accept link cannot decline, which is the point of minting two.
            var token = await MintDeclineTokenAsync(changeId);

            using (var db = NewContext())
            {
                var result = await DeclineHandler(db, contactId, contactEmail).Handle(
                    new DeclineOperationalContactConfirmationCommand(token, "Không phụ trách cơ sở này."),
                    CancellationToken.None);
                Assert.Equal(IdentityChangeStatuses.Declined, result.ChangeStatus);
            }

            using var check = NewContext();
            var instance = await check.VisitRequestCampuses.AsNoTracking()
                .SingleAsync(c => c.VisitInstanceId == instanceId);
            Assert.Null(instance.OperationalContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, instance.Status);

            // Declining does not open the gate: the campus still has nobody to run it.
            var request = await check.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
            Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, request.Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Resend ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resend_mints_a_new_link_for_the_same_campus_and_counts_it()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var (_, contactEmail) = await VisitorUserAsync(seed);
            requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));

            var (instanceId, changeId) = await PendingInvitationAsync(requestId);
            var email = new FakeEmail();
            await MintTokenAsync(changeId, email);
            var tokensBefore = await LiveTokenCountAsync(changeId);
            // One SEND, two live links: accept and decline.
            Assert.Equal(LinksPerInvitation, tokensBefore);

            // The cooldown is measured from the newest token of this invitation, so age it: this test
            // is about what a resend DOES, not about the one-minute window.
            await AgeLastTokenAsync(changeId);

            using (var db = NewContext())
            {
                var result = await ResendHandler(db, Registrant, email).Handle(
                    new ResendOperationalContactConfirmationCommand(requestId, instanceId),
                    CancellationToken.None);
                Assert.Equal(1u, result.ResendCount);
            }

            // A resend MINTS a fresh PAIR of links and kills the previous pair, so exactly one
            // invitation's worth stays usable. Asserted on the token rows rather than on the mail
            // body: whether the body renders a URL depends on the email template seed, and the
            // links' validity does not.
            Assert.Equal(LinksPerInvitation, await LiveTokenCountAsync(changeId));
            Assert.Equal(LinksPerInvitation * 2, await TotalTokenCountAsync(changeId));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// The resend budget is per invitation. Past the cap the handler refuses with the rate-limit code
    /// rather than quietly minting link number six.
    /// </summary>
    [Fact]
    public async Task Resend_stops_at_the_cap()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var (_, contactEmail) = await VisitorUserAsync(seed);
            requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));

            var (instanceId, changeId) = await PendingInvitationAsync(requestId);
            var email = new FakeEmail();
            await MintTokenAsync(changeId, email);

            // Jump straight to the cap, and age the token so the cooldown cannot be what refuses:
            // the assertion below is about the CAP.
            using (var bump = NewContext())
            {
                await bump.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_identity_changes SET resend_count = {0} WHERE identity_change_id = {1}",
                    MaxResends, changeId);
            }
            await AgeLastTokenAsync(changeId);

            using var db = NewContext();
            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                ResendHandler(db, Registrant, email).Handle(
                    new ResendOperationalContactConfirmationCommand(requestId, instanceId),
                    CancellationToken.None));
            Assert.Contains(OperationalContactErrorCodes.RateLimited, ErrorCodeOf(ex));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Replace ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// A direct call to REPLACE against a campus that already has a CONFIRMED contact is refused
    /// outright, whether or not the campus has been decided — this is the defense-in-depth guard that
    /// stops a caller from bypassing the router's holder-based classification and destructively
    /// clearing a confirmed holder the way the old rule did. The gate, already open from the accept,
    /// must not re-close over a refused call that changed nothing.
    /// </summary>
    [Fact]
    public async Task Replacing_a_confirmed_contact_is_refused_and_the_gate_stays_open()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var (contactId, contactEmail) = await VisitorUserAsync(seed);
            var (_, newEmail) = await VisitorUserAsync(seed, contactId);

            requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));
            var (instanceId, changeId) = await PendingInvitationAsync(requestId);
            var token = await MintTokenAsync(changeId);

            using (var db = NewContext())
            {
                await Accept(db, contactId, contactEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);
            }

            using var before = NewContext();
            var instanceBefore = await before.VisitRequestCampuses.AsNoTracking()
                .SingleAsync(c => c.VisitInstanceId == instanceId);
            var requestBefore = await before.VisitRequests.AsNoTracking()
                .SingleAsync(v => v.VisitRequestId == requestId);
            var changesBefore = await before.VisitRequestIdentityChanges.AsNoTracking()
                .CountAsync(c => c.VisitInstanceId == instanceId);

            var refusal = await Assert.ThrowsAsync<ConflictException>(async () =>
            {
                using var db = NewContext();
                await ReplaceHandler(db, Registrant, new FakeEmail()).Handle(
                    new ReplaceOperationalContactCommand(
                        requestId, instanceId, "Đầu mối mới", "OrgC", "Điều phối viên", "+8493", newEmail),
                    CancellationToken.None);
            });
            Assert.Equal(OperationalContactErrorCodes.ChangeConflict, refusal.ErrorCode);

            using var check = NewContext();
            var instance = await check.VisitRequestCampuses.AsNoTracking()
                .SingleAsync(c => c.VisitInstanceId == instanceId);
            Assert.Equal(contactId, instance.OperationalContactUserId);
            Assert.Equal(instanceBefore.Status, instance.Status);
            // No new invitation was raised on the way to the refusal — only the accepted original row.
            Assert.Equal(changesBefore, await check.VisitRequestIdentityChanges.AsNoTracking()
                .CountAsync(c => c.VisitInstanceId == instanceId));

            var request = await check.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
            Assert.Equal(requestBefore.Status, request.Status);
            Assert.Equal(requestBefore.ContactGateRevision, request.ContactGateRevision);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Replacing_one_campus_never_touches_its_sibling()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var (contactA, emailA) = await VisitorUserAsync(seed);
            var (_, emailB) = await VisitorUserAsync(seed, contactA);

            requestId = await CreateAsync(Form(
                Campus("HN", emailA, 0),
                Campus("HCM", emailB, 1)));

            var (firstInstance, _) = await PendingInvitationAsync(requestId);

            string siblingEmailBefore;
            ulong siblingId;
            using (var before = NewContext())
            {
                var sibling = await before.VisitRequestCampuses.AsNoTracking()
                    .Include(c => c.FormDetail)
                    .SingleAsync(c => c.VisitRequestId == requestId && c.VisitInstanceId != firstInstance);
                siblingId = sibling.VisitInstanceId;
                siblingEmailBefore = sibling.FormDetail!.OperationalContactEmail;
            }

            using (var db = NewContext())
            {
                await ReplaceHandler(db, Registrant, new FakeEmail()).Handle(
                    new ReplaceOperationalContactCommand(
                        requestId, firstInstance, "Đầu mối mới", "OrgC", "Điều phối viên", "+8493", "brand-new@example.com"),
                    CancellationToken.None);
            }

            using var check = NewContext();
            var siblingAfter = await check.VisitRequestCampuses.AsNoTracking()
                .Include(c => c.FormDetail)
                .SingleAsync(c => c.VisitInstanceId == siblingId);
            Assert.Equal(siblingEmailBefore, siblingAfter.FormDetail!.OperationalContactEmail);

            // And the sibling's own invitation is still the one it started with.
            var siblingPending = await check.VisitRequestIdentityChanges.AsNoTracking()
                .CountAsync(c => c.VisitInstanceId == siblingId && c.Status == IdentityChangeStatuses.Pending);
            Assert.Equal(1, siblingPending);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Replacing with the registrant's own verified address self-matches, exactly like create.</summary>
    [Fact]
    public async Task Replacing_with_the_registrants_own_address_links_immediately()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var (_, contactEmail) = await VisitorUserAsync(seed);
            requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));
            var (instanceId, _) = await PendingInvitationAsync(requestId);

            using (var db = NewContext())
            {
                await ReplaceHandler(db, Registrant, new FakeEmail()).Handle(
                    new ReplaceOperationalContactCommand(
                        requestId, instanceId, "Registrant", "Org", "Trưởng phòng Hợp tác", "+8491", V2SeedActor.Email(Registrant)),
                    CancellationToken.None);
            }

            using var check = NewContext();
            var instance = await check.VisitRequestCampuses.AsNoTracking()
                .SingleAsync(c => c.VisitInstanceId == instanceId);
            Assert.Equal(Registrant, instance.OperationalContactUserId);
            Assert.Equal(OperationalContactSources.RegistrantSelfMatch, instance.OperationalContactConfirmationSource);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, instance.Status);

            // The superseded invitation is closed, not left pending.
            Assert.Equal(0, await check.VisitRequestIdentityChanges.AsNoTracking()
                .CountAsync(c => c.VisitInstanceId == instanceId && c.Status == IdentityChangeStatuses.Pending));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Expiry ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_expired_link_is_refused_and_changes_nothing()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var (contactId, contactEmail) = await VisitorUserAsync(seed);
            requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));

            var (instanceId, changeId) = await PendingInvitationAsync(requestId);
            var token = await MintTokenAsync(changeId);

            using (var age = NewContext())
            {
                await age.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_identity_changes SET expires_at = {0} WHERE identity_change_id = {1}",
                    Now.AddDays(-1), changeId);
            }

            using var db = NewContext();
            await Assert.ThrowsAnyAsync<Exception>(() =>
                Accept(db, contactId, contactEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None));

            using var check = NewContext();
            var instance = await check.VisitRequestCampuses.AsNoTracking()
                .SingleAsync(c => c.VisitInstanceId == instanceId);
            Assert.Null(instance.OperationalContactUserId);
        }
        finally { await CleanupAsync(requestId); }
    }

    private static string ErrorCodeOf(Exception ex) => ex switch
    {
        ConflictException c => c.ErrorCode ?? string.Empty,
        BusinessRuleException b => b.ErrorCode ?? string.Empty,
        _ => ex.Message,
    };

    // ── Gate activation: what the LAST confirmation does to a preauthorized host (plan §6) ────────
    //
    // The rule these protect is narrow and easy to lose: confirming a contact must never CHOOSE a
    // host. It may only switch on one that somebody with the authority to pick already named — and
    // only if that person still qualifies. Everything below is a way of getting that wrong.

    [Fact]
    public async Task A_leaders_self_proposal_is_activated_when_the_last_contact_confirms()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var leader = await InternalHostAsync(seed, UserSubRoles.Leader);
            if (leader is null) return; // seed has no eligible Staff Leader; nothing to assert about
            var (contactId, contactEmail) = await VisitorUserAsync(seed);

            requestId = await CreateAsInternalAsync(
                FormFor(leader.Value.Email, CampusWithProposal(leader.Value.Campus, contactEmail, 0, HostSelectionModes.Self)),
                leader.Value.UserId);

            var (instanceId, changeId) = await PendingInvitationAsync(requestId);

            // Before the confirmation: a proposal, and nothing else.
            var before = await InstanceAsync(instanceId);
            Assert.Equal(leader.Value.UserId, before.ProposedHostUserId);
            Assert.Null(before.CurrentHostUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, before.Status);

            var token = await MintTokenAsync(changeId);
            using (var db = NewContext())
            {
                await Accept(db, contactId, contactEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);
            }

            var after = await InstanceAsync(instanceId);
            Assert.Equal(VisitInstanceStatuses.Assigned, after.Status);
            Assert.Equal(leader.Value.UserId, after.CurrentHostUserId);
            // decided_by is the PROPOSER, not the contact who clicked accept: the contact chose
            // nothing here, and an audit trail saying otherwise attributes an FPTU staffing decision
            // to somebody outside FPTU.
            Assert.Equal(leader.Value.UserId, after.DecidedBy);
            Assert.Equal(DecisionSources.PreauthorizedHostActivation, after.DecisionSource);
            Assert.Equal(ProposedHostActivationStatuses.Activated, after.ProposedHostActivationStatus);
            Assert.NotNull(after.ProposedHostActivatedAt);

            // ASSIGNED, not BEFORE_VISIT: the Host still has to start the preparation themself.
            Assert.NotEqual(VisitInstanceStatuses.BeforeVisit, after.Status);

            using var check = NewContext();
            Assert.True(await check.VisitParticipants.AnyAsync(
                x => x.VisitInstanceId == instanceId && x.UserId == leader.Value.UserId && x.IsHost));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// VISIT_HISTORY_INTEGRITY final phase, Phase B (B2). Same activation as the test above, but
    /// proving the WRITER side: a fully-scoped, structured decision audit exists — same shape
    /// CampusApprovalExecutor writes for a live approval (status + host AuditLogChange rows) — under
    /// the shared CampusDecisionAudit.HostProposalActivated action, filed under the PROPOSER (the
    /// Leader), never the contact who merely clicked accept.
    /// </summary>
    [Fact]
    public async Task B2_Activation_writes_a_fully_scoped_structured_decision_audit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var leader = await InternalHostAsync(seed, UserSubRoles.Leader);
            if (leader is null) return;
            var (contactId, contactEmail) = await VisitorUserAsync(seed);

            requestId = await CreateAsInternalAsync(
                FormFor(leader.Value.Email, CampusWithProposal(leader.Value.Campus, contactEmail, 0, HostSelectionModes.Self)),
                leader.Value.UserId);

            var (instanceId, changeId) = await PendingInvitationAsync(requestId);

            var token = await MintTokenAsync(changeId);
            using (var db = NewContext())
            {
                await Accept(db, contactId, contactEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);
            }

            var after = await InstanceAsync(instanceId);

            using var check = NewContext();
            var audit = Assert.Single(await check.AuditLogs.AsNoTracking().Include(a => a.Changes)
                .Where(a => a.VisitRequestId == requestId && a.Action == CampusDecisionAudit.HostProposalActivated)
                .ToListAsync());
            Assert.Equal(requestId, audit.VisitRequestId);
            Assert.Equal(instanceId, audit.VisitInstanceId);
            Assert.Equal(after.CampusId, audit.CampusId);
            Assert.Equal(leader.Value.UserId, audit.ActorUserId); // the proposer, not the contact
            Assert.Equal(CampusDecisionAudit.SourceType, audit.SourceType);

            var statusChange = Assert.Single(audit.Changes.Where(c => c.FieldName == "visit_request_campuses.status"));
            // Within ONE Accept call, the campus first flips WAITING_CONTACT_CONFIRMATION →
            // WAITING_REQUEST_APPROVAL (its own confirmation logic) and — since this was the request's
            // only outstanding campus, so the gate opens in the same call — is picked up by
            // ActivateAsync from THAT intermediate value, not the value the row held before Accept ran.
            // Both are genuinely valid pending-bucket starting points per ActivateAsync's own filter.
            Assert.Contains(statusChange.OldValueText, new[]
            {
                VisitInstanceStatuses.WaitingContactConfirmation, VisitInstanceStatuses.WaitingRequestApproval,
            });
            Assert.Equal(VisitInstanceStatuses.Assigned, statusChange.NewValueText);
            var hostChange = Assert.Single(audit.Changes.Where(c => c.FieldName == "current_host_user_id"));
            Assert.Null(hostChange.OldValueText);
            Assert.Equal(leader.Value.UserId.ToString(), hostChange.NewValueText);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// VISIT_HISTORY_INTEGRITY final phase, Phase B (B2). Proving the READER side: the timeline
    /// renders this as its own HostProposalActivated event — never InstanceApproved, which would
    /// misattribute a live Staff Leader review to what was actually a preauthorized proposal
    /// auto-applying when the LAST contact confirmed. The detail drawer shows the same status/host
    /// facts the audit above proved were written.
    /// </summary>
    [Fact]
    public async Task B2_Timeline_and_detail_show_host_proposal_activated_not_instance_approved()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var leader = await InternalHostAsync(seed, UserSubRoles.Leader);
            if (leader is null) return;
            var (contactId, contactEmail) = await VisitorUserAsync(seed);

            requestId = await CreateAsInternalAsync(
                FormFor(leader.Value.Email, CampusWithProposal(leader.Value.Campus, contactEmail, 0, HostSelectionModes.Self)),
                leader.Value.UserId);

            var (instanceId, changeId) = await PendingInvitationAsync(requestId);
            var token = await MintTokenAsync(changeId);
            using (var db = NewContext())
            {
                await Accept(db, contactId, contactEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);
            }

            // The Leader is also this request's registrant (CreateAsInternalAsync self-registers) —
            // registrant scope sees the whole request, identity timeline included.
            var viewer = new FakeUser(leader.Value.UserId, leader.Value.Email);
            using var db2 = NewContext();
            var historyOptions = new PerCampusFormV2Options { Enabled = true };
            var result = await new GetVisitRequestHistoryQueryHandler(db2, viewer, historyOptions).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);

            var entry = Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.HostProposalActivated);
            Assert.Equal(instanceId, entry.VisitInstanceId);
            Assert.Equal(VisitInstanceStatuses.Assigned, entry.StatusCode);
            Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.InstanceApproved
                && e.VisitInstanceId == instanceId);

            var drawer = await new GetVisitHistoryDetailQueryHandler(db2, viewer, historyOptions).Handle(
                new GetVisitHistoryDetailQuery(requestId, entry.EventId!), CancellationToken.None);
            Assert.Equal(VisitHistoryEventCodes.HostProposalActivated, drawer.EventCode);
            var statusField = Assert.Single(drawer.FieldChanges, f => f.FieldCode == "status");
            Assert.Equal(VisitInstanceStatuses.Assigned, statusField.AfterValue);
            var hostField = Assert.Single(drawer.FieldChanges, f => f.FieldCode == "host");
            Assert.Equal(leader.Value.UserId.ToString(), hostField.AfterValue);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Wait_for_later_lands_on_waiting_request_approval_instead_of_a_host()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var leader = await InternalHostAsync(seed, UserSubRoles.Leader);
            if (leader is null) return;
            var (contactId, contactEmail) = await VisitorUserAsync(seed);

            requestId = await CreateAsInternalAsync(
                FormFor(leader.Value.Email, CampusWithProposal(leader.Value.Campus, contactEmail, 0, HostSelectionModes.WaitForLater)),
                leader.Value.UserId);

            var (instanceId, changeId) = await PendingInvitationAsync(requestId);
            var token = await MintTokenAsync(changeId);
            using (var db = NewContext())
            {
                await Accept(db, contactId, contactEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);
            }

            // No proposal means no auto-assignment. The campus waits for its Staff Leader, which is
            // the ONLY other way a host is ever named.
            var after = await InstanceAsync(instanceId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, after.Status);
            Assert.Null(after.CurrentHostUserId);
            Assert.Null(after.ProposedHostUserId);
            Assert.Null(after.DecidedBy);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_proposal_that_no_longer_qualifies_falls_back_without_failing_the_confirmation()
    {
        RequireDb();
        ulong requestId = 0;
        ulong? deactivated = null;
        try
        {
            using var seed = NewContext();
            var leader = await InternalHostAsync(seed, UserSubRoles.Leader);
            if (leader is null) return;
            var (contactId, contactEmail) = await VisitorUserAsync(seed);

            // A Staff Leader proposes an IC Staff of their own campus…
            var leaderCampus = await seed.Users.AsNoTracking()
                .Where(u => u.UserId == leader.Value.UserId).Select(u => u.PrimaryCampusId).FirstAsync();
            // Not somebody who already holds a live campus as its operational contact: deactivating
            // one of those is refused outright by trg_users_protect_operational_contact_bu, and this
            // test is about a stale PROPOSAL rather than about that guard.
            var staff = await seed.Users.AsNoTracking()
                .Where(u => u.Role.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Staff
                            && u.Status == UserStatuses.Active
                            && u.Department != null && u.Department.DepartmentType == "IC"
                            && u.PrimaryCampusId == leaderCampus
                            && !seed.VisitRequestCampuses.Any(c =>
                                   c.OperationalContactUserId == u.UserId
                                   && c.Status != VisitInstanceStatuses.Cancelled
                                   && c.Status != VisitInstanceStatuses.Rejected
                                   && c.Status != VisitInstanceStatuses.Closed))
                .OrderBy(u => u.UserId)
                .Select(u => u.UserId)
                .FirstOrDefaultAsync();
            if (staff == 0) return;

            requestId = await CreateAsInternalAsync(
                FormFor(leader.Value.Email, CampusWithProposal(leader.Value.Campus, contactEmail, 0, HostSelectionModes.Selected, staff)),
                leader.Value.UserId);

            var (instanceId, changeId) = await PendingInvitationAsync(requestId);

            // …and by the time the contact answers, that person has left. Nothing about this is the
            // contact's problem, and the confirmation must still succeed.
            using (var db = NewContext())
            {
                var target = await db.Users.FirstAsync(u => u.UserId == staff);
                target.Status = "INACTIVE";
                await db.SaveChangesAsync();
                deactivated = staff;
            }

            var token = await MintTokenAsync(changeId);
            using (var db = NewContext())
            {
                var result = await Accept(db, contactId, contactEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);
                Assert.Equal(IdentityChangeStatuses.Applied, result.ChangeStatus);
            }

            var after = await InstanceAsync(instanceId);
            Assert.NotNull(after.OperationalContactUserId);          // the confirmation stuck
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, after.Status);
            Assert.Null(after.CurrentHostUserId);                    // and nobody was substituted
            Assert.Equal(ProposedHostActivationStatuses.NeedsReselection, after.ProposedHostActivationStatus);
            // The proposal is KEPT: a Staff Leader re-picking needs to see what fell through.
            Assert.Equal(staff, after.ProposedHostUserId);
        }
        finally
        {
            if (deactivated is not null)
            {
                using var db = NewContext();
                var target = await db.Users.FirstOrDefaultAsync(u => u.UserId == deactivated.Value);
                if (target is not null) { target.Status = "ACTIVE"; await db.SaveChangesAsync(); }
            }
            await CleanupAsync(requestId);
        }
    }

    [Fact]
    public async Task One_campus_confirming_does_not_activate_anything_while_a_sibling_still_waits()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var seed = NewContext();
            var leader = await InternalHostAsync(seed, UserSubRoles.Leader);
            if (leader is null) return;
            var (contactA, emailA) = await VisitorUserAsync(seed);
            var (contactB, emailB) = await VisitorUserAsync(seed, contactA);

            var other = await seed.Campuses.AsNoTracking()
                .Where(c => c.Status == "ACTIVE" && c.CampusCode != leader.Value.Campus)
                .OrderBy(c => c.CampusId).Select(c => c.CampusCode).FirstOrDefaultAsync();
            if (other is null) return;

            requestId = await CreateAsInternalAsync(
                FormFor(leader.Value.Email,
                    CampusWithProposal(leader.Value.Campus, emailA, 0, HostSelectionModes.Self),
                    Campus(other, emailB, 1)),
                leader.Value.UserId);

            var (instanceA, changeA) = await PendingInvitationAsync(requestId);
            var tokenA = await MintTokenAsync(changeA);
            using (var db = NewContext())
            {
                await Accept(db, contactA, emailA).Handle(
                    new AcceptOperationalContactConfirmationCommand(tokenA), CancellationToken.None);
            }

            // The gate belongs to the REQUEST. One campus being ready says nothing while a sibling
            // has nobody — activating here would hand a campus to a host for a visit that may never
            // be confirmed at all.
            var afterFirst = await InstanceAsync(instanceA);
            Assert.Null(afterFirst.CurrentHostUserId);
            Assert.Equal(ProposedHostActivationStatuses.Pending, afterFirst.ProposedHostActivationStatus);

            using var check = NewContext();
            var visit = await check.VisitRequests.AsNoTracking().FirstAsync(v => v.VisitRequestId == requestId);
            Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, visit.Status);

            // The LAST confirmation is what opens the gate, and only then does the proposal activate.
            var (_, changeB) = await PendingInvitationAsync(requestId);
            var tokenB = await MintTokenAsync(changeB);
            using (var db = NewContext())
            {
                await Accept(db, contactB, emailB).Handle(
                    new AcceptOperationalContactConfirmationCommand(tokenB), CancellationToken.None);
            }

            var afterSecond = await InstanceAsync(instanceA);
            Assert.Equal(VisitInstanceStatuses.Assigned, afterSecond.Status);
            Assert.Equal(leader.Value.UserId, afterSecond.CurrentHostUserId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── The invitation actually renders and actually leaves ───────────────────────

    /// <summary>
    /// The invitation is the ONLY way the gate ever opens, and the service sends it best-effort: a
    /// render that throws is logged and swallowed so a committed confirmation state is not rolled
    /// back. That is right for the token, and it is exactly why nothing noticed when the per-campus
    /// cutover started supplying campus and time variables the seeded template did not declare —
    /// every caller still got its token back while no contact ever got a link.
    ///
    /// <para>
    /// So the assertion is on the OUTBOUND MESSAGE, not on the return value, and it is made through
    /// the real renderer against the real seeded row. Asserting a variable dictionary built inside the
    /// test would re-create the drift: what matters is that what the SERVICE supplies and what the
    /// TEMPLATE declares still agree.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_claim_invitation_renders_and_names_the_campus_it_invites_for()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var contact = "oc-render-" + Guid.NewGuid().ToString("N")[..8] + "@external.example";
            requestId = await CreateAsync(Form(Campus("HN", contact, 0)));
            var (instanceId, changeId) = await PendingInvitationAsync(requestId);

            var mail = new FakeEmail();
            await MintTokenAsync(changeId, mail);

            var sent = Assert.Single(mail.Sent);
            Assert.Equal(contact, sent.To);
            // The path half of the pair the SPA routes. Its counterpart is asserted in
            // frontend/pems-react/src/pages/identity/__tests__/OperationalContactInvitationRoute.test.ts —
            // neither side can see the other, so each pins the string it owns.
            Assert.Contains("/operational-contact-confirmation/", sent.Html);
            Assert.NotNull(mail.LastConfirmationToken);

            // The contact role is held per campus, so one request can invite the same person twice.
            // The campus and the window are what tell the two invitations apart.
            using var db = NewContext();
            var instance = await db.VisitRequestCampuses.AsNoTracking()
                .FirstAsync(c => c.VisitInstanceId == instanceId);
            var campusName = await db.Campuses.AsNoTracking()
                .Where(c => c.CampusId == instance.CampusId).Select(c => c.Name).FirstAsync();

            // Decoded first: the renderer HTML-encodes every substituted value, so a Vietnamese campus
            // name reaches the body as numeric entities. Asserting on the raw HTML would be asserting
            // on the encoder.
            var body = System.Net.WebUtility.HtmlDecode(sent.Html);
            Assert.Contains(campusName, body);
            Assert.Contains(instance.PlannedStartAt.ToString("HH:mm dd/MM/yyyy"), body);
            Assert.Contains(instance.PlannedEndAt.ToString("HH:mm dd/MM/yyyy"), body);

            // Repair v3 §8. The invitation greets a PERSON. `contactFullName` used to be assigned
            // change.NewEmailNormalized, so it opened "Kính gửi oc-render-1a2b3c@external.example" —
            // not a name, and it read as machine-generated to exactly the reader being asked to take on
            // a responsibility. The name was already stored in pending_snapshot_json all along.
            var detail = await db.VisitInstanceFormDetails.AsNoTracking()
                .FirstAsync(d => d.VisitInstanceId == instanceId);
            Assert.Contains(detail.OperationalContactFullName!, body);
            Assert.DoesNotContain(contact, body);

            // A placeholder that survived into the body means a variable was declared and not supplied.
            Assert.DoesNotContain("{{", sent.Html);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// The transfer invitation supplies one variable the claim does not — who is handing the role over —
    /// and a declared/supplied mismatch fails the WHOLE template, not the one variable that differs. So
    /// the second kind needs its own render.
    ///
    /// <para>
    /// The pending TRANSFER row is written here rather than through
    /// <c>InitiateOperationalContactTransferCommandHandler</c>: that handler only opens the transfer
    /// window at BEFORE_VISIT, and driving a campus that far is a different test's subject. What this
    /// one pins is the render, so it starts from the row the handler would have produced.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_transfer_invitation_renders_and_names_the_outgoing_contact()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactId;
            string contactEmail;
            using (var seed = NewContext())
                (contactId, contactEmail) = await VisitorUserAsync(seed);

            requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));
            var (instanceId, changeId) = await PendingInvitationAsync(requestId);

            var acceptToken = await MintTokenAsync(changeId);
            using (var db = NewContext())
            {
                await Accept(db, contactId, contactEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(acceptToken), CancellationToken.None);
            }

            var successor = "oc-transfer-" + Guid.NewGuid().ToString("N")[..8] + "@external.example";
            ulong transferId;
            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.AsNoTracking()
                    .FirstAsync(c => c.VisitInstanceId == instanceId);
                var transfer = new PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange
                {
                    VisitRequestId = requestId,
                    VisitInstanceId = instanceId,
                    ChangeKind = IdentityChangeKinds.Transfer,
                    TokenVersion = 1,
                    ConfirmationMethod = IdentityConfirmationMethods.GoogleSso,
                    OldUserId = contactId,
                    OldEmailNormalized = contactEmail.ToLowerInvariant(),
                    NewEmailNormalized = successor,
                    NewEmailMasked = successor[..2] + "***",
                    // The proposed person's own details, exactly as the transfer command writes them.
                    // For a TRANSFER this is the ONLY place the invitee's name exists: the campus
                    // snapshot still describes the person handing the role over.
                    PendingSnapshotJson =
                        "{\"fullName\":\"Người Nhận Bàn Giao\",\"organization\":\"OrgC\"," +
                        "\"jobTitle\":\"Trưởng phòng\",\"phone\":\"+8493\",\"email\":\"" + successor + "\"}",
                    Status = IdentityChangeStatuses.Pending,
                    ExpectedRequestRowVersion = (uint)instance.RowVersion,
                    RequestedBy = contactId,
                    RequestedAt = Now,
                    ExpiresAt = Now.AddDays(3),
                    ResendCount = 0,
                    CreatedAt = Now,
                };
                db.VisitRequestIdentityChanges.Add(transfer);
                await db.SaveChangesAsync();
                transferId = transfer.IdentityChangeId;
            }

            var mail = new FakeEmail();
            await MintTokenAsync(transferId, mail);

            var sent = Assert.Single(mail.Sent);
            Assert.Equal(successor, sent.To);
            Assert.NotNull(mail.LastConfirmationToken);
            Assert.DoesNotContain("{{", sent.Html);

            using var check = NewContext();
            var detail = await check.VisitInstanceFormDetails.AsNoTracking()
                .FirstAsync(d => d.VisitInstanceId == instanceId);
            var campusId = await check.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitInstanceId == instanceId).Select(c => c.CampusId).FirstAsync();
            var campusName = await check.Campuses.AsNoTracking()
                .Where(c => c.CampusId == campusId).Select(c => c.Name).FirstAsync();

            var body = System.Net.WebUtility.HtmlDecode(sent.Html);
            Assert.Contains(campusName, body);
            // An invitation to REPLACE somebody is unverifiable — and indistinguishable from a phishing
            // mail — if it will not say who is being replaced.
            Assert.Contains(detail.OperationalContactFullName!, body);

            // Repair v3 §8, the harder half. The person being GREETED is the invitee, whose name comes
            // from the pending snapshot — never the outgoing contact's name (they are named separately,
            // as the person handing over) and never the address. The two names are different strings
            // here precisely so a mix-up cannot pass.
            Assert.Contains("Người Nhận Bàn Giao", body);
            Assert.DoesNotContain(successor, body);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Legacy pending TRANSFER snapshots predating the Organization-required patch ────────────────
    //
    // Every write path that can raise a TRANSFER today (Save/Replace/Transfer's own FluentValidation
    // rules) now refuses a blank Organization. But a transfer invitation minted BEFORE that rule could
    // still carry `organization: null` in its pending_snapshot_json, and until now Accept applied it to
    // the live campus unconditionally — a legacy row bypassing a rule the write side no longer allows.
    // These three pin the fix: two REFUSE (null, blank) and one confirms a valid legacy snapshot still
    // accepts exactly as before (no regression).

    /// <summary>
    /// ASSIGNED, not BEFORE_VISIT: <c>EnsureTransferWindowOpen</c> accepts either, and ASSIGNED needs
    /// one fewer trigger-satisfying update. Mirrors the pattern <c>OperationalContactManagementTests
    /// .DriveToBeforeVisitAsync</c> uses to reach BEFORE_VISIT, stopped one step earlier.
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

    /// <summary>
    /// Confirms the current contact, drives the campus to ASSIGNED, then writes a PENDING transfer
    /// directly — exactly the row <c>InitiateOperationalContactTransferCommandHandler</c> would have
    /// produced before the Organization-required patch, using whatever <paramref name="organizationJson"/>
    /// literal the caller wants baked into <c>pending_snapshot_json</c>.
    /// </summary>
    private static async Task<(ulong InstanceId, ulong ContactId, string ContactEmail,
        ulong SuccessorId, string SuccessorEmail, ulong TransferId, string AcceptToken)>
        SeedLegacyTransferAsync(ulong requestId, string organizationJson)
    {
        using var seed = NewContext();
        var (contactId, contactEmail) = await VisitorUserAsync(seed);
        var (successorId, successorEmail) = await VisitorUserAsync(seed, contactId);

        var (instanceId, changeId) = await PendingInvitationAsync(requestId);
        var acceptToken0 = await MintTokenAsync(changeId);
        using (var db = NewContext())
            await Accept(db, contactId, contactEmail).Handle(
                new AcceptOperationalContactConfirmationCommand(acceptToken0), CancellationToken.None);

        await DriveToAssignedAsync(instanceId);

        var newEmail = VisitRequestFingerprintBuilder.NormalizeEmail(successorEmail);
        ulong transferId;
        using (var db = NewContext())
        {
            var instance = await db.VisitRequestCampuses.AsNoTracking()
                .FirstAsync(c => c.VisitInstanceId == instanceId);
            var transfer = new PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange
            {
                VisitRequestId = requestId,
                VisitInstanceId = instanceId,
                ChangeKind = IdentityChangeKinds.Transfer,
                TokenVersion = 1,
                ConfirmationMethod = IdentityConfirmationMethods.GoogleSso,
                OldUserId = contactId,
                OldEmailNormalized = VisitRequestFingerprintBuilder.NormalizeEmail(contactEmail),
                NewUserId = null,
                NewEmailNormalized = newEmail,
                NewEmailMasked = VisitRequestFingerprintBuilder.MaskEmail(newEmail),
                PendingSnapshotJson =
                    "{\"fullName\":\"Người Nhận Bàn Giao\",\"organization\":" + organizationJson + "," +
                    "\"jobTitle\":\"Trưởng phòng\",\"phone\":\"+8493\",\"email\":\"" + newEmail + "\"}",
                Status = IdentityChangeStatuses.Pending,
                ExpectedRequestRowVersion = (uint)instance.RowVersion,
                RequestedBy = contactId,
                RequestedAt = Now,
                ExpiresAt = Now.AddDays(3),
                ResendCount = 0,
                CreatedAt = Now,
            };
            db.VisitRequestIdentityChanges.Add(transfer);
            await db.SaveChangesAsync();
            transferId = transfer.IdentityChangeId;
        }

        var acceptToken = await MintTokenAsync(transferId);
        return (instanceId, contactId, contactEmail, successorId, successorEmail, transferId, acceptToken);
    }

    /// <summary>LEGACY-ORG-01. A transfer snapshot with `organization: null` cannot be Accepted.</summary>
    [Fact]
    public async Task LEGACY_ORG_01_null_organization_snapshot_is_refused_at_accept()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var seed = NewContext()) (_, contactEmail) = await VisitorUserAsync(seed);
            requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));

            var (instanceId, contactId, _, successorId, successorEmail, transferId, acceptToken) =
                await SeedLegacyTransferAsync(requestId, "null");

            var before = await InstanceAsync(instanceId);
            var detailBefore = await FormDetailAsync(instanceId);

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
            {
                using var db = NewContext();
                await Accept(db, successorId, successorEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(acceptToken), CancellationToken.None);
            });
            Assert.Equal(OperationalContactErrorCodes.OrganizationRequired, ex.ErrorCode);

            await AssertLegacyTransferRefusedAsync(
                requestId, instanceId, transferId, contactId, before, detailBefore);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>LEGACY-ORG-02. A transfer snapshot with `organization: "   "` cannot be Accepted either.</summary>
    [Fact]
    public async Task LEGACY_ORG_02_blank_organization_snapshot_is_refused_at_accept()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var seed = NewContext()) (_, contactEmail) = await VisitorUserAsync(seed);
            requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));

            var (instanceId, contactId, _, successorId, successorEmail, transferId, acceptToken) =
                await SeedLegacyTransferAsync(requestId, "\"   \"");

            var before = await InstanceAsync(instanceId);
            var detailBefore = await FormDetailAsync(instanceId);

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
            {
                using var db = NewContext();
                await Accept(db, successorId, successorEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(acceptToken), CancellationToken.None);
            });
            Assert.Equal(OperationalContactErrorCodes.OrganizationRequired, ex.ErrorCode);

            await AssertLegacyTransferRefusedAsync(
                requestId, instanceId, transferId, contactId, before, detailBefore);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// LEGACY-ORG-03 (regression). A legacy snapshot that DOES carry a valid Organization still Accepts
    /// exactly as it always has — the new guard only refuses what was already missing.
    /// </summary>
    [Fact]
    public async Task LEGACY_ORG_03_valid_organization_snapshot_still_accepts()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var seed = NewContext()) (_, contactEmail) = await VisitorUserAsync(seed);
            requestId = await CreateAsync(Form(Campus("HN", contactEmail, 0)));

            var (instanceId, contactId, _, successorId, successorEmail, transferId, acceptToken) =
                await SeedLegacyTransferAsync(requestId, "\"SeoulTech Global Engagement Center\"");

            using (var db = NewContext())
                await Accept(db, successorId, successorEmail).Handle(
                    new AcceptOperationalContactConfirmationCommand(acceptToken), CancellationToken.None);

            var after = await InstanceAsync(instanceId);
            Assert.Equal(successorId, after.OperationalContactUserId);
            Assert.Equal(OperationalContactSources.Transfer, after.OperationalContactConfirmationSource);

            var detailAfter = await FormDetailAsync(instanceId);
            Assert.Equal("SeoulTech Global Engagement Center", detailAfter.OperationalContactOrganization);
            Assert.Equal("Người Nhận Bàn Giao", detailAfter.OperationalContactFullName);

            using var check = NewContext();
            var change = await check.VisitRequestIdentityChanges.AsNoTracking()
                .FirstAsync(c => c.IdentityChangeId == transferId);
            Assert.Equal(IdentityChangeStatuses.Applied, change.Status);
            Assert.Equal(successorId, change.NewUserId);
        }
        finally { await CleanupAsync(requestId); }
    }

    private static async Task<PEMS.Domain.Entities.Delegations.VisitInstanceFormDetail> FormDetailAsync(
        ulong instanceId)
    {
        using var db = NewContext();
        return await db.VisitInstanceFormDetails.AsNoTracking().FirstAsync(d => d.VisitInstanceId == instanceId);
    }

    /// <summary>
    /// Everything a refused legacy transfer must NOT have done: no relation move, no snapshot write, no
    /// status change, invitation still PENDING (not fake-APPLIED), its token still unconsumed, and no
    /// success audit — the refusal happened before any of it, inside the accept's own transaction.
    /// </summary>
    private static async Task AssertLegacyTransferRefusedAsync(
        ulong requestId, ulong instanceId, ulong transferId, ulong contactId,
        PEMS.Domain.Entities.Delegations.VisitRequestCampus before,
        PEMS.Domain.Entities.Delegations.VisitInstanceFormDetail detailBefore)
    {
        var after = await InstanceAsync(instanceId);
        Assert.Equal(contactId, after.OperationalContactUserId);        // still the ORIGINAL contact
        Assert.Equal(before.Status, after.Status);                       // ASSIGNED, unchanged
        Assert.Equal(before.OperationalContactConfirmedAt, after.OperationalContactConfirmedAt);
        Assert.Equal(before.OperationalContactConfirmationSource, after.OperationalContactConfirmationSource);

        var detailAfter = await FormDetailAsync(instanceId);
        Assert.Equal(detailBefore.OperationalContactOrganization, detailAfter.OperationalContactOrganization);
        Assert.Equal(detailBefore.OperationalContactEmail, detailAfter.OperationalContactEmail);
        Assert.Equal(detailBefore.OperationalContactFullName, detailAfter.OperationalContactFullName);

        using var check = NewContext();
        var change = await check.VisitRequestIdentityChanges.AsNoTracking()
            .FirstAsync(c => c.IdentityChangeId == transferId);
        Assert.Equal(IdentityChangeStatuses.Pending, change.Status);     // never marked Applied
        Assert.Null(change.NewUserId);
        Assert.Null(change.AppliedAt);

        Assert.Equal(LinksPerInvitation, await LiveTokenCountAsync(transferId)); // token still unconsumed

        Assert.False(await check.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.VisitRequestId == requestId && a.Action == "OPERATIONAL_CONTACT_TRANSFER_APPLIED"));
    }
}
