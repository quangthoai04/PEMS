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
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.StartVisitPreparation;
using PEMS.Application.Delegations.Commands.CancelVisitRequest;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Policies;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using PEMS.Shared;
using Xunit;
using NotificationTypes = PEMS.Application.Notifications.Common.NotificationTypes;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 2D — CANCEL (request-wide and per-campus) and the invitee's own INVITATION RESPONSE.
///
/// Neither command had an executing test. Both are actor-and-window guarded and both mutate rows that
/// belong to one campus instance, so the risk they carry is the Pure V2 risk: cancelling or responding
/// on one campus quietly moving a sibling, or the aggregate request status being rolled up when some
/// campus is still live.
/// </summary>
public sealed class CancelAndInvitationResponseV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;    // VISITOR
    private const ulong OtherVisitor = 20; // an unrelated VISITOR
    private const ulong LeaderHn = 3;
    private const ulong LeaderHcm = 9;
    private const ulong IcStaffHn = 101;
    private const ulong IcStaffHn2 = 102;
    private const ulong IcStaffHcm = 103;
    // A second HCM IC Staff: uq_visit_participants_user is (visit_instance_id, user_id), so an instance's
    // host cannot also hold an invitation row on that same instance.
    private const ulong IcStaffHcm2 = 104;
    private const ulong CampusHn = 1;
    private const ulong CampusHcm = 2;

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
        public FakeUser(ulong id, string roleCode = RoleCodes.Visitor, string? subRole = null, ulong? campusId = null)
        {
            UserId = id;
            RoleCode = roleCode;
            SubRole = subRole;
            PrimaryCampusId = campusId;
        }

        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
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
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct)
        {
            Sent.Add(request);
            return Task.CompletedTask;
        }
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static CancelVisitRequestCommandHandler CancelHandler(
        ApplicationDbContext db, FakeUser actor, RecordingNotifications notifications)
        => new(db, actor, new FixedClock(), notifications, new MySqlUserMutationLockService(db));

    private static RespondVisitParticipantInvitationCommandHandler RespondHandler(
        ApplicationDbContext db, FakeUser actor, RecordingNotifications notifications)
        => new(db, actor, new FixedClock(), notifications, new MySqlUserMutationLockService(db));

    private static CampusVisitFormDto Campus(string code, DateTime start, string delegationName)
        => new(code, start, start.AddMinutes(120), delegationName, "MEETING", null,
            $"Mục đích {delegationName}", $"Nội dung {delegationName}",
            new List<VisitorDto> { new($"Khách {delegationName}", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            // The contact is the REGISTRANT'S own address, so the campus self-matches at submit: confirmed
            // with no invitation, and the request is past the confirmation gate from the start. This suite
            // does not test that gate, and a campus behind it can be neither decided nor moved forward.
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "VI", null, "DECLINED", null, null);

    /// <summary>
    /// How far forward a set of campuses has to be FILED so create accepts it. A visit cannot be created
    /// inside <see cref="VisitMutationPolicy.MinScheduleLeadHours"/>; it only ends up hours from its date
    /// by the date approaching. The schedule is shifted back by the same amount immediately after, so
    /// durations and relative order survive — the cancel window these cases test is a different rule.
    /// </summary>
    private static TimeSpan LeadTimeShift(IEnumerable<CampusVisitFormDto> campuses)
    {
        var earliest = campuses.Min(c => c.PlannedStartAt);
        var floor = Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours).AddMinutes(5);
        return earliest < floor ? floor - earliest : TimeSpan.Zero;
    }

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        var shift = LeadTimeShift(campuses);
        using var db = NewContext();
        var actor = new FakeUser(Registrant);
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(), new VisitRequestV2CreateService(db),
            new RecordingNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "CX" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses
                .Select(c => shift == TimeSpan.Zero
                    ? c
                    : c with { PlannedStartAt = c.PlannedStartAt + shift, PlannedEndAt = c.PlannedEndAt + shift })
                .ToList());
        var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);

        if (shift != TimeSpan.Zero)
        {
            using var patch = NewContext();
            var instances = await patch.VisitRequestCampuses
                .Where(c => c.VisitRequestId == created.VisitRequestId).ToListAsync();
            foreach (var instance in instances)
            {
                instance.PlannedStartAt -= shift;
                instance.PlannedEndAt -= shift;
            }
            await patch.SaveChangesAsync();
        }

        return created.VisitRequestId;
    }

    private static async Task<Dictionary<ulong, ulong>> InstanceIdsByCampusAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .ToDictionaryAsync(c => c.CampusId, c => c.VisitInstanceId);
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId, ulong leaderId, ulong campusId, ulong hostId)
    {
        using var db = NewContext();
        var actor = new FakeUser(leaderId, RoleCodes.Staff, UserSubRoles.Leader, campusId);
        // Approving states the revision it was decided on. The command requires it, so a fixture
        // that left it out would be exercising a call shape no caller can make any more.
        var rowVersion = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.RowVersion).SingleAsync();
        var handler = new ApproveCampusInstanceCommandHandler(
            db, actor, new FixedClock(), 
            new CampusApprovalExecutor(
                db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db), new RecordingNotifications(),
                new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()),
                NullLogger<CampusApprovalExecutor>.Instance));
        await handler.Handle(new ApproveCampusInstanceCommand(requestId, instanceId, hostId, null, rowVersion), CancellationToken.None);
    }

    private sealed record CampusSnapshot(string Status, ulong? HostUserId, string? CancellationReason, int RowVersion);

    private static async Task<Dictionary<ulong, CampusSnapshot>> SnapshotAsync(ulong requestId)
    {
        using var db = NewContext();
        var rows = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .Select(c => new { c.CampusId, Snap = new CampusSnapshot(c.Status, c.CurrentHostUserId, c.CancellationReason, c.RowVersion) })
            .ToListAsync();
        return rows.ToDictionary(r => r.CampusId, r => r.Snap);
    }

    private static async Task<string> RequestStatusAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.Status).SingleAsync();
    }

    /// <summary>Seeds a pending invitation for <paramref name="userId"/> on one instance.</summary>
    /// <summary>
    /// The Host's own step: ASSIGNED → BEFORE_VISIT. Approving assigns the Host and stops there;
    /// inviting is a setup action, so an invitation can only exist on a campus the Host has opened.
    /// </summary>
    private static async Task StartPreparationAsync(ulong requestId, ulong instanceId, ulong hostId, ulong campusId)
    {
        using var db = NewContext();
        var actor = new FakeUser(hostId, RoleCodes.Staff, UserSubRoles.Staff, campusId);
        await new StartVisitPreparationCommandHandler(db, actor, new FixedClock())
            .Handle(new StartVisitPreparationCommand(requestId, instanceId), CancellationToken.None);
    }

    private static async Task<ulong> InviteAsync(ulong instanceId, ulong userId, string role)
    {
        using var db = NewContext();
        var participant = new VisitParticipant
        {
            VisitInstanceId = instanceId,
            UserId = userId,
            ParticipantRole = role,
            IsHost = false,
            Status = ParticipantStatuses.Invited,
            AssignedBy = LeaderHn,
            AssignedAt = Now,
            CreatedAt = Now,
            CreatedBy = LeaderHn,
        };
        db.VisitParticipants.Add(participant);
        await db.SaveChangesAsync();
        return participant.ParticipantId;
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM notifications WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM email_action_tokens WHERE target_type='VISIT_PARTICIPANT' AND target_id IN (SELECT participant_id FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0}))");
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM email_action_tokens WHERE target_type='VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM audit_logs WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── Cancel before approval ───────────────────────────────────────────────────

    [Fact]
    public async Task Only_the_owning_visitor_may_cancel_a_pending_request_and_a_refusal_writes_nothing()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(30);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn hủy HN"),
                Campus("HCM", start.AddDays(1), "Đoàn hủy HCM"));
            var before = await SnapshotAsync(requestId);

            // An unrelated visitor is refused.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    CancelHandler(db, new FakeUser(OtherVisitor), new RecordingNotifications()).Handle(
                        new CancelVisitRequestCommand(requestId, null, "Không còn nhu cầu"), CancellationToken.None));

            // A blank reason is refused even for the owner.
            using (var db = NewContext())
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    CancelHandler(db, new FakeUser(Registrant), new RecordingNotifications()).Handle(
                        new CancelVisitRequestCommand(requestId, null, "  "), CancellationToken.None));

            Assert.Equal(before, await SnapshotAsync(requestId));
            Assert.Equal(VisitRequestStatuses.PendingApproval, await RequestStatusAsync(requestId));

            // The owner cancels: the request AND every still-pending campus go to CANCELLED together.
            var notifications = new RecordingNotifications();
            using (var db = NewContext())
            {
                var res = await CancelHandler(db, new FakeUser(Registrant), notifications).Handle(
                    new CancelVisitRequestCommand(requestId, null, "Đổi kế hoạch"), CancellationToken.None);
                Assert.Equal(VisitRequestStatuses.Cancelled, res.RequestStatus);
                Assert.Equal(2, res.CancelledCampuses.Count);
            }

            var after = await SnapshotAsync(requestId);
            Assert.All(after.Values, s =>
            {
                Assert.Equal(VisitInstanceStatus.Cancelled, s.Status);
                Assert.Equal("Đổi kế hoạch", s.CancellationReason);
            });
            Assert.Equal(VisitRequestStatuses.Cancelled, await RequestStatusAsync(requestId));

            // Both campus Staff Leaders are told — cancelling before approval is their queue emptying.
            var recipients = notifications.Sent
                .Where(n => n.NotificationType == NotificationTypes.VisitCancelled)
                .Select(n => n.RecipientUserId).ToHashSet();
            Assert.Contains(LeaderHn, recipients);
            Assert.Contains(LeaderHcm, recipients);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task The_24h_window_blocks_a_pending_cancel_and_leaves_the_request_alive()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            // One campus inside the 24h window, one far outside: the near one must veto the whole cancel.
            requestId = await CreateAsync(
                Campus("HN", Now.AddHours(10), "Đoàn sát giờ"),
                Campus("HCM", Now.AddDays(30), "Đoàn còn xa"));
            var before = await SnapshotAsync(requestId);

            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    CancelHandler(db, new FakeUser(Registrant), new RecordingNotifications()).Handle(
                        new CancelVisitRequestCommand(requestId, null, "Đổi kế hoạch"), CancellationToken.None));
                Assert.Equal(VisitRequestErrorCodes.VisitCancelWindowExpired, ex.ErrorCode);
            }

            Assert.Equal(before, await SnapshotAsync(requestId));
            Assert.Equal(VisitRequestStatuses.PendingApproval, await RequestStatusAsync(requestId));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Cancel after approval, per campus ────────────────────────────────────────

    [Fact]
    public async Task A_host_cancels_only_their_own_campus_and_never_rolls_up_the_request()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(31);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn host HN"),
                Campus("HCM", start.AddDays(1), "Đoàn host HCM"));
            var instances = await InstanceIdsByCampusAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, IcStaffHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, IcStaffHcm);
            Assert.Equal(VisitRequestStatuses.Approved, await RequestStatusAsync(requestId));
            var before = await SnapshotAsync(requestId);

            // HN's host reaching for HCM's instance is refused.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    CancelHandler(db, new FakeUser(IcStaffHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
                            new RecordingNotifications())
                        .Handle(new CancelVisitRequestCommand(requestId, instances[CampusHcm], "Không tiếp được"),
                            CancellationToken.None));
            Assert.Equal(before, await SnapshotAsync(requestId));

            // HN's host cancels HN only.
            using (var db = NewContext())
            {
                var res = await CancelHandler(db,
                        new FakeUser(IcStaffHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new RecordingNotifications())
                    .Handle(new CancelVisitRequestCommand(requestId, instances[CampusHn], "Khách xác nhận hủy qua điện thoại"),
                        CancellationToken.None);
                Assert.Equal(instances[CampusHn], Assert.Single(res.CancelledCampuses).VisitInstanceId);
            }

            var after = await SnapshotAsync(requestId);
            Assert.Equal(VisitInstanceStatus.Cancelled, after[CampusHn].Status);
            Assert.Equal(before[CampusHcm], after[CampusHcm]); // sibling untouched, host intact

            // A host cancel is an instance-level event: the request stays APPROVED because HCM is still live.
            Assert.Equal(VisitRequestStatuses.Approved, await RequestStatusAsync(requestId));

            // The audit row names the campus that was actually cancelled.
            using (var db = NewContext())
            {
                var audit = Assert.Single(await db.AuditLogs.AsNoTracking()
                    .Where(a => a.VisitInstanceId == instances[CampusHn] && a.Action == "CANCEL_VISIT_REQUEST")
                    .ToListAsync());
                Assert.Equal(CampusHn, audit.CampusId);
                Assert.Equal(requestId, audit.VisitRequestId);
                Assert.Equal(IcStaffHn, audit.ActorUserId);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_request_wide_cancel_after_approval_is_audited_without_claiming_a_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(32);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn toàn phần HN"),
                Campus("HCM", start.AddDays(1), "Đoàn toàn phần HCM"));
            var instances = await InstanceIdsByCampusAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, IcStaffHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, IcStaffHcm);

            using (var db = NewContext())
            {
                var res = await CancelHandler(db, new FakeUser(Registrant), new RecordingNotifications()).Handle(
                    new CancelVisitRequestCommand(requestId, null, "Đoàn hủy chuyến"), CancellationToken.None);
                Assert.Equal(VisitRequestStatuses.Cancelled, res.RequestStatus);
                Assert.Equal(2, res.CancelledCampuses.Count);
            }

            Assert.All((await SnapshotAsync(requestId)).Values,
                s => Assert.Equal(VisitInstanceStatus.Cancelled, s.Status));

            using (var db = NewContext())
            {
                var audit = Assert.Single(await db.AuditLogs.AsNoTracking()
                    .Where(a => a.VisitRequestId == requestId && a.Action == "CANCEL_VISIT_REQUEST")
                    .ToListAsync());
                Assert.Equal(requestId, audit.VisitRequestId);
                // A request-wide cancel belongs to no single campus, so it claims none.
                Assert.Null(audit.VisitInstanceId);
                Assert.Null(audit.CampusId);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Invitation response ──────────────────────────────────────────────────────

    [Fact]
    public async Task An_invitee_responds_only_to_their_own_invitation_on_their_own_instance()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(33);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn mời HN"),
                Campus("HCM", start.AddDays(1), "Đoàn mời HCM"));
            var instances = await InstanceIdsByCampusAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, IcStaffHn);
            await StartPreparationAsync(requestId, instances[CampusHn], IcStaffHn, CampusHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, IcStaffHcm);
            await StartPreparationAsync(requestId, instances[CampusHcm], IcStaffHcm, CampusHcm);

            var hnInvite = await InviteAsync(instances[CampusHn], IcStaffHn2, ParticipantRoles.IcSupport);
            var hcmInvite = await InviteAsync(instances[CampusHcm], IcStaffHcm2, ParticipantRoles.IcSupport);

            // Someone else's invitation is not theirs to answer.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    RespondHandler(db, new FakeUser(IcStaffHn2, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
                            new RecordingNotifications())
                        .Handle(new RespondVisitParticipantInvitationCommand(hcmInvite, true, null), CancellationToken.None));

            // The host slot is an assignment, not an invitation.
            using (var db = NewContext())
            {
                var hostParticipantId = await db.VisitParticipants.AsNoTracking()
                    .Where(p => p.VisitInstanceId == instances[CampusHn] && p.IsHost)
                    .Select(p => p.ParticipantId).SingleAsync();
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    RespondHandler(db, new FakeUser(IcStaffHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
                            new RecordingNotifications())
                        .Handle(new RespondVisitParticipantInvitationCommand(hostParticipantId, true, null),
                            CancellationToken.None));
            }

            var notifications = new RecordingNotifications();
            using (var db = NewContext())
            {
                var res = await RespondHandler(db,
                        new FakeUser(IcStaffHn2, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), notifications)
                    .Handle(new RespondVisitParticipantInvitationCommand(hnInvite, true, null), CancellationToken.None);
                Assert.Equal(ParticipantStatuses.Accepted, res.Status);
            }

            using (var db = NewContext())
            {
                var mine = await db.VisitParticipants.AsNoTracking().SingleAsync(p => p.ParticipantId == hnInvite);
                Assert.Equal(ParticipantStatuses.Accepted, mine.Status);
                Assert.NotNull(mine.RespondedAt);

                // The other campus's invitation is exactly where it was.
                var theirs = await db.VisitParticipants.AsNoTracking().SingleAsync(p => p.ParticipantId == hcmInvite);
                Assert.Equal(ParticipantStatuses.Invited, theirs.Status);
                Assert.Null(theirs.RespondedAt);

                var audit = Assert.Single(await db.AuditLogs.AsNoTracking()
                    .Where(a => a.EntityType == "VisitParticipant" && a.EntityId == hnInvite).ToListAsync());
                Assert.Equal("ACCEPT_VISIT_INVITATION", audit.Action);
                Assert.Equal(CampusHn, audit.CampusId);
                Assert.Equal(instances[CampusHn], audit.VisitInstanceId);
                Assert.Equal(requestId, audit.VisitRequestId);
            }

            // The host of THIS campus is told, and the message names THIS campus's delegation.
            var hostNote = Assert.Single(notifications.Sent, n => n.RecipientUserId == IcStaffHn);
            Assert.Contains("Đoàn mời HN", hostNote.Message);
            Assert.DoesNotContain("Đoàn mời HCM", hostNote.Message);
            Assert.Equal(instances[CampusHn], hostNote.VisitInstanceId);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Declining_records_the_reason_and_a_second_response_is_refused()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(34), "Đoàn từ chối lời mời"));
            var instances = await InstanceIdsByCampusAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, IcStaffHn);
            await StartPreparationAsync(requestId, instances[CampusHn], IcStaffHn, CampusHn);
            var invite = await InviteAsync(instances[CampusHn], IcStaffHn2, ParticipantRoles.IcSupport);

            using (var db = NewContext())
            {
                var res = await RespondHandler(db,
                        new FakeUser(IcStaffHn2, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new RecordingNotifications())
                    .Handle(new RespondVisitParticipantInvitationCommand(invite, false, "  Bận lịch giảng dạy  "),
                        CancellationToken.None);
                Assert.Equal(ParticipantStatuses.Declined, res.Status);
            }

            using (var db = NewContext())
            {
                var p = await db.VisitParticipants.AsNoTracking().SingleAsync(x => x.ParticipantId == invite);
                Assert.Equal(ParticipantStatuses.Declined, p.Status);
                Assert.Equal("Bận lịch giảng dạy", p.Note); // trimmed, stored on note
            }

            // Changing the answer afterwards is a conflict, and the recorded answer stands.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ConflictException>(() =>
                    RespondHandler(db, new FakeUser(IcStaffHn2, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
                            new RecordingNotifications())
                        .Handle(new RespondVisitParticipantInvitationCommand(invite, true, null), CancellationToken.None));

            using (var db = NewContext())
                Assert.Equal(ParticipantStatuses.Declined,
                    (await db.VisitParticipants.AsNoTracking().SingleAsync(x => x.ParticipantId == invite)).Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_cancelled_campus_kills_its_own_invitations_but_not_a_siblings()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(35);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn hủy lời mời HN"),
                Campus("HCM", start.AddDays(1), "Đoàn hủy lời mời HCM"));
            var instances = await InstanceIdsByCampusAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, IcStaffHn);
            await StartPreparationAsync(requestId, instances[CampusHn], IcStaffHn, CampusHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, IcStaffHcm);
            await StartPreparationAsync(requestId, instances[CampusHcm], IcStaffHcm, CampusHcm);

            var hnInvite = await InviteAsync(instances[CampusHn], IcStaffHn2, ParticipantRoles.IcSupport);
            var hcmInvite = await InviteAsync(instances[CampusHcm], IcStaffHn2, ParticipantRoles.IcSupport);

            using (var db = NewContext())
                await CancelHandler(db, new FakeUser(IcStaffHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
                        new RecordingNotifications())
                    .Handle(new CancelVisitRequestCommand(requestId, instances[CampusHn], "Khách hủy cơ sở này"),
                        CancellationToken.None);

            // Responding on the cancelled campus is a conflict...
            using (var db = NewContext())
                await Assert.ThrowsAsync<ConflictException>(() =>
                    RespondHandler(db, new FakeUser(IcStaffHn2, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
                            new RecordingNotifications())
                        .Handle(new RespondVisitParticipantInvitationCommand(hnInvite, true, null), CancellationToken.None));

            // ...while the sibling campus's invitation is still perfectly answerable.
            using (var db = NewContext())
            {
                var res = await RespondHandler(db,
                        new FakeUser(IcStaffHn2, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new RecordingNotifications())
                    .Handle(new RespondVisitParticipantInvitationCommand(hcmInvite, true, null), CancellationToken.None);
                Assert.Equal(ParticipantStatuses.Accepted, res.Status);
            }

            using (var db = NewContext())
            {
                Assert.Equal(ParticipantStatuses.Invited,
                    (await db.VisitParticipants.AsNoTracking().SingleAsync(x => x.ParticipantId == hnInvite)).Status);
                Assert.Equal(ParticipantStatuses.Accepted,
                    (await db.VisitParticipants.AsNoTracking().SingleAsync(x => x.ParticipantId == hcmInvite)).Status);
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
