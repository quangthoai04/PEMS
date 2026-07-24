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
using PEMS.Application.Delegations.Commands.VisitContactTransfer;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase D-4 — 24h primary-contact TRANSFER (plan §16.4/§4.4, handoff §6) against pems_pr3_test.
/// Setup per test: committed v2 request whose contact B accepted the INITIAL_CLAIM (owner ACTIVE), then
/// the transfer workflow runs with its own transactions; finally everything is cascade-deleted so the DB
/// keeps v2_requests = 0. Actors: registrant = 8; owner B and target C = seed ACTIVE VISITOR users.
/// </summary>
public sealed class VisitContactTransferWorkflowTests
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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master + 06/07_up patches to run these tests.");
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

    private sealed class FakeEmail : IEmailService
    {
        public List<(string To, string Subject, string Html)> Sent { get; } = new();
        public string? LastTransferToken
        {
            get
            {
                var html = Sent.LastOrDefault().Html;
                if (html is null) return null;
                var m = Regex.Match(html, @"visit-contact-transfer/([A-Za-z0-9_\-]+)");
                return m.Success ? m.Groups[1].Value : null;
            }
        }
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

    private static InitiateVisitContactTransferCommandHandler Initiate(
        ApplicationDbContext db, ulong actor, FakeEmail email, bool write = true)
        => new(db, new FakeUser(actor), new FixedClock(), ClaimSvc(db, email),
            new PerCampusFormV2WriteOptions { Enabled = write });

    private static AcceptVisitContactTransferCommandHandler AcceptTransfer(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor), new FixedClock(), Tokens(), ClaimSvc(db),
            new NoopNotifications(), WriteOn);

    private static DeclineVisitContactTransferCommandHandler DeclineTransfer(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor), new FixedClock(), Tokens(), ClaimSvc(db), WriteOn);

    private static ResendVisitContactTransferCommandHandler ResendTransfer(
        ApplicationDbContext db, ulong actor, FakeEmail email)
        => new(db, new FakeUser(actor), new FixedClock(), ClaimSvc(db, email), WriteOn);

    private static CancelVisitContactTransferCommandHandler CancelTransfer(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor), new FixedClock(), ClaimSvc(db), WriteOn);

    // ── Data helpers ──────────────────────────────────────────────────────────────

    private static async Task<(ulong UserId, string Email)> VisitorUserAsync(ApplicationDbContext db, params ulong[] exclude)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                        && u.UserId != Registrant && !exclude.Contains(u.UserId))
            .OrderBy(u => u.UserId)
            .Select(u => new { u.UserId, u.Email })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pems_pr3_test needs ACTIVE VISITOR seed users besides user 8.");
        return (row.UserId, row.Email!);
    }

    private static async Task<string> InternalEmailAsync(ApplicationDbContext db)
        => await db.Users.AsNoTracking()
               .Where(u => u.Role.RoleCode != RoleCodes.Visitor && u.Status == UserStatuses.Active && u.Email != null)
               .OrderBy(u => u.UserId)
               .Select(u => u.Email!)
               .FirstAsync();

    private static VisitRequestFormDataV2 Form(string submissionId, string contactEmail)
    {
        var start = Now.AddDays(20);
        var campus = new CampusVisitFormDto(
            "HN", start, start.AddMinutes(120), "Đoàn Transfer", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null, null);
        return new VisitRequestFormDataV2(
            submissionId,
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", "registrant@example.com"),
            new ContactPointDto("Contact B", "OrgB", "+8492", contactEmail),
            null, new List<CampusVisitFormDto> { campus });
    }

    /// <summary>Creates + commits a v2 request with contact B, then B accepts the INITIAL_CLAIM →
    /// an established ACTIVE owner. Returns the request id.</summary>
    private static async Task<ulong> CreateOwnedRequestAsync(ulong contactB, string emailB)
    {
        ulong requestId;
        ulong claimId;
        using (var db = NewContext())
        {
            var handler = new CreateVisitRequestV2CommandHandler(
                db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
                new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
                new UserProvisionService(db),
                NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
                new PerCampusFormV2Options { Enabled = true }, WriteOn,
                new VisitRequestAggregateStatusService(db));
            var created = await handler.Handle(
                new CreateVisitRequestV2Command(Form("TR" + Guid.NewGuid().ToString("N"), emailB)),
                CancellationToken.None);
            requestId = created.VisitRequestId;
            claimId = await db.VisitRequestIdentityChanges.AsNoTracking()
                .Where(c => c.VisitRequestId == requestId && c.Status == IdentityChangeStatuses.Pending)
                .Select(c => c.IdentityChangeId)
                .SingleAsync();
        }
        string raw;
        using (var db = NewContext())
            raw = (await ClaimSvc(db).SendInvitationAsync(claimId, CancellationToken.None))!;
        using (var db = NewContext())
        {
            var accept = new AcceptVisitContactClaimCommandHandler(
                db, new FakeUser(contactB), new FixedClock(), Tokens(), ClaimSvc(db), WriteOn);
            await accept.Handle(new AcceptVisitContactClaimCommand(raw), CancellationToken.None);
        }
        return requestId;
    }

    /// <summary>Initiates a transfer to <paramref name="targetEmail"/> and returns the raw token from the email.</summary>
    private static async Task<string> InitiateTransferAsync(ulong requestId, ulong actor, string targetEmail)
    {
        var email = new FakeEmail();
        using var db = NewContext();
        var res = await Initiate(db, actor, email).Handle(
            new InitiateVisitContactTransferCommand(requestId, "Contact C", "OrgC", "+8493", targetEmail, "Đổi người phụ trách."),
            CancellationToken.None);
        Assert.Equal(IdentityChangeStatuses.Pending, res.TransferStatus);
        var raw = email.LastTransferToken;
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
    public async Task Accept_swaps_relation_only_old_owner_keeps_rights_until_then_and_replay_is_idempotent()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong ownerB; string emailB; ulong targetC; string emailC;
            using (var db = NewContext())
            {
                (ownerB, emailB) = await VisitorUserAsync(db);
                (targetC, emailC) = await VisitorUserAsync(db, ownerB);
            }
            requestId = await CreateOwnedRequestAsync(ownerB, emailB);
            var raw = await InitiateTransferAsync(requestId, Registrant, emailC);

            // BEFORE accept: B is still the owner with full rights; the transfer row is PENDING 24h.
            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Equal(ownerB, visit.VisitorUserId);
                Assert.Equal(PrimaryContactAccessStatuses.Active, visit.PrimaryContactAccessStatus);
                var transfer = await db.VisitRequestIdentityChanges.AsNoTracking()
                    .SingleAsync(c => c.VisitRequestId == requestId && c.ChangeKind == IdentityChangeKinds.Transfer);
                Assert.Equal(IdentityChangeStatuses.Pending, transfer.Status);
                Assert.Equal(ownerB, transfer.OldUserId);
                Assert.True(transfer.ExpiresAt <= Now.AddHours(24).AddMinutes(1)); // 24h, not 72h
                Assert.True(await db.VisitRequestIdentityChangeEvents.AsNoTracking()
                    .AnyAsync(e => e.IdentityChangeId == transfer.IdentityChangeId
                                   && e.EventType == "PRIMARY_CONTACT_TRANSFER_REQUESTED"));
            }

            // C accepts with the matching account → the ONLY changes are relation + contact snapshot.
            using (var db = NewContext())
            {
                var res = await AcceptTransfer(db, targetC).Handle(
                    new AcceptVisitContactTransferCommand(raw), CancellationToken.None);
                Assert.Equal(IdentityChangeStatuses.Applied, res.TransferStatus);
                Assert.False(res.Idempotent);
            }
            int rowVersionAfterApply;
            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Equal(targetC, visit.VisitorUserId);
                Assert.Equal(emailC.ToLowerInvariant(), visit.ContactPersonEmail);
                Assert.Equal("Contact C", visit.ContactPersonFullName);
                Assert.Equal(PrimaryContactAccessStatuses.Active, visit.PrimaryContactAccessStatus);
                rowVersionAfterApply = visit.RowVersion;

                var transfer = await db.VisitRequestIdentityChanges.AsNoTracking()
                    .SingleAsync(c => c.VisitRequestId == requestId && c.ChangeKind == IdentityChangeKinds.Transfer);
                Assert.Equal(IdentityChangeStatuses.Applied, transfer.Status);
                Assert.Equal(targetC, transfer.NewUserId);

                // Campus decision state untouched; the OLD owner's ACCOUNT is untouched (never locked/deleted).
                var instance = await db.VisitRequestCampuses.AsNoTracking().SingleAsync(c => c.VisitRequestId == requestId);
                Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, instance.Status);
                var oldAccount = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == ownerB);
                Assert.Equal(UserStatuses.Active, oldAccount.Status);

                Assert.True(await db.VisitRequestIdentityChangeEvents.AsNoTracking()
                    .AnyAsync(e => e.VisitRequestId == requestId && e.EventType == "PRIMARY_CONTACT_TRANSFER_APPLIED"));
                Assert.True(await db.AuditLogs.AsNoTracking()
                    .AnyAsync(a => a.VisitRequestId == requestId && a.Action == "PRIMARY_CONTACT_TRANSFER_APPLIED"));
            }

            // Replay by the SAME accepted user → idempotent applied result, no second swap/bump.
            using (var db = NewContext())
            {
                var res = await AcceptTransfer(db, targetC).Handle(
                    new AcceptVisitContactTransferCommand(raw), CancellationToken.None);
                Assert.True(res.Idempotent);
                Assert.Equal(IdentityChangeStatuses.Applied, res.TransferStatus);
            }
            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Equal(rowVersionAfterApply, visit.RowVersion);
                Assert.Equal(targetC, visit.VisitorUserId);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Initiate_guards_reject_wrong_actor_state_and_target()
    {
        RequireDb();
        ulong owned = 0, unclaimed = 0;
        try
        {
            ulong ownerB; string emailB; ulong strangerC; string emailC; string internalEmail;
            using (var db = NewContext())
            {
                (ownerB, emailB) = await VisitorUserAsync(db);
                (strangerC, emailC) = await VisitorUserAsync(db, ownerB);
                internalEmail = await InternalEmailAsync(db);
            }
            owned = await CreateOwnedRequestAsync(ownerB, emailB);
            var email = new FakeEmail();

            // Unrelated visitor (not registrant, not owner) → forbidden.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Initiate(db, strangerC, email).Handle(
                        new InitiateVisitContactTransferCommand(owned, "X", "O", "+84", emailC, null),
                        CancellationToken.None));

            // Same email as the current contact → EMAIL_UNCHANGED.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Initiate(db, Registrant, email).Handle(
                        new InitiateVisitContactTransferCommand(owned, "B", "O", "+84", emailB, null),
                        CancellationToken.None));
                Assert.Equal(VisitContactTransferErrorCodes.EmailUnchanged, ex.ErrorCode);
            }

            // Internal (non-VISITOR) target → INTERNAL_ACCOUNT_CONFLICT.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Initiate(db, Registrant, email).Handle(
                        new InitiateVisitContactTransferCommand(owned, "X", "O", "+84", internalEmail, null),
                        CancellationToken.None));
                Assert.Equal(VisitContactTransferErrorCodes.InternalAccountConflict, ex.ErrorCode);
            }

            // Write flag OFF → the endpoint does not exist.
            using (var db = NewContext())
                await Assert.ThrowsAsync<NotFoundException>(() =>
                    Initiate(db, Registrant, email, write: false).Handle(
                        new InitiateVisitContactTransferCommand(owned, "X", "O", "+84", emailC, null),
                        CancellationToken.None));

            // Valid initiate, then a SECOND initiate while pending → ALREADY_PENDING.
            await InitiateTransferAsync(owned, Registrant, emailC);
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Initiate(db, Registrant, email).Handle(
                        new InitiateVisitContactTransferCommand(owned, "X", "O", "+84", emailC, null),
                        CancellationToken.None));
                Assert.Equal(VisitContactTransferErrorCodes.AlreadyPending, ex.ErrorCode);
            }

            // Unclaimed contact (INITIAL_CLAIM still pending) → transfer refuses: CONTACT_ACCOUNT_NOT_ACTIVE.
            using (var db = NewContext())
            {
                var handler = new CreateVisitRequestV2CommandHandler(
                    db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
                    new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
                    new UserProvisionService(db),
                    NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
                    new PerCampusFormV2Options { Enabled = true }, WriteOn,
                new VisitRequestAggregateStatusService(db));
                var created = await handler.Handle(
                    new CreateVisitRequestV2Command(Form("TR" + Guid.NewGuid().ToString("N"), emailB)),
                    CancellationToken.None);
                unclaimed = created.VisitRequestId;
            }
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Initiate(db, Registrant, email).Handle(
                        new InitiateVisitContactTransferCommand(unclaimed, "X", "O", "+84", emailC, null),
                        CancellationToken.None));
                Assert.Equal(VisitContactTransferErrorCodes.ContactNotActive, ex.ErrorCode);
            }
        }
        finally
        {
            await CleanupAsync(owned);
            await CleanupAsync(unclaimed);
        }
    }

    [Fact]
    public async Task Accept_wrong_account_or_stale_request_version_is_rejected_until_resend_restamps()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong ownerB; string emailB; ulong targetC; string emailC;
            using (var db = NewContext())
            {
                (ownerB, emailB) = await VisitorUserAsync(db);
                (targetC, emailC) = await VisitorUserAsync(db, ownerB);
            }
            requestId = await CreateOwnedRequestAsync(ownerB, emailB);
            var raw = await InitiateTransferAsync(requestId, Registrant, emailC);

            // Wrong logged-in account (registrant 8 is not the invited email) → GOOGLE_EMAIL_MISMATCH.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    AcceptTransfer(db, Registrant).Handle(new AcceptVisitContactTransferCommand(raw), CancellationToken.None));
                Assert.Equal(VisitContactTransferErrorCodes.GoogleEmailMismatch, ex.ErrorCode);
            }

            // The request changes AFTER the invitation (row version moves) → accept refuses (stale stamp)…
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_requests SET row_version = row_version + 1 WHERE visit_request_id = {0}", requestId);
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    AcceptTransfer(db, targetC).Handle(new AcceptVisitContactTransferCommand(raw), CancellationToken.None));
                Assert.Equal(VisitContactTransferErrorCodes.Conflict, ex.ErrorCode);
            }
            using (var db = NewContext())
                Assert.Equal(ownerB, (await db.VisitRequests.AsNoTracking()
                    .SingleAsync(v => v.VisitRequestId == requestId)).VisitorUserId); // owner unchanged

            // …resend re-stamps the expected version and supersedes the old link…
            var email = new FakeEmail();
            using (var db = NewContext())
            {
                var res = await ResendTransfer(db, Registrant, email).Handle(
                    new ResendVisitContactTransferCommand(requestId), CancellationToken.None);
                Assert.Equal(1u, res.ResendCount);
            }
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    AcceptTransfer(db, targetC).Handle(new AcceptVisitContactTransferCommand(raw), CancellationToken.None));
                Assert.Equal(VisitContactClaimErrorCodes.TokenInvalid, ex.ErrorCode); // old link is dead
            }
            // …and the fresh link applies.
            var newRaw = email.LastTransferToken!;
            using (var db = NewContext())
            {
                var res = await AcceptTransfer(db, targetC).Handle(
                    new AcceptVisitContactTransferCommand(newRaw), CancellationToken.None);
                Assert.Equal(IdentityChangeStatuses.Applied, res.TransferStatus);
            }
            using (var db = NewContext())
                Assert.Equal(targetC, (await db.VisitRequests.AsNoTracking()
                    .SingleAsync(v => v.VisitRequestId == requestId)).VisitorUserId);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Decline_cancel_and_expiry_all_keep_the_old_owner()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong ownerB; string emailB; ulong targetC; string emailC;
            using (var db = NewContext())
            {
                (ownerB, emailB) = await VisitorUserAsync(db);
                (targetC, emailC) = await VisitorUserAsync(db, ownerB);
            }
            requestId = await CreateOwnedRequestAsync(ownerB, emailB);

            // (a) DECLINE by the invited exact account.
            var raw1 = await InitiateTransferAsync(requestId, Registrant, emailC);
            using (var db = NewContext())
            {
                var res = await DeclineTransfer(db, targetC).Handle(
                    new DeclineVisitContactTransferCommand(raw1, "Không phụ trách."), CancellationToken.None);
                Assert.Equal(IdentityChangeStatuses.Declined, res.TransferStatus);
            }
            using (var db = NewContext())
            {
                Assert.Equal(ownerB, (await db.VisitRequests.AsNoTracking()
                    .SingleAsync(v => v.VisitRequestId == requestId)).VisitorUserId);
                Assert.True(await db.VisitRequestIdentityChangeEvents.AsNoTracking()
                    .AnyAsync(e => e.VisitRequestId == requestId && e.EventType == "PRIMARY_CONTACT_TRANSFER_DECLINED"));
            }

            // (b) CANCEL by the current ACTIVE owner (B) — owner-side management is not registrant-only.
            var raw2 = await InitiateTransferAsync(requestId, Registrant, emailC);
            using (var db = NewContext())
            {
                var res = await CancelTransfer(db, ownerB).Handle(
                    new CancelVisitContactTransferCommand(requestId, "Đổi ý."), CancellationToken.None);
                Assert.Equal(IdentityChangeStatuses.Cancelled, res.TransferStatus);
            }
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    AcceptTransfer(db, targetC).Handle(new AcceptVisitContactTransferCommand(raw2), CancellationToken.None));
                // Cancelled transfer → the locked-state guard reports it as settled.
                Assert.Equal(VisitContactTransferErrorCodes.Conflict, ex.ErrorCode);
                Assert.Equal(ownerB, (await db.VisitRequests.AsNoTracking()
                    .SingleAsync(v => v.VisitRequestId == requestId)).VisitorUserId);
            }

            // (c) EXPIRY (24h window) — job settles it kind-aware; owner unchanged.
            var raw3 = await InitiateTransferAsync(requestId, Registrant, emailC);
            ulong transferId;
            using (var db = NewContext())
                transferId = await db.VisitRequestIdentityChanges.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId && c.Status == IdentityChangeStatuses.Pending)
                    .Select(c => c.IdentityChangeId).SingleAsync();
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_identity_changes SET expires_at = {0} WHERE identity_change_id = {1}",
                    Now.AddMinutes(-5), transferId);
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    AcceptTransfer(db, targetC).Handle(new AcceptVisitContactTransferCommand(raw3), CancellationToken.None));
                Assert.Equal(VisitContactTransferErrorCodes.Expired, ex.ErrorCode);
            }
            using (var db = NewContext())
            {
                var swept = await new VisitContactClaimMaintenanceService(db, NullLogger<VisitContactClaimMaintenanceService>.Instance)
                    .RunOnceAsync(Now, 50, CancellationToken.None);
                Assert.True(swept.Expired >= 1);
            }
            using (var db = NewContext())
            {
                var transfer = await db.VisitRequestIdentityChanges.AsNoTracking()
                    .SingleAsync(c => c.IdentityChangeId == transferId);
                Assert.Equal(IdentityChangeStatuses.Expired, transfer.Status);
                Assert.NotNull(transfer.RetentionUntil);
                Assert.True(await db.VisitRequestIdentityChangeEvents.AsNoTracking()
                    .AnyAsync(e => e.IdentityChangeId == transferId
                                   && e.EventType == "PRIMARY_CONTACT_TRANSFER_EXPIRED")); // kind-aware
                Assert.Equal(ownerB, (await db.VisitRequests.AsNoTracking()
                    .SingleAsync(v => v.VisitRequestId == requestId)).VisitorUserId);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Pending_transfer_does_not_open_cancel_3A_for_the_registrant()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong ownerB; string emailB; ulong targetC; string emailC;
            using (var db = NewContext())
            {
                (ownerB, emailB) = await VisitorUserAsync(db);
                (targetC, emailC) = await VisitorUserAsync(db, ownerB);
            }
            requestId = await CreateOwnedRequestAsync(ownerB, emailB);
            await InitiateTransferAsync(requestId, Registrant, emailC);

            // The contact is ACTIVE (a transfer is merely pending) → the registrant gets NO 3A exception.
            using (var db = NewContext())
            {
                var handler = new CancelVisitRequestCommandHandler(
                    db, new FakeUser(Registrant), new FixedClock(), new NoopNotifications());
                await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
                    new CancelVisitRequestCommand(requestId, null, "Registrant thử 3A khi transfer pending."),
                    CancellationToken.None));
            }
            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.NotEqual(VisitRequestStatuses.Cancelled, visit.Status);
                Assert.Equal(ownerB, visit.VisitorUserId);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Landing_info_is_masked_and_state_view_shows_pending_then_none()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong ownerB; string emailB; ulong targetC; string emailC;
            using (var db = NewContext())
            {
                (ownerB, emailB) = await VisitorUserAsync(db);
                (targetC, emailC) = await VisitorUserAsync(db, ownerB);
            }
            requestId = await CreateOwnedRequestAsync(ownerB, emailB);
            var raw = await InitiateTransferAsync(requestId, Registrant, emailC);

            using (var db = NewContext())
            {
                var info = await new GetVisitContactTransferInfoQueryHandler(db, Tokens(), new FixedClock(), WriteOn)
                    .Handle(new GetVisitContactTransferInfoQuery(raw), CancellationToken.None);
                Assert.Equal(IdentityChangeStatuses.Pending, info.Status);
                Assert.True(info.Actionable);
                Assert.DoesNotContain(emailC.ToLowerInvariant(), info.MaskedEmail!); // masked, never full
                Assert.NotNull(info.RequestCode);

                var junk = await new GetVisitContactTransferInfoQueryHandler(db, Tokens(), new FixedClock(), WriteOn)
                    .Handle(new GetVisitContactTransferInfoQuery("not-a-token"), CancellationToken.None);
                Assert.Equal("INVALID", junk.Status); // unknown token == malformed token (no enumeration)
            }

            using (var db = NewContext())
            {
                var state = await new GetActiveVisitContactTransferQueryHandler(db, new FakeUser(ownerB), new FixedClock(), WriteOn)
                    .Handle(new GetActiveVisitContactTransferQuery(requestId), CancellationToken.None);
                Assert.True(state.HasPendingTransfer);
                Assert.Equal(IdentityChangeStatuses.Pending, state.Status);
            }

            using (var db = NewContext())
                await AcceptTransfer(db, targetC).Handle(new AcceptVisitContactTransferCommand(raw), CancellationToken.None);

            using (var db = NewContext())
            {
                // After apply, the NEW owner views the state (the old owner lost the relation → forbidden).
                var state = await new GetActiveVisitContactTransferQueryHandler(db, new FakeUser(targetC), new FixedClock(), WriteOn)
                    .Handle(new GetActiveVisitContactTransferQuery(requestId), CancellationToken.None);
                Assert.False(state.HasPendingTransfer);
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    new GetActiveVisitContactTransferQueryHandler(db, new FakeUser(ownerB), new FixedClock(), WriteOn)
                        .Handle(new GetActiveVisitContactTransferQuery(requestId), CancellationToken.None));
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
