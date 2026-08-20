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
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequestV2;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Public OTP create-v2 (B-2.5) tests. Covers the NEW logic the handler adds on top of the proven OTP
/// primitive: the two feature-flag gates (which short-circuit before any OTP verify or DB write) and the
/// pre-OTP idempotent replay (a retry of an already-committed submission returns the existing request WITHOUT
/// consulting the OTP). The <see cref="ThrowingOtp"/> fake fails the test if the OTP is ever consulted on those
/// paths. The live "valid/invalid/expired/used code" matrix is the unchanged v1 <c>IOtpService</c> primitive,
/// reused verbatim; the aggregate-creation semantics are covered by <c>CreateVisitRequestV2ServiceTests</c>.
/// </summary>
public sealed class VerifyAndCreateVisitRequestV2CommandTests
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

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    /// <summary>Fails loudly if any OTP method is consulted — proves the gate/replay paths never touch OTP.</summary>
    private sealed class ThrowingOtp : IOtpService
    {
        // Plain settings, not decisions — reading them proves nothing about this path.
        public int CodeMinutes => 15;
        public int VisitRequestCodeMinutes => 5;

        public Task<string> CreateAsync(User user, string purpose, string? ipAddress, string? userAgent, CancellationToken ct = default)
            => throw new InvalidOperationException("OTP must not be consulted on this path.");
        public Task<string> CreateForEmailAsync(string email, string purpose, string? ipAddress, string? userAgent, CancellationToken ct = default)
            => throw new InvalidOperationException("OTP must not be consulted on this path.");
        public Task<OtpVerificationResult> VerifyAsync(string email, string purpose, string rawCode, CancellationToken ct = default)
            => throw new InvalidOperationException("OTP must not be consulted on this path.");
        public Task<OtpChallengeIssue> CreateChallengeAsync(string email, string purpose, string submissionId, string issueReason, string? ipAddress, string? userAgent, CancellationToken ct = default)
            => throw new InvalidOperationException("OTP must not be consulted on this path.");
        public Task<OtpChallengeVerification> VerifyChallengeAsync(string sessionToken, string email, string purpose, string submissionId, string rawCode, CancellationToken ct = default)
            => throw new InvalidOperationException("OTP must not be consulted on this path.");
        public Task<OtpChallengeIssue> ResendChallengeAsync(string oldSessionToken, string purpose, string submissionId, string? ipAddress, string? userAgent, CancellationToken ct = default)
            => throw new InvalidOperationException("OTP must not be consulted on this path.");
        public Task<OtpChallengeIssue> RecoverChallengeAsync(string oldSessionToken, string purpose, string submissionId, string? ipAddress, string? userAgent, CancellationToken ct = default)
            => throw new InvalidOperationException("OTP must not be consulted on this path.");
    }

    /// <summary>Fails loudly if provisioning is attempted (no gate/replay path should provision).</summary>
    private sealed class ThrowingProvision : IUserProvisionService
    {
        public Task<ulong> EnsureVisitorAccountAsync(string email, string fullName, string? phone, string? nationality, DateTime utcNow, CancellationToken ct = default)
            => throw new InvalidOperationException("Provisioning must not happen on this path.");
        public Task ValidateContactEmailCanBeUsedForVisitorAsync(string email, CancellationToken ct = default)
            => throw new InvalidOperationException("Provisioning must not happen on this path.");
        public Task ValidateRegistrantEmailUsableForPublicFlowAsync(string email, CancellationToken ct = default)
            => throw new InvalidOperationException("Provisioning must not happen on this path.");
    }

    private sealed class NoopNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType, string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    private static VerifyAndCreateVisitRequestV2CommandHandler Handler(ApplicationDbContext db, bool read, bool write)
        => new(db, new ThrowingOtp(), new ThrowingProvision(), new VisitRequestV2CreateService(db),
            new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(), new FixedClock(),
            NullLogger<VerifyAndCreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = read }, new PerCampusFormV2WriteOptions { Enabled = write });

    private static VisitRequestFormDataV2 Form(string submissionId)
    {
        var start = Now.AddDays(20);
        var campus = new CampusVisitFormDto(
            "HN", start, start.AddMinutes(120), "Đoàn Public", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null);
        return new VisitRequestFormDataV2(
            submissionId,
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", "registrant@example.com"),
            null, new List<CampusVisitFormDto> { campus });
    }

    private static VerifyAndCreateVisitRequestV2Command Command(string submissionId)
        => new(Form(submissionId), OtpCode: "123456", SessionToken: "sess-" + submissionId);

    [Fact]
    public async Task Write_flag_off_is_404_without_touching_otp_or_db()
    {
        RequireDb();
        using var db = NewContext();
        var before = await db.VisitRequests.CountAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, read: true, write: false).Handle(Command(Guid.NewGuid().ToString("N")), CancellationToken.None));

        var after = await db.VisitRequests.CountAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Write_on_read_off_is_rejected_without_touching_otp_or_db()
    {
        RequireDb();
        using var db = NewContext();
        var before = await db.VisitRequests.CountAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, read: false, write: true).Handle(Command(Guid.NewGuid().ToString("N")), CancellationToken.None));
        Assert.Equal(CreateVisitRequestV2ErrorCodes.ReadRequired, ex.ErrorCode);

        var after = await db.VisitRequests.CountAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Retry_of_committed_submission_replays_without_verifying_otp()
    {
        RequireDb();
        var submissionId = Guid.NewGuid().ToString("N"); // 32 chars — fits submission_id (varchar(36))
        ulong createdId = 0;
        try
        {
            // Seed a committed v2 request for this submissionId directly through the shared create service.
            using (var db = NewContext())
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                var created = await new VisitRequestV2CreateService(db)
                    .CreateV2Async(Form(submissionId), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
                await tx.CommitAsync();
                createdId = created.VisitRequestId;
            }

            // The public verify handler must replay it (idempotent) WITHOUT consulting the OTP (ThrowingOtp).
            using (var db = NewContext())
            {
                var replay = await Handler(db, read: true, write: true)
                    .Handle(Command(submissionId), CancellationToken.None);
                Assert.True(replay.Idempotent);
                Assert.Equal(createdId, replay.VisitRequestId);
                Assert.NotEmpty(replay.Instances);

                // The receipt (plan §15). A replay must carry the SAME facts as the original create:
                // the client that lost the first response has nothing else to rebuild its success
                // screen from, and a blank status or timestamp would leave it unable to say where
                // the request stands.
                Assert.False(string.IsNullOrWhiteSpace(replay.RequestCode));
                Assert.False(string.IsNullOrWhiteSpace(replay.Status));
                Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$", replay.SubmittedAt);
                Assert.Equal(replay.Instances.Count, replay.CampusCount);
            }

            using (var db = NewContext())
            {
                var count = await db.VisitRequests.CountAsync(v => v.SubmissionId == submissionId);
                Assert.Equal(1, count); // no duplicate
            }
        }
        finally
        {
            if (createdId != 0)
            {
                using var db = NewContext();
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
