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
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Identity;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using PEMS.Shared;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The DELEGATED authenticated submission (plan §5.4, §8.2/§8.3): an internal user fills the form in on
/// behalf of somebody else, so the JWT proves nothing about the registrant and the OTP sent to the ENTERED
/// registrant address is the only thing that can. Mechanically this is the same initiate → verify pair the
/// public visitor submit uses (one implementation, no second OTP stack), so what these tests pin is the part
/// that is specific to delegation:
///   • a delegated submission may not carry an internal processing intent — it is REJECTED, not silently
///     dropped, so the caller cannot believe a self-host applied;
///   • no OTP is minted and no snapshot is bound when that rejection fires;
///   • after a correct OTP the request exists with NO host and every campus routed to its Staff Leader.
/// Snapshot binding / replay / mismatch mechanics are covered by <see cref="PublicInitiateVisitRequestV2Tests"/>.
/// </summary>
public sealed class AuthenticatedDelegatedOtpV2Tests
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
        public int SendCount { get; private set; }

        public Task<EmailDeliveryResult> TrySendAsync(OutboundEmail message, CancellationToken ct = default)
        { Capture(message); return Task.FromResult(EmailDeliveryResult.Sent()); }
        public Task SendAsync(OutboundEmail message, CancellationToken ct = default)
        { Capture(message); return Task.CompletedTask; }

        private void Capture(OutboundEmail message)
        {
            var text = Regex.Replace(message.Body ?? string.Empty, "<[^>]+>", " ");
            var m = Regex.Match(text, @"(?<!\d)(\d{6})(?!\d)");
            if (!m.Success) return;
            LastCode = m.Groups[1].Value;
            SendCount++;
        }

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.CompletedTask;
        public Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.FromResult(EmailDeliveryResult.Sent());
    }

    private sealed class FakeProvision : IUserProvisionService
    {
        public Task<ulong> EnsureVisitorAccountAsync(string email, string fullName, string? phone, string? nationality, DateTime utcNow, CancellationToken ct = default)
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

    /// <summary>A delegated form: the registrant is somebody other than whoever is typing it in.</summary>
    private static VisitRequestFormDataV2 Form(
        string submissionId,
        string registrantEmail,
        CampusHostSelectionV2Dto? processing = null,
        string delegationName = "Đoàn Delegated V2")
    {
        var start = Now.AddDays(20);
        var campus = new CampusVisitFormDto(
            "HN", start, start.AddHours(2), delegationName, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, processing);
        return new VisitRequestFormDataV2(
            submissionId,
            new RegistrantInputV2("Delegated Registrant", "VN", "Org", "Job", "+8491", registrantEmail),
            null, new List<CampusVisitFormDto> { campus });
    }

    private static string NewEmail() => $"delegatedv2_{Guid.NewGuid():N}@example.com".ToLowerInvariant();

    // ── A delegated submission may not name a reception host ───────────────────────
    //
    // Proposing a host is a right that comes from being internal staff of that campus. The person
    // these submissions register on behalf of is not, so a payload that names anybody is REFUSED
    // rather than silently downgraded — a forged payload must be distinguishable from a clean one.

    [Theory]
    [InlineData(HostSelectionModes.Self, null)]
    [InlineData(HostSelectionModes.Selected, 9UL)]
    [InlineData(HostSelectionModes.WaitForLater, 9UL)] // host smuggled under the harmless mode
    public async Task Initiate_rejects_a_host_proposal_without_sending_an_otp(
        string mode, ulong? proposedHostUserId)
    {
        RequireDb();
        using var db = NewContext();
        var submissionId = Guid.NewGuid().ToString("N");
        var email = NewEmail();
        var mail = new CapturingEmail();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            InitiateHandler(db, mail).Handle(
                new InitiateVisitRequestV2Command(
                    Form(submissionId, email, new CampusHostSelectionV2Dto(mode, proposedHostUserId))),
                CancellationToken.None));

        // WAIT_FOR_LATER carrying a host is a contradiction before it is a permission question, so
        // it answers with the shape code; the other two answer with the role code.
        Assert.Contains(ex.ErrorCode, new[]
        {
            VisitRequestErrorCodes.ProposedHostNotAllowedForRole,
            VisitRequestErrorCodes.InvalidHostSelectionMode,
        });
        // Refused BEFORE the OTP primitive and BEFORE the snapshot binding — nothing to clean up, and no
        // mail lands in a third party's inbox because of a forged payload.
        Assert.Equal(0, mail.SendCount);
        Assert.Null(mail.LastCode);
        Assert.False(await db.VisitRequestPendingForms.AnyAsync(p => p.SubmissionId == submissionId));
        Assert.False(await db.OtpTokens.AnyAsync(t => t.SubmissionId == submissionId));
    }

    [Fact]
    public async Task Initiate_allows_explicit_wait_for_later_and_sends_the_otp()
    {
        RequireDb();
        using var db = NewContext();
        var submissionId = Guid.NewGuid().ToString("N");
        var email = NewEmail();
        var mail = new CapturingEmail();

        var result = await InitiateHandler(db, mail).Handle(
            new InitiateVisitRequestV2Command(
                Form(submissionId, email, new CampusHostSelectionV2Dto(HostSelectionModes.WaitForLater, null))),
            CancellationToken.None);

        // WAIT_FOR_LATER with nobody named IS what a delegated submission always means — it asserts
        // nothing that delegation disallows.
        Assert.False(string.IsNullOrWhiteSpace(result.SessionToken));
        Assert.Equal(1, mail.SendCount);
        Assert.True(await db.VisitRequestPendingForms.AnyAsync(p => p.SubmissionId == submissionId));
    }

    [Fact]
    public async Task Verify_rejects_a_host_proposal_smuggled_in_after_a_clean_initiate()
    {
        RequireDb();
        using var db = NewContext();
        var submissionId = Guid.NewGuid().ToString("N");
        var email = NewEmail();
        var mail = new CapturingEmail();

        var issued = await InitiateHandler(db, mail).Handle(
            new InitiateVisitRequestV2Command(Form(submissionId, email)), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            VerifyHandler(db).Handle(
                new VerifyAndCreateVisitRequestV2Command(
                    Form(submissionId, email, new CampusHostSelectionV2Dto(HostSelectionModes.Self, null)),
                    mail.LastCode!, issued.SessionToken),
                CancellationToken.None));

        Assert.Equal(VisitRequestErrorCodes.ProposedHostNotAllowedForRole, ex.ErrorCode);
        // The OTP is NOT burned by a payload we refused to look at, and no request was created.
        Assert.False(await db.VisitRequests.AnyAsync(v => v.SubmissionId == submissionId));
        var pending = await db.VisitRequestPendingForms.AsNoTracking()
            .FirstAsync(p => p.SubmissionId == submissionId);
        Assert.Null(pending.ConsumedAt);
    }

    // ── The happy delegated path: OTP → request with NO host, routed to the Staff Leader ─

    [Fact]
    public async Task Verify_creates_one_request_with_no_host_and_every_campus_awaiting_the_staff_leader()
    {
        RequireDb();
        using var db = NewContext();
        var submissionId = Guid.NewGuid().ToString("N");
        var email = NewEmail();
        var mail = new CapturingEmail();

        var issued = await InitiateHandler(db, mail).Handle(
            new InitiateVisitRequestV2Command(Form(submissionId, email)), CancellationToken.None);

        var created = await VerifyHandler(db).Handle(
            new VerifyAndCreateVisitRequestV2Command(Form(submissionId, email), mail.LastCode!, issued.SessionToken),
            CancellationToken.None);

        try
        {
            Assert.False(created.Idempotent);

            var instances = await db.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitRequestId == created.VisitRequestId)
                .ToListAsync();

            Assert.NotEmpty(instances);
            // Delegation never auto-hosts: nobody has been made host and nothing was decided at submit time.
            Assert.All(instances, i =>
            {
                Assert.Null(i.CurrentHostUserId);
                Assert.Null(i.DecidedBy);
                // The contact here is not the registrant, so each campus starts by waiting for its own
                // operational contact to confirm — one step earlier than WAITING_REQUEST_APPROVAL.
                Assert.Equal(VisitInstanceStatus.WaitingContactConfirmation, i.Status);
            });
            Assert.False(await db.VisitParticipants.AnyAsync(
                p => instances.Select(i => i.VisitInstanceId).Contains(p.VisitInstanceId) && p.IsHost));
        }
        finally
        {
            await CleanupAsync(db, created.VisitRequestId);
        }
    }

    [Fact]
    public async Task Verify_replay_of_the_same_submission_does_not_create_a_second_request()
    {
        RequireDb();
        using var db = NewContext();
        var submissionId = Guid.NewGuid().ToString("N");
        var email = NewEmail();
        var mail = new CapturingEmail();

        var issued = await InitiateHandler(db, mail).Handle(
            new InitiateVisitRequestV2Command(Form(submissionId, email)), CancellationToken.None);

        var first = await VerifyHandler(db).Handle(
            new VerifyAndCreateVisitRequestV2Command(Form(submissionId, email), mail.LastCode!, issued.SessionToken),
            CancellationToken.None);

        try
        {
            var replay = await VerifyHandler(db).Handle(
                new VerifyAndCreateVisitRequestV2Command(Form(submissionId, email), mail.LastCode!, issued.SessionToken),
                CancellationToken.None);

            Assert.True(replay.Idempotent);
            Assert.Equal(first.VisitRequestId, replay.VisitRequestId);
            Assert.Equal(1, await db.VisitRequests.CountAsync(v => v.SubmissionId == submissionId));
        }
        finally
        {
            await CleanupAsync(db, first.VisitRequestId);
        }
    }

    /// <summary>Cascade-deletes a committed request so the shared disposable DB keeps its v2 count at 0.</summary>
    private static async Task CleanupAsync(ApplicationDbContext db, ulong visitRequestId)
    {
        var instanceIds = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == visitRequestId)
            .Select(c => c.VisitInstanceId)
            .ToListAsync();

        await db.VisitParticipants.Where(p => instanceIds.Contains(p.VisitInstanceId)).ExecuteDeleteAsync();
        await db.VisitRequestCampuses.Where(c => c.VisitRequestId == visitRequestId).ExecuteDeleteAsync();
        await db.VisitGuestMembers.Where(g => g.VisitRequestId == visitRequestId).ExecuteDeleteAsync();
        await db.VisitRequests.Where(v => v.VisitRequestId == visitRequestId).ExecuteDeleteAsync();
    }
}
