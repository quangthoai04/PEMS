using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Application.Delegations.Commands.RepairLegacyOperationalContact;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// RepairLegacyOperationalContactCommandHandler, against real MySQL. The pre-fix destructive REPLACE
/// shape (a confirmed A cleared to null, campus forced back to WAITING_CONTACT_CONFIRMATION, a fresh
/// invitation raised for B) can no longer be produced by any live handler — the confirmed-handover fix
/// refuses it outright — so every fixture hand-simulates exactly that legacy write, the same way
/// BackfillVisitHistoryTests.cs hand-simulates its own pre-fix shapes. Everything BEFORE the corruption
/// (create, real invitation, real accept) goes through the actual handlers, so the confirming
/// identity-change row carries a genuine PendingSnapshotJson — the detector's real evidence source.
/// </summary>
public sealed class RepairLegacyOperationalContactTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8, AdminUser = 1;
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
        public FakeUser(ulong id, string roleCode, string? email = null) { UserId = id; RoleCode = roleCode; Email = email; }
        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
        public string? Email { get; }
        public ulong? RoleId => null;
        public string? RoleCode { get; }
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

    private static FakeUser Admin() => new(AdminUser, RoleCodes.Admin);

    private static async Task<RepairLegacyOperationalContactResponse> RunAsync(
        string? mode, ICurrentUserService? user = null)
    {
        using var db = NewContext();
        var handler = new RepairLegacyOperationalContactCommandHandler(
            db, user ?? Admin(), new FixedClock(), new VisitRequestAggregateStatusService(db),
            new MySqlUserMutationLockService(db),
            NullLogger<RepairLegacyOperationalContactCommandHandler>.Instance);
        return await handler.Handle(new RepairLegacyOperationalContactCommand(mode), CancellationToken.None);
    }

    // ── Fixture plumbing (mirrors OperationalContactLifecycleLockTests.cs) ─────────────────────

    private sealed class NoopNotifications : Application.Notifications.Common.INotificationService
    {
        public Task CreateManyAsync(IEnumerable<Application.Notifications.Common.CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<Application.Notifications.Common.CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong r, string t, string? m, string nt, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(Application.Notifications.Common.CreateNotificationRequest r, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeEmail : IEmailService
    {
        public List<(string To, string Subject, string Html)> Sent { get; } = new();
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
        { Sent.Add((toEmail, subject, htmlBody)); return Task.CompletedTask; }
        public Task<EmailDeliveryResult> TrySendAsync(OutboundEmail message, CancellationToken ct = default)
        { Sent.Add((message.To.Count > 0 ? message.To[0].Email : "", message.Subject ?? "", message.Body ?? "")); return Task.FromResult(EmailDeliveryResult.Sent()); }
        public Task SendAsync(OutboundEmail message, CancellationToken ct = default)
        { Sent.Add((message.To.Count > 0 ? message.To[0].Email : "", message.Subject ?? "", message.Body ?? "")); return Task.CompletedTask; }
        public Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
            => Task.FromResult(EmailDeliveryResult.Sent());
    }

    private static readonly Microsoft.Extensions.Configuration.IConfiguration EmptyConfig =
        new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();

    private static EmailActionTokenService Tokens() => new(EmptyConfig);

    private static OperationalContactInvitationService Invitations(ApplicationDbContext db, FakeEmail email)
        => new(db, Tokens(),
            new SystemEmailDispatcher(db, new EmailTemplateRenderer(db), email),
            new FixedClock(), NullLogger<OperationalContactInvitationService>.Instance, EmptyConfig);

    private static AcceptOperationalContactConfirmationCommandHandler Accept(
        ApplicationDbContext db, ulong actor, string actorEmail, FakeEmail email)
        => new(db, new FakeUser(actor, RoleCodes.Visitor, actorEmail), new FixedClock(), Tokens(), Invitations(db, email),
            new VisitRequestAggregateStatusService(db), new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)),
            new NoopNotifications(), NullLogger<AcceptOperationalContactConfirmationCommandHandler>.Instance,
            new PerCampusFormV2WriteOptions { Enabled = true });

    private static async Task<(ulong UserId, string Email)> VisitorUserAsync(ApplicationDbContext db, params ulong[] exclude)
    {
        var taken = exclude.Append(Registrant).ToList();
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active && !taken.Contains(u.UserId))
            .OrderBy(u => u.UserId).Select(u => new { u.UserId, u.Email }).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("This database needs at least two ACTIVE VISITOR accounts besides user 8.");
        return (row.UserId, row.Email!);
    }

    private static CampusVisitFormDto Campus(string code, string contactEmail)
    {
        var start = Now.AddDays(25);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(120), "Đoàn khôi phục", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Đầu mối gốc", "OrgA gốc", "Trưởng phòng gốc", "+84900000001", contactEmail),
            "EN", null, "DECLINED", null, null);
    }

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(Registrant, RoleCodes.Visitor), new FixedClock(), new VisitRequestV2CreateService(db),
            new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db), NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = true }, new PerCampusFormV2WriteOptions { Enabled = true },
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));

        var form = new VisitRequestFormDataV2(
            "RP" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private sealed record CampusRow(ulong InstanceId, string Status, ulong? ContactUserId);

    private static async Task<CampusRow> CampusStateAsync(ulong requestId, ulong instanceId)
    {
        using var db = NewContext();
        var c = await db.VisitRequestCampuses.AsNoTracking().SingleAsync(x => x.VisitInstanceId == instanceId);
        return new CampusRow(c.VisitInstanceId, c.Status, c.OperationalContactUserId);
    }

    private static async Task<(ulong InstanceId, ulong ContactId)> ConfirmedCampusAsync(
        string campusCode, ulong contactId, string contactEmail, FakeEmail mail, ulong requestId)
    {
        var invitation = await GetSoleChangeAsync(requestId);
        using var db = NewContext();
        var invitations = Invitations(db, mail);
        var tokens = await invitations.MintInvitationTokensAsync(invitation.IdentityChangeId, CancellationToken.None);
        Assert.NotNull(tokens);
        await db.SaveChangesAsync();
        await invitations.DispatchInvitationEmailAsync(invitation.IdentityChangeId, tokens!, CancellationToken.None);

        await Accept(db, contactId, contactEmail, mail).Handle(
            new AcceptOperationalContactConfirmationCommand(tokens!.AcceptToken), CancellationToken.None);
        return (invitation.VisitInstanceId, contactId);
    }

    private static async Task<VisitRequestIdentityChange> GetSoleChangeAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestIdentityChanges.AsNoTracking()
            .SingleAsync(c => c.VisitRequestId == requestId);
    }

    /// <summary>
    /// Hand-simulates the pre-fix destructive REPLACE against an already-confirmed campus: clears the
    /// holder, overwrites the snapshot to B, forces the campus back to WAITING_CONTACT_CONFIRMATION, and
    /// writes the exact REPLACED audit + B-invitation shape the real (now-removed) buggy branch used to
    /// leave behind — including the shared CorrelationId the detector joins on.
    /// </summary>
    private static async Task<(ulong CorruptingAuditId, ulong BChangeId)> CorruptAsync(
        ulong requestId, ulong instanceId, ulong oldContactUserId, string bTerminalStatus, string bEmail)
    {
        using var db = NewContext();
        var instance = await db.VisitRequestCampuses.Include(c => c.FormDetail)
            .SingleAsync(c => c.VisitInstanceId == instanceId);

        instance.OperationalContactUserId = null;
        instance.OperationalContactConfirmedAt = null;
        instance.OperationalContactConfirmationSource = null;
        instance.Status = VisitInstanceStatuses.WaitingContactConfirmation;
        instance.RowVersion += 1;
        instance.FormDetail!.OperationalContactFullName = "Người B (chưa xác nhận)";
        instance.FormDetail.OperationalContactOrganization = "OrgB chưa xác nhận";
        instance.FormDetail.OperationalContactJobTitle = "Chức danh B";
        instance.FormDetail.OperationalContactPhone = "+84900000099";
        instance.FormDetail.OperationalContactEmail = bEmail;
        instance.FormDetail.RowVersion += 1;

        var correlationId = Guid.NewGuid().ToString("N");
        var replaceAudit = new AuditLog
        {
            ActorUserId = Registrant, Action = OperationalContactHistoryAudit.Replaced,
            EntityType = "VisitRequestCampus", EntityId = instanceId,
            CampusId = instance.CampusId, VisitRequestId = requestId, VisitInstanceId = instanceId,
            SourceType = "IDENTITY", CorrelationId = correlationId, CreatedAt = Now,
        };
        replaceAudit.Changes.Add(new AuditLogChange
        {
            FieldName = "operational_contact_user_id",
            OldValueText = oldContactUserId.ToString(), NewValueText = null, CreatedAt = Now,
        });
        db.AuditLogs.Add(replaceAudit);

        var bChange = new VisitRequestIdentityChange
        {
            VisitRequestId = requestId, VisitInstanceId = instanceId,
            ChangeKind = IdentityChangeKinds.InitialConfirmation, TokenVersion = 1,
            NewEmailNormalized = bTerminalStatus == IdentityChangeStatuses.Expired ? null : bEmail,
            NewEmailMasked = "b***@example.com",
            PendingSnapshotJson = "{\"fullName\":\"B\",\"organization\":\"OrgB\",\"jobTitle\":\"Job B\",\"phone\":null,\"email\":\"" + bEmail + "\"}",
            Status = bTerminalStatus, RequestedBy = Registrant, RequestedAt = Now,
            ExpiresAt = bTerminalStatus == IdentityChangeStatuses.Pending ? Now.AddHours(72) : Now.AddHours(-1),
            ResendCount = 0, CreatedAt = Now,
        };
        if (bTerminalStatus == IdentityChangeStatuses.Applied)
        {
            var (successorId, _) = await VisitorUserAsync(db, oldContactUserId);
            bChange.NewUserId = successorId;
            bChange.AppliedAt = Now;
        }
        else if (bTerminalStatus == IdentityChangeStatuses.Cancelled) bChange.CancelledAt = Now;
        else if (bTerminalStatus == IdentityChangeStatuses.Declined) bChange.DeclinedAt = Now;
        else if (bTerminalStatus == IdentityChangeStatuses.Superseded) bChange.SupersededAt = Now;
        db.VisitRequestIdentityChanges.Add(bChange);
        await db.SaveChangesAsync();

        db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = bChange.IdentityChangeId, VisitRequestId = requestId, VisitInstanceId = instanceId,
            EventType = "OPERATIONAL_CONTACT_INVITATION_CREATED", ToStatus = IdentityChangeStatuses.Pending,
            ActorUserId = Registrant, CorrelationId = correlationId, CreatedAt = Now,
        });
        await db.SaveChangesAsync();

        return (replaceAudit.AuditLogId, bChange.IdentityChangeId);
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

    // ── R1: safe corrupted case — dry-run no-op, apply restores exactly, second apply idempotent ──

    [Fact]
    public async Task R1_A_safe_corrupted_campus_is_dry_run_reported_then_restored_exactly_and_idempotently()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactId; string contactEmail, successorEmail;
            using (var db = NewContext())
            {
                (contactId, contactEmail) = await VisitorUserAsync(db);
                (_, successorEmail) = await VisitorUserAsync(db, contactId);
            }

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var mail = new FakeEmail();
            var (instanceId, _) = await ConfirmedCampusAsync("HN", contactId, contactEmail, mail, requestId);
            var confirmedBefore = await CampusStateAsync(requestId, instanceId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, confirmedBefore.Status);
            Assert.Equal(contactId, confirmedBefore.ContactUserId);

            var (corruptingAuditId, _) = await CorruptAsync(
                requestId, instanceId, contactId, IdentityChangeStatuses.Cancelled, successorEmail);

            var corrupted = await CampusStateAsync(requestId, instanceId);
            Assert.Null(corrupted.ContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, corrupted.Status);

            // ── Dry run: reports SAFE, writes nothing. ──
            var dry = await RunAsync(mode: null);
            Assert.False(dry.Applied);
            Assert.Equal(0, dry.Repaired);
            var diag = $"Scanned={dry.Scanned} Candidates={dry.Candidates} Safe={dry.SafeAutoRepair} " +
                $"Manual={dry.ManualReview} NotCorrupted={dry.NotCorrupted} Errors={dry.Errors} " +
                $"ManualReasons=[{string.Join(" | ", dry.ManualReviewCandidates.Select(c => c.Reason))}]";
            Assert.True(dry.SafeAutoRepairCandidates.Any(c => c.CorruptingAuditLogId == corruptingAuditId), diag);

            var afterDry = await CampusStateAsync(requestId, instanceId);
            Assert.Null(afterDry.ContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, afterDry.Status);

            // ── An almost-right mode value must NOT apply — only the exact literal does. ──
            var wrongMode = await RunAsync(mode: "apply");
            Assert.False(wrongMode.Applied);
            var stillCorrupted = await CampusStateAsync(requestId, instanceId);
            Assert.Null(stillCorrupted.ContactUserId);

            // ── APPLY: restores A exactly. ──
            var applied = await RunAsync(mode: "APPLY");
            Assert.True(applied.Applied);
            Assert.Equal(1, applied.Repaired);

            using (var db = NewContext())
            {
                var restored = await db.VisitRequestCampuses.AsNoTracking().Include(c => c.FormDetail)
                    .SingleAsync(c => c.VisitInstanceId == instanceId);
                Assert.Equal(contactId, restored.OperationalContactUserId);
                Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, restored.Status);
                Assert.Equal(contactEmail, restored.FormDetail!.OperationalContactEmail);
                Assert.Equal("Đầu mối gốc", restored.FormDetail.OperationalContactFullName);
                Assert.Equal("OrgA gốc", restored.FormDetail.OperationalContactOrganization);

                var repairAudit = await db.AuditLogs.AsNoTracking()
                    .Where(a => a.VisitInstanceId == instanceId
                                && a.Action == RepairLegacyOperationalContactCommandHandler.RepairAction)
                    .SingleAsync();
                Assert.Equal(requestId, repairAudit.VisitRequestId);
                Assert.Equal(AdminUser, repairAudit.ActorUserId);
                Assert.Contains(corruptingAuditId.ToString(), repairAudit.Reason);
            }

            // ── Second APPLY: idempotent — nothing left to repair. ──
            var second = await RunAsync(mode: "APPLY");
            Assert.Equal(0, second.Repaired);
            Assert.Equal(0, second.SafeAutoRepair);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── R2: a later legitimate successor means never auto-restore ───────────────────────────

    [Theory]
    [InlineData(IdentityChangeStatuses.Applied)]
    public async Task R2_A_later_legitimate_successor_is_never_auto_restored(string bTerminalStatus)
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactId; string contactEmail, successorEmail;
            using (var db = NewContext())
            {
                (contactId, contactEmail) = await VisitorUserAsync(db);
                (_, successorEmail) = await VisitorUserAsync(db, contactId);
            }

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var mail = new FakeEmail();
            var (instanceId, _) = await ConfirmedCampusAsync("HN", contactId, contactEmail, mail, requestId);
            var (corruptingAuditId, bChangeId) = await CorruptAsync(requestId, instanceId, contactId, bTerminalStatus, successorEmail);

            // B legitimately applied — the campus's CURRENT holder is B now, so this is already
            // NOT_CORRUPTED from the detector's own safety rule (a non-null current holder is never
            // overwritten), which is itself proof the classification never reaches SAFE here.
            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == instanceId);
                var b = await db.VisitRequestIdentityChanges.SingleAsync(c => c.IdentityChangeId == bChangeId);
                instance.OperationalContactUserId = b.NewUserId;
                instance.Status = VisitInstanceStatuses.WaitingRequestApproval;
                await db.SaveChangesAsync();
            }

            var dry = await RunAsync(mode: null);
            Assert.DoesNotContain(dry.SafeAutoRepairCandidates, c => c.CorruptingAuditLogId == corruptingAuditId);
            Assert.DoesNotContain(dry.ManualReviewCandidates, c => c.CorruptingAuditLogId == corruptingAuditId);
            Assert.True(dry.NotCorrupted >= 1);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── R3: old user id known but exact old snapshot unrecoverable (self-matched A) ─────────

    [Fact]
    public async Task R3_A_self_matched_holder_with_no_invitation_snapshot_goes_to_manual_review()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var registrantEmail = V2SeedActor.Email(Registrant);
            string successorEmail;
            using (var db = NewContext()) (_, successorEmail) = await VisitorUserAsync(db);

            // The registrant's own verified email self-matches at CREATE — Registrant becomes the
            // confirmed holder immediately, with NO identity-change row raised at all.
            requestId = await CreateAsync(Campus("HN", registrantEmail));
            using var seed = NewContext();
            var instance = await seed.VisitRequestCampuses.AsNoTracking().SingleAsync(c => c.VisitRequestId == requestId);
            Assert.Equal(Registrant, instance.OperationalContactUserId);
            Assert.Empty(await seed.VisitRequestIdentityChanges.AsNoTracking()
                .Where(c => c.VisitRequestId == requestId).ToListAsync());

            var (corruptingAuditId, _) = await CorruptAsync(
                requestId, instance.VisitInstanceId, Registrant, IdentityChangeStatuses.Declined, successorEmail);

            var dry = await RunAsync(mode: null);
            var candidate = Assert.Single(dry.ManualReviewCandidates, c => c.CorruptingAuditLogId == corruptingAuditId);
            Assert.Contains("xác nhận ban đầu", candidate.Reason);
            Assert.DoesNotContain(dry.SafeAutoRepairCandidates, c => c.CorruptingAuditLogId == corruptingAuditId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── R4: an ordinary, never-corrupted confirmation contributes no candidate at all ────────

    [Fact]
    public async Task R4_A_normal_confirmed_campus_with_no_replace_history_is_never_a_candidate()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactId; string contactEmail;
            using (var db = NewContext()) (contactId, contactEmail) = await VisitorUserAsync(db);

            requestId = await CreateAsync(Campus("HN", contactEmail));
            var mail = new FakeEmail();
            var (instanceId, _) = await ConfirmedCampusAsync("HN", contactId, contactEmail, mail, requestId);

            var dry = await RunAsync(mode: null);
            Assert.DoesNotContain(dry.SafeAutoRepairCandidates, c => c.VisitInstanceId == instanceId);
            Assert.DoesNotContain(dry.ManualReviewCandidates, c => c.VisitInstanceId == instanceId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── R5: multi-campus — only the target instance is ever touched ─────────────────────────

    [Fact]
    public async Task R5_Repairing_one_campus_never_touches_a_confirmed_sibling()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            ulong contactHn, contactHcm; string emailHn, emailHcm, successorEmail;
            using (var db = NewContext())
            {
                (contactHn, emailHn) = await VisitorUserAsync(db);
                (contactHcm, emailHcm) = await VisitorUserAsync(db, contactHn);
                (_, successorEmail) = await VisitorUserAsync(db, contactHn, contactHcm);
            }

            requestId = await CreateAsync(Campus("HN", emailHn), Campus("HCM", emailHcm));

            ulong hnInstanceId, hcmInstanceId;
            using (var db = NewContext())
            {
                var rows = await db.VisitRequestCampuses.AsNoTracking().Include(c => c.FormDetail)
                    .Where(c => c.VisitRequestId == requestId).ToListAsync();
                hnInstanceId = rows.Single(c => c.FormDetail!.OperationalContactEmail == emailHn).VisitInstanceId;
                hcmInstanceId = rows.Single(c => c.FormDetail!.OperationalContactEmail == emailHcm).VisitInstanceId;
            }

            var mail = new FakeEmail();
            await ConfirmSpecificAsync(requestId, hnInstanceId, contactHn, emailHn, mail);
            await ConfirmSpecificAsync(requestId, hcmInstanceId, contactHcm, emailHcm, mail);

            var (corruptingAuditId, _) = await CorruptAsync(
                requestId, hnInstanceId, contactHn, IdentityChangeStatuses.Expired, successorEmail);

            var applied = await RunAsync(mode: "APPLY");
            Assert.Equal(1, applied.Repaired);

            using var check = NewContext();
            var hn = await check.VisitRequestCampuses.AsNoTracking().SingleAsync(c => c.VisitInstanceId == hnInstanceId);
            Assert.Equal(contactHn, hn.OperationalContactUserId);

            var hcm = await check.VisitRequestCampuses.AsNoTracking().Include(c => c.FormDetail)
                .SingleAsync(c => c.VisitInstanceId == hcmInstanceId);
            Assert.Equal(contactHcm, hcm.OperationalContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, hcm.Status);
            Assert.Equal(emailHcm, hcm.FormDetail!.OperationalContactEmail);
            Assert.False(await check.AuditLogs.AsNoTracking().AnyAsync(
                a => a.VisitInstanceId == hcmInstanceId
                     && a.Action == RepairLegacyOperationalContactCommandHandler.RepairAction));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Accepts the specific campus's own invitation among several on one multi-campus request.</summary>
    private static async Task ConfirmSpecificAsync(
        ulong requestId, ulong instanceId, ulong contactId, string contactEmail, FakeEmail mail)
    {
        using var db = NewContext();
        var invitation = await db.VisitRequestIdentityChanges.AsNoTracking()
            .SingleAsync(c => c.VisitInstanceId == instanceId);
        var invitations = Invitations(db, mail);
        var tokens = await invitations.MintInvitationTokensAsync(invitation.IdentityChangeId, CancellationToken.None);
        Assert.NotNull(tokens);
        await db.SaveChangesAsync();
        await invitations.DispatchInvitationEmailAsync(invitation.IdentityChangeId, tokens!, CancellationToken.None);
        await Accept(db, contactId, contactEmail, mail).Handle(
            new AcceptOperationalContactConfirmationCommand(tokens!.AcceptToken), CancellationToken.None);
    }
}
