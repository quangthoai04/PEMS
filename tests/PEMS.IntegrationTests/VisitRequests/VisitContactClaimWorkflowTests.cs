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
using PEMS.Application.Delegations.Services;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CancelVisitRequest;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.VisitContactClaim;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase D — primary-contact INITIAL_CLAIM workflow (plan §16.4/§16.7/§16.8) against pems_pr3_test.
/// Each test creates its own committed v2 request (contact ≠ registrant → PENDING claim), exercises the
/// claim handlers with their own transactions, and cascade-deletes everything in finally so the DB keeps
/// v2_requests = 0. Actors: registrant = user 8; invited contact B / bystander C = existing ACTIVE
/// VISITOR users looked up at run time.
/// </summary>
public sealed class VisitContactClaimWorkflowTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");
    private const ulong Registrant = 8;
    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;

    // ── Infrastructure fakes/helpers ──────────────────────────────────────────────

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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master + 06_up patch to run these tests.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        private readonly ulong _id;
        public FakeUser(ulong id) => _id = id;
        public bool IsAuthenticated => true;
        public ulong? UserId => _id;
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

    /// <summary>Records outbound mail; exposes the last claim link token embedded in a body.</summary>
    private sealed class FakeEmail : IEmailService
    {
        public List<(string To, string Subject, string Html)> Sent { get; } = new();
        public string? LastClaimToken
        {
            get
            {
                var html = Sent.LastOrDefault().Html;
                if (html is null) return null;
                var m = Regex.Match(html, @"visit-contact-claim/([A-Za-z0-9_\-]+)");
                return m.Success ? m.Groups[1].Value : null;
            }
        }
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
        {
            Sent.Add((toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
        public Task SendAsync(OutboundEmail message, CancellationToken ct = default) => Task.CompletedTask;
        public Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.FromResult(EmailDeliveryResult.Sent());
        public Task SendPasswordResetAsync(string toEmail, string fullName, string code, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendVisitRequestOtpAsync(string toEmail, string fullName, string code, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendVisitorAccountCreatedOrLinkedEmailAsync(string toEmail, string contactName, string delegationName, string requestCode, string visitScope, string plannedTimeText, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendRegistrantConfirmationAsync(string toEmail, string registrantName, string contactName, string contactEmail, string delegationName, string requestCode, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoopNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType, string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();
    private static EmailActionTokenService Tokens() => new(EmptyConfig);
    private static VisitContactClaimService ClaimSvc(ApplicationDbContext db, FakeEmail? email = null)
        => new(db, Tokens(), email ?? new FakeEmail(), new FixedClock(),
            NullLogger<VisitContactClaimService>.Instance, EmptyConfig);

    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static AcceptVisitContactClaimCommandHandler Accept(ApplicationDbContext db, ulong actor, bool write = true)
        => new(db, new FakeUser(actor), new FixedClock(), Tokens(), ClaimSvc(db),
            new PerCampusFormV2WriteOptions { Enabled = write });

    private static DeclineVisitContactClaimCommandHandler Decline(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor), new FixedClock(), Tokens(), ClaimSvc(db), WriteOn);

    private static ResendVisitContactClaimCommandHandler Resend(ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new FakeUser(actor), new FixedClock(), ClaimSvc(db, email), WriteOn);

    private static ReplacePendingVisitContactCommandHandler Replace(ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new FakeUser(actor), new FixedClock(), ClaimSvc(db, email), WriteOn);

    // ── Data helpers ──────────────────────────────────────────────────────────────

    private static async Task<(ulong UserId, string Email)> VisitorUserAsync(ApplicationDbContext db, params ulong[] exclude)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                        && u.UserId != Registrant && !exclude.Contains(u.UserId))
            .OrderBy(u => u.UserId)
            .Select(u => new { u.UserId, u.Email })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pems_pr3_test needs at least two ACTIVE VISITOR users besides user 8.");
        return (row.UserId, row.Email!);
    }

    private static VisitRequestFormDataV2 Form(string submissionId, string contactEmail)
    {
        var start = Now.AddDays(20);
        var campus = new CampusVisitFormDto(
            "HN", start, start.AddMinutes(120), "Đoàn Claim", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null, null);
        return new VisitRequestFormDataV2(
            submissionId,
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", "registrant@example.com"),
            new ContactPointDto("Contact B", "OrgB", "+8492", contactEmail), // ≠ registrant → INITIAL_CLAIM
            null, new List<CampusVisitFormDto> { campus });
    }

    /// <summary>Creates + commits a v2 request whose contact is <paramref name="contactEmail"/> and
    /// returns (requestId, pending claimId).</summary>
    private static async Task<(ulong RequestId, ulong ClaimId)> CreateWithClaimAsync(string contactEmail)
    {
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
            new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = true }, WriteOn,
            new VisitRequestAggregateStatusService(db));
        var created = await handler.Handle(
            new CreateVisitRequestV2Command(Form("CL" + Guid.NewGuid().ToString("N"), contactEmail)),
            CancellationToken.None);
        Assert.True(created.ContactClaimPending);
        Assert.Equal(PrimaryContactAccessStatuses.PendingConfirmation, created.PrimaryContactAccessStatus);

        var claimId = await db.VisitRequestIdentityChanges.AsNoTracking()
            .Where(c => c.VisitRequestId == created.VisitRequestId && c.Status == IdentityChangeStatuses.Pending)
            .Select(c => c.IdentityChangeId)
            .SingleAsync();
        return (created.VisitRequestId, claimId);
    }

    private static async Task<string> MintTokenAsync(ulong claimId, FakeEmail? email = null)
    {
        using var db = NewContext();
        var raw = await ClaimSvc(db, email).SendInvitationAsync(claimId, CancellationToken.None);
        Assert.NotNull(raw);
        return raw!;
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
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_links_invited_visitor_and_never_touches_campus_decisions()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactB; string emailB;
            using (var db = NewContext()) (contactB, emailB) = await VisitorUserAsync(db);
            (requestId, var claimId) = await CreateWithClaimAsync(emailB);
            var raw = await MintTokenAsync(claimId);

            using (var db = NewContext())
            {
                var res = await Accept(db, contactB).Handle(
                    new AcceptVisitContactClaimCommand(raw), CancellationToken.None);
                Assert.Equal(IdentityChangeStatuses.Applied, res.ClaimStatus);
                Assert.Equal(PrimaryContactAccessStatuses.Active, res.PrimaryContactAccessStatus);
            }

            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Equal(contactB, visit.VisitorUserId);
                Assert.Equal(PrimaryContactAccessStatuses.Active, visit.PrimaryContactAccessStatus);
                Assert.NotNull(visit.PrimaryContactVerifiedAt);
                Assert.Equal(1, visit.RowVersion); // create=0 → accept bumped once

                var claim = await db.VisitRequestIdentityChanges.AsNoTracking().SingleAsync(c => c.IdentityChangeId == claimId);
                Assert.Equal(IdentityChangeStatuses.Applied, claim.Status);
                Assert.Equal(contactB, claim.NewUserId);
                Assert.NotNull(claim.AppliedAt);

                // Campus decision state is untouched by the claim.
                var instance = await db.VisitRequestCampuses.AsNoTracking().SingleAsync(c => c.VisitRequestId == requestId);
                Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, instance.Status);

                var token = await db.EmailActionTokens.AsNoTracking()
                    .SingleAsync(t => t.TargetType == "VISIT_REQUEST_IDENTITY_CHANGE" && t.TargetId == claimId);
                Assert.NotNull(token.UsedAt);
                Assert.Equal("ACCEPT", token.UsedAction);

                Assert.True(await db.VisitRequestIdentityChangeEvents.AsNoTracking()
                    .AnyAsync(e => e.IdentityChangeId == claimId && e.EventType == "PRIMARY_CONTACT_CLAIM_APPLIED"));
                Assert.True(await db.AuditLogs.AsNoTracking()
                    .AnyAsync(a => a.VisitRequestId == requestId && a.Action == "PRIMARY_CONTACT_CLAIM_APPLIED"));
            }

            // Replay with the same (used) token → TokenInvalid, nothing changes.
            using (var db = NewContext())
            {
                ulong contactB2; using (var db2 = NewContext()) (contactB2, _) = await VisitorUserAsync(db2);
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Accept(db, contactB2).Handle(new AcceptVisitContactClaimCommand(raw), CancellationToken.None));
                Assert.Equal(VisitContactClaimErrorCodes.NotPending, ex.ErrorCode); // claim already APPLIED
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Accept_by_wrong_account_or_with_flag_off_is_rejected_and_writes_nothing()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactB; string emailB; ulong bystanderC;
            using (var db = NewContext())
            {
                (contactB, emailB) = await VisitorUserAsync(db);
                (bystanderC, _) = await VisitorUserAsync(db, contactB);
            }
            (requestId, var claimId) = await CreateWithClaimAsync(emailB);
            var raw = await MintTokenAsync(claimId);

            // Wrong logged-in account (C ≠ invited B) → EMAIL_MISMATCH.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Accept(db, bystanderC).Handle(new AcceptVisitContactClaimCommand(raw), CancellationToken.None));
                Assert.Equal(VisitContactClaimErrorCodes.EmailMismatch, ex.ErrorCode);
            }
            // Write flag OFF → the endpoint does not exist (404), even with a valid token.
            using (var db = NewContext())
            {
                await Assert.ThrowsAsync<NotFoundException>(() =>
                    Accept(db, contactB, write: false).Handle(new AcceptVisitContactClaimCommand(raw), CancellationToken.None));
            }
            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Null(visit.VisitorUserId);
                Assert.Equal(PrimaryContactAccessStatuses.PendingConfirmation, visit.PrimaryContactAccessStatus);
                var claim = await db.VisitRequestIdentityChanges.AsNoTracking().SingleAsync(c => c.IdentityChangeId == claimId);
                Assert.Equal(IdentityChangeStatuses.Pending, claim.Status);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Decline_records_terminal_state_but_keeps_request_alive_and_unowned()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactB; string emailB;
            using (var db = NewContext()) (contactB, emailB) = await VisitorUserAsync(db);
            (requestId, var claimId) = await CreateWithClaimAsync(emailB);
            var raw = await MintTokenAsync(claimId);

            using (var db = NewContext())
            {
                var res = await Decline(db, contactB).Handle(
                    new DeclineVisitContactClaimCommand(raw, "Không phụ trách đoàn này."), CancellationToken.None);
                Assert.Equal(IdentityChangeStatuses.Declined, res.ClaimStatus);
                Assert.Equal(PrimaryContactAccessStatuses.PendingConfirmation, res.PrimaryContactAccessStatus);
            }
            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Null(visit.VisitorUserId); // still unowned; registrant can resend/replace
                Assert.NotEqual(VisitRequestStatuses.Cancelled, visit.Status);
                var claim = await db.VisitRequestIdentityChanges.AsNoTracking().SingleAsync(c => c.IdentityChangeId == claimId);
                Assert.Equal(IdentityChangeStatuses.Declined, claim.Status);
                Assert.NotNull(claim.DeclinedAt);
                Assert.NotNull(claim.RetentionUntil); // 90-day redaction stamp
                Assert.True(await db.VisitRequestIdentityChangeEvents.AsNoTracking()
                    .AnyAsync(e => e.IdentityChangeId == claimId && e.EventType == "PRIMARY_CONTACT_INVITATION_DECLINED"));
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Resend_supersedes_the_old_link_and_the_new_link_works()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactB; string emailB;
            using (var db = NewContext()) (contactB, emailB) = await VisitorUserAsync(db);
            (requestId, var claimId) = await CreateWithClaimAsync(emailB);
            var oldRaw = await MintTokenAsync(claimId);

            var email = new FakeEmail();
            using (var db = NewContext())
            {
                var res = await Resend(db, Registrant, email).Handle(
                    new ResendVisitContactClaimCommand(requestId), CancellationToken.None);
                Assert.Equal(1u, res.ResendCount);
            }
            var newRaw = email.LastClaimToken;
            Assert.NotNull(newRaw);
            Assert.NotEqual(oldRaw, newRaw);

            // The pre-resend link is dead…
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Accept(db, contactB).Handle(new AcceptVisitContactClaimCommand(oldRaw), CancellationToken.None));
                Assert.Equal(VisitContactClaimErrorCodes.TokenInvalid, ex.ErrorCode);
            }
            // …and the fresh link applies the claim.
            using (var db = NewContext())
            {
                var res = await Accept(db, contactB).Handle(
                    new AcceptVisitContactClaimCommand(newRaw!), CancellationToken.None);
                Assert.Equal(IdentityChangeStatuses.Applied, res.ClaimStatus);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Maintenance_expires_overdue_claims_then_redacts_after_retention_idempotently()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactB; string emailB;
            using (var db = NewContext()) (contactB, emailB) = await VisitorUserAsync(db);
            (requestId, var claimId) = await CreateWithClaimAsync(emailB);
            var raw = await MintTokenAsync(claimId);

            // Force the claim overdue, then accept must refuse and the job must expire it.
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_identity_changes SET expires_at = {0} WHERE identity_change_id = {1}",
                    Now.AddHours(-1), claimId);
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Accept(db, contactB).Handle(new AcceptVisitContactClaimCommand(raw), CancellationToken.None));
                Assert.Equal(VisitContactClaimErrorCodes.NotPending, ex.ErrorCode);
            }
            using (var db = NewContext())
            {
                var result = await new VisitContactClaimMaintenanceService(db, NullLogger<VisitContactClaimMaintenanceService>.Instance)
                    .RunOnceAsync(Now, 50, CancellationToken.None);
                Assert.True(result.Expired >= 1);
            }
            using (var db = NewContext())
            {
                var claim = await db.VisitRequestIdentityChanges.AsNoTracking().SingleAsync(c => c.IdentityChangeId == claimId);
                Assert.Equal(IdentityChangeStatuses.Expired, claim.Status);
                Assert.NotNull(claim.RetentionUntil);
                Assert.True(await db.VisitRequestIdentityChangeEvents.AsNoTracking()
                    .AnyAsync(e => e.IdentityChangeId == claimId && e.EventType == "PRIMARY_CONTACT_INVITATION_EXPIRED"));
                var token = await db.EmailActionTokens.AsNoTracking()
                    .SingleAsync(t => t.TargetType == "VISIT_REQUEST_IDENTITY_CHANGE" && t.TargetId == claimId);
                Assert.Equal("INVALID", token.ResultStatus);
            }

            // Push past retention → redaction clears PII but keeps the masked identity + history.
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_identity_changes SET retention_until = {0} WHERE identity_change_id = {1}",
                    Now.AddDays(-1), claimId);
            using (var db = NewContext())
            {
                var result = await new VisitContactClaimMaintenanceService(db, NullLogger<VisitContactClaimMaintenanceService>.Instance)
                    .RunOnceAsync(Now, 50, CancellationToken.None);
                Assert.True(result.Redacted >= 1);
            }
            using (var db = NewContext())
            {
                var claim = await db.VisitRequestIdentityChanges.AsNoTracking().SingleAsync(c => c.IdentityChangeId == claimId);
                Assert.Null(claim.NewEmailNormalized);
                Assert.Null(claim.PendingSnapshotJson);
                Assert.NotNull(claim.RedactedAt);
                Assert.False(string.IsNullOrEmpty(claim.NewEmailMasked)); // masked identity survives
                var token = await db.EmailActionTokens.AsNoTracking()
                    .SingleAsync(t => t.TargetType == "VISIT_REQUEST_IDENTITY_CHANGE" && t.TargetId == claimId);
                Assert.Equal(claim.NewEmailMasked, token.RecipientEmail); // token PII redacted too
                Assert.True(await db.VisitRequestIdentityChangeEvents.AsNoTracking()
                    .AnyAsync(e => e.IdentityChangeId == claimId && e.EventType == "IDENTITY_CHANGE_REDACTED"));

                // Idempotent: a second sweep finds nothing for this claim.
                var again = await new VisitContactClaimMaintenanceService(db, NullLogger<VisitContactClaimMaintenanceService>.Instance)
                    .RunOnceAsync(Now, 50, CancellationToken.None);
                Assert.False(await db.VisitRequestIdentityChangeEvents.AsNoTracking()
                    .Where(e => e.IdentityChangeId == claimId && e.EventType == "IDENTITY_CHANGE_REDACTED")
                    .GroupBy(e => e.IdentityChangeId).AnyAsync(g => g.Count() > 1));
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Registrant_cancel_3A_works_only_while_contact_is_pending()
    {
        RequireDb();
        ulong pendingRequest = 0, ownedRequest = 0;
        try
        {
            ulong contactB; string emailB;
            using (var db = NewContext()) (contactB, emailB) = await VisitorUserAsync(db);

            // (a) Contact still PENDING → registrant may cancel (exception 3A); claim is closed with it.
            (pendingRequest, var pendingClaim) = await CreateWithClaimAsync(emailB);
            using (var db = NewContext())
            {
                var handler = new CancelVisitRequestCommandHandler(
                    db, new FakeUser(Registrant), new FixedClock(), new NoopNotifications());
                var res = await handler.Handle(
                    new CancelVisitRequestCommand(pendingRequest, null, "Đăng ký nhầm email đầu mối."),
                    CancellationToken.None);
                Assert.Equal(VisitRequestStatuses.Cancelled, res.RequestStatus);
            }
            using (var db = NewContext())
            {
                var claim = await db.VisitRequestIdentityChanges.AsNoTracking().SingleAsync(c => c.IdentityChangeId == pendingClaim);
                Assert.Equal(IdentityChangeStatuses.Cancelled, claim.Status);
                Assert.True(await db.AuditLogs.AsNoTracking().AnyAsync(a =>
                    a.VisitRequestId == pendingRequest
                    && a.Action == "VISIT_REQUEST_CANCELLED_BY_REGISTRANT_PENDING_CONTACT"));
            }

            // (b) Contact ACTIVE (claim accepted) → the 3A exception is gone; registrant cancel is forbidden.
            (ownedRequest, var ownedClaim) = await CreateWithClaimAsync(emailB);
            var raw = await MintTokenAsync(ownedClaim);
            using (var db = NewContext())
                await Accept(db, contactB).Handle(new AcceptVisitContactClaimCommand(raw), CancellationToken.None);
            using (var db = NewContext())
            {
                var handler = new CancelVisitRequestCommandHandler(
                    db, new FakeUser(Registrant), new FixedClock(), new NoopNotifications());
                await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
                    new CancelVisitRequestCommand(ownedRequest, null, "Registrant thử hủy sau khi contact ACTIVE."),
                    CancellationToken.None));
            }
        }
        finally
        {
            await CleanupAsync(pendingRequest);
            await CleanupAsync(ownedRequest);
        }
    }

    [Fact]
    public async Task Replace_pending_contact_supersedes_old_claim_and_links_or_reinvites()
    {
        RequireDb();
        ulong reinvited = 0, selfLinked = 0;
        try
        {
            ulong contactB; string emailB; ulong contactC; string emailC;
            using (var db = NewContext())
            {
                (contactB, emailB) = await VisitorUserAsync(db);
                (contactC, emailC) = await VisitorUserAsync(db, contactB);
            }

            // (a) Replace with a DIFFERENT email → old claim SUPERSEDED, fresh PENDING claim + invitation.
            (reinvited, var oldClaim) = await CreateWithClaimAsync(emailB);
            var oldRaw = await MintTokenAsync(oldClaim);
            var email = new FakeEmail();
            using (var db = NewContext())
            {
                var res = await Replace(db, Registrant, email).Handle(
                    new ReplacePendingVisitContactCommand(reinvited, "Contact C", "OrgC", "+8493", emailC),
                    CancellationToken.None);
                Assert.Equal(PrimaryContactAccessStatuses.PendingConfirmation, res.PrimaryContactAccessStatus);
                Assert.Equal(IdentityChangeStatuses.Pending, res.ClaimStatus);
            }
            using (var db = NewContext())
            {
                var old = await db.VisitRequestIdentityChanges.AsNoTracking().SingleAsync(c => c.IdentityChangeId == oldClaim);
                Assert.Equal(IdentityChangeStatuses.Superseded, old.Status);
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == reinvited);
                Assert.Equal(emailC, visit.ContactPersonEmail);
                Assert.Equal("Contact C", visit.ContactPersonFullName);

                // Old link is dead (its claim is SUPERSEDED); the new invitation works for C.
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Accept(db, contactB).Handle(new AcceptVisitContactClaimCommand(oldRaw), CancellationToken.None));
                Assert.Equal(VisitContactClaimErrorCodes.NotPending, ex.ErrorCode);
            }
            var newRaw = email.LastClaimToken;
            Assert.NotNull(newRaw);
            using (var db = NewContext())
            {
                var res = await Accept(db, contactC).Handle(
                    new AcceptVisitContactClaimCommand(newRaw!), CancellationToken.None);
                Assert.Equal(IdentityChangeStatuses.Applied, res.ClaimStatus);
            }
            using (var db = NewContext())
                Assert.Equal(contactC, (await db.VisitRequests.AsNoTracking()
                    .SingleAsync(v => v.VisitRequestId == reinvited)).VisitorUserId);

            // (b) Replace with the REGISTRANT's own email → immediate link, no pending claim left.
            (selfLinked, _) = await CreateWithClaimAsync(emailB);
            using (var db = NewContext())
            {
                var res = await Replace(db, Registrant, new FakeEmail()).Handle(
                    new ReplacePendingVisitContactCommand(selfLinked, "Registrant", "Org", "+8491", "registrant@example.com"),
                    CancellationToken.None);
                Assert.Equal(PrimaryContactAccessStatuses.Active, res.PrimaryContactAccessStatus);
                Assert.Null(res.ClaimStatus);
            }
            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == selfLinked);
                Assert.Equal(Registrant, visit.VisitorUserId);
                Assert.False(await db.VisitRequestIdentityChanges.AsNoTracking()
                    .AnyAsync(c => c.VisitRequestId == selfLinked && c.Status == IdentityChangeStatuses.Pending));
            }
        }
        finally
        {
            await CleanupAsync(reinvited);
            await CleanupAsync(selfLinked);
        }
    }
}
