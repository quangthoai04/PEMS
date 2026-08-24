using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Create-v2 COMMAND tests (Phase B-2b): flag gating + idempotency on the handler that owns the transaction.
/// Flag-reject cases never touch the DB (asserted before any write). The idempotency case commits and then
/// cascade-deletes the request so <c>pems_pr3_test</c> keeps v2_requests = 0.
/// </summary>
public sealed class CreateVisitRequestV2CommandTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");
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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master to run these tests.");
    }

    // Seeded internal actors reused from CompleteVisitStageV2Tests: LeaderHn is the HN campus's own
    // Staff Leader, HostHn a regular IC Staff of the same campus — both ACTIVE in pems_pr3_test.
    private const ulong LeaderHn = 3;
    private const ulong HostHn = 101;
    private const ulong CampusHn = 1;

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(ulong id = Registrant, string roleCode = RoleCodes.Visitor,
            string? subRole = null, ulong? primaryCampusId = null)
        { UserId = id; RoleCode = roleCode; SubRole = subRole; PrimaryCampusId = primaryCampusId; }
        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; }
        public string? SubRole { get; }
        public ulong? PrimaryCampusId { get; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    /// <summary>Records dispatched notifications so tests can assert first-create-only (idempotent) behaviour.</summary>
    private sealed class RecordingNotifications : INotificationService
    {
        public List<ulong> Recipients { get; } = new();
        public int Batches { get; private set; }
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken cancellationToken)
        {
            Batches++;
            Recipients.AddRange(requests.Select(r => r.RecipientUserId));
            return Task.CompletedTask;
        }
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken cancellationToken)
            => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType,
            string? relatedType, ulong? relatedId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    /// <summary>
    /// Records confirmation-invitation sends. The forms in these tests name the registrant’s own
    /// verified address as every campus’s contact, so the create path self-matches and this recorder
    /// should stay empty — which is exactly what makes it worth asserting on.
    /// </summary>
    internal sealed class RecordingInvitationService : IOperationalContactInvitationService
    {
        public List<ulong> Invitations { get; } = new();
        // The two halves of the same act: callers mint inside their transaction and dispatch after it.
        // Recorded on the MINT, which is the moment an invitation becomes real.
        public Task<OperationalContactInvitationTokens?> MintInvitationTokensAsync(
            ulong identityChangeId, CancellationToken cancellationToken)
        {
            Invitations.Add(identityChangeId);
            return Task.FromResult<OperationalContactInvitationTokens?>(
                new OperationalContactInvitationTokens($"accept-{identityChangeId}", $"decline-{identityChangeId}"));
        }
        public Task DispatchInvitationEmailAsync(
            ulong identityChangeId, OperationalContactInvitationTokens tokens, CancellationToken cancellationToken)
            => Task.CompletedTask;
        public Task<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?> LockChangeAsync(
            ulong identityChangeId, CancellationToken cancellationToken)
            => Task.FromResult<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?>(null);
        public Task<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?> LockPendingChangeForInstanceAsync(
            ulong visitInstanceId, CancellationToken cancellationToken)
            => Task.FromResult<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?>(null);
    }

    private static CreateVisitRequestV2CommandHandler Handler(
        ApplicationDbContext db, bool read, bool write, INotificationService? notifications = null,
        ICurrentUserService? user = null)
        => new(db, user ?? new FakeUser(), new FixedClock(), new VisitRequestV2CreateService(db),
            notifications ?? new RecordingNotifications(),
            new RecordingInvitationService(), new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = read }, new PerCampusFormV2WriteOptions { Enabled = write },
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));

    private static VisitRequestFormDataV2 Form(string submissionId)
        => FormFor(Registrant, Now.AddDays(20), submissionId);

    /// <summary>
    /// Same shape as <see cref="Form"/>, but for an ARBITRARY actor/start — what the short-notice tests
    /// need to file as HostHn/LeaderHn at an offset under 72h, or as HostHn naming somebody else as
    /// registrant. <paramref name="registrantEmail"/> defaults to the ACTOR's own verified address (self
    /// registration); pass a different one to build a delegated-registrant payload.
    ///
    /// <para>
    /// The campus contact defaults to a THIRD address, never the registrant's: an internal actor may
    /// not appoint themself as their own campus's contact (<c>InternalRegistrantCannotBeContact</c>),
    /// so a self-match contact would fail every internal-actor fixture before the short-notice rule is
    /// even reached. <see cref="Form"/> keeps the old self-match shape for its Visitor fixtures, where
    /// self-matching IS legal.
    /// </para>
    /// </summary>
    private static VisitRequestFormDataV2 FormFor(
        ulong actorUserId, DateTime start, string submissionId,
        string? registrantEmail = null, string? contactEmail = null)
    {
        var email = registrantEmail ?? V2SeedActor.Email(actorUserId);
        var campus = new CampusVisitFormDto(
            "HN", start, start.AddMinutes(120), "Đoàn ABC", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "Org", "Trưởng phòng Hợp tác", "+8491", contactEmail ?? email),
            "EN", null, "DECLINED", null, null);
        return new VisitRequestFormDataV2(
            submissionId,
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", email),
            null, new List<CampusVisitFormDto> { campus });
    }

    /// <summary>Child-first delete so pems_pr3_test keeps v2_requests at its baseline count.</summary>
    private static async Task CleanupCreatedRequestAsync(ulong id)
    {
        if (id == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    [Fact]
    public async Task Write_flag_off_is_404_and_writes_nothing()
    {
        RequireDb();
        using var db = NewContext();
        var before = await db.VisitRequests.CountAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, read: true, write: false).Handle(new CreateVisitRequestV2Command(Form(Guid.NewGuid().ToString("N"))), CancellationToken.None));

        var after = await db.VisitRequests.CountAsync();
        Assert.Equal(before, after); // nothing created
    }

    [Fact]
    public async Task Write_on_read_off_is_rejected_and_writes_nothing()
    {
        RequireDb();
        using var db = NewContext();
        var before = await db.VisitRequests.CountAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, read: false, write: true).Handle(new CreateVisitRequestV2Command(Form(Guid.NewGuid().ToString("N"))), CancellationToken.None));
        Assert.Equal(CreateVisitRequestV2ErrorCodes.ReadRequired, ex.ErrorCode);

        var after = await db.VisitRequests.CountAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Idempotent_same_submission_returns_same_request_no_duplicate()
    {
        RequireDb();
        var submissionId = "IT-" + Guid.NewGuid().ToString("N");
        ulong createdId = 0;
        var notifications = new RecordingNotifications();
        try
        {
            using (var db = NewContext())
            {
                var first = await Handler(db, read: true, write: true, notifications)
                    .Handle(new CreateVisitRequestV2Command(Form(submissionId)), CancellationToken.None);
                createdId = first.VisitRequestId;
                Assert.False(first.Idempotent);
                Assert.Equal(FormSchemaVersions.PerCampus >= 2 ? "SINGLE_CAMPUS" : first.VisitScope, first.VisitScope);
            }
            // First create dispatched exactly one post-commit notification batch (the campus Staff Leader).
            Assert.Equal(1, notifications.Batches);
            Assert.NotEmpty(notifications.Recipients);
            using (var db = NewContext())
            {
                var second = await Handler(db, read: true, write: true, notifications)
                    .Handle(new CreateVisitRequestV2Command(Form(submissionId)), CancellationToken.None);
                Assert.True(second.Idempotent);
                Assert.Equal(createdId, second.VisitRequestId); // same request, not a duplicate
            }
            // The idempotent replay must NOT re-notify.
            Assert.Equal(1, notifications.Batches);
            using (var db = NewContext())
            {
                var count = await db.VisitRequests.CountAsync(v => v.SubmissionId == submissionId);
                Assert.Equal(1, count);
            }
        }
        finally
        {
            await CleanupCreatedRequestAsync(createdId);
        }
    }

    // ── Short-notice authorization (PEMS_INTERNAL_SELF_CREATE_SHORT_NOTICE_72H plan §7.2) ──
    // The Create SERVICE's own boundary matrix (CreateVisitRequestV2ServiceTests) already proves the
    // 72h floor is correctly gated by the capability flag; these prove the HANDLER computes that flag
    // correctly from the real actor role loaded from the DB — never from the request payload.

    [Fact]
    public async Task Staff_self_registration_may_create_inside_the_72h_floor()
    {
        RequireDb();
        var submissionId = Guid.NewGuid().ToString("N");
        ulong createdId = 0;
        try
        {
            using var db = NewContext();
            var start = Now.AddHours(24); // < 72h — only allowed for internal self-registration
            var result = await Handler(db, read: true, write: true,
                    user: new FakeUser(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn))
                .Handle(new CreateVisitRequestV2Command(
                    FormFor(HostHn, start, submissionId, contactEmail: "op-contact-sn1@example.com")),
                    CancellationToken.None);
            createdId = result.VisitRequestId;
            Assert.False(result.Idempotent);
        }
        finally
        {
            await CleanupCreatedRequestAsync(createdId);
        }
    }

    [Fact]
    public async Task Staff_leader_self_registration_may_create_inside_the_72h_floor()
    {
        RequireDb();
        var submissionId = Guid.NewGuid().ToString("N");
        ulong createdId = 0;
        try
        {
            using var db = NewContext();
            var start = Now.AddHours(24);
            var result = await Handler(db, read: true, write: true,
                    user: new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn))
                .Handle(new CreateVisitRequestV2Command(
                    FormFor(LeaderHn, start, submissionId, contactEmail: "op-contact-sn2@example.com")),
                    CancellationToken.None);
            createdId = result.VisitRequestId;
            Assert.False(result.Idempotent);
        }
        finally
        {
            await CleanupCreatedRequestAsync(createdId);
        }
    }

    [Fact]
    public async Task Visitor_self_registration_at_24h_is_still_refused()
    {
        RequireDb();
        using var db = NewContext();
        var before = await db.VisitRequests.CountAsync();

        var start = Now.AddHours(24);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Handler(db, read: true, write: true).Handle(
                new CreateVisitRequestV2Command(FormFor(Registrant, start, Guid.NewGuid().ToString("N"))),
                CancellationToken.None));
        Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);

        var after = await db.VisitRequests.CountAsync();
        Assert.Equal(before, after); // nothing created
    }

    /// <summary>
    /// A Staff account naming somebody ELSE as the registrant must be refused at the self-registration
    /// gate — same as today — before the short-notice capability is ever computed. This is the exact
    /// bypass the plan's design exists to rule out: the capability answers "is THIS actor an internal
    /// Staff/Staff Leader registering THEMSELF", never "is whoever is typing internal staff".
    /// </summary>
    [Fact]
    public async Task Internal_actor_naming_someone_else_as_registrant_gets_no_short_notice()
    {
        RequireDb();
        using var db = NewContext();
        var before = await db.VisitRequests.CountAsync();

        var start = Now.AddHours(24); // would only be legal if this were self-registration
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, read: true, write: true,
                    user: new FakeUser(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn))
                .Handle(new CreateVisitRequestV2Command(FormFor(
                        HostHn, start, Guid.NewGuid().ToString("N"),
                        registrantEmail: "someone-else@example.com")),
                    CancellationToken.None));
        Assert.Equal(VisitRequestErrorCodes.RegistrantEmailVerificationRequired, ex.ErrorCode);

        var after = await db.VisitRequests.CountAsync();
        Assert.Equal(before, after);
    }
}
