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

    private sealed class FakeUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId => Registrant;
        public string? Email => null;
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

    /// <summary>Records claim-invitation sends; the A==B forms in these tests never create a claim.</summary>
    internal sealed class RecordingClaimService : IVisitContactClaimService
    {
        public List<ulong> Invitations { get; } = new();
        public Task<string?> SendInvitationAsync(ulong identityChangeId, CancellationToken cancellationToken)
        {
            Invitations.Add(identityChangeId);
            return Task.FromResult<string?>(null);
        }
        public Task<string?> SendTransferInvitationAsync(ulong identityChangeId, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
        public Task<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?> LockClaimAsync(
            ulong identityChangeId, CancellationToken cancellationToken)
            => Task.FromResult<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?>(null);
        public Task<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?> LockPendingInitialClaimAsync(
            ulong visitRequestId, CancellationToken cancellationToken)
            => Task.FromResult<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?>(null);
        public Task<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?> LockPendingChangeAsync(
            ulong visitRequestId, string? changeKind, CancellationToken cancellationToken)
            => Task.FromResult<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?>(null);
    }

    private static CreateVisitRequestV2CommandHandler Handler(
        ApplicationDbContext db, bool read, bool write, INotificationService? notifications = null)
        => new(db, new FakeUser(), new FixedClock(), new VisitRequestV2CreateService(db),
            notifications ?? new RecordingNotifications(),
            new RecordingClaimService(), new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = read }, new PerCampusFormV2WriteOptions { Enabled = write },
            new VisitRequestAggregateStatusService(db));

    private static VisitRequestFormDataV2 Form(string submissionId)
    {
        var start = Now.AddDays(20);
        var campus = new CampusVisitFormDto(
            "HN", start, start.AddMinutes(120), "Đoàn ABC", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null, null);
        return new VisitRequestFormDataV2(
            submissionId,
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", "registrant@example.com"),
            new ContactPointDto("Registrant", "Org", "+8491", "registrant@example.com"), // A==B → ACTIVE
            null, new List<CampusVisitFormDto> { campus });
    }

    [Fact]
    public async Task Write_flag_off_is_404_and_writes_nothing()
    {
        RequireDb();
        using var db = NewContext();
        var before = await db.VisitRequests.CountAsync(v => v.FormSchemaVersion >= FormSchemaVersions.PerCampus);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, read: true, write: false).Handle(new CreateVisitRequestV2Command(Form(Guid.NewGuid().ToString("N"))), CancellationToken.None));

        var after = await db.VisitRequests.CountAsync(v => v.FormSchemaVersion >= FormSchemaVersions.PerCampus);
        Assert.Equal(before, after); // nothing created
    }

    [Fact]
    public async Task Write_on_read_off_is_rejected_and_writes_nothing()
    {
        RequireDb();
        using var db = NewContext();
        var before = await db.VisitRequests.CountAsync(v => v.FormSchemaVersion >= FormSchemaVersions.PerCampus);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, read: false, write: true).Handle(new CreateVisitRequestV2Command(Form(Guid.NewGuid().ToString("N"))), CancellationToken.None));
        Assert.Equal(CreateVisitRequestV2ErrorCodes.ReadRequired, ex.ErrorCode);

        var after = await db.VisitRequests.CountAsync(v => v.FormSchemaVersion >= FormSchemaVersions.PerCampus);
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
            if (createdId != 0)
            {
                using var db = NewContext();
                // Explicit child-first delete so pems_pr3_test keeps v2_requests = 0 (some FKs are RESTRICT).
                var id = createdId;
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
        }
    }
}
