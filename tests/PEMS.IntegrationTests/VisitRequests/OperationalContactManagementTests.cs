using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
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
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Emails.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Contact management as its own workflow, separate from editing the request (repair v3 §4–§6, §17).
///
/// <para>
/// The subject is the fork. One form, five fields, one save — and the SERVER decides what the save
/// means by comparing the submitted address with the stored one. Everything that used to go through
/// <c>ReplaceOperationalContactCommand</c> regardless: correcting a typo in a name superseded the live
/// invitation, dropped the campus's confirmed contact, re-closed the global gate for every campus on
/// the request and sent a confirmation email. This suite pins that a metadata correction now does none
/// of those things, and that an address change still does all of them.
/// </para>
///
/// Each test creates its own committed request and cascade-deletes it in <c>finally</c>.
/// </summary>
public sealed class OperationalContactManagementTests
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

    /// <summary>Records every outbound message. What matters most in this suite is when it stays empty.</summary>
    private sealed class FakeEmail : IEmailService
    {
        public List<(string To, string Subject, string Html)> Sent { get; } = new();

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

    /// <summary>
    /// Issues an invitation the way every production caller does — mint, make the links durable, then
    /// send — and returns its ACCEPT link. There is no mint-and-send convenience on the service any
    /// more: a token minted after somebody else's commit is exactly the bug the split prevents.
    /// </summary>
    private static async Task<string> IssueInvitationAsync(
        ApplicationDbContext db, FakeEmail email, ulong identityChangeId)
    {
        var invitations = Invitations(db, email);
        var tokens = await invitations.MintInvitationTokensAsync(identityChangeId, CancellationToken.None);
        Assert.NotNull(tokens);
        await db.SaveChangesAsync(CancellationToken.None);
        await invitations.DispatchInvitationEmailAsync(identityChangeId, tokens!, CancellationToken.None);
        return tokens!.AcceptToken;
    }

    /// <summary>
    /// The branching save, wired to the three real handlers behind a tiny in-process dispatcher.
    ///
    /// <para>
    /// Deliberately the real handlers rather than fakes: the whole point of the fork is WHICH one runs,
    /// and a test that stubbed them would prove only that the comparison in the router works, not that
    /// the branch it chose has the effects the plan requires.
    /// </para>
    /// </summary>
    private static SaveOperationalContactCommandHandler Save(
        ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new InProcessSender(db, actor, email), new FakeUser(actor), WriteOn);

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
                        _db, new FakeUser(_actor), new FixedClock(), Invitations(_db, _email), WriteOn)
                        .Handle(c, ct)),
                ReplaceOperationalContactCommand c => Cast<TResponse>(
                    new ReplaceOperationalContactCommandHandler(
                        _db, new FakeUser(_actor), new FixedClock(), Invitations(_db, _email),
                        new VisitRequestAggregateStatusService(_db), new NoopNotifications(),
                        NullLogger<ReplaceOperationalContactCommandHandler>.Instance, WriteOn)
                        .Handle(c, ct)),
                InitiateOperationalContactTransferCommand c => Cast<TResponse>(
                    new InitiateOperationalContactTransferCommandHandler(
                        _db, new FakeUser(_actor), new FixedClock(), Invitations(_db, _email), WriteOn)
                        .Handle(c, ct)),
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

    private static AcceptOperationalContactConfirmationCommandHandler Accept(
        ApplicationDbContext db, ulong actor, string actorEmail, FakeEmail email)
        => new(db, new FakeUser(actor, actorEmail), new FixedClock(), Tokens(), Invitations(db, email),
            new VisitRequestAggregateStatusService(db), new ProposedHostActivationService(db),
            new NoopNotifications(),
            NullLogger<AcceptOperationalContactConfirmationCommandHandler>.Instance, WriteOn);

    private static ReinviteOperationalContactConfirmationCommandHandler Reinvite(
        ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new FakeUser(actor), new FixedClock(), Invitations(db, email),
            new VisitRequestAggregateStatusService(db), WriteOn);

    private static ResendOperationalContactConfirmationCommandHandler Resend(
        ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new FakeUser(actor), new FixedClock(), Invitations(db, email), WriteOn);

    private static CancelOperationalContactChangeCommandHandler Cancel(
        ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new FakeUser(actor), new FixedClock(), Invitations(db, email), WriteOn);

    // ── Data helpers ──────────────────────────────────────────────────────────────

    private static async Task<(ulong UserId, string Email)> VisitorUserAsync(ApplicationDbContext db)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                        && u.UserId != Registrant)
            .OrderBy(u => u.UserId)
            .Select(u => new { u.UserId, u.Email })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("This database needs an ACTIVE VISITOR besides user 8.");
        return (row.UserId, row.Email!);
    }

    /// <summary>A SECOND active visitor, for the person a campus is handed over TO.</summary>
    private static async Task<(ulong UserId, string Email)> SuccessorUserAsync(ApplicationDbContext db)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                        && u.UserId != Registrant)
            .OrderBy(u => u.UserId).Skip(1)
            .Select(u => new { u.UserId, u.Email })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("This database needs a SECOND active VISITOR.");
        return (row.UserId, row.Email!);
    }

    /// <summary>
    /// Points the campus's contact at one of its delegation members — the ordinary arrangement, and
    /// the one a stale link damages: the biên bản reads this column to decide who wears "· Đầu mối".
    /// Written directly so the test is about what happens NEXT, not about how the link was made.
    /// </summary>
    private static async Task<ulong> LinkContactToFirstMemberAsync(ulong requestId, ulong instanceId)
    {
        using var db = NewContext();
        var memberId = await db.VisitGuestMembers.AsNoTracking()
            .Where(g => g.VisitRequestId == requestId)
            .OrderBy(g => g.GuestMemberId).Select(g => g.GuestMemberId).FirstAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_instance_form_details SET operational_contact_guest_member_id = {0} "
            + "WHERE visit_instance_id = {1}",
            memberId, instanceId);
        return memberId;
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
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));

        var form = new VisitRequestFormDataV2(
            "OM" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private sealed record CampusRow(
        ulong InstanceId, string Status, ulong? ContactUserId, DateTime? ConfirmedAt,
        string? ConfirmationSource, int RowVersion);

    private static async Task<CampusRow> CampusStateAsync(ulong requestId)
    {
        using var db = NewContext();
        var c = await db.VisitRequestCampuses.AsNoTracking()
            .Where(x => x.VisitRequestId == requestId)
            .OrderBy(x => x.VisitInstanceId)
            .FirstAsync();
        return new CampusRow(c.VisitInstanceId, c.Status, c.OperationalContactUserId,
            c.OperationalContactConfirmedAt, c.OperationalContactConfirmationSource, c.RowVersion);
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

    private static async Task<int> TokenCountAsync(ulong changeId)
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
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
        await Del("DELETE FROM email_action_tokens WHERE target_type = 'VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    /// <summary>The five fields as the detail screen would submit them, starting from what is stored.</summary>
    private static SaveOperationalContactCommand SaveOf(
        ulong requestId, ulong instanceId, PEMS.Domain.Entities.Delegations.VisitInstanceFormDetail d,
        string? fullName = null, string? organization = null, string? jobTitle = null,
        string? phone = null, string? email = null, int? rowVersion = null)
        => new(requestId, instanceId,
            fullName ?? d.OperationalContactFullName!,
            organization ?? d.OperationalContactOrganization,
            jobTitle ?? d.OperationalContactJobTitle!,
            phone ?? d.OperationalContactPhone,
            email ?? d.OperationalContactEmail,
            Reason: null, ExpectedRowVersion: rowVersion);

    // ── Path A: same address ──────────────────────────────────────────────────────

    /// <summary>
    /// TC-META-01. The correction everybody makes: a name spelt right, a new phone number. It writes
    /// four columns and does nothing else — and "nothing else" is nine separate assertions because
    /// every one of them used to happen.
    /// </summary>
    [Fact]
    public async Task Same_address_with_changed_details_updates_the_snapshot_and_nothing_else()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);
            var changesBefore = await ChangesAsync(requestId);
            var invitation = Assert.Single(changesBefore);

            var mail = new FakeEmail();
            using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail,
                        fullName: "Nguyễn Văn Sửa", organization: "Đơn vị mới",
                        jobTitle: "Phó phòng", phone: "+84900000001"),
                    CancellationToken.None);

            // What it DID: the four columns.
            var after = await DetailAsync(before.InstanceId);
            Assert.Equal("Nguyễn Văn Sửa", after.OperationalContactFullName);
            Assert.Equal("Đơn vị mới", after.OperationalContactOrganization);
            Assert.Equal("Phó phòng", after.OperationalContactJobTitle);
            Assert.Equal("+84900000001", after.OperationalContactPhone);
            // The address is untouched, and so is the revision counter — this is not a new version of
            // what the campus is being asked to host.
            Assert.Equal(detail.OperationalContactEmail, after.OperationalContactEmail);
            Assert.Equal(detail.FormRevision, after.FormRevision);

            // What it did NOT do.
            Assert.Empty(mail.Sent);                                        // no confirmation email
            var changesAfter = await ChangesAsync(requestId);
            Assert.Single(changesAfter);                                    // no new identity change
            Assert.Equal(IdentityChangeStatuses.Pending, changesAfter[0].Status);
            Assert.Equal(invitation.TokenVersion, changesAfter[0].TokenVersion);
            Assert.Equal(invitation.ResendCount, changesAfter[0].ResendCount);
            Assert.Equal(invitation.ExpiresAt, changesAfter[0].ExpiresAt);
            Assert.Equal(0, await TokenCountAsync(invitation.IdentityChangeId)); // no new link minted

            var campus = await CampusStateAsync(requestId);
            Assert.Equal(before.Status, campus.Status);                      // no lifecycle move
            Assert.Equal(before.ContactUserId, campus.ContactUserId);        // nobody's authority moved
            Assert.Equal(before.ConfirmedAt, campus.ConfirmedAt);
            Assert.Equal(before.ConfirmationSource, campus.ConfirmationSource);

            // The pending invitation's snapshot follows the correction, so accepting it later writes the
            // corrected details rather than silently undoing them.
            var snapshot = JsonDocument.Parse(changesAfter[0].PendingSnapshotJson!).RootElement;
            Assert.Equal("Nguyễn Văn Sửa", snapshot.GetProperty("fullName").GetString());
            Assert.Equal("Phó phòng", snapshot.GetProperty("jobTitle").GetString());
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-META-02. Case and surrounding whitespace do not make a different person, and a correction that
    /// happened to retype the address in a different case must not email them to prove who they are.
    /// </summary>
    [Fact]
    public async Task An_address_differing_only_in_case_or_whitespace_is_the_same_identity()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);

            var mail = new FakeEmail();
            using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail,
                        phone: "+84900000002",
                        email: "  " + contactEmail.ToUpperInvariant() + " "),
                    CancellationToken.None);

            Assert.Empty(mail.Sent);
            Assert.Single(await ChangesAsync(requestId));
            var after = await DetailAsync(before.InstanceId);
            Assert.Equal("+84900000002", after.OperationalContactPhone);
            Assert.Equal(detail.OperationalContactEmail, after.OperationalContactEmail);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-META-03. An invitation that has already lapsed must not come back to life because somebody
    /// fixed a job title. Resending is an explicit act with its own button and its own rate limit.
    /// </summary>
    [Fact]
    public async Task An_expired_invitation_is_not_revived_by_a_metadata_correction()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);

            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_identity_changes SET status = 'EXPIRED' WHERE visit_request_id = {0}",
                    requestId);

            var mail = new FakeEmail();
            using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail, jobTitle: "Giám đốc"),
                    CancellationToken.None);

            Assert.Empty(mail.Sent);
            var change = Assert.Single(await ChangesAsync(requestId));
            Assert.Equal(IdentityChangeStatuses.Expired, change.Status);     // still expired
            Assert.Equal("Giám đốc", (await DetailAsync(before.InstanceId)).OperationalContactJobTitle);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-META-04. A stale modal — opened, left, submitted after somebody else saved — is refused rather
    /// than allowed to write back the values it read minutes ago.
    /// </summary>
    [Fact]
    public async Task A_stale_form_cannot_overwrite_newer_contact_information()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);

            var mail = new FakeEmail();
            using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail,
                        fullName: "Người sửa trước", rowVersion: before.RowVersion),
                    CancellationToken.None);

            var stale = await Assert.ThrowsAsync<ConflictException>(async () =>
            {
                using var db = NewContext();
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail,
                        fullName: "Người sửa sau", rowVersion: before.RowVersion),
                    CancellationToken.None);
            });
            Assert.Equal(VisitRequestErrorCodes.InstanceVersionConflict, stale.ErrorCode);
            Assert.Equal("Người sửa trước", (await DetailAsync(before.InstanceId)).OperationalContactFullName);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-META-05. A save that would write exactly what is stored is refused rather than reported as a
    /// change — an audit trail full of no-op "updates" is an audit trail nobody can read.
    /// </summary>
    [Fact]
    public async Task A_save_that_changes_nothing_is_refused_with_a_stable_code()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);

            var noop = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
            {
                using var db = NewContext();
                await Save(db, Registrant, new FakeEmail()).Handle(
                    SaveOf(requestId, before.InstanceId, detail), CancellationToken.None);
            });
            Assert.Equal(OperationalContactErrorCodes.ProfileNoChanges, noop.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Path B: changed address ───────────────────────────────────────────────────

    /// <summary>
    /// TC-IDENTITY-01. Before a decision, a new address is a replace: the campus loses its contact, a
    /// fresh INITIAL_CONFIRMATION goes out, and the old invitation is superseded rather than left live.
    /// </summary>
    [Fact]
    public async Task A_changed_address_before_a_decision_runs_the_canonical_replace()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);
            var successor = "oc-new-" + Guid.NewGuid().ToString("N")[..8] + "@external.example";

            var mail = new FakeEmail();
            using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail,
                        fullName: "Người kế nhiệm", email: successor),
                    CancellationToken.None);

            // A confirmation email, to the NEW address — this branch is the one that mails.
            var sent = Assert.Single(mail.Sent);
            Assert.Equal(successor, sent.To);

            var changes = await ChangesAsync(requestId);
            Assert.Equal(2, changes.Count);
            Assert.Equal(IdentityChangeStatuses.Superseded, changes[0].Status);
            Assert.Equal(IdentityChangeKinds.InitialConfirmation, changes[1].ChangeKind);
            Assert.Equal(IdentityChangeStatuses.Pending, changes[1].Status);
            Assert.Equal(successor, changes[1].NewEmailNormalized);

            var campus = await CampusStateAsync(requestId);
            Assert.Null(campus.ContactUserId);                                   // the gate re-closes
            Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, campus.Status);
            Assert.Equal(successor, (await DetailAsync(before.InstanceId)).OperationalContactEmail);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-IDENTITY-02. Once the campus has been decided, a new address is a HANDOVER, and the defining
    /// property is that nothing moves yet: the current contact still holds the campus, the campus keeps
    /// its status, and the snapshot still names the person who is there today.
    ///
    /// <para>
    /// The campus is driven to BEFORE_VISIT the way the database allows — confirm the contact through
    /// the real accept handler, then the two transitions a Staff Leader's approval and the Host's
    /// "start preparation" would have made. Going through those two commands instead would put a Staff
    /// Leader, a host assignment and an approval decision inside a test about a contact handover.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_changed_address_after_a_decision_proposes_a_transfer_and_moves_nothing_yet()
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

            // The invited person accepts, so the campus has a real confirmed contact to hand over.
            var mail = new FakeEmail();
            string token;
            using (var db = NewContext())
                token = await IssueInvitationAsync(db, mail, invitation.IdentityChangeId);
            using (var db = NewContext())
                await Accept(db, contactId, contactEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);

            await DriveToBeforeVisitAsync(created.InstanceId);

            var decided = await CampusStateAsync(requestId);
            Assert.Equal(VisitInstanceStatuses.BeforeVisit, decided.Status);
            Assert.Equal(contactId, decided.ContactUserId);

            var detail = await DetailAsync(decided.InstanceId);
            var successor = "oc-take-" + Guid.NewGuid().ToString("N")[..8] + "@external.example";

            var handoverMail = new FakeEmail();
            using (var db = NewContext())
                await Save(db, Registrant, handoverMail).Handle(
                    SaveOf(requestId, decided.InstanceId, detail,
                        fullName: "Người nhận bàn giao", email: successor),
                    CancellationToken.None);

            // A TRANSFER was raised, and the invitation went to the proposed person.
            var pending = (await ChangesAsync(requestId))
                .Single(c => c.Status == IdentityChangeStatuses.Pending);
            Assert.Equal(IdentityChangeKinds.Transfer, pending.ChangeKind);
            Assert.Equal(successor, pending.NewEmailNormalized);
            Assert.Equal(contactId, pending.OldUserId);
            Assert.Equal(successor, Assert.Single(handoverMail.Sent).To);

            // Nothing moved. This is the whole difference from a replace.
            var after = await CampusStateAsync(requestId);
            Assert.Equal(contactId, after.ContactUserId);
            Assert.Equal(VisitInstanceStatuses.BeforeVisit, after.Status);
            var snapshot = await DetailAsync(decided.InstanceId);
            Assert.Equal(detail.OperationalContactEmail, snapshot.OperationalContactEmail);
            Assert.Equal(detail.OperationalContactFullName, snapshot.OperationalContactFullName);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── The link to a delegation member belongs to the person who HELD the role ──────────────────
    //
    // The contact snapshot and the link answer two different questions — "what was agreed" and "which
    // member of the delegation is that" — and every path that rewrites the first must settle the
    // second. Neither of these two did: they rewrote all five snapshot columns and left the id alone,
    // so it went on naming the previous contact for the rest of the request's life. The biên bản is
    // where that surfaced: it badges "· Đầu mối" from this column, so the campus kept naming the
    // person who had handed the role over, and the person who took it never appeared in the record at
    // all — the auto-fill saw the old member already in the list and stopped there.

    [Fact]
    public async Task Replacing_the_contact_clears_the_link_to_the_previous_delegation_member()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var memberId = await LinkContactToFirstMemberAsync(requestId, before.InstanceId);
            Assert.Equal(memberId, (await DetailAsync(before.InstanceId)).OperationalContactGuestMemberId);

            var detail = await DetailAsync(before.InstanceId);
            var successor = "oc-new-" + Guid.NewGuid().ToString("N")[..8] + "@external.example";
            using (var db = NewContext())
                await Save(db, Registrant, new FakeEmail()).Handle(
                    SaveOf(requestId, before.InstanceId, detail,
                        fullName: "Người kế nhiệm", email: successor),
                    CancellationToken.None);

            Assert.Null((await DetailAsync(before.InstanceId)).OperationalContactGuestMemberId);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Accepting_a_handover_clears_the_link_to_the_previous_delegation_member()
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
                (successorId, successorEmail) = await SuccessorUserAsync(db);
            }

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var created = await CampusStateAsync(requestId);
            var invitation = Assert.Single(await ChangesAsync(requestId));

            var mail = new FakeEmail();
            string token;
            using (var db = NewContext())
                token = await IssueInvitationAsync(db, mail, invitation.IdentityChangeId);
            using (var db = NewContext())
                await Accept(db, contactId, contactEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);

            await DriveToBeforeVisitAsync(created.InstanceId);
            await LinkContactToFirstMemberAsync(requestId, created.InstanceId);

            // Hand the campus over, then let the successor take it — the post-approval path, where a
            // replace is refused and only a transfer exists.
            var detail = await DetailAsync(created.InstanceId);
            using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, created.InstanceId, detail,
                        fullName: "Người nhận bàn giao", email: successorEmail),
                    CancellationToken.None);

            var pending = (await ChangesAsync(requestId))
                .Single(c => c.Status == IdentityChangeStatuses.Pending);
            Assert.Equal(IdentityChangeKinds.Transfer, pending.ChangeKind);

            string handoverToken;
            using (var db = NewContext())
                handoverToken = await IssueInvitationAsync(db, mail, pending.IdentityChangeId);
            using (var db = NewContext())
                await Accept(db, successorId, successorEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(handoverToken), CancellationToken.None);

            Assert.Equal(successorId, (await CampusStateAsync(requestId)).ContactUserId);
            Assert.Null((await DetailAsync(created.InstanceId)).OperationalContactGuestMemberId);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-META-06. The case the plan calls out by name: an APPROVED campus starting inside 72 hours still
    /// accepts a metadata correction. The registration lead time is a rule about when a visit may be
    /// SCHEDULED and has nothing to say about a phone number — least of all on the day it matters most.
    /// </summary>
    [Fact]
    public async Task An_approved_campus_starting_inside_72h_still_accepts_a_metadata_correction()
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
            string token;
            using (var db = NewContext())
                token = await IssueInvitationAsync(db, mail, invitation.IdentityChangeId);
            using (var db = NewContext())
                await Accept(db, contactId, contactEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);

            await DriveToBeforeVisitAsync(created.InstanceId);

            // The visit is now 40 hours out — inside both the 72h registration floor and the 24h
            // transfer lead time.
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_campuses SET planned_start_at = {0}, planned_end_at = {1} WHERE visit_instance_id = {2}",
                    Now.AddHours(40), Now.AddHours(42), created.InstanceId);

            var detail = await DetailAsync(created.InstanceId);
            var quietMail = new FakeEmail();
            using (var db = NewContext())
                await Save(db, Registrant, quietMail).Handle(
                    SaveOf(requestId, created.InstanceId, detail, phone: "+84900000009"),
                    CancellationToken.None);

            Assert.Equal("+84900000009", (await DetailAsync(created.InstanceId)).OperationalContactPhone);
            Assert.Empty(quietMail.Sent);
            var after = await CampusStateAsync(requestId);
            Assert.Equal(VisitInstanceStatuses.BeforeVisit, after.Status);
            Assert.Equal(contactId, after.ContactUserId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Organization is required on every write path, not just Create/Pending Edit/Resubmit ──────────
    //
    // These pin the SAME rule the unit-level validator tests pin, but through the real handlers and a
    // committed database, proving a blank Organization does not silently reach a write anywhere along
    // the fork — and that a legitimate Organization correction is written AND audited like every other
    // field. Validation itself runs through the SAME FluentValidation validator class the production
    // MediatR pipeline (`ValidationBehaviour<TRequest,TResponse>`) resolves and runs before Handle is
    // ever reached — this harness dispatches straight to handlers (see `InProcessSender` above) for
    // every test in this file, so the validator is invoked explicitly here to prove the identical
    // refusal a real HTTP call would get, and that nothing downstream depends on it being skipped.

    /// <summary>ORG-INT-01. A blank Organization on a same-address save is refused before anything moves.</summary>
    [Fact]
    public async Task Blank_organization_on_a_profile_correction_is_refused_before_any_write()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);
            var changesBefore = await ChangesAsync(requestId);

            var blankOrgSave = SaveOf(requestId, before.InstanceId, detail,
                organization: "   ", phone: "+84900000003");

            var result = new SaveOperationalContactCommandValidator().Validate(blankOrgSave);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(SaveOperationalContactCommand.Organization));

            // Nothing about the campus moved — same as production, where ValidationBehaviour throws
            // BEFORE the handler (and therefore this save) ever runs.
            var after = await DetailAsync(before.InstanceId);
            Assert.Equal(detail.OperationalContactOrganization, after.OperationalContactOrganization);
            Assert.Equal(detail.OperationalContactPhone, after.OperationalContactPhone);
            Assert.Equal(changesBefore.Count, (await ChangesAsync(requestId)).Count);
            using (var db = NewContext())
                Assert.False(await db.AuditLogs.AsNoTracking()
                    .AnyAsync(a => a.VisitRequestId == requestId
                                   && a.Action == "OPERATIONAL_CONTACT_PROFILE_UPDATED"));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>ORG-INT-02. A real Organization correction writes the column AND lands in the audit trail.</summary>
    [Fact]
    public async Task Valid_organization_correction_persists_and_is_audited()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);
            var oldOrganization = detail.OperationalContactOrganization;

            using (var db = NewContext())
                await Save(db, Registrant, new FakeEmail()).Handle(
                    SaveOf(requestId, before.InstanceId, detail, organization: "Đơn vị mới"),
                    CancellationToken.None);

            var after = await DetailAsync(before.InstanceId);
            Assert.Equal("Đơn vị mới", after.OperationalContactOrganization);

            using (var db = NewContext())
            {
                var audit = await db.AuditLogs.AsNoTracking().Include(x => x.Changes)
                    .Where(x => x.VisitRequestId == requestId && x.Action == "OPERATIONAL_CONTACT_PROFILE_UPDATED")
                    .SingleAsync();
                var change = Assert.Single(audit.Changes, c => c.FieldName == "operational_contact_organization");
                Assert.Equal(oldOrganization, change.OldValueText);
                Assert.Equal("Đơn vị mới", change.NewValueText);
            }

            // No identity change, no invitation, no status move — a metadata correction, same as every
            // other field on this branch.
            Assert.Single(await ChangesAsync(requestId));
            var campus = await CampusStateAsync(requestId);
            Assert.Equal(before.Status, campus.Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>ORG-INT-03. A blank Organization on a pre-decision replace never reaches the invitation.</summary>
    [Fact]
    public async Task Blank_organization_on_replace_is_refused_before_the_invitation_is_raised()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);
            var changesBefore = await ChangesAsync(requestId);
            var successor = "oc-blank-org-" + Guid.NewGuid().ToString("N")[..8] + "@external.example";

            var replace = new ReplaceOperationalContactCommand(
                requestId, before.InstanceId, "Người kế nhiệm", "  ", "Trưởng phòng", null, successor);

            var result = new ReplaceOperationalContactCommandValidator().Validate(replace);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(ReplaceOperationalContactCommand.Organization));

            // The campus keeps its current contact snapshot and invitation — REFUSED, not half-applied.
            var after = await DetailAsync(before.InstanceId);
            Assert.Equal(detail.OperationalContactEmail, after.OperationalContactEmail);
            Assert.Equal(detail.OperationalContactOrganization, after.OperationalContactOrganization);
            Assert.Equal(changesBefore.Count, (await ChangesAsync(requestId)).Count);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Patch 5 (E-6/E-13/E-14): a malformed Operational Contact email is refused the same way a blank
    /// Organization is — before the invitation is raised, before any identity-change row is written,
    /// before any audit row is written. Mirrors ORG-INT-03 exactly, swapping the malformed field.
    /// </summary>
    [Fact]
    public async Task Malformed_email_on_replace_is_refused_before_the_invitation_is_raised()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);
            var changesBefore = await ChangesAsync(requestId);
            int auditCountBefore;
            using (var db = NewContext())
                auditCountBefore = await db.AuditLogs.CountAsync(a => a.VisitRequestId == requestId);

            var replace = new ReplaceOperationalContactCommand(
                requestId, before.InstanceId, "Người kế nhiệm", "OrgX", "Trưởng phòng", null, "not-an-email");

            var result = new ReplaceOperationalContactCommandValidator().Validate(replace);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(ReplaceOperationalContactCommand.Email));

            // Refused BEFORE anything is written: no invitation (no new identity-change row), no new
            // audit row, snapshot byte-identical.
            var after = await DetailAsync(before.InstanceId);
            Assert.Equal(detail.OperationalContactEmail, after.OperationalContactEmail);
            Assert.Equal(changesBefore.Count, (await ChangesAsync(requestId)).Count);
            using (var db = NewContext())
                Assert.Equal(auditCountBefore, await db.AuditLogs.CountAsync(a => a.VisitRequestId == requestId));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Patch 5 (E-15): two submissions of the SAME address, spelled differently (padding, casing),
    /// must resolve to ONE account — never a duplicate. UserProvisionService already normalizes
    /// (trim + lowercase) both its lookup and its write internally, independent of Patch 5's own
    /// RegistrantEmail/OperationalContactEmail write-time normalization fix; this test pins that
    /// guarantee directly at the unit responsible for it.
    /// </summary>
    [Fact]
    public async Task Registrant_provisioning_normalizes_so_padding_and_casing_do_not_create_duplicate_identity()
    {
        RequireDb();
        using var db = NewContext();
        var uniqueLocal = "e15-" + Guid.NewGuid().ToString("N")[..8];
        var provisioning = new UserProvisionService(db);

        var firstId = await provisioning.EnsureVisitorAccountAsync(
            $"  {uniqueLocal}@Example.COM  ", "Nguyễn Văn A", null, null, DateTime.UtcNow, CancellationToken.None);
        var secondId = await provisioning.EnsureVisitorAccountAsync(
            $"{uniqueLocal.ToUpperInvariant()}@EXAMPLE.com", "Nguyễn Văn A", null, null, DateTime.UtcNow, CancellationToken.None);

        try
        {
            Assert.Equal(firstId, secondId); // same account, never a duplicate
            Assert.Equal(1, await db.Users.CountAsync(u => u.Email == $"{uniqueLocal}@example.com"));
        }
        finally
        {
            var created = await db.Users.SingleAsync(u => u.UserId == firstId);
            db.Users.Remove(created);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>ORG-INT-04. A blank Organization on a post-decision transfer leaves the current contact untouched.</summary>
    [Fact]
    public async Task Blank_organization_on_transfer_is_refused_and_current_contact_is_unchanged()
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
            string token;
            using (var db = NewContext())
                token = await IssueInvitationAsync(db, mail, invitation.IdentityChangeId);
            using (var db = NewContext())
                await Accept(db, contactId, contactEmail, mail).Handle(
                    new AcceptOperationalContactConfirmationCommand(token), CancellationToken.None);

            await DriveToBeforeVisitAsync(created.InstanceId);
            var decided = await CampusStateAsync(requestId);
            var changesBefore = await ChangesAsync(requestId);
            var successor = "oc-blank-org-take-" + Guid.NewGuid().ToString("N")[..8] + "@external.example";

            var transfer = new InitiateOperationalContactTransferCommand(
                requestId, decided.InstanceId, "Người nhận bàn giao", "", "Trưởng phòng", null, successor,
                "Bàn giao");

            var result = new InitiateOperationalContactTransferCommandValidator().Validate(transfer);
            Assert.False(result.IsValid);
            Assert.Contains(
                result.Errors, e => e.PropertyName == nameof(InitiateOperationalContactTransferCommand.Organization));

            Assert.Equal(changesBefore.Count, (await ChangesAsync(requestId)).Count); // no TRANSFER raised
            var after = await CampusStateAsync(requestId);
            Assert.Equal(contactId, after.ContactUserId);
            Assert.Equal(VisitInstanceStatuses.BeforeVisit, after.Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// WAITING_REQUEST_APPROVAL → ASSIGNED → BEFORE_VISIT, satisfying what the database insists on at
    /// each step: a Staff Leader OF THIS CAMPUS as the decider, an official host assigned by then, and
    /// two separate updates because the trigger only lets BEFORE_VISIT be entered from ASSIGNED.
    ///
    /// <para>
    /// The decider is looked up rather than passed in: the trigger checks that <c>decided_by</c> really
    /// is a Staff Leader of the campus, and a campus that could be registered against at all is
    /// guaranteed to have one — campus availability at create time requires it.
    /// </para>
    /// </summary>
    private static async Task DriveToBeforeVisitAsync(ulong instanceId)
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
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'BEFORE_VISIT' WHERE visit_instance_id = {0}",
            instanceId);
    }

    // ── Commit 3 — Contact History Integrity (Fix Group C/D) ───────────────────────
    //
    // The writer-triggered outcomes (profile update, external replace, self-match replace) run
    // through the REAL handlers this file already has a harness for, then read back through the
    // real history handlers below — proving the reader against actual writer output, not against
    // an assumption of what the writer produces. Privacy/visibility regression for these same
    // events lives in VisitRequestHistoryV2Tests.cs (C3-7..C3-13), matching that file's own
    // hand-built-fixture convention for multi-role scoping.

    private static GetVisitRequestHistoryQueryHandler HistoryHandler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new PerCampusFormV2Options { Enabled = true });

    private static GetVisitHistoryDetailQueryHandler HistoryDetailHandler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new PerCampusFormV2Options { Enabled = true });

    /// <summary>
    /// Moves a campus straight to "confirmed contact, nothing pending" — the precondition C3-3/C3-5
    /// need (a settled contact A about to be replaced) without going through a real accept/token
    /// flow, which is not what either test is about. Written directly, the same way
    /// DriveToBeforeVisitAsync above advances status for a precondition that is not the test's
    /// subject.
    /// </summary>
    private static async Task ForceConfirmedNoPendingAsync(ulong instanceId, ulong contactUserId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET operational_contact_user_id = {0}, "
            + "operational_contact_confirmed_at = {1}, "
            + "operational_contact_confirmation_source = 'REGISTRANT_SELF_MATCH', "
            + "status = 'WAITING_REQUEST_APPROVAL' WHERE visit_instance_id = {2}",
            contactUserId, Now, instanceId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_request_identity_change_events WHERE visit_instance_id = {0}", instanceId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_request_identity_changes WHERE visit_instance_id = {0}", instanceId);
    }

    /// <summary>C3-1. A metadata-only correction must appear in history with the fields that moved.</summary>
    [Fact]
    public async Task C3_1_Profile_update_is_visible_in_history_with_before_after()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);
            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);

            var mail = new FakeEmail();
            await using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail,
                        phone: "+84900000002", jobTitle: "Trưởng phòng mới"),
                    CancellationToken.None);

            using var read = NewContext();
            var auditRow = await read.AuditLogs.AsNoTracking()
                .Where(a => a.VisitRequestId == requestId
                            && a.Action == OperationalContactHistoryAudit.ProfileUpdated)
                .FirstAsync();
            Assert.Equal(before.InstanceId, auditRow.VisitInstanceId);
            Assert.NotNull(auditRow.CampusId);

            var result = await HistoryHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var entry = Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactProfileUpdated);
            Assert.NotNull(entry.EventId);

            // Never a form revision — it corrects who is asked, not what is being asked.
            var afterDetail = await DetailAsync(before.InstanceId);
            Assert.Equal(detail.FormRevision, afterDetail.FormRevision);
            var afterCampus = await CampusStateAsync(requestId);
            Assert.Equal(before.Status, afterCampus.Status);
            Assert.Equal(before.ContactUserId, afterCampus.ContactUserId);

            var drawer = await HistoryDetailHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitHistoryDetailQuery(requestId, entry.EventId!), CancellationToken.None);
            var phoneField = Assert.Single(drawer.FieldChanges, f => f.FieldCode == "contactPhone");
            Assert.Equal(detail.OperationalContactPhone, phoneField.BeforeValue);
            Assert.Equal("+84900000002", phoneField.AfterValue);
            var jobField = Assert.Single(drawer.FieldChanges, f => f.FieldCode == "contactJobTitle");
            Assert.Equal(detail.OperationalContactJobTitle, jobField.BeforeValue);
            Assert.Equal("Trưởng phòng mới", jobField.AfterValue);
            // Only the two fields that actually changed — full name/organization were resubmitted
            // unchanged and must not appear as a "change" from themselves to themselves.
            Assert.Equal(2, drawer.FieldChanges.Count);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>C3-2. The existing no-op refusal is unchanged, and refusing means no history row.</summary>
    [Fact]
    public async Task C3_2_Profile_update_no_op_creates_no_history_event()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);
            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);

            var mail = new FakeEmail();
            await using (var db = NewContext())
                await Assert.ThrowsAsync<BusinessRuleException>(() => Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail), CancellationToken.None));

            using var read = NewContext();
            Assert.False(await read.AuditLogs.AnyAsync(a => a.VisitRequestId == requestId
                && a.Action == OperationalContactHistoryAudit.ProfileUpdated));

            var result = await HistoryHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactProfileUpdated);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// C3-3. Replacing a SETTLED contact (no pending invitation in flight) with an external address:
    /// exactly one business event for the new invitation, the OPERATIONAL_CONTACT_REPLACED audit
    /// exists (scoped, per Fix Group D item 9) but is never itself surfaced for this outcome.
    /// </summary>
    [Fact]
    public async Task C3_3_External_replace_produces_exactly_one_invitation_event_no_duplicate()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string initialEmail, externalEmail;
            ulong initialUserId;
            using (var db = NewContext())
            {
                (initialUserId, initialEmail) = await VisitorUserAsync(db);
                (_, externalEmail) = await SuccessorUserAsync(db);
            }
            requestId = await CreateAsync(Campus("HN", initialEmail));
            var before = await CampusStateAsync(requestId);
            await ForceConfirmedNoPendingAsync(before.InstanceId, initialUserId);
            var detail = await DetailAsync(before.InstanceId);

            var mail = new FakeEmail();
            await using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail, email: externalEmail),
                    CancellationToken.None);

            var afterCampus = await CampusStateAsync(requestId);
            Assert.Null(afterCampus.ContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, afterCampus.Status);
            var invitation = Assert.Single(await ChangesAsync(requestId));
            Assert.Equal(IdentityChangeStatuses.Pending, invitation.Status);
            Assert.Equal(detail.FormRevision, (await DetailAsync(before.InstanceId)).FormRevision);

            using var read = NewContext();
            var replacedAudit = await read.AuditLogs.AsNoTracking()
                .Where(a => a.VisitRequestId == requestId && a.Action == OperationalContactHistoryAudit.Replaced)
                .FirstAsync();
            Assert.Equal(before.InstanceId, replacedAudit.VisitInstanceId); // Fix Group D item 9
            Assert.NotNull(replacedAudit.CampusId);

            var result = await HistoryHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactReplacedWithRegistrant);
            var created = Assert.Single(result.Entries, e =>
                e.EventCode == VisitHistoryEventCodes.ContactInitialConfirmationCreated
                && e.VisitInstanceId == before.InstanceId);
            Assert.NotNull(created.EventId);

            // A guessed id for the un-surfaced REPLACED audit must 404 like anything out of scope.
            var guessedId = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, replacedAudit.AuditLogId);
            await Assert.ThrowsAsync<NotFoundException>(() => HistoryDetailHandler(read, new FakeUser(Registrant))
                .Handle(new GetVisitHistoryDetailQuery(requestId, guessedId), CancellationToken.None));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// C3-4. Replacing a contact that STILL has a pending invitation: the old invitation is
    /// superseded and a new one created — each exactly once, two distinct events.
    /// </summary>
    [Fact]
    public async Task C3_4_External_replace_with_existing_pending_invitation_supersedes_then_creates_once_each()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string initialEmail, externalEmail;
            using (var db = NewContext())
            {
                (_, initialEmail) = await VisitorUserAsync(db);
                (_, externalEmail) = await SuccessorUserAsync(db);
            }
            requestId = await CreateAsync(Campus("HN", initialEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);
            var originalInvitation = Assert.Single(await ChangesAsync(requestId));

            var mail = new FakeEmail();
            await using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail, email: externalEmail),
                    CancellationToken.None);

            var afterChanges = await ChangesAsync(requestId);
            Assert.Equal(2, afterChanges.Count);
            Assert.Equal(IdentityChangeStatuses.Superseded,
                afterChanges.Single(c => c.IdentityChangeId == originalInvitation.IdentityChangeId).Status);
            var newInvitation = Assert.Single(afterChanges, c => c.IdentityChangeId != originalInvitation.IdentityChangeId);
            Assert.Equal(IdentityChangeStatuses.Pending, newInvitation.Status);

            using var read = NewContext();
            var result = await HistoryHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var superseded = Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactInvitationSuperseded);
            // Two CREATED events legitimately exist — the request's own original invitation (at
            // submit time) and this replace's new one — each for its own IdentityChangeId, and each
            // exactly once. The assertion is on the NEW one specifically, not merely "one exists".
            var newEventRow = await read.VisitRequestIdentityChangeEvents.AsNoTracking()
                .Where(e => e.IdentityChangeId == newInvitation.IdentityChangeId
                            && e.EventType == "OPERATIONAL_CONTACT_INVITATION_CREATED")
                .FirstAsync();
            var expectedNewEventId = VisitHistoryEventSources.Build(
                VisitHistoryEventSources.IdentityChange, newEventRow.IdentityChangeEventId);
            var created = Assert.Single(result.Entries, e =>
                e.EventCode == VisitHistoryEventCodes.ContactInitialConfirmationCreated
                && e.EventId == expectedNewEventId);
            Assert.NotEqual(superseded.EventId, created.EventId);
            Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactReplacedWithRegistrant);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// C3-5. Replacing a settled contact with the registrant's OWN verified address: linked
    /// immediately, no invitation of any kind, and exactly one history event represents it.
    /// </summary>
    [Fact]
    public async Task C3_5_Self_match_replacement_produces_exactly_one_history_event_no_invitation()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string externalEmail;
            ulong externalUserId;
            using (var db = NewContext()) (externalUserId, externalEmail) = await VisitorUserAsync(db);
            requestId = await CreateAsync(Campus("HN", externalEmail));
            var before = await CampusStateAsync(requestId);
            await ForceConfirmedNoPendingAsync(before.InstanceId, externalUserId);
            var detail = await DetailAsync(before.InstanceId);

            var registrantEmail = V2SeedActor.Email(Registrant);
            var mail = new FakeEmail();
            await using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail, email: registrantEmail),
                    CancellationToken.None);

            var afterCampus = await CampusStateAsync(requestId);
            Assert.Equal(Registrant, afterCampus.ContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, afterCampus.Status);
            Assert.Empty(await ChangesAsync(requestId)); // no invitation of any kind was created
            Assert.Equal(detail.FormRevision, (await DetailAsync(before.InstanceId)).FormRevision);

            using var read = NewContext();
            var replacedAudit = await read.AuditLogs.AsNoTracking()
                .Where(a => a.VisitRequestId == requestId && a.Action == OperationalContactHistoryAudit.Replaced)
                .FirstAsync();
            var campusId = await read.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitInstanceId == before.InstanceId).Select(c => c.CampusId).FirstAsync();
            Assert.Equal(before.InstanceId, replacedAudit.VisitInstanceId);
            Assert.Equal(campusId, replacedAudit.CampusId);

            var result = await HistoryHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var contactEntries = result.Entries.Where(e => e.VisitInstanceId == before.InstanceId
                && (e.EventCode == VisitHistoryEventCodes.ContactReplacedWithRegistrant
                    || e.EventCode == VisitHistoryEventCodes.ContactIdentityChanged
                    || e.EventCode == VisitHistoryEventCodes.ContactInitialConfirmationCreated)).ToList();
            var entry = Assert.Single(contactEntries);
            Assert.Equal(VisitHistoryEventCodes.ContactReplacedWithRegistrant, entry.EventCode);

            var drawer = await HistoryDetailHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitHistoryDetailQuery(requestId, entry.EventId!), CancellationToken.None);
            Assert.Equal(VisitHistoryEventCodes.ContactReplacedWithRegistrant, drawer.EventCode);
            var emailField = Assert.Single(drawer.FieldChanges, f => f.FieldCode == "contactEmailMasked");
            Assert.NotNull(emailField.BeforeValue);
            Assert.NotNull(emailField.AfterValue);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// C3-6. A profile correction submitted while an invitation is still pending must not touch that
    /// invitation's lifecycle, and history must show the correction alone — never a fabricated resend
    /// or confirmation.
    /// </summary>
    [Fact]
    public async Task C3_6_Profile_update_while_invitation_pending_does_not_alter_invitation_lifecycle()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);
            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);
            var pendingBefore = Assert.Single(await ChangesAsync(requestId));

            var mail = new FakeEmail();
            await using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail, phone: "+84900000099"),
                    CancellationToken.None);

            var pendingAfter = Assert.Single(await ChangesAsync(requestId));
            Assert.Equal(pendingBefore.IdentityChangeId, pendingAfter.IdentityChangeId);
            Assert.Equal(pendingBefore.Status, pendingAfter.Status);
            Assert.Equal(pendingBefore.ExpiresAt, pendingAfter.ExpiresAt);
            Assert.Equal(pendingBefore.TokenVersion, pendingAfter.TokenVersion);
            Assert.Equal(pendingBefore.ResendCount, pendingAfter.ResendCount);

            using var read = NewContext();
            var result = await HistoryHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var forInstance = result.Entries.Where(e => e.VisitInstanceId == before.InstanceId).ToList();
            Assert.Contains(forInstance, e => e.EventCode == VisitHistoryEventCodes.ContactProfileUpdated);
            Assert.DoesNotContain(forInstance, e => e.EventCode == VisitHistoryEventCodes.ContactInvitationResent);
            Assert.DoesNotContain(forInstance, e => e.EventCode == VisitHistoryEventCodes.ContactConfirmed);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Commit 3 semantic-fix patch — Reinvite vs Invitation Created vs Resend ─────────────────────
    //
    // ReinviteOperationalContactConfirmationCommandHandler used to write EventType =
    // OPERATIONAL_CONTACT_INVITATION_CREATED (the same string a brand-new contact's first invitation
    // uses), even though its own AuditLog already used the distinct OPERATIONAL_CONTACT_REINVITED.
    // Reading the handler proved the lifecycle really IS different from both a fresh invitation
    // (Replace/submit: a different contact) and a resend (Resend: the SAME VisitRequestIdentityChange
    // row, TokenVersion bumped) — Reinvite only runs when NO pending row exists (the previous one
    // already ended terminally) and always creates a NEW row with TokenVersion reset to 1 and
    // ResendCount reset to 0. So the fix changed exactly one string in the writer to match its own
    // audit; every other mutation stays byte-for-byte identical to before this patch.

    /// <summary>C3-R1. The request's own original invitation reads as CREATED, never as reinvited.</summary>
    [Fact]
    public async Task C3_R1_Initial_invitation_produces_exactly_one_created_event()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);
            requestId = await CreateAsync(Campus("HN", contactEmail));

            using var read = NewContext();
            var result = await HistoryHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactInitialConfirmationCreated);
            Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactReinvited);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// C3-R2. A real Cancel (the original invitation lapses) followed by a real Reinvite: exactly one
    /// CONTACT_REINVITED event, and the reinvite itself adds no second CONTACT_INITIAL_CONFIRMATION_
    /// CREATED (the one that legitimately exists is the request's own original, from before the cancel).
    /// </summary>
    [Fact]
    public async Task C3_R2_Reinvite_after_a_lapsed_invitation_produces_exactly_one_reinvited_event()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);
            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var original = Assert.Single(await ChangesAsync(requestId));

            var mail = new FakeEmail();
            await using (var db = NewContext())
                await Cancel(db, Registrant, mail).Handle(
                    new CancelOperationalContactChangeCommand(requestId, before.InstanceId, "test cancel"),
                    CancellationToken.None);
            await using (var db = NewContext())
                await Reinvite(db, Registrant, mail).Handle(
                    new ReinviteOperationalContactConfirmationCommand(requestId, before.InstanceId),
                    CancellationToken.None);

            var afterChanges = await ChangesAsync(requestId);
            Assert.Equal(2, afterChanges.Count); // the cancelled original + the fresh reinvite row
            var reinvited = Assert.Single(afterChanges, c => c.IdentityChangeId != original.IdentityChangeId);
            Assert.Equal(IdentityChangeStatuses.Pending, reinvited.Status);

            using var read = NewContext();
            var result = await HistoryHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactReinvited);
            // Only the ORIGINAL create-time invitation is CREATED — the reinvite must not add a second one.
            Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactInitialConfirmationCreated);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>C3-R3. Resend keeps its own distinct code and is never confused with reinvite.</summary>
    [Fact]
    public async Task C3_R3_Resend_produces_the_existing_resent_event_not_reinvited()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);
            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var pendingBefore = Assert.Single(await ChangesAsync(requestId));

            var mail = new FakeEmail();
            await using (var db = NewContext())
                await Resend(db, Registrant, mail).Handle(
                    new ResendOperationalContactConfirmationCommand(requestId, before.InstanceId),
                    CancellationToken.None);

            var pendingAfter = Assert.Single(await ChangesAsync(requestId));
            Assert.Equal(pendingBefore.IdentityChangeId, pendingAfter.IdentityChangeId); // SAME row — never a new one
            Assert.Equal(pendingBefore.TokenVersion + 1, pendingAfter.TokenVersion);
            Assert.Equal(pendingBefore.ResendCount + 1, pendingAfter.ResendCount);

            using var read = NewContext();
            var result = await HistoryHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactInvitationResent);
            Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactReinvited);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>C3-R4. External replacement of a settled contact still reads as a fresh invitation, never a reinvite.</summary>
    [Fact]
    public async Task C3_R4_External_replacement_is_never_classified_as_reinvite()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string initialEmail, externalEmail;
            ulong initialUserId;
            using (var db = NewContext())
            {
                (initialUserId, initialEmail) = await VisitorUserAsync(db);
                (_, externalEmail) = await SuccessorUserAsync(db);
            }
            requestId = await CreateAsync(Campus("HN", initialEmail));
            var before = await CampusStateAsync(requestId);
            await ForceConfirmedNoPendingAsync(before.InstanceId, initialUserId);
            var detail = await DetailAsync(before.InstanceId);

            var mail = new FakeEmail();
            await using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail, email: externalEmail),
                    CancellationToken.None);

            using var read = NewContext();
            var result = await HistoryHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactInitialConfirmationCreated
                && e.VisitInstanceId == before.InstanceId);
            Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactReinvited);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>C3-R5. Superseding a live pending invitation with a replacement never fabricates a reinvite.</summary>
    [Fact]
    public async Task C3_R5_Supersede_then_replace_is_never_classified_as_reinvite()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string initialEmail, externalEmail;
            using (var db = NewContext())
            {
                (_, initialEmail) = await VisitorUserAsync(db);
                (_, externalEmail) = await SuccessorUserAsync(db);
            }
            requestId = await CreateAsync(Campus("HN", initialEmail));
            var before = await CampusStateAsync(requestId);
            var detail = await DetailAsync(before.InstanceId);

            var mail = new FakeEmail();
            await using (var db = NewContext())
                await Save(db, Registrant, mail).Handle(
                    SaveOf(requestId, before.InstanceId, detail, email: externalEmail),
                    CancellationToken.None);

            using var read = NewContext();
            var result = await HistoryHandler(read, new FakeUser(Registrant)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactInvitationSuperseded);
            Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactReinvited);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>C3-R6. None of initial/resend/reinvite ever bump FormRevision.</summary>
    [Fact]
    public async Task C3_R6_Initial_resend_and_reinvite_never_bump_formrevision()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);
            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var revisionAtCreate = (await DetailAsync(before.InstanceId)).FormRevision;

            var mail = new FakeEmail();
            await using (var db = NewContext())
                await Resend(db, Registrant, mail).Handle(
                    new ResendOperationalContactConfirmationCommand(requestId, before.InstanceId),
                    CancellationToken.None);
            Assert.Equal(revisionAtCreate, (await DetailAsync(before.InstanceId)).FormRevision);

            await using (var db = NewContext())
                await Cancel(db, Registrant, mail).Handle(
                    new CancelOperationalContactChangeCommand(requestId, before.InstanceId, null),
                    CancellationToken.None);
            await using (var db = NewContext())
                await Reinvite(db, Registrant, mail).Handle(
                    new ReinviteOperationalContactConfirmationCommand(requestId, before.InstanceId),
                    CancellationToken.None);
            Assert.Equal(revisionAtCreate, (await DetailAsync(before.InstanceId)).FormRevision);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// C3-R7. Proves this patch changed EventType ONLY: the reinvited row's own lifecycle fields
    /// match exactly what ReinviteOperationalContactConfirmationCommandHandler's doc comment already
    /// claimed before this patch (new row, TokenVersion 1, ResendCount 0, same address).
    /// </summary>
    [Fact]
    public async Task C3_R7_Reinvite_identity_lifecycle_fields_match_expected_behavior()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext()) (_, contactEmail) = await VisitorUserAsync(db);
            requestId = await CreateAsync(Campus("HN", contactEmail));
            var before = await CampusStateAsync(requestId);
            var original = Assert.Single(await ChangesAsync(requestId));

            var mail = new FakeEmail();
            await using (var db = NewContext())
                await Cancel(db, Registrant, mail).Handle(
                    new CancelOperationalContactChangeCommand(requestId, before.InstanceId, null),
                    CancellationToken.None);
            await using (var db = NewContext())
                await Reinvite(db, Registrant, mail).Handle(
                    new ReinviteOperationalContactConfirmationCommand(requestId, before.InstanceId),
                    CancellationToken.None);

            var afterChanges = await ChangesAsync(requestId);
            Assert.Equal(2, afterChanges.Count);
            var reinvited = Assert.Single(afterChanges, c => c.IdentityChangeId != original.IdentityChangeId);
            Assert.Equal(IdentityChangeKinds.InitialConfirmation, reinvited.ChangeKind);
            Assert.Equal(IdentityChangeStatuses.Pending, reinvited.Status);
            Assert.Equal(1u, reinvited.TokenVersion);
            Assert.Equal(0u, reinvited.ResendCount);
            Assert.Equal(original.NewEmailNormalized, reinvited.NewEmailNormalized);
            Assert.Equal(original.NewEmailMasked, reinvited.NewEmailMasked);
            Assert.True(reinvited.ExpiresAt > Now);

            var campus = await CampusStateAsync(requestId);
            Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, campus.Status);
            Assert.Null(campus.ContactUserId);
        }
        finally { await CleanupAsync(requestId); }
    }
}
