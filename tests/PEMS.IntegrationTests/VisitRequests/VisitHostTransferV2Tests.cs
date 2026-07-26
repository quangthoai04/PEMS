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
using PEMS.Application.Delegations.Commands.TransferVisitHost;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Policies;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Post-approval Host handover, end to end against the disposable schema.
///
/// The database is a participant in this feature, not a bystander: two triggers on
/// visit_request_campuses previously refused any change to current_host_user_id after the first
/// assignment, so these tests are the proof that the relaxed trigger admits exactly the intended
/// transition and still refuses everything else.
/// </summary>
public sealed class VisitHostTransferV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong Campus1 = 1;
    private static readonly DateTime Now = DateTime.Now;
    private static bool? _dbUp;

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
        Assert.True(_dbUp!.Value, "disposable test database is not reachable.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId { get; init; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; init; } = RoleCodes.Visitor;
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId { get; init; }
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
        { Sent.AddRange(requests); return Task.CompletedTask; }
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong r, string t, string? m, string nt, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static CampusVisitFormDto Campus(string code, DateTime start)
        => new(code, start, start.AddMinutes(120), "Đoàn Host", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null, null);

    /// <summary>Creates a request and drives every campus to ASSIGNED with the campus leader self-hosting.</summary>
    private static async Task<(ulong RequestId, ulong InstanceId, ulong LeaderId)> CreateApprovedAsync(DateTime start)
    {
        ulong requestId;
        using (var db = NewContext())
        {
            var handler = new CreateVisitRequestV2CommandHandler(
                db, new FakeUser { UserId = Registrant }, new FixedClock(), new VisitRequestV2CreateService(db),
                new RecordingNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
                new UserProvisionService(db),
                NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
                new VisitRequestAggregateStatusService(db));
            var form = new VisitRequestFormDataV2(
                "HT" + Guid.NewGuid().ToString("N"),
                new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
                new ContactPointDto("Registrant", "Org", "+8491", V2SeedActor.Email(Registrant)),
                null, new List<CampusVisitFormDto> { Campus("HN", start) });
            requestId = (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
        }

        ulong instanceId, leaderId;
        using (var db = NewContext())
        {
            var visit = await db.VisitRequests.Include(v => v.CampusInstances)
                .SingleAsync(v => v.VisitRequestId == requestId);
            var instance = visit.CampusInstances.Single();
            leaderId = instance.CoordinatorUserId!.Value;
            instance.Status = VisitInstanceStatuses.Assigned;
            instance.CurrentHostUserId = leaderId;   // leader self-hosts
            instance.HostAssignedBy = leaderId;
            instance.HostAssignedAt = Now;
            instance.DecidedBy = leaderId;
            instance.DecidedAt = Now;
            instance.DecisionActorRole = "STAFF_LEADER";
            instance.DecisionSource = "STANDARD_CAMPUS_REVIEW";
            instance.RowVersion += 1;
            await db.SaveChangesAsync();
            visit.Status = VisitRequestStatuses.Approved;
            visit.RowVersion += 1;
            await db.SaveChangesAsync();
            instanceId = instance.VisitInstanceId;

            // The real approve-and-assign path also writes the Host's participant row. Without it the
            // seed would be a shape production never produces, and the handover would have no
            // outgoing participant to demote.
            db.VisitParticipants.Add(new PEMS.Domain.Entities.Delegations.VisitParticipant
            {
                VisitInstanceId = instanceId,
                UserId = leaderId,
                ParticipantRole = ParticipantRoles.IcHost,
                IsHost = true,
                Status = ParticipantStatuses.Assigned,
                AssignedBy = leaderId,
                AssignedAt = Now,
                CreatedAt = Now,
                CreatedBy = leaderId,
            });
            await db.SaveChangesAsync();
        }
        return (requestId, instanceId, leaderId);
    }

    /// <summary>An ACTIVE IC Staff of the campus — the eligible pool a handover can draw from.</summary>
    private static async Task<ulong> EligibleIcStaffAsync(ulong campusId, ulong excluding)
    {
        using var db = NewContext();
        return await (from u in db.Users
                      join r in db.Roles on u.RoleId equals r.RoleId
                      join d in db.Departments on u.DepartmentId equals d.DepartmentId
                      where r.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Staff
                            && u.PrimaryCampusId == campusId && u.Status == UserStatuses.Active
                            && d.DepartmentType == "IC" && u.UserId != excluding
                      orderby u.UserId
                      select u.UserId).FirstAsync();
    }

    private static TransferVisitHostCommandHandler Handler(
        ApplicationDbContext db, ulong actor, ulong campusId, RecordingNotifications? notifications = null)
        => new(db,
            new FakeUser { UserId = actor, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader, PrimaryCampusId = campusId },
            new FixedClock(),
            notifications ?? new RecordingNotifications(),
            new VisitFormReadService(db, new FakeUser { UserId = actor }, NullLogger<VisitFormReadService>.Instance, new FixedClock(), WriteOn),
            NullLogger<TransferVisitHostCommandHandler>.Instance);

    private static async Task<int> RowVersionAsync(ulong instanceId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.RowVersion).SingleAsync();
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE ac FROM visit_instance_amendment_changes ac JOIN visit_instance_amendments a ON a.amendment_id = ac.amendment_id WHERE a.visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_amendments WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── The handover itself ──────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_moves_the_host_and_leaves_the_approval_untouched()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceId, var leaderId) = await CreateApprovedAsync(Now.AddDays(20));
            var newHost = await EligibleIcStaffAsync(Campus1, leaderId);
            var version = await RowVersionAsync(instanceId);

            using (var db = NewContext())
            {
                var res = await Handler(db, leaderId, Campus1).Handle(
                    new TransferVisitHostCommand(instanceId, newHost, "Host cũ đi công tác", version),
                    CancellationToken.None);
                Assert.Equal(leaderId, res.PreviousHostUserId);
                Assert.Equal(newHost, res.NewHostUserId);
            }

            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.AsNoTracking()
                    .SingleAsync(c => c.VisitInstanceId == instanceId);
                Assert.Equal(newHost, instance.CurrentHostUserId);

                // The decision is the point: a handover must NOT send a settled request back through
                // a queue or rewrite who approved it.
                Assert.Equal(VisitInstanceStatuses.Assigned, instance.Status);
                Assert.Equal(leaderId, instance.DecidedBy);
                Assert.NotNull(instance.DecidedAt);

                var detail = await db.VisitInstanceFormDetails.AsNoTracking()
                    .SingleAsync(d => d.VisitInstanceId == instanceId);
                Assert.Equal(1u, detail.FormRevision);      // untouched by a handover
                Assert.Equal(1u, detail.ApprovalRevision);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Outgoing_host_stays_on_the_visit_as_support()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceId, var leaderId) = await CreateApprovedAsync(Now.AddDays(20));
            var newHost = await EligibleIcStaffAsync(Campus1, leaderId);
            var version = await RowVersionAsync(instanceId);

            using (var db = NewContext())
                await Handler(db, leaderId, Campus1).Handle(
                    new TransferVisitHostCommand(instanceId, newHost, "Bàn giao", version), CancellationToken.None);

            using (var db = NewContext())
            {
                var participants = await db.VisitParticipants.AsNoTracking()
                    .Where(p => p.VisitInstanceId == instanceId && p.Status != ParticipantStatuses.Removed)
                    .ToListAsync();

                var incoming = participants.Single(p => p.UserId == newHost);
                Assert.True(incoming.IsHost);
                Assert.Equal(ParticipantRoles.IcHost, incoming.ParticipantRole);

                // Removing them would revoke their access to the very request they were handing over,
                // and they may still hold logistics items on it.
                var outgoing = participants.SingleOrDefault(p => p.UserId == leaderId);
                Assert.NotNull(outgoing);
                Assert.False(outgoing!.IsHost);
                Assert.Equal(ParticipantRoles.IcSupport, outgoing.ParticipantRole);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Transfer_notifies_both_hosts_and_the_visitor()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceId, var leaderId) = await CreateApprovedAsync(Now.AddDays(20));
            var newHost = await EligibleIcStaffAsync(Campus1, leaderId);
            var version = await RowVersionAsync(instanceId);
            var notifications = new RecordingNotifications();

            using (var db = NewContext())
                await Handler(db, leaderId, Campus1, notifications).Handle(
                    new TransferVisitHostCommand(instanceId, newHost, "Bàn giao", version), CancellationToken.None);

            // The incoming Host has something to DO; nobody else does.
            var incoming = notifications.Sent.Single(n => n.RecipientUserId == newHost);
            Assert.True(incoming.IsActionRequired);
            // The acting leader was also the outgoing host here — telling them what they just did is noise.
            Assert.DoesNotContain(notifications.Sent, n => n.RecipientUserId == leaderId);
            // Every notification points at this instance, so opening one lands on the right campus.
            Assert.All(notifications.Sent, n => Assert.Equal(instanceId, n.VisitInstanceId));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Transfer_records_the_old_and_new_host_for_the_history_drawer()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceId, var leaderId) = await CreateApprovedAsync(Now.AddDays(20));
            var newHost = await EligibleIcStaffAsync(Campus1, leaderId);
            var version = await RowVersionAsync(instanceId);

            using (var db = NewContext())
                await Handler(db, leaderId, Campus1).Handle(
                    new TransferVisitHostCommand(instanceId, newHost, "Host cũ nghỉ phép", version), CancellationToken.None);

            using (var db = NewContext())
            {
                var audit = await db.AuditLogs.AsNoTracking()
                    .Include(a => a.Changes)
                    .SingleAsync(a => a.VisitRequestId == requestId && a.Action == VisitAuditActions.HostTransferred);
                Assert.Equal("Host cũ nghỉ phép", audit.Reason);

                var ids = audit.Changes.Single(c => c.FieldName == "currentHostUserId");
                Assert.Equal(leaderId.ToString(), ids.OldValueText);
                Assert.Equal(newHost.ToString(), ids.NewValueText);

                // Names too: the account may be renamed or deactivated long before anyone reads this.
                var names = audit.Changes.Single(c => c.FieldName == "currentHostName");
                Assert.False(string.IsNullOrWhiteSpace(names.OldValueText));
                Assert.False(string.IsNullOrWhiteSpace(names.NewValueText));
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Refusals ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_leader_of_another_campus_cannot_transfer()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceId, var leaderId) = await CreateApprovedAsync(Now.AddDays(20));
            var newHost = await EligibleIcStaffAsync(Campus1, leaderId);
            var version = await RowVersionAsync(instanceId);

            using var db = NewContext();
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Handler(db, leaderId, campusId: 2).Handle(
                    new TransferVisitHostCommand(instanceId, newHost, "Bàn giao", version), CancellationToken.None));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Transfer_is_refused_inside_the_lead_time()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceId, var leaderId) =
                await CreateApprovedAsync(Now.AddHours(VisitMutationPolicy.RequiredLeadHours - 1));
            var newHost = await EligibleIcStaffAsync(Campus1, leaderId);
            var version = await RowVersionAsync(instanceId);

            using var db = NewContext();
            var ex = await Assert.ThrowsAsync<VisitMutationRefusedException>(() =>
                Handler(db, leaderId, Campus1).Handle(
                    new TransferVisitHostCommand(instanceId, newHost, "Bàn giao", version), CancellationToken.None));
            Assert.Equal(VisitMutationErrorCodes.CutoffReached, ex.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Transfer_to_the_current_host_is_refused()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceId, var leaderId) = await CreateApprovedAsync(Now.AddDays(20));
            var version = await RowVersionAsync(instanceId);

            using var db = NewContext();
            var ex = await Assert.ThrowsAnyAsync<BusinessRuleException>(() =>
                Handler(db, leaderId, Campus1).Handle(
                    new TransferVisitHostCommand(instanceId, leaderId, "Bàn giao", version), CancellationToken.None));
            Assert.Equal(VisitHostTransferErrorCodes.SameHost, ex.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_stale_row_version_loses_rather_than_overwriting()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceId, var leaderId) = await CreateApprovedAsync(Now.AddDays(20));
            var newHost = await EligibleIcStaffAsync(Campus1, leaderId);
            var version = await RowVersionAsync(instanceId);

            using (var db = NewContext())
                await Handler(db, leaderId, Campus1).Handle(
                    new TransferVisitHostCommand(instanceId, newHost, "Lần một", version), CancellationToken.None);

            // Two leaders acting from the same stale screen must not both succeed — the second one's
            // notification would name a Host who no longer holds the role.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ConflictException>(() =>
                    Handler(db, leaderId, Campus1).Handle(
                        new TransferVisitHostCommand(instanceId, leaderId, "Lần hai", version), CancellationToken.None));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task An_ineligible_user_cannot_be_made_host()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceId, var leaderId) = await CreateApprovedAsync(Now.AddDays(20));
            var version = await RowVersionAsync(instanceId);

            // The registrant is a VISITOR — never a valid Host for a campus.
            using var db = NewContext();
            var ex = await Assert.ThrowsAnyAsync<BusinessRuleException>(() =>
                Handler(db, leaderId, Campus1).Handle(
                    new TransferVisitHostCommand(instanceId, Registrant, "Bàn giao", version), CancellationToken.None));
            Assert.Equal(VisitHostTransferErrorCodes.HostNotEligible, ex.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Capability agreement (§12: the UI must never offer what the handler refuses) ──

    [Fact]
    public async Task The_read_model_offers_transfer_exactly_when_the_handler_accepts_it()
    {
        RequireDb();
        ulong openRequest = 0, lateRequest = 0;
        try
        {
            (openRequest, var openInstance, var leaderId) = await CreateApprovedAsync(Now.AddDays(20));
            (lateRequest, _, var lateLeader) =
                await CreateApprovedAsync(Now.AddHours(VisitMutationPolicy.RequiredLeadHours - 1));

            using var db = NewContext();
            var leaderView = new FakeUser
            {
                UserId = leaderId, RoleCode = RoleCodes.Staff,
                SubRole = UserSubRoles.Leader, PrimaryCampusId = Campus1,
            };
            var reader = new VisitFormReadService(
                db, leaderView, NullLogger<VisitFormReadService>.Instance, new FixedClock(), WriteOn);

            var open = await reader.ResolveAsync(openRequest, CancellationToken.None);
            Assert.Contains(VisitFormActions.TransferHost, open.CampusVisits.Single().AllowedActions);

            var late = await reader.ResolveAsync(lateRequest, CancellationToken.None);
            var lateCampus = late.CampusVisits.Single();
            Assert.DoesNotContain(VisitFormActions.TransferHost, lateCampus.AllowedActions);

            // Refused, but REPORTED with its reason and deadline — that is what lets the screen show a
            // disabled button that explains itself instead of silently dropping the action.
            var capability = lateCampus.Capabilities.Single(c => c.Code == VisitFormActions.TransferHost);
            Assert.False(capability.Enabled);
            Assert.Equal(VisitMutationErrorCodes.CutoffReached, capability.DisabledReasonCode);
            Assert.NotNull(capability.CutoffAt);
            Assert.Equal(VisitMutationPolicy.RequiredLeadHours, capability.RequiredLeadHours);
            _ = lateLeader;
        }
        finally
        {
            await CleanupAsync(openRequest);
            await CleanupAsync(lateRequest);
        }
    }
}
