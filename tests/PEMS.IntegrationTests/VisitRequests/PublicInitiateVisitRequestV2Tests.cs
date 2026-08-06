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
using PEMS.Application.Delegations.Commands.InitiateVisitRequestV2;
using PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequestV2;
using PEMS.Application.Emails.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Identity;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Public v2 OTP initiate → verify security round-trip (G-4A). Proves the snapshot binding: the request is
/// built from EXACTLY the form bound at initiate, never the verify-time form. Uses the REAL <see cref="OtpService"/>
/// primitive (so the OTP challenge is genuine) with a capturing email fake to read the issued code, a fake
/// provision (returns the seed VISITOR), and the shared create service — the whole verify path runs end to end.
/// Each test uses a UNIQUE registrant email so per-email OTP issue quotas never cross tests.
/// </summary>
public sealed class PublicInitiateVisitRequestV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");
    private const ulong SeedRegistrant = 8;
    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;
    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();

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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master + 08_up_pending_v2_forms.sql.");
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private sealed class NoMetadata : IRequestMetadataService
    {
        public string? IpAddress => null;
        public string? UserAgent => null;
    }

    /// <summary>
    /// Captures the raw OTP code the initiate handler emails so verify can use it. It reads the code from
    /// the rendered body as well as from the legacy typed method, so it keeps working either side of the
    /// migration that moves OTP content into <c>email_templates</c>.
    /// </summary>
    private sealed class CapturingEmail : IEmailService
    {
        public string? LastCode { get; private set; }

        public Task<EmailDeliveryResult> TrySendAsync(OutboundEmail message, CancellationToken ct = default)
        { Capture(message); return Task.FromResult(EmailDeliveryResult.Sent()); }
        public Task SendAsync(OutboundEmail message, CancellationToken ct = default)
        { Capture(message); return Task.CompletedTask; }

        private void Capture(OutboundEmail message)
        {
            var code = OtpFromBody(message.Body);
            if (code is not null) LastCode = code;
        }

        internal static string? OtpFromBody(string? body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            var text = Regex.Replace(body, "<[^>]+>", " ");
            var m = Regex.Match(text, @"(?<!\d)(\d{6})(?!\d)");
            return m.Success ? m.Groups[1].Value : null;
        }

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.CompletedTask;
        public Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.FromResult(EmailDeliveryResult.Sent());
    }

    private sealed class FakeProvision : IUserProvisionService
    {
        public Task<ulong> EnsureVisitorAccountAsync(string email, string fullName, string? phone, DateTime utcNow, CancellationToken ct = default)
            => Task.FromResult(SeedRegistrant);
        public Task ValidateContactEmailCanBeUsedForVisitorAsync(string email, CancellationToken ct = default) => Task.CompletedTask;
        public Task ValidateRegistrantEmailUsableForPublicFlowAsync(string email, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoopNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType, string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// The REAL dispatcher and the REAL renderer, with the capturing fake standing in only for SMTP.
    /// The OTP therefore travels the same path it does in production — rendered from the seeded
    /// VISIT_REQUEST_OTP row — and the tests still read the code out of the produced message.
    /// </summary>
    private static SystemEmailDispatcher Dispatcher(ApplicationDbContext db, IEmailService sender)
        => new(db, new EmailTemplateRenderer(db), sender);

    private static InitiateVisitRequestV2CommandHandler InitiateHandler(ApplicationDbContext db, CapturingEmail email)
        => new(db, new OtpService(db, new FixedClock(), EmptyConfig), Dispatcher(db, email), new FakeProvision(),
            new NoMetadata(), new FixedClock(), EmptyConfig,
            new PerCampusFormV2Options { Enabled = true }, new PerCampusFormV2WriteOptions { Enabled = true });

    private static VerifyAndCreateVisitRequestV2CommandHandler VerifyHandler(ApplicationDbContext db)
        => new(db, new OtpService(db, new FixedClock(), EmptyConfig), new FakeProvision(),
            new VisitRequestV2CreateService(db), new NoopNotifications(),
            new CreateVisitRequestV2CommandTests.RecordingInvitationService(), new FixedClock(),
            NullLogger<VerifyAndCreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = true }, new PerCampusFormV2WriteOptions { Enabled = true });

    private static VisitRequestFormDataV2 Form(string submissionId, string email, string delegationName = "Đoàn Public V2", int durationMinutes = 30)
    {
        var start = Now.AddDays(20);
        var campus = new CampusVisitFormDto(
            "HN", start, start.AddMinutes(durationMinutes), delegationName, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(), // zero support — valid under v2
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null);
        return new VisitRequestFormDataV2(
            submissionId,
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", email),
            null, new List<CampusVisitFormDto> { campus });
    }

    private static string NewEmail() => $"initv2_{Guid.NewGuid():N}@example.com".ToLowerInvariant();

    [Fact]
    public async Task Initiate_flag_off_is_404_and_binds_no_snapshot()
    {
        RequireDb();
        using var db = NewContext();
        var submissionId = Guid.NewGuid().ToString("N");
        var handler = new InitiateVisitRequestV2CommandHandler(
            db, new OtpService(db, new FixedClock(), EmptyConfig), Dispatcher(db, new CapturingEmail()),
            new FakeProvision(), new NoMetadata(), new FixedClock(), EmptyConfig,
            new PerCampusFormV2Options { Enabled = true }, new PerCampusFormV2WriteOptions { Enabled = false });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new InitiateVisitRequestV2Command(Form(submissionId, NewEmail())), CancellationToken.None));

        Assert.False(await db.VisitRequestPendingForms.AnyAsync(p => p.SubmissionId == submissionId));
    }

    [Fact]
    public async Task Initiate_then_verify_creates_request_from_the_bound_snapshot()
    {
        RequireDb();
        var submissionId = Guid.NewGuid().ToString("N");
        var email = NewEmail();
        ulong createdId = 0;
        try
        {
            var capture = new CapturingEmail();
            string session;
            using (var db = NewContext())
            {
                var res = await InitiateHandler(db, capture)
                    .Handle(new InitiateVisitRequestV2Command(Form(submissionId, email, "Đoàn Ràng Buộc")), CancellationToken.None);
                session = res.SessionToken;
                Assert.False(string.IsNullOrWhiteSpace(session));
            }

            // Snapshot bound, not yet consumed.
            using (var db = NewContext())
            {
                var pending = await db.VisitRequestPendingForms.SingleAsync(p => p.SubmissionId == submissionId);
                Assert.Null(pending.ConsumedAt);
                Assert.Equal(64, pending.FingerprintV2.Length);
                // The snapshot round-trips faithfully (System.Text.Json escapes non-ASCII, so assert on the
                // DESERIALIZED value, not the raw JSON substring).
                var boundBack = V2PendingFormSnapshot.Deserialize(pending.SnapshotJson);
                Assert.Equal("Đoàn Ràng Buộc", boundBack.CampusVisits[0].DelegationName);
            }

            // Verify with the captured code → creates the request from the BOUND snapshot.
            using (var db = NewContext())
            {
                var cmd = new VerifyAndCreateVisitRequestV2Command(
                    Form(submissionId, email, "Đoàn Ràng Buộc"), capture.LastCode!, session);
                var result = await VerifyHandler(db).Handle(cmd, CancellationToken.None);
                Assert.False(result.Idempotent);
                createdId = result.VisitRequestId;
                Assert.NotEqual(0ul, createdId);
            }

            using (var db = NewContext())
            {
                Assert.Equal(1, await db.VisitRequests.CountAsync(v => v.SubmissionId == submissionId));
                var pending = await db.VisitRequestPendingForms.SingleAsync(p => p.SubmissionId == submissionId);
                Assert.NotNull(pending.ConsumedAt); // consumed atomically with create
                var instance = await db.VisitRequestCampuses.FirstAsync(c => c.VisitRequestId == createdId);
                var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == instance.VisitInstanceId);
                Assert.Equal("Đoàn Ràng Buộc", detail.DelegationName);
            }
        }
        finally { await Cleanup(submissionId, createdId); }
    }

    [Fact]
    public async Task Verify_with_a_tampered_form_is_rejected_and_creates_nothing()
    {
        RequireDb();
        var submissionId = Guid.NewGuid().ToString("N");
        var email = NewEmail();
        try
        {
            var capture = new CapturingEmail();
            string session;
            using (var db = NewContext())
            {
                var res = await InitiateHandler(db, capture)
                    .Handle(new InitiateVisitRequestV2Command(Form(submissionId, email, "Đoàn GỐC")), CancellationToken.None);
                session = res.SessionToken;
            }

            // A VALID code but a form whose core content changed after initiate → stable conflict, no create.
            using (var db = NewContext())
            {
                var tampered = new VerifyAndCreateVisitRequestV2Command(
                    Form(submissionId, email, "Đoàn ĐÃ SỬA"), capture.LastCode!, session);
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    VerifyHandler(db).Handle(tampered, CancellationToken.None));
                Assert.Equal(InitiateVisitRequestV2ErrorCodes.SubmissionFormMismatch, ex.ErrorCode);
            }

            using (var db = NewContext())
            {
                Assert.Equal(0, await db.VisitRequests.CountAsync(v => v.SubmissionId == submissionId));
                var pending = await db.VisitRequestPendingForms.SingleAsync(p => p.SubmissionId == submissionId);
                Assert.Null(pending.ConsumedAt); // untouched — the snapshot can still create the CORRECT request
            }
        }
        finally { await Cleanup(submissionId, 0); }
    }

    [Fact]
    public async Task Verify_without_an_initiated_snapshot_is_rejected()
    {
        RequireDb();
        var submissionId = Guid.NewGuid().ToString("N");
        var email = NewEmail();
        try
        {
            // A genuine OTP challenge but NO pending snapshot (initiate-v2 never ran).
            string session, code;
            using (var db = NewContext())
            {
                var issue = await new OtpService(db, new FixedClock(), EmptyConfig).CreateChallengeAsync(
                    email, OtpPurposes.VisitRequestVerify, submissionId, OtpIssueReasons.Initial, null, null, CancellationToken.None);
                session = issue.SessionToken;
                code = issue.Code;
            }

            using (var db = NewContext())
            {
                var cmd = new VerifyAndCreateVisitRequestV2Command(Form(submissionId, email), code, session);
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    VerifyHandler(db).Handle(cmd, CancellationToken.None));
                Assert.Equal(InitiateVisitRequestV2ErrorCodes.PendingSubmissionNotFound, ex.ErrorCode);
            }

            using (var db = NewContext())
                Assert.Equal(0, await db.VisitRequests.CountAsync(v => v.SubmissionId == submissionId));
        }
        finally { await Cleanup(submissionId, 0); }
    }

    private static async Task Cleanup(string submissionId, ulong createdId)
    {
        using var db = NewContext();
        if (createdId != 0)
        {
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
        await db.Database.ExecuteSqlRawAsync("DELETE FROM visit_request_pending_forms WHERE submission_id = {0}", submissionId);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM otp_tokens WHERE submission_id = {0}", submissionId);
    }
}
