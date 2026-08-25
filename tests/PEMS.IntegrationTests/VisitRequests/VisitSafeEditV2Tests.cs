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
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Common;
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
        // Optional role/subRole/campus (default: plain Visitor, matching every pre-existing call site in
        // this file) — additive so `new FakeUser(actor)` everywhere else is unaffected. Only the GAP-E
        // amendment-approval test needs a Staff Leader actor.
        public FakeUser(ulong id, string role = RoleCodes.Visitor, string? subRole = null, ulong? campusId = null)
        {
            _id = id; RoleCode = role; SubRole = subRole; PrimaryCampusId = campusId;
        }
        public bool IsAuthenticated => true;
        public ulong? UserId => _id;
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
        => new(db, new FakeUser(actor), new FixedClock(), new VisitSafeEditService(db, new NoopInvitations()),
            notifications ?? new RecordingNotifications(),
            NullLogger<SubmitVisitSafeEditCommandHandler>.Instance, ReadOn, WriteOn);

    /// <summary>
    /// No-op <see cref="IOperationalContactInvitationService"/> for tests that don't exercise the
    /// pending-invitation-snapshot refresh (plan CanhIter3FixBug) — "no pending invitation found" is a
    /// legitimate, common answer, and every contact-metadata test below creates its campus with a
    /// self-matched (already-confirmed) contact, so there is never a live invitation to refresh anyway.
    /// </summary>
    private sealed class NoopInvitations : IOperationalContactInvitationService
    {
        public Task<OperationalContactInvitationTokens?> MintInvitationTokensAsync(
            ulong identityChangeId, CancellationToken ct) => Task.FromResult<OperationalContactInvitationTokens?>(null);
        public Task DispatchInvitationEmailAsync(
            ulong identityChangeId, OperationalContactInvitationTokens tokens, CancellationToken ct) => Task.CompletedTask;
        public Task<VisitRequestIdentityChange?> LockChangeAsync(ulong identityChangeId, CancellationToken ct)
            => Task.FromResult<VisitRequestIdentityChange?>(null);
        public Task<VisitRequestIdentityChange?> LockPendingChangeForInstanceAsync(
            ulong visitInstanceId, CancellationToken ct) => Task.FromResult<VisitRequestIdentityChange?>(null);
    }

    private static CampusVisitFormDto Campus(string code, DateTime start, string media = "AGREED", string? contactPhone = "+8410")
        => new(code, start, start.AddMinutes(120), "Đoàn Safe", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            // The contact is the REGISTRANT'S own address, so the campus self-matches at submit: confirmed
            // with no invitation, and the request is past the confirmation gate from the start. This suite
            // does not test that gate, and a campus behind it can be neither decided nor moved forward.
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", contactPhone, V2SeedActor.Email(Registrant)),
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

    // ── Same-person contact metadata + relation (plan CanhIter3FixBug) ──────────────────────────────
    // Sửa nhanh now edits the operational contact's own details directly (email locked, relation to a
    // delegation member direct — never an amendment). Campus(...) seeds the contact as the REGISTRANT'S
    // own address, self-matched at submit, so it is already confirmed and Safe-Edit-eligible the moment
    // the campus is decided — no separate confirmation flow needed for these tests.

    private static async Task<ulong> SoleGuestMemberIdAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitGuestMembers.AsNoTracking()
            .Where(m => m.VisitRequestId == requestId).Select(m => m.GuestMemberId).SingleAsync();
    }

    [Fact]
    public async Task Contact_metadata_only_edit_applies_without_bumping_form_revision()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();
            // detail.RowVersion is a SEPARATE counter from the campus's own RowVersion (instV above) —
            // ApproveAllAsync bumps the campus one but never touches VisitInstanceFormDetail, so the two
            // diverge across the lifecycle. Captured directly here rather than assumed in lockstep.
            int detailRowVersionBefore;
            using (var db = NewContext())
                detailRowVersionBefore = await db.VisitInstanceFormDetails.AsNoTracking()
                    .Where(d => d.VisitInstanceId == instance).Select(d => d.RowVersion).SingleAsync();

            VisitRequestSafeEditResponse res;
            using (var db = NewContext())
            {
                res = await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance],
                            new SafeContactPatchDto("Tên mới", "Org mới", "Chức vụ mới", "+8499999999",
                                V2SeedActor.Email(Registrant), null),
                            null, null, null),
                    })), CancellationToken.None);
            }
            // Contact-classed, not Safe/PrivacyUrgent — but still names the exact instance (decision F).
            Assert.Contains(res.AppliedChanges, c => c.VisitInstanceId == instance && c.ChangeClass == AmendmentChangeClasses.Contact);

            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.Equal("Tên mới", detail.OperationalContactFullName);
                Assert.Equal("Org mới", detail.OperationalContactOrganization);
                Assert.Equal("Chức vụ mới", detail.OperationalContactJobTitle);
                Assert.Equal("+8499999999", detail.OperationalContactPhone);
                Assert.Equal(1u, detail.FormRevision); // unchanged (decision B)
                Assert.Equal(detailRowVersionBefore + 1, detail.RowVersion); // still bumped

                // CREATE already wrote the FormRevision=1 baseline row — the contact-only edit must not
                // add a second one (it would collide with the unique (VisitInstanceId, FormRevision)
                // index anyway, since FormRevision itself never moved).
                Assert.Equal(1, await db.VisitInstanceFormRevisionHistories.AsNoTracking()
                    .CountAsync(r => r.VisitInstanceId == instance));
                Assert.True(await db.AuditLogs.AsNoTracking()
                    .AnyAsync(a => a.VisitInstanceId == instance && a.Action == "OPERATIONAL_CONTACT_PROFILE_UPDATED"));
                Assert.False(await db.AuditLogs.AsNoTracking()
                    .AnyAsync(a => a.VisitRequestId == requestId && a.Action == "VISIT_SAFE_FIELDS_UPDATED")); // no empty generic audit
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Contact_email_change_is_rejected_with_zero_mutation()
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
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, instV[instance],
                                new SafeContactPatchDto("Tên mới", "Org mới", "Chức vụ", null,
                                    "khac@vidu.com", null),
                                null, null, null),
                        })), CancellationToken.None));
                Assert.Equal(OperationalContactErrorCodes.ChangeConflict, ex.ErrorCode);
            }
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.NotEqual("Tên mới", detail.OperationalContactFullName);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Relation_link_and_unlink_apply_with_durable_human_readable_history()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var memberId = await SoleGuestMemberIdAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            // null → A: link the contact to the sole guest, whose profile is made to match first.
            using (var db = NewContext())
            {
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance],
                            new SafeContactPatchDto("Guest A", "GuestOrg", "Guest", null,
                                V2SeedActor.Email(Registrant), new SafeContactMemberLinkPatchDto(memberId)),
                            null, null, null),
                    })), CancellationToken.None);
            }
            (reqV, instV) = await VersionsAsync(requestId);
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.Equal(memberId, detail.OperationalContactGuestMemberId);
                var relAudit = await db.AuditLogs.AsNoTracking().Include(a => a.Changes)
                    .Where(a => a.VisitInstanceId == instance && a.Action == "OPERATIONAL_CONTACT_RELATION_UPDATED")
                    .SingleAsync();
                var change = relAudit.Changes.Single();
                Assert.Equal("Không nằm trong danh sách đoàn", change.OldValueText);
                Assert.Equal("Guest A", change.NewValueText); // human name, never a raw id
            }

            // A → null: explicit unlink.
            using (var db = NewContext())
            {
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance],
                            new SafeContactPatchDto("Guest A", "GuestOrg", "Guest", null,
                                V2SeedActor.Email(Registrant), new SafeContactMemberLinkPatchDto(null)),
                            null, null, null),
                    })), CancellationToken.None);
            }
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.Null(detail.OperationalContactGuestMemberId);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Relation_mismatch_is_rejected_with_zero_mutation()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var memberId = await SoleGuestMemberIdAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            // The contact snapshot ("Op Contact" / OpOrg / Trưởng phòng Hợp tác) does not describe
            // "Guest A" — linking to memberId while keeping the ORIGINAL (mismatching) metadata must fail.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, instV[instance],
                                new SafeContactPatchDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410",
                                    V2SeedActor.Email(Registrant), new SafeContactMemberLinkPatchDto(memberId)),
                                null, null, null),
                        })), CancellationToken.None));
                Assert.Equal(OperationalContactErrorCodes.RelationProfileMismatch, ex.ErrorCode);
            }
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.Null(detail.OperationalContactGuestMemberId); // zero mutation
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // Operation-aware (operational-contact consistency fix): editing ONLY the shared metadata into a
    // mismatch, with MemberLink omitted (relation itself untouched, still linked to memberId), is
    // Case C — retyping a linked contact's identity by hand — and gets its OWN dedicated code,
    // distinct from RelationProfileMismatch (which is reserved for actually LINKING/repointing to a
    // mismatched member, Case F/G). Both describe "this profile disagrees with the linked member," but
    // a different mistake needs a different fix: RelationProfileMismatch says "pick someone else";
    // LinkedProfileRequiresMemberUpdate says "edit the member, or unlink."
    [Fact]
    public async Task Effective_relation_is_validated_even_when_memberlink_is_omitted()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var memberId = await SoleGuestMemberIdAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            // First, link the contact to the guest (metadata already matching "Guest A").
            using (var db = NewContext())
            {
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance],
                            new SafeContactPatchDto("Guest A", "GuestOrg", "Guest", null,
                                V2SeedActor.Email(Registrant), new SafeContactMemberLinkPatchDto(memberId)),
                            null, null, null),
                    })), CancellationToken.None);
            }
            (reqV, instV) = await VersionsAsync(requestId);

            // Now edit ONLY the metadata into a mismatch, with MemberLink entirely OMITTED (decision N) —
            // the existing link to memberId must still be validated against the new proposed name.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, instV[instance],
                                new SafeContactPatchDto("Yoon Soo Jin", "Organization B", "Programme Coordinator",
                                    null, V2SeedActor.Email(Registrant), null),
                                null, null, null),
                        })), CancellationToken.None));
                Assert.Equal(OperationalContactErrorCodes.LinkedProfileRequiresMemberUpdate, ex.ErrorCode);
            }
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.Equal(memberId, detail.OperationalContactGuestMemberId); // unchanged
                Assert.Equal("Guest A", detail.OperationalContactFullName); // metadata NOT desynced either
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // Case A (operational-contact consistency fix): a legacy mismatch the save does NOT itself touch
    // must never block — specifically, a phone-only correction on a linked contact whose shared fields
    // have already drifted from the member (by some other historical path) must succeed.
    [Fact]
    public async Task Phone_only_edit_survives_a_pre_existing_legacy_shared_field_mismatch()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var memberId = await SoleGuestMemberIdAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            // Link, then force a legacy-shaped mismatch directly (bypassing this service's own guard,
            // simulating drift from a different historical write path — not something this save did).
            using (var db = NewContext())
            {
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance],
                            new SafeContactPatchDto("Guest A", "GuestOrg", "Guest", null,
                                V2SeedActor.Email(Registrant), new SafeContactMemberLinkPatchDto(memberId)),
                            null, null, null),
                    })), CancellationToken.None);
            }
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instance);
                detail.OperationalContactJobTitle = "Senior Director (drift, không qua Safe Edit)";
                await db.SaveChangesAsync();
            }
            (reqV, instV) = await VersionsAsync(requestId);

            // Phone-only save: FullName/Organization/JobTitle echoed back UNCHANGED (still the drifted
            // JobTitle) — must succeed despite the legacy mismatch this save never touches.
            using (var db = NewContext())
            {
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance],
                            new SafeContactPatchDto("Guest A", "GuestOrg", "Senior Director (drift, không qua Safe Edit)",
                                "+84987654321", V2SeedActor.Email(Registrant), null),
                            null, null, null),
                    })), CancellationToken.None);
            }
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.Equal("+84987654321", detail.OperationalContactPhone);
                Assert.Equal(memberId, detail.OperationalContactGuestMemberId); // still linked, untouched
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Safe Edit matrix Case E: a linked contact's shared fields are retyped away from a pre-existing
    /// legacy mismatch to EXACTLY the linked member's own values — this must succeed (relation unchanged,
    /// <c>RelationMatchesContact</c> now true), the mirror image of Case D
    /// (<see cref="Effective_relation_is_validated_even_when_memberlink_is_omitted"/>, which retypes to a
    /// value that STILL mismatches and is rejected).
    /// </summary>
    [Fact]
    public async Task Retyping_a_linked_contacts_shared_fields_to_exactly_match_the_member_succeeds()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var memberId = await SoleGuestMemberIdAsync(requestId); // "Guest A" / "Guest" / "GuestOrg"
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            // Link, with a legacy-shaped mismatch on JobTitle forced directly afterwards (bypassing this
            // service's own guard — simulating drift from a different historical write path).
            using (var db = NewContext())
            {
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance],
                            new SafeContactPatchDto("Guest A", "GuestOrg", "Guest", null,
                                V2SeedActor.Email(Registrant), new SafeContactMemberLinkPatchDto(memberId)),
                            null, null, null),
                    })), CancellationToken.None);
            }
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instance);
                detail.OperationalContactJobTitle = "Senior Director (drift, không qua Safe Edit)";
                await db.SaveChangesAsync();
            }
            (reqV, instV) = await VersionsAsync(requestId);

            // Retype JobTitle back to EXACTLY the member's own value — relation untouched (MemberLink
            // omitted), sharedFieldsChanged=true, and the new value matches the member: must succeed.
            using (var db = NewContext())
            {
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance],
                            new SafeContactPatchDto("Guest A", "GuestOrg", "Guest",
                                null, V2SeedActor.Email(Registrant), null),
                            null, null, null),
                    })), CancellationToken.None);
            }
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.Equal("Guest", detail.OperationalContactJobTitle);
                Assert.Equal(memberId, detail.OperationalContactGuestMemberId); // still linked
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Whole_request_contact_no_op_is_rejected_but_no_op_plus_notes_succeeds()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();
            var identicalContact = new SafeContactPatchDto(
                "Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant), null);

            // Contact block identical to current, nothing else in the request → rejected.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, instV[instance], identicalContact, null, null, null),
                        })), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.SafeEditFieldNotAllowed, ex.ErrorCode);
            }

            // Same identical contact block, but Notes also changed → succeeds; only Notes applied.
            VisitRequestSafeEditResponse res;
            using (var db = NewContext())
            {
                res = await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance], identicalContact, null, null, "Ghi chú mới"),
                    })), CancellationToken.None);
            }
            Assert.DoesNotContain(res.AppliedChanges, c => c.ChangeClass == AmendmentChangeClasses.Contact);
            Assert.Contains(res.AppliedChanges, c => c.FieldPath == VisitFieldClassifier.Notes);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Contact_only_edit_sends_no_notification()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();
            var notifications = new RecordingNotifications();

            using (var db = NewContext())
            {
                await Handler(db, Registrant, notifications).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance],
                            new SafeContactPatchDto("Tên mới", "Org mới", "Chức vụ mới", null,
                                V2SeedActor.Email(Registrant), null),
                            null, null, null),
                    })), CancellationToken.None);
            }
            Assert.Empty(notifications.Sent);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Concurrent_relation_writers_same_starting_version_yield_exactly_one_winner()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var memberId = await SoleGuestMemberIdAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();
            var payload = new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
            {
                new(instance, instV[instance],
                    new SafeContactPatchDto("Guest A", "GuestOrg", "Guest", null,
                        V2SeedActor.Email(Registrant), new SafeContactMemberLinkPatchDto(memberId)),
                    null, null, null),
            });

            // Both writers "load" the SAME starting instance RowVersion before either commits.
            using var dbA = NewContext();
            using var dbB = NewContext();
            await Handler(dbA, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId, payload), CancellationToken.None);
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                Handler(dbB, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId, payload), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.VisitFormConcurrencyConflict, ex.ErrorCode);

            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.Equal(memberId, detail.OperationalContactGuestMemberId); // writer A's result preserved
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Contact Phone is OPTIONAL (GitHub bug report, CanhIter3FixBug live-UI repro: a Safe Edit
    // relation/name-only change on a contact with a phone on file, or with none at all, must never be
    // rejected as "The Phone field is required." — Phone has no NotEmpty rule anywhere in this chain,
    // see SafeContactPatchDto/SubmitVisitSafeEditCommandValidator/OperationalContactProfileMutation).

    [Fact]
    public async Task B1_Phone_on_file_survives_a_fullname_only_edit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20), contactPhone: "+8412345678"));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            await Handler(NewContext(), Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                {
                    new(instance, instV[instance],
                        new SafeContactPatchDto("Tên mới", "OpOrg", "Trưởng phòng Hợp tác", "+8412345678",
                            V2SeedActor.Email(Registrant), null),
                        null, null, null),
                })), CancellationToken.None);

            using var db = NewContext();
            var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
            Assert.Equal("Tên mới", detail.OperationalContactFullName);
            Assert.Equal("+8412345678", detail.OperationalContactPhone); // preserved, not required to be resent-and-lost
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task B2_Phone_on_file_survives_a_relation_only_edit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20), contactPhone: "+8412345678"));
            await ApproveAllAsync(requestId);
            var memberId = await SoleGuestMemberIdAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            await Handler(NewContext(), Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                {
                    new(instance, instV[instance],
                        new SafeContactPatchDto("Guest A", "GuestOrg", "Guest", "+8412345678",
                            V2SeedActor.Email(Registrant), new SafeContactMemberLinkPatchDto(memberId)),
                        null, null, null),
                })), CancellationToken.None);

            using var db = NewContext();
            var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
            Assert.Equal(memberId, detail.OperationalContactGuestMemberId);
            Assert.Equal("+8412345678", detail.OperationalContactPhone); // relation-only must not touch phone
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task B3_Null_phone_stays_null_through_a_fullname_only_edit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20), contactPhone: null));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            var res = await Handler(NewContext(), Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                {
                    new(instance, instV[instance],
                        new SafeContactPatchDto("Tên mới", "OpOrg", "Trưởng phòng Hợp tác", null,
                            V2SeedActor.Email(Registrant), null),
                        null, null, null),
                })), CancellationToken.None);
            Assert.Contains(res.AppliedChanges, c => c.VisitInstanceId == instance && c.ChangeClass == AmendmentChangeClasses.Contact);

            using var db = NewContext();
            var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
            Assert.Equal("Tên mới", detail.OperationalContactFullName);
            Assert.Null(detail.OperationalContactPhone);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task B4_Null_phone_is_accepted_on_a_relation_only_edit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20), contactPhone: null));
            await ApproveAllAsync(requestId);
            var memberId = await SoleGuestMemberIdAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            await Handler(NewContext(), Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                {
                    new(instance, instV[instance],
                        new SafeContactPatchDto("Guest A", "GuestOrg", "Guest", null,
                            V2SeedActor.Email(Registrant), new SafeContactMemberLinkPatchDto(memberId)),
                        null, null, null),
                })), CancellationToken.None);

            using var db = NewContext();
            var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
            Assert.Equal(memberId, detail.OperationalContactGuestMemberId);
            Assert.Null(detail.OperationalContactPhone);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task B5_Clearing_an_existing_phone_persists_null_with_audit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20), contactPhone: "+8412345678"));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            await Handler(NewContext(), Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                {
                    new(instance, instV[instance],
                        new SafeContactPatchDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", null,
                            V2SeedActor.Email(Registrant), null),
                        null, null, null),
                })), CancellationToken.None);

            using var db = NewContext();
            var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
            Assert.Null(detail.OperationalContactPhone);
            var audit = await db.AuditLogs.AsNoTracking().Include(a => a.Changes)
                .Where(a => a.VisitInstanceId == instance && a.Action == "OPERATIONAL_CONTACT_PROFILE_UPDATED")
                .SingleAsync();
            var phoneChange = audit.Changes.Single(c => c.FieldName == "operational_contact_phone");
            Assert.Equal("+8412345678", phoneChange.OldValueText);
            Assert.Null(phoneChange.NewValueText);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task B6_Invalid_nonblank_phone_is_rejected_by_the_command_validator()
    {
        // Pure FluentValidation check — no DB required, mirrors Classifier_is_table_driven_and_fails_closed
        // above. Proves Phone is validated (format) WITHOUT being required: NotEmpty is never applied to
        // it anywhere in this chain, only MustBeAPhoneNumber, which passes blank and rejects malformed.
        var validator = new PEMS.Application.Delegations.Commands.VisitAmendments.SubmitVisitSafeEditCommandValidator();
        var cmd = new SubmitVisitSafeEditCommand(1,
            new VisitRequestSafeEditDto(1, null, new List<SafeInstancePatchDto>
            {
                new(1, 1,
                    new SafeContactPatchDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "123-not-a-phone",
                        V2SeedActor.Email(Registrant), null),
                    null, null, null),
            }));
        var result = await validator.ValidateAsync(cmd, CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("OperationalContact") && e.PropertyName.Contains("Phone"));
    }

    [Fact]
    public async Task B6b_Blank_or_null_contact_phone_passes_the_command_validator()
    {
        var validator = new PEMS.Application.Delegations.Commands.VisitAmendments.SubmitVisitSafeEditCommandValidator();
        foreach (string? blank in new[] { null, "", "   " })
        {
            var cmd = new SubmitVisitSafeEditCommand(1,
                new VisitRequestSafeEditDto(1, null, new List<SafeInstancePatchDto>
                {
                    new(1, 1,
                        new SafeContactPatchDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", blank,
                            V2SeedActor.Email(Registrant), null),
                        null, null, null),
                }));
            var result = await validator.ValidateAsync(cmd, CancellationToken.None);
            Assert.DoesNotContain(result.Errors, e => e.PropertyName.Contains("OperationalContact") && e.PropertyName.Contains("Phone"));
        }
    }

    [Fact]
    public async Task B9_Relation_only_edit_leaves_email_user_and_form_revision_untouched()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20), contactPhone: "+8412345678"));
            await ApproveAllAsync(requestId);
            var memberId = await SoleGuestMemberIdAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();
            var emailBefore = await NewContext().VisitInstanceFormDetails.AsNoTracking()
                .Where(d => d.VisitInstanceId == instance).Select(d => d.OperationalContactEmail).SingleAsync();
            var userBefore = await NewContext().VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitInstanceId == instance).Select(c => c.OperationalContactUserId).SingleAsync();

            await Handler(NewContext(), Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                {
                    new(instance, instV[instance],
                        new SafeContactPatchDto("Guest A", "GuestOrg", "Guest", "+8412345678",
                            V2SeedActor.Email(Registrant), new SafeContactMemberLinkPatchDto(memberId)),
                        null, null, null),
                })), CancellationToken.None);

            using var db = NewContext();
            var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
            var userAfter = await db.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitInstanceId == instance).Select(c => c.OperationalContactUserId).SingleAsync();
            Assert.Equal(emailBefore, detail.OperationalContactEmail);
            Assert.Equal(userBefore, userAfter);
            Assert.Equal(1u, detail.FormRevision);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task B10_Multi_campus_contact_edit_on_one_campus_does_not_touch_the_other()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            requestId = await CreateAsync(
                Campus("HN", start, contactPhone: "+8412345678"),
                Campus("HCM", start.AddDays(1), contactPhone: null));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            ulong hn, hcm;
            using (var db = NewContext())
            {
                hn = await db.VisitRequestCampuses.AsNoTracking().Where(c => c.VisitRequestId == requestId)
                    .OrderBy(c => c.CampusId).Select(c => c.VisitInstanceId).FirstAsync();
                hcm = await db.VisitRequestCampuses.AsNoTracking().Where(c => c.VisitRequestId == requestId)
                    .OrderBy(c => c.CampusId).Select(c => c.VisitInstanceId).Skip(1).FirstAsync();
            }

            var res = await Handler(NewContext(), Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                {
                    // Only HN is named in the patch — a sparse payload never mentions HCM at all.
                    new(hn, instV[hn],
                        new SafeContactPatchDto("Tên mới HN", "OpOrg", "Trưởng phòng Hợp tác", "+8412345678",
                            V2SeedActor.Email(Registrant), null),
                        null, null, null),
                })), CancellationToken.None);
            Assert.DoesNotContain(res.AppliedChanges, c => c.VisitInstanceId == hcm);

            using var db2 = NewContext();
            var hcmDetail = await db2.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == hcm);
            Assert.Equal("Op Contact", hcmDetail.OperationalContactFullName); // untouched
            Assert.Null(hcmDetail.OperationalContactPhone); // untouched, still null — no accidental patch
            Assert.Equal(1u, hcmDetail.FormRevision);
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

    // ── Closing the 7 previously-reported acceptance gaps (plan CanhIter3FixBug §19) ────────────────
    // Each of these was confirmed absent from this file before being added — grepped for an existing
    // dedicated test first (none found for any of the 7), so none of these duplicate prior coverage.

    private static UpdateOperationalContactProfileCommandHandler ProfileHandler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor), new FixedClock(), new NoopInvitations(),
            new CanonicalContentRefresher(db), WriteOn);

    /// <summary>GAP A — Safe Edit's contact block uses its OWN lifecycle window (WaitingContactConfirmation/
    /// WaitingRequestApproval/Assigned/BeforeVisit), not generic Safe Edit's (Assigned/BeforeVisit only).
    /// A freshly-created campus here lands in WAITING_REQUEST_APPROVAL (self-matched contact, not yet
    /// decided) — proves the contact edit succeeds there while a generic field edit is still refused.</summary>
    [Fact]
    public async Task GAP_A_Contact_edit_succeeds_at_WaitingRequestApproval_while_generic_fields_stay_refused()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            ulong instance;
            using (var db = NewContext())
            {
                instance = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId).Select(c => c.VisitInstanceId).SingleAsync();
                var status = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instance).Select(c => c.Status).SingleAsync();
                Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, status); // sanity: proves the state under test
            }
            var (reqV, instV) = await VersionsAsync(requestId);

            // Contact-only edit succeeds at this still-pending status.
            using (var db = NewContext())
            {
                var res = await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance],
                            new SafeContactPatchDto("Tên mới WRA", "OpOrg", "Trưởng phòng Hợp tác", "+8410",
                                V2SeedActor.Email(Registrant), null),
                            null, null, null),
                    })), CancellationToken.None);
                Assert.Contains(res.AppliedChanges, c => c.ChangeClass == AmendmentChangeClasses.Contact);
            }

            // A generic field (Notes) at the SAME status is still refused — decision M's split is scoped
            // per-field, not a blanket widening of the whole Safe Edit gate.
            (reqV, instV) = await VersionsAsync(requestId);
            using (var db = NewContext())
                await Assert.ThrowsAnyAsync<Exception>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, instV[instance], null, null, null, "Ghi chú"),
                        })), CancellationToken.None));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>GAP B — a non-registrant (a campus's confirmed operational contact) cannot smuggle a
    /// request-level Registrant patch through by also naming their own campus validly in Instances.</summary>
    [Fact]
    public async Task GAP_B_Non_registrant_cannot_handcraft_a_registrant_patch()
    {
        RequireDb();
        ulong requestId = 0;
        const ulong nonRegistrantContact = 20; // seed visitor distinct from Registrant=8, reused elsewhere in this file
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            ulong instance;
            using (var db = NewContext())
            {
                instance = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId).Select(c => c.VisitInstanceId).SingleAsync();
                // Simulate a confirmed per-campus contact who is NOT the registrant — direct write, same
                // shortcut OperationalContactManagementTests uses, bypassing the invitation flow entirely
                // since only VisitRequestOwnership.IsOperationalContact's read of this column matters here.
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_campuses SET operational_contact_user_id = {0} WHERE visit_instance_id = {1}",
                    nonRegistrantContact, instance);
            }
            var (reqV, instV) = await VersionsAsync(requestId);

            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Handler(db, nonRegistrantContact).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV,
                            new SafeRegistrantPatchDto("Tên giả mạo", "Org", "Job", "+84900000000", "VN"),
                            new List<SafeInstancePatchDto>
                            {
                                // Their OWN campus, otherwise perfectly valid — passes the instance-ownership
                                // loop on its own; only the Registrant block must stop this.
                                new(instance, instV[instance], null, "Xe khác", null, null),
                            })), CancellationToken.None));
                Assert.Contains("người đăng ký", ex.Message);
            }
            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.NotEqual("Tên giả mạo", visit.RegistrantFullName); // zero mutation
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.NotEqual("Xe khác", detail.TransportationNote);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>GAP C — Safe Edit commits first; a stale UpdateOperationalContactProfile call that read
    /// the SAME starting version is rejected, not silently overwriting the Safe Edit result.</summary>
    [Fact]
    public async Task GAP_C1_SafeEdit_first_then_stale_UpdateProfile_is_rejected()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();
            var startingVersion = instV[instance];

            using (var db = NewContext())
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, startingVersion,
                            new SafeContactPatchDto("Từ Safe Edit", "OpOrg", "Trưởng phòng Hợp tác", null,
                                V2SeedActor.Email(Registrant), null),
                            null, null, null),
                    })), CancellationToken.None);

            using (var db = NewContext())
                await Assert.ThrowsAnyAsync<Exception>(() =>
                    ProfileHandler(db, Registrant).Handle(new UpdateOperationalContactProfileCommand(
                        requestId, instance, "Từ UpdateProfile (stale)", "OpOrg", "Trưởng phòng Hợp tác", null,
                        V2SeedActor.Email(Registrant), startingVersion), CancellationToken.None));

            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.Equal("Từ Safe Edit", detail.OperationalContactFullName); // Safe Edit's result preserved
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>GAP C — reverse direction: UpdateOperationalContactProfile commits first; a stale Safe
    /// Edit call that read the SAME starting version is rejected.</summary>
    [Fact]
    public async Task GAP_C2_UpdateProfile_first_then_stale_SafeEdit_is_rejected()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();
            var startingVersion = instV[instance];

            using (var db = NewContext())
                await ProfileHandler(db, Registrant).Handle(new UpdateOperationalContactProfileCommand(
                    requestId, instance, "Từ UpdateProfile", "OpOrg", "Trưởng phòng Hợp tác", null,
                    V2SeedActor.Email(Registrant), startingVersion), CancellationToken.None);

            using (var db = NewContext())
                await Assert.ThrowsAsync<ConflictException>(() =>
                    Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                        {
                            new(instance, startingVersion,
                                new SafeContactPatchDto("Từ Safe Edit (stale)", "OpOrg", "Trưởng phòng Hợp tác", null,
                                    V2SeedActor.Email(Registrant), null),
                                null, null, null),
                        })), CancellationToken.None));

            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                Assert.Equal("Từ UpdateProfile", detail.OperationalContactFullName); // preserved
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>GAP D — canonical equivalence: the SAME Organization edit produces the SAME
    /// HasMixedCampusDetails verdict regardless of which of the two profile-write doors performed it.</summary>
    [Fact]
    public async Task GAP_D_Canonical_state_is_equivalent_regardless_of_which_door_wrote_the_same_change()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            requestId = await CreateAsync(Campus("HN", start), Campus("HCM", start.AddDays(1)));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            ulong hn, hcm;
            using (var db = NewContext())
            {
                hn = await db.VisitRequestCampuses.AsNoTracking().Where(c => c.VisitRequestId == requestId)
                    .OrderBy(c => c.CampusId).Select(c => c.VisitInstanceId).FirstAsync();
                hcm = await db.VisitRequestCampuses.AsNoTracking().Where(c => c.VisitRequestId == requestId)
                    .OrderBy(c => c.CampusId).Select(c => c.VisitInstanceId).Skip(1).FirstAsync();
            }

            // Same NEW organization text applied to HN via Safe Edit and to HCM via UpdateProfile —
            // both campuses end up textually identical again, so HasMixed must go back to false either way.
            using (var db = NewContext())
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(hn, instV[hn],
                            new SafeContactPatchDto("Op Contact", "Org Đồng Nhất", "Trưởng phòng Hợp tác", "+8410",
                                V2SeedActor.Email(Registrant), null),
                            null, null, null),
                    })), CancellationToken.None);

            using (var db = NewContext())
            {
                var hcmVersion = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == hcm).Select(c => c.RowVersion).SingleAsync();
                await ProfileHandler(db, Registrant).Handle(new UpdateOperationalContactProfileCommand(
                    requestId, hcm, "Op Contact", "Org Đồng Nhất", "Trưởng phòng Hợp tác", "+8410",
                    V2SeedActor.Email(Registrant), hcmVersion), CancellationToken.None);
            }

            using (var db = NewContext())
            {
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.False(visit.HasMixedCampusDetails); // both doors converged to the same canonical content
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    private static SubmitVisitAmendmentCommandHandler AmendmentSubmit(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor), new FixedClock(),
            new VisitAmendmentService(db, NullLogger<VisitAmendmentService>.Instance),
            new RecordingNotifications(), NullLogger<SubmitVisitAmendmentCommandHandler>.Instance, WriteOn);

    private static DecideVisitAmendmentCommandHandlers AmendmentDecide(ApplicationDbContext db, ulong actor, ulong campusId)
        => new(db, new FakeUser(actor, RoleCodes.Staff, UserSubRoles.Leader, campusId), new FixedClock(),
            new VisitAmendmentService(db, NullLogger<VisitAmendmentService>.Instance),
            new RecordingNotifications(), NullLogger<DecideVisitAmendmentCommandHandlers>.Instance, WriteOn);

    /// <summary>GAP E — an unrelated pending general amendment on an instance survives a contact-only
    /// Safe Edit on that SAME instance (never touches FormRevision/ApprovalRevision) and stays approvable.</summary>
    [Fact]
    public async Task GAP_E_Pending_amendment_survives_a_contact_only_safe_edit_and_stays_approvable()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            ulong instance, leader, campusId;
            using (var db = NewContext())
            {
                var c = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(x => x.VisitRequestId == requestId).SingleAsync();
                instance = c.VisitInstanceId; leader = c.CoordinatorUserId!.Value; campusId = c.CampusId;
            }
            var (reqV, instV) = await VersionsAsync(requestId);

            ulong amendmentId;
            using (var db = NewContext())
            {
                var c = await db.VisitRequestCampuses.AsNoTracking().SingleAsync(x => x.VisitInstanceId == instance);
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                var members = await db.VisitGuestMembers.AsNoTracking()
                    .Where(m => m.VisitRequestId == requestId).ToListAsync();
                var proposal = new VisitAmendmentProposalDto(
                    instV[instance], detail.FormRevision, detail.ApprovalRevision, "Đổi mục đích",
                    detail.DelegationName, detail.VisitType ?? "MEETING", detail.VisitTypeOther,
                    "Mục đích mới", detail.WorkingContent, detail.WorkingLanguage ?? "EN",
                    new ContactPointDto(detail.OperationalContactFullName, detail.OperationalContactOrganization ?? "",
                        detail.OperationalContactJobTitle, detail.OperationalContactPhone, detail.OperationalContactEmail),
                    members.Where(m => m.MemberType == "GUEST")
                        .Select(m => new VisitorDto(m.FullName, m.Nationality ?? "", m.JobTitle ?? "", m.Organization ?? "", m.OrganizationPartnerId))
                        .ToList(),
                    new List<SupportTeamMemberDto>(),
                    c.PlannedStartAt, c.PlannedEndAt);
                amendmentId = (await AmendmentSubmit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instance, proposal), CancellationToken.None)).AmendmentId;
            }

            // Contact-only Safe Edit on the SAME instance while the amendment is pending.
            using (var db = NewContext())
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance],
                            new SafeContactPatchDto("Tên mới trong lúc pending", "OpOrg", "Trưởng phòng Hợp tác", null,
                                V2SeedActor.Email(Registrant), null),
                            null, null, null),
                    })), CancellationToken.None);

            using (var db = NewContext())
            {
                var amendment = await db.VisitInstanceAmendments.AsNoTracking()
                    .SingleAsync(a => a.AmendmentId == amendmentId);
                Assert.Equal("PENDING_APPROVAL", amendment.Status); // untouched by the contact-only edit

                var res = await AmendmentDecide(db, leader, campusId).Handle(
                    new ApproveVisitAmendmentCommand(instance, amendmentId, "OK"), CancellationToken.None);
                Assert.NotNull(res); // approved without AmendmentBaseRevisionConflict
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// CONC-03 (operational-contact consistency fix): an Amendment is submitted while Kim is the linked
    /// contact, proposing a content change to Kim's OWN fields with real continuity evidence (so at
    /// submit time it genuinely preserves the relation). Before Approve runs, a CONCURRENT Safe Edit
    /// unlinks the contact on the SAME instance — the live relation Approve must re-check is no longer
    /// what the proposal assumed. Approve must fail closed (never silently re-establish the relation from
    /// stale submit-time evidence, never apply a content change under a false continuity assumption).
    /// </summary>
    [Fact]
    public async Task Conc03_amendment_approve_re_checks_the_live_relation_against_a_concurrent_safe_edit_unlink()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var kimId = await SoleGuestMemberIdAsync(requestId);
            ulong instance, leader, campusId;
            using (var db = NewContext())
            {
                var c = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(x => x.VisitRequestId == requestId).SingleAsync();
                instance = c.VisitInstanceId; leader = c.CoordinatorUserId!.Value; campusId = c.CampusId;
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instance);
                detail.OperationalContactGuestMemberId = kimId;
                await db.SaveChangesAsync();
            }

            ulong amendmentId;
            using (var db = NewContext())
            {
                var c = await db.VisitRequestCampuses.AsNoTracking().SingleAsync(x => x.VisitInstanceId == instance);
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
                var proposal = new VisitAmendmentProposalDto(
                    c.RowVersion, detail.FormRevision, detail.ApprovalRevision, "Đổi mục đích",
                    detail.DelegationName, detail.VisitType ?? "MEETING", detail.VisitTypeOther,
                    "Mục đích mới", detail.WorkingContent, detail.WorkingLanguage ?? "EN",
                    new ContactPointDto(detail.OperationalContactFullName, detail.OperationalContactOrganization ?? "",
                        detail.OperationalContactJobTitle, detail.OperationalContactPhone, detail.OperationalContactEmail),
                    new List<VisitorDto> { new("Guest A", "VN", "Senior Director", "GuestOrg", null, "a-key", kimId) },
                    new List<SupportTeamMemberDto>(),
                    c.PlannedStartAt, c.PlannedEndAt,
                    OperationalContactClientMemberKey: "a-key");
                amendmentId = (await AmendmentSubmit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instance, proposal), CancellationToken.None)).AmendmentId;
            }

            // Concurrent Safe Edit: unlinks the contact on the SAME instance before Approve runs.
            using (var db = NewContext())
            {
                var (reqV, instV) = await VersionsAsync(requestId);
                await Handler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(instance, instV[instance],
                            new SafeContactPatchDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", null,
                                V2SeedActor.Email(Registrant), new SafeContactMemberLinkPatchDto(null)),
                            null, null, null),
                    })), CancellationToken.None);
            }

            using (var db = NewContext())
            {
                // Fail-closed either way this reaches it: a stale-version conflict, or (since a
                // contact-only Safe Edit does not bump FormRevision/ApprovalRevision, per GAP E just
                // above) the relation-continuity re-check — never a silent last-write-wins.
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    AmendmentDecide(db, leader, campusId).Handle(
                        new ApproveVisitAmendmentCommand(instance, amendmentId, "OK"), CancellationToken.None));
                Assert.True(
                    ex.ErrorCode == VisitFormV2ErrorCodes.AmendmentLegacyContactRelationRequiresResubmission
                    || ex.ErrorCode == VisitFormV2ErrorCodes.AmendmentBaseRevisionConflict
                    || ex.ErrorCode == VisitFormV2ErrorCodes.VisitFormConcurrencyConflict,
                    $"Expected a fail-closed conflict code, got '{ex.ErrorCode}'.");
            }

            // Never last-write-wins: the concurrent Safe Edit's unlink is the surviving state either way.
            using var db2 = NewContext();
            var detailAfter = await db2.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
            Assert.Null(detailAfter.OperationalContactGuestMemberId);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>GAP F — a generic field AND a contact field changed in the SAME call bump FormRevision
    /// exactly once (not twice, not once per changed group) and insert exactly one revision-history row.</summary>
    [Fact]
    public async Task GAP_F_Combined_generic_and_contact_edit_bumps_form_revision_exactly_once()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            var instance = instV.Keys.Single();

            var res = await Handler(NewContext(), Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                {
                    new(instance, instV[instance],
                        new SafeContactPatchDto("Tên mới GAP-F", "OpOrg", "Trưởng phòng Hợp tác", null,
                            V2SeedActor.Email(Registrant), null),
                        null, null, "Ghi chú GAP-F"),
                })), CancellationToken.None);
            Assert.Contains(res.AppliedChanges, c => c.ChangeClass == AmendmentChangeClasses.Contact);
            Assert.Contains(res.AppliedChanges, c => c.FieldPath == VisitFieldClassifier.Notes);

            using var db = NewContext();
            var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instance);
            Assert.Equal(2u, detail.FormRevision); // exactly one bump, not two
            Assert.Equal(1, await db.VisitInstanceFormRevisionHistories.AsNoTracking()
                .CountAsync(r => r.VisitInstanceId == instance && r.FormRevision == 2));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>GAP G — a request-level Safe change (registrant phone) unioned with an instance-level
    /// Safe change (Notes at HN only) notifies BOTH the request-wide leader set AND HN's own Host —
    /// the pre-fix if/else would have dropped HN's Host once a request-level component was present.</summary>
    [Fact]
    public async Task GAP_G_Request_level_and_instance_level_notification_scopes_union()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            requestId = await CreateAsync(Campus("HN", start), Campus("HCM", start.AddDays(1)));
            await ApproveAllAsync(requestId);
            var (reqV, instV) = await VersionsAsync(requestId);
            ulong hn; ulong hnHost;
            using (var db = NewContext())
            {
                var c = await db.VisitRequestCampuses.AsNoTracking().Where(x => x.VisitRequestId == requestId)
                    .OrderBy(x => x.CampusId).FirstAsync();
                hn = c.VisitInstanceId; hnHost = c.CurrentHostUserId!.Value;
            }

            var notifications = new RecordingNotifications();
            using (var db = NewContext())
                await Handler(db, Registrant, notifications).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84900001111", "VN"), // request-level
                        new List<SafeInstancePatchDto>
                        {
                            new(hn, instV[hn], null, null, null, "Ghi chú GAP-G"), // instance-level, HN only
                        })), CancellationToken.None);

            var recipientIds = notifications.Sent.Select(n => n.RecipientUserId).ToHashSet();
            // HN's Host is a per-instance recipient that a mutually-exclusive if/else would have dropped
            // the moment the request-level registrant change was also present — it must still be here.
            Assert.Contains(hnHost, recipientIds);
            // The notification also carries a request-wide target (null instance id) rather than being
            // narrowed to HN alone, proving the request-level component fired too.
            Assert.Contains(notifications.Sent, n => n.VisitInstanceId == null);
        }
        finally { await CleanupAsync(requestId); }
    }
}
