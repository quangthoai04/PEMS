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
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Enums;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase E-1 — SAFE EDIT (plan §16.6) against pems_pr3_test. Committed create per test + child-first
/// cascade cleanup (v2_requests stays 0). Covers: classifier fail-closed table, apply+revision+audit,
/// editor policy, per-instance targeting + mixed recompute, the 24h cutoff with the privacy-urgent
/// media-withdrawal exemption (URGENT notification), and stale-version 409s.
/// </summary>
public sealed class VisitSafeEditV2Tests
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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
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

    private sealed class RecordingNotifications : INotificationService
    {
        public List<CreateNotificationRequest> Sent { get; } = new();
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct)
        {
            Sent.AddRange(requests);
            return Task.CompletedTask;
        }
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType, string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static SubmitVisitSafeEditCommandHandler Handler(
        ApplicationDbContext db, ulong actor, RecordingNotifications? notifications = null)
        => new(db, new FakeUser(actor), new FixedClock(), new VisitSafeEditService(db),
            notifications ?? new RecordingNotifications(),
            NullLogger<SubmitVisitSafeEditCommandHandler>.Instance, ReadOn, WriteOn);

    private static CampusVisitFormDto Campus(string code, DateTime start, string media = "AGREED")
        => new(code, start, start.AddMinutes(120), "Đoàn Safe", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", "Xe 16 chỗ", media, null, "Ghi chú", null);

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
            new RecordingNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db));
        var form = new VisitRequestFormDataV2(
            "SE" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            new ContactPointDto("Registrant", "Org", "+8491", V2SeedActor.Email(Registrant)), // A==B → ACTIVE
            null, campuses.ToList());
        var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);
        return created.VisitRequestId;
    }

    private static async Task<(int RequestVersion, Dictionary<ulong, int> InstanceVersions)> VersionsAsync(ulong requestId)
    {
        using var db = NewContext();
        var reqV = await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.RowVersion).SingleAsync();
        var instV = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .ToDictionaryAsync(c => c.VisitInstanceId, c => c.RowVersion);
        return (reqV, instV);
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE ac FROM visit_instance_amendment_changes ac JOIN visit_instance_amendments a ON a.amendment_id = ac.amendment_id WHERE a.visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_amendments WHERE visit_request_id = {0}");
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

    // ── Classifier table (pure, no DB) ────────────────────────────────────────────

    [Fact]
    public void Classifier_is_table_driven_and_fails_closed()
    {
        // SAFE set
        Assert.Equal(AmendmentChangeClasses.Safe,
            VisitFieldClassifier.ClassifyChange(VisitFieldClassifier.RegistrantPhone, "+84", "+85"));
        Assert.Equal(AmendmentChangeClasses.Safe,
            VisitFieldClassifier.ClassifyChange(VisitFieldClassifier.TransportationNote, null, "Xe"));
        // Privacy-urgent = media consent WITHDRAWAL only
        Assert.Equal(AmendmentChangeClasses.PrivacyUrgent,
            VisitFieldClassifier.ClassifyChange(VisitFieldClassifier.MediaConsentStatus, "AGREED", "DECLINED"));
        Assert.Equal(AmendmentChangeClasses.Safe,
            VisitFieldClassifier.ClassifyChange(VisitFieldClassifier.MediaConsentStatus, "DECLINED", "AGREED"));
        // Approval-sensitive + structural
        Assert.Equal(AmendmentChangeClasses.ApprovalSensitive,
            VisitFieldClassifier.ClassifyChange(VisitFieldClassifier.DelegationName, "A", "B"));
        Assert.Equal(AmendmentChangeClasses.Structural,
            VisitFieldClassifier.ClassifyChange(VisitFieldClassifier.PlannedStartAt, "x", "y"));
        Assert.False(VisitFieldClassifier.IsSafeEditable(VisitFieldClassifier.Purpose));
        Assert.True(VisitFieldClassifier.IsAmendable(VisitFieldClassifier.Purpose));
        Assert.False(VisitFieldClassifier.IsAmendable(VisitFieldClassifier.RegistrantPhone));
        // Unknown path → null (callers fail closed) — primary-contact EMAIL is deliberately unknown.
        Assert.Null(VisitFieldClassifier.ClassifyChange("request.contact.email", "a@x", "b@x"));
        Assert.False(VisitFieldClassifier.IsKnown("request.contact.email"));
    }

    // ── Apply + revision + audit + targeting + recompute ─────────────────────────

    [Fact]
    public async Task Safe_edit_applies_target_only_with_revisions_audit_and_mixed_recompute()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            requestId = await CreateAsync(Campus("HN", start), Campus("HCM", start.AddDays(1)));
            var (reqV, instV) = await VersionsAsync(requestId);
            ulong instanceA;
            using (var db = NewContext())
            {
                var mixed0 = await db.VisitRequests.AsNoTracking()
                    .Where(v => v.VisitRequestId == requestId).Select(v => v.HasMixedCampusDetails).SingleAsync();
                Assert.False(mixed0); // same content on both campuses
                instanceA = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId).OrderBy(c => c.CampusId)
                    .Select(c => c.VisitInstanceId).FirstAsync();
            }

            using (var db = NewContext())
            {
                var res = await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(
                        reqV,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84999999"), // phone changed
                        null,
                        new List<SafeInstancePatchDto>
                        {
                            new(instanceA, instV[instanceA], "Xe 45 chỗ", "Ghi chú", "AGREED", null), // note changed on A only
                        })), CancellationToken.None);
                Assert.Contains(res.AppliedChanges, c => c.FieldPath == VisitFieldClassifier.RegistrantPhone);
                Assert.Contains(res.AppliedChanges, c => c.FieldPath == VisitFieldClassifier.TransportationNote
                                                          && c.VisitInstanceId == instanceA);
                Assert.Equal(2, res.AppliedChanges.Count);
            }

            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Equal("+84999999", visit.RegistrantPhone);
                Assert.True(visit.HasMixedCampusDetails); // A's transportation note now differs from B → mixed
                Assert.Equal(reqV + 1, visit.RowVersion);

                var details = await db.VisitInstanceFormDetails.AsNoTracking()
                    .Where(d => db.VisitRequestCampuses.Any(c => c.VisitRequestId == requestId && c.VisitInstanceId == d.VisitInstanceId))
                    .ToListAsync();
                var a = details.Single(d => d.VisitInstanceId == instanceA);
                var b = details.Single(d => d.VisitInstanceId != instanceA);
                Assert.Equal("Xe 45 chỗ", a.TransportationNote);
                Assert.Equal(2u, a.FormRevision);          // bumped on A
                Assert.Equal("Xe 16 chỗ", b.TransportationNote); // sibling untouched
                Assert.Equal(1u, b.FormRevision);

                Assert.True(await db.VisitInstanceFormRevisionHistories.AsNoTracking()
                    .AnyAsync(r => r.VisitInstanceId == instanceA && r.SourceType == "SAFE_EDIT" && r.FormRevision == 2));
                Assert.True(await db.VisitRequestRevisionHistories.AsNoTracking()
                    .AnyAsync(r => r.VisitRequestId == requestId && r.SourceType == "SAFE_EDIT"));

                var audit = await db.AuditLogs.AsNoTracking().Include(x => x.Changes)
                    .Where(x => x.VisitRequestId == requestId && x.Action == "VISIT_SAFE_FIELDS_UPDATED")
                    .SingleAsync();
                Assert.Equal(2, audit.Changes.Count);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Editor_policy_and_stale_versions_are_enforced()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();
            var patch = new VisitRequestSafeEditDto(reqV,
                new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84000001"), null, null);

            // Unrelated visitor → 403.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Handler(db, 20).Handle(new SubmitVisitSafeEditCommand(requestId, patch), CancellationToken.None));

            // Stale request version → stable 409.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        patch with { ExpectedRequestRowVersion = reqV + 7 }), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.VisitFormConcurrencyConflict, ex.ErrorCode);
            }
            // Stale instance version → stable 409.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV, null, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, instV[instance] + 5, "Xe khác", "Ghi chú", "AGREED", null),
                        })), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.VisitFormConcurrencyConflict, ex.ErrorCode);
            }
            using (var db = NewContext())
                Assert.NotEqual("+84000001", (await db.VisitRequests.AsNoTracking()
                    .SingleAsync(v => v.VisitRequestId == requestId)).RegistrantPhone); // nothing applied
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Cutoff_blocks_normal_safe_edit_but_media_withdrawal_applies_with_urgent_notify()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddHours(10))); // starts INSIDE the 24h window
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            // Normal safe change <24h → blocked.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV, null, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, instV[instance], "Xe khác", "Ghi chú", "AGREED", null),
                        })), CancellationToken.None));
                Assert.Equal(VisitRequestErrorCodes.VisitCancelWindowExpired, ex.ErrorCode);
            }

            // Privacy-urgent media WITHDRAWAL (+ its note) → applies even <24h, URGENT notification.
            var notifications = new RecordingNotifications();
            using (var db = NewContext())
            {
                var res = await Handler(db, Registrant, notifications).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance], "Xe 16 chỗ", "Ghi chú", "DECLINED", "Rút quyền hình ảnh"),
                    })), CancellationToken.None);
                Assert.Contains(res.AppliedChanges,
                    c => c.FieldPath == VisitFieldClassifier.MediaConsentStatus
                         && c.ChangeClass == AmendmentChangeClasses.PrivacyUrgent);
            }
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking()
                    .SingleAsync(d => d.VisitInstanceId == instance);
                Assert.Equal("DECLINED", detail.MediaConsentStatus);
            }
            Assert.NotEmpty(notifications.Sent);
            Assert.All(notifications.Sent, n => Assert.Equal(NotificationPriority.URGENT, n.Priority));
        }
        finally { await CleanupAsync(requestId); }
    }
}
