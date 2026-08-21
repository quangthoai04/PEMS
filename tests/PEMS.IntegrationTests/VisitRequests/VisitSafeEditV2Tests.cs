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
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Policies;
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
            // The contact is the REGISTRANT'S own address, so the campus self-matches at submit: confirmed
            // with no invitation, and the request is past the confirmation gate from the start. This suite
            // does not test that gate, and a campus behind it can be neither decided nor moved forward.
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "EN", "Xe 16 chỗ", media, null, null);

    /// <summary>
    /// How far forward a set of campuses has to be FILED so the create service accepts it. A visit
    /// cannot be created inside <see cref="VisitMutationPolicy.MinScheduleLeadHours"/>; a request only
    /// ends up that close to its date by the date approaching. The schedule is shifted back by the same
    /// amount straight after, so relative order and durations survive and the request lands exactly
    /// where the test wants it — which is what these cases are about: the ACTION cutoff, a different
    /// rule from the scheduling floor.
    /// </summary>
    private static TimeSpan LeadTimeShift(IEnumerable<CampusVisitFormDto> campuses)
    {
        var earliest = campuses.Min(c => c.PlannedStartAt);
        var floor = Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours).AddMinutes(5);
        return earliest < floor ? floor - earliest : TimeSpan.Zero;
    }

    private static CampusVisitFormDto Shifted(CampusVisitFormDto c, TimeSpan by) =>
        by == TimeSpan.Zero ? c : c with { PlannedStartAt = c.PlannedStartAt + by, PlannedEndAt = c.PlannedEndAt + by };

    /// <summary>Moves a committed request's schedule back by <paramref name="by"/> — "time passed".</summary>
    private static async Task RewindScheduleAsync(ulong requestId, TimeSpan by)
    {
        if (by == TimeSpan.Zero) return;
        using var db = NewContext();
        var instances = await db.VisitRequestCampuses.Where(c => c.VisitRequestId == requestId).ToListAsync();
        foreach (var instance in instances)
        {
            instance.PlannedStartAt -= by;
            instance.PlannedEndAt -= by;
        }
        await db.SaveChangesAsync();
    }

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        var shift = LeadTimeShift(campuses);
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
            new RecordingNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "SE" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.Select(c => Shifted(c, shift)).ToList());
        var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);
        await RewindScheduleAsync(created.VisitRequestId, shift);
        return created.VisitRequestId;
    }

    /// <summary>
    /// Drives every campus of a committed request to ASSIGNED, using the real transition order
    /// (instances decided under the still-pending parent, then the parent flips).
    ///
    /// Safe edit is a POST-decision correction now: a still-pending campus belongs to pending-edit,
    /// which can change everything, so these tests have to start from a decided campus to exercise it
    /// at all.
    /// </summary>
    private static async Task ApproveAllAsync(ulong requestId)
    {
        using var db = NewContext();
        var visit = await db.VisitRequests.Include(v => v.CampusInstances)
            .SingleAsync(v => v.VisitRequestId == requestId);
        foreach (var instance in visit.CampusInstances)
        {
            instance.Status = VisitInstanceStatuses.Assigned;
            instance.CurrentHostUserId = instance.CoordinatorUserId; // leader self-hosts (valid seed user)
            instance.HostAssignedBy = instance.CoordinatorUserId;
            instance.HostAssignedAt = Now;
            instance.DecidedBy = instance.CoordinatorUserId;
            instance.DecidedAt = Now;
            instance.DecisionActorRole = "STAFF_LEADER";
            instance.DecisionSource = "STANDARD_CAMPUS_REVIEW";
            instance.RowVersion += 1;
        }
        await db.SaveChangesAsync();
        visit.Status = VisitRequestStatuses.Approved;
        visit.RowVersion += 1;
        await db.SaveChangesAsync();
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
            // Safe edit only applies to a DECIDED campus now — a pending one belongs to pending-edit.
            await ApproveAllAsync(requestId);
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
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84987654321", "VN"), // phone changed
                        new List<SafeInstancePatchDto>
                        {
                            // Only the transport note is sent — the sparse patch carries what changed
                            // and nothing else, so campus B is absent from the payload entirely.
                            new(instanceA, instV[instanceA], null, "Xe 45 chỗ", null, null),
                        })), CancellationToken.None);
                Assert.Contains(res.AppliedChanges, c => c.FieldPath == VisitFieldClassifier.RegistrantPhone);
                Assert.Contains(res.AppliedChanges, c => c.FieldPath == VisitFieldClassifier.TransportationNote
                                                          && c.VisitInstanceId == instanceA);
                Assert.Equal(2, res.AppliedChanges.Count);
            }

            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Equal("+84987654321", visit.RegistrantPhone);
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
                new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84000001", "VN"), null);

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
                        new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, instV[instance] + 5, null, "Xe khác", "AGREED", null),
                        })), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.VisitFormConcurrencyConflict, ex.ErrorCode);
            }
            using (var db = NewContext())
                Assert.NotEqual("+84000001", (await db.VisitRequests.AsNoTracking()
                    .SingleAsync(v => v.VisitRequestId == requestId)).RegistrantPhone); // nothing applied
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// The cutoff applies to EVERY safe edit, with no per-field exception.
    ///
    /// <para>
    /// A media-consent withdrawal used to be waved through it on privacy grounds. Two things were wrong
    /// with that. The answer to "until when may I change this" depended on which field the payload
    /// happened to carry, which nothing in the UI could explain; and a campus hours from starting had
    /// already printed its list and briefed its Host, so the change landed after the only people who
    /// could act on it had stopped looking. Withdrawing consent late is a conversation with the Host
    /// now — the campus can honour it in the room, which a database write at that point cannot.
    /// </para>
    /// <para>
    /// The PRIVACY_URGENT classification itself is untouched: it still drives the URGENT notification
    /// while the window is open (asserted in the target-only test above). It just no longer moves a
    /// deadline.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Cutoff_blocks_every_safe_edit_including_a_media_withdrawal()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            // Decided (safe edit is a post-decision correction) and starting INSIDE the lead time.
            requestId = await CreateAsync(Campus("HN", Now.AddHours(VisitMutationPolicy.RequiredLeadHours - 1)));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            // Normal safe change past the cutoff → blocked, and the refusal NAMES the campus and the
            // deadline. On a multi-campus request "không thể sửa" alone leaves the user checking all
            // of them to work out which one closed.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<VisitMutationRefusedException>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, instV[instance], null, "Xe khác", null, null),
                        })), CancellationToken.None));
                Assert.Equal(VisitMutationErrorCodes.CutoffReached, ex.ErrorCode);
                Assert.NotNull(ex.CampusName);
                Assert.NotNull(ex.CutoffAt);
                Assert.Equal(VisitMutationPolicy.RequiredLeadHours, ex.RequiredLeadHours);
            }

            // A withdrawal travelling WITH a note is refused, exactly like the note alone.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<VisitMutationRefusedException>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, instV[instance], null, null, "DECLINED", "Xin thêm một chỗ đỗ xe"),
                        })), CancellationToken.None));
                Assert.Equal(VisitMutationErrorCodes.CutoffReached, ex.ErrorCode);
            }

            // And a withdrawal ON ITS OWN — the case that used to be exempt — is refused too.
            var notifications = new RecordingNotifications();
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<VisitMutationRefusedException>(() =>
                    Handler(db, Registrant, notifications).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, instV[instance], null, null, "DECLINED", null),
                        })), CancellationToken.None));
                Assert.Equal(VisitMutationErrorCodes.CutoffReached, ex.ErrorCode);
            }
            using (var db = NewContext())
            {
                // Nothing was written, and nobody was told about a change that did not happen.
                var detail = await db.VisitInstanceFormDetails.AsNoTracking()
                    .SingleAsync(d => d.VisitInstanceId == instance);
                Assert.NotEqual("DECLINED", detail.MediaConsentStatus);
            }
            Assert.Empty(notifications.Sent);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Producer contract (plan continuation §17): privacy-urgent notification exact-instance target ──

    /// <summary>
    /// A media-consent withdrawal touching exactly ONE campus is unambiguous — every recipient (that
    /// campus's Host + its Staff Leaders) is being told about that specific instance, so the
    /// notification must NAME it (VisitInstanceId/CampusId), not fall back to the generic
    /// request-level target the frontend can only resolve to an ambiguous "pick a campus" landing.
    /// </summary>
    [Fact]
    public async Task Privacy_withdrawal_on_a_single_campus_names_the_exact_instance()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            requestId = await CreateAsync(Campus("HN", start), Campus("HCM", start.AddDays(1)));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);

            ulong hnInstance;
            ulong hnCampusId;
            using (var db = NewContext())
            {
                var hn = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId).OrderBy(c => c.CampusId).FirstAsync();
                hnInstance = hn.VisitInstanceId;
                hnCampusId = hn.CampusId;
            }

            var notifications = new RecordingNotifications();
            using (var db = NewContext())
            {
                await Handler(db, Registrant, notifications).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(hnInstance, instV[hnInstance], null, null, "DECLINED", null),
                    })), CancellationToken.None);
            }

            Assert.NotEmpty(notifications.Sent);
            foreach (var n in notifications.Sent)
            {
                Assert.Equal(requestId, n.VisitRequestId);
                Assert.Equal(hnInstance, n.VisitInstanceId);
                Assert.Equal(hnCampusId, n.CampusId);
                Assert.Contains(NotificationEventKeys.VisitPrivacyConsentWithdrawn, n.MetadataJson);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// A withdrawal touching MULTIPLE campuses in one save must NOT guess a single instance to name —
    /// recipients differ per campus (an HCM leader was never told about an HN-only change), so there
    /// is no one instance id that is correct for the whole recipient set. Stays request-level.
    /// </summary>
    [Fact]
    public async Task Privacy_withdrawal_touching_multiple_campuses_never_guesses_a_single_instance()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            requestId = await CreateAsync(Campus("HN", start), Campus("HCM", start.AddDays(1)));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instances = instV.Keys.ToList();

            var notifications = new RecordingNotifications();
            using (var db = NewContext())
            {
                await Handler(db, Registrant, notifications).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, instances.Select(i =>
                        new SafeInstancePatchDto(i, instV[i], null, null, "DECLINED", null)).ToList())),
                    CancellationToken.None);
            }

            Assert.NotEmpty(notifications.Sent);
            Assert.All(notifications.Sent, n => Assert.Null(n.VisitInstanceId));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Registrant nationality + partner identity (PEMS_PATCH_SAFE_EDIT_AMENDMENT_PARTNER_SEARCH) ──

    [Fact]
    public async Task Registrant_nationality_can_be_changed_via_safe_edit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, _) = await VersionsAsync(requestId);
            using (var db = NewContext())
            {
                var res = await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84912345678", "JP"), null)),
                    CancellationToken.None);
                Assert.Contains(res.AppliedChanges, c => c.FieldPath == VisitFieldClassifier.RegistrantNationality);
            }
            using (var db = NewContext())
                // Patch 4: "JP" resolves and persists as the canonical Vietnamese short name.
                Assert.Equal("Nhật Bản", (await db.VisitRequests.AsNoTracking()
                    .SingleAsync(v => v.VisitRequestId == requestId)).RegistrantNationality);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Blank_registrant_nationality_is_refused()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, _) = await VersionsAsync(requestId);
            using var db = NewContext();
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+8491", "   "), null)),
                    CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.SafeEditFieldNotAllowed, ex.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Request-revision numbering (Fix Group E, VISIT_HISTORY_INTEGRITY plan) ───────────────────
    //
    // A registrant-level safe edit stages a possible RECOVERED_BASELINE row via
    // VisitRevisionBaselineGuard.EnsureRequestBaselineAsync (unflushed), then must number its OWN
    // revision from VisitRevisionBaselineGuard.NextRequestRevisionAsync — which unions the DB MAX
    // with EF's .Local staged rows — rather than a raw MaxAsync query, which cannot see the staged
    // baseline and used to collide both writers on revision 1.

    /// <summary>
    /// CreateVisitRequestV2CommandHandler actually writes its own request-level revision 1
    /// (SourceType=CREATE) at creation time, so to exercise the genuinely-empty-history case this
    /// plan describes (legacy/migrated requests whose chain predates that row, or whose CREATE row
    /// was never written) the fixture explicitly clears it first — mirroring how the per-campus
    /// baseline-recovery test simulates the same gap for instance-level history.
    /// </summary>
    [Fact]
    public async Task Registrant_safe_edit_on_empty_request_history_recovers_baseline_without_colliding()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, _) = await VersionsAsync(requestId);

            using (var db = NewContext())
            {
                var seedRows = await db.VisitRequestRevisionHistories
                    .Where(r => r.VisitRequestId == requestId).ToListAsync();
                db.VisitRequestRevisionHistories.RemoveRange(seedRows);
                await db.SaveChangesAsync();
                var historyCountBefore = await db.VisitRequestRevisionHistories
                    .CountAsync(r => r.VisitRequestId == requestId);
                Assert.Equal(0, historyCountBefore);
            }

            using (var db = NewContext())
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant Mới", "Org", "Job", "+84912345678", "VN"), null)),
                    CancellationToken.None);

            using (var db = NewContext())
            {
                var revisions = await db.VisitRequestRevisionHistories.AsNoTracking()
                    .Where(r => r.VisitRequestId == requestId)
                    .OrderBy(r => r.RequestRevision)
                    .ToListAsync();
                // No collision: exactly two rows, never two rows both claiming revision 1.
                Assert.Equal(2, revisions.Count);
                Assert.Equal(1u, revisions[0].RequestRevision);
                Assert.Equal(VisitRevisionBaselineGuard.BaselineReason, revisions[0].Reason);
                Assert.Equal(FormRevisionSourceTypes.Migration, revisions[0].SourceType);
                Assert.Equal(2u, revisions[1].RequestRevision);
                Assert.Equal("SAFE_EDIT", revisions[1].SourceType);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Registrant_safe_edit_after_existing_revisions_continues_the_sequence()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, _) = await VersionsAsync(requestId);

            using (var db = NewContext())
            {
                // Replace the CREATE-time revision 1 with a deterministic chain 1..4 so the "next
                // revision" this test asserts on is unambiguous. Deleted and re-added in separate
                // SaveChanges calls — same unique key in one batch is not safe to order on.
                var seedRows = await db.VisitRequestRevisionHistories
                    .Where(r => r.VisitRequestId == requestId).ToListAsync();
                db.VisitRequestRevisionHistories.RemoveRange(seedRows);
                await db.SaveChangesAsync();
                for (uint i = 1; i <= 4; i++)
                    db.VisitRequestRevisionHistories.Add(new VisitRequestRevisionHistory
                    {
                        VisitRequestId = requestId,
                        RequestRevision = i,
                        SourceType = "SAFE_EDIT",
                        SnapshotJson = "{}",
                        AppliedBy = Registrant,
                        AppliedAt = Now,
                        Reason = Guid.NewGuid().ToString("N"),
                    });
                await db.SaveChangesAsync();
            }

            using (var db = NewContext())
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant Kế Tiếp", "Org", "Job", "+84912345678", "VN"), null)),
                    CancellationToken.None);

            using (var db = NewContext())
            {
                var revisions = await db.VisitRequestRevisionHistories.AsNoTracking()
                    .Where(r => r.VisitRequestId == requestId)
                    .OrderBy(r => r.RequestRevision)
                    .ToListAsync();
                Assert.Equal(5, revisions.Count); // no extra baseline — the chain already had a first link
                Assert.Equal(5u, revisions[^1].RequestRevision);
                Assert.Equal("SAFE_EDIT", revisions[^1].SourceType);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Selecting_an_existing_partner_persists_canonical_organization_and_id_with_audit()
    {
        RequireDb();
        ulong requestId = 0;
        // ACTIVE + APPROVED + PUBLIC seed fixture — selectable on a registration form (see
        // RequestFormPartnerSelectableTests.ApprovedPublic).
        const ulong approvedPublicPartnerId = 103;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, _) = await VersionsAsync(requestId);

            string expectedOrg;
            using (var db = NewContext())
            {
                var partner = await db.Partners.AsNoTracking().SingleAsync(p => p.PartnerId == approvedPublicPartnerId);
                expectedOrg = string.IsNullOrWhiteSpace(partner.ShortName)
                    ? partner.Name : $"{partner.Name} ({partner.ShortName})";
            }

            using (var db = NewContext())
            {
                var res = await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant", "Văn bản người dùng gõ tay — phải bị bỏ qua",
                            "Job", "+84912345678", "VN", approvedPublicPartnerId), null)),
                    CancellationToken.None);
                Assert.Contains(res.AppliedChanges, c => c.FieldPath == VisitFieldClassifier.RegistrantPartnerId);
                Assert.Contains(res.AppliedChanges, c => c.FieldPath == VisitFieldClassifier.RegistrantOrganization);
            }
            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Equal(approvedPublicPartnerId, visit.PartnerId);
                // Canonical text resolved server-side — NOT the client-supplied text next to the id.
                Assert.Equal(expectedOrg, visit.RegistrantOrganization);
                var audit = await db.AuditLogs.AsNoTracking().Include(x => x.Changes)
                    .Where(x => x.VisitRequestId == requestId && x.Action == "VISIT_SAFE_FIELDS_UPDATED")
                    .SingleAsync();
                Assert.Contains(audit.Changes, c => c.FieldName == VisitFieldClassifier.RegistrantPartnerId);
                Assert.Contains(audit.Changes, c => c.FieldName == VisitFieldClassifier.RegistrantOrganization);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Editing_organization_to_free_text_after_selecting_a_partner_clears_the_partner_id()
    {
        RequireDb();
        ulong requestId = 0;
        const ulong approvedPublicPartnerId = 103;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV1, _) = await VersionsAsync(requestId);
            using (var db = NewContext())
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV1,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84912345678", "VN", approvedPublicPartnerId), null)),
                    CancellationToken.None);

            var (reqV2, _) = await VersionsAsync(requestId);
            using (var db = NewContext())
            {
                var res = await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV2,
                        new SafeRegistrantPatchDto("Registrant", "Tổ chức tự nhập", "Job", "+84912345678", "VN", null), null)),
                    CancellationToken.None);
                Assert.Contains(res.AppliedChanges, c => c.FieldPath == VisitFieldClassifier.RegistrantPartnerId);
            }
            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Null(visit.PartnerId);
                Assert.Equal("Tổ chức tự nhập", visit.RegistrantOrganization);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Contact profile has exactly one door now — "Manage the contact role" (PEMS_CONTACT_ONE_DOOR) ──
    // SE-NEW-01/02/03: a handcrafted request that still tries to patch the contact's fullName,
    // organization or phone through Safe Edit is refused outright, not silently applied or dropped —
    // the guard trips on the block being present at all, regardless of which sub-field it carries.

    [Fact]
    public async Task Contact_profile_patch_is_refused_on_safe_edit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, instV[instance],
                                new SafeContactPatchDto("Tên mới", "Org mới", null, "+8499999999"),
                                null, null, null),
                        })), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.SafeEditFieldNotAllowed, ex.ErrorCode);
            }
            using (var db = NewContext())
            {
                // Refused means nothing applied — not even the sibling fields (nothing else was sent).
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.NotEqual("Tên mới", detail.OperationalContactFullName);
                Assert.NotEqual("Org mới", detail.OperationalContactOrganization);
                Assert.NotEqual("+8499999999", detail.OperationalContactPhone);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // SE-NEW-04: registrant safe fields still apply normally, even in the same request as a campus that
    // sends nothing else — the contact guard above is scoped to the contact block only.
    [Fact]
    public async Task Registrant_safe_fields_still_apply_when_no_contact_patch_is_sent()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, _) = await VersionsAsync(requestId);
            using (var db = NewContext())
            {
                var res = await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant Mới", "Org", "Job", "+84912345678", "VN"), null)),
                    CancellationToken.None);
                Assert.Contains(res.AppliedChanges, c => c.FieldPath == VisitFieldClassifier.RegistrantFullName);
            }
            using (var db = NewContext())
                Assert.Equal("Registrant Mới", (await db.VisitRequests.AsNoTracking()
                    .SingleAsync(v => v.VisitRequestId == requestId)).RegistrantFullName);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Non_selectable_partner_id_is_refused()
    {
        RequireDb();
        ulong requestId = 0;
        // PENDING_APPROVAL, own-campus visible only — not selectable on a registration form (see
        // RequestFormPartnerSelectableTests.PendingOwnCampus).
        const ulong pendingPartnerId = 120;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, _) = await VersionsAsync(requestId);
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV,
                            new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+8491", "VN", pendingPartnerId), null)),
                        CancellationToken.None));
                Assert.Equal(GuestOrganizationPartnerPolicy.NotSelectableCode, ex.ErrorCode);
            }
            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Null(visit.PartnerId);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Patch 4 — nationality contract ──────────────────────────────────────────

    [Fact]
    public async Task SafeEdit_rejects_a_registrant_nationality_that_does_not_resolve()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, _) = await VersionsAsync(requestId);
            using var db = NewContext();
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84912345678", "FPTU123"), null)),
                    CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.SafeEditFieldNotAllowed, ex.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Legacy compatibility (Patch 4 decision, explicit constraint): a safe edit that only touches the
    /// full name must not be blocked — or silently rewritten — because the request's legacy nationality
    /// does not resolve to a real country. Simulates pre-Patch-4 data with a direct write (bypassing the
    /// service), then edits only the name while echoing nationality back unchanged.
    /// </summary>
    [Fact]
    public async Task SafeEdit_of_an_unrelated_field_leaves_an_unresolvable_legacy_nationality_untouched()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            using (var seed = NewContext())
            {
                var visit = await seed.VisitRequests.SingleAsync(v => v.VisitRequestId == requestId);
                visit.RegistrantNationality = "Legacy Unrecognized Value";
                await seed.SaveChangesAsync();
            }
            var (reqV, _) = await VersionsAsync(requestId);

            using (var db = NewContext())
            {
                // Must NOT throw: the nationality is echoed back unchanged, not genuinely re-typed.
                var res = await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant Đổi Tên", "Org", "Job", "+84912345678", "Legacy Unrecognized Value"), null)),
                    CancellationToken.None);
                Assert.DoesNotContain(res.AppliedChanges, c => c.FieldPath == VisitFieldClassifier.RegistrantNationality);
            }
            using (var db = NewContext())
            {
                var saved = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Equal("Registrant Đổi Tên", saved.RegistrantFullName);
                Assert.Equal("Legacy Unrecognized Value", saved.RegistrantNationality);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// The Patch 4 decision's own example: "Hàn Quốc" / "South Korea" / "KR" must all resolve to the
    /// SAME canonical value — standing in for "VI UI / EN UI both persist canonical VI".
    /// </summary>
    [Theory]
    [InlineData("Hàn Quốc")]
    [InlineData("South Korea")]
    [InlineData("KR")]
    public async Task SafeEdit_persists_the_same_canonical_value_regardless_of_input_spelling(string input)
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, _) = await VersionsAsync(requestId);
            using (var db = NewContext())
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84912345678", input), null)),
                    CancellationToken.None);
            using (var db = NewContext())
                Assert.Equal("Hàn Quốc", (await db.VisitRequests.AsNoTracking()
                    .SingleAsync(v => v.VisitRequestId == requestId)).RegistrantNationality);
        }
        finally { await CleanupAsync(requestId); }
    }
}
