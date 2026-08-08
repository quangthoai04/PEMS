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
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.News.Queries.GetEligibleVisitInstancesForNews;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using MediaConsentStatus = PEMS.Shared.MediaConsentStatus;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 3D-2 — the "which visits can I write news about" query.
///
/// Eligibility is scoped by relation (accepted participant or host) AND, per Pure V2, by THIS campus's
/// own media consent: a mixed request where one campus agreed to media coverage and the other declined
/// must offer only the campus that agreed, even though the author is related to both instances. Consent
/// is a per-campus fact on visit_instance_form_details, not a request-level one, so a reader that took
/// consent from an arbitrary sibling campus would either hide an eligible visit or offer a forbidden one.
/// </summary>
public sealed class NewsEligibilityScopeV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
    private const ulong LeaderHcm = 9;
    private const ulong HostHn = 102;      // IC Host Protocol HN — host on the HN instance
    private const ulong HostHcm = 104;     // IC Host Media HCM — host on the HCM instance
    private const ulong AuthorStaff = 101; // IC Staff HN (sub_role STAFF) — the news author, a participant on both
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
        public FakeUser(ulong id, string roleCode, string? subRole = null, ulong? campusId = null)
        { UserId = id; RoleCode = roleCode; SubRole = subRole; PrimaryCampusId = campusId; }
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

    private sealed class SilentNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong u, string t, string? m, string n, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest r, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static CampusVisitFormDto Campus(string code, DateTime start, string delegationName, string consent)
        => new(code, start, start.AddMinutes(120), delegationName, "MEETING", null,
            $"Mục đích {delegationName}", $"Nội dung {delegationName}",
            new List<VisitorDto> { new($"Khách {delegationName}", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            // The contact is the REGISTRANT'S own address, so the campus self-matches at submit: confirmed
            // with no invitation, and the request is past the confirmation gate from the start. This suite
            // does not test that gate, and a campus behind it can be neither decided nor moved forward.
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "VI", null, consent, null, null);

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var actor = new FakeUser(Registrant, RoleCodes.Visitor);
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(), new VisitRequestV2CreateService(db),
            new SilentNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "NW" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId, ulong leaderId, ulong campusId, ulong hostId)
    {
        using var db = NewContext();
        var actor = new FakeUser(leaderId, RoleCodes.Staff, UserSubRoles.Leader, campusId);
        await new ApproveCampusInstanceCommandHandler(
                db, actor, new FixedClock(), 
                new CampusApprovalExecutor(
                    db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db), new SilentNotifications(),
                    new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()),
                    NullLogger<CampusApprovalExecutor>.Instance))
            .Handle(new ApproveCampusInstanceCommand(requestId, instanceId, hostId, null), CancellationToken.None);
    }

    private static async Task<Dictionary<ulong, ulong>> InstanceIdsAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .ToDictionaryAsync(c => c.CampusId, c => c.VisitInstanceId);
    }

    private static async Task AcceptParticipantAsync(ulong instanceId, ulong userId)
    {
        using var db = NewContext();
        db.VisitParticipants.Add(new VisitParticipant
        {
            VisitInstanceId = instanceId,
            UserId = userId,
            ParticipantRole = ParticipantRoles.IcSupport,
            IsHost = false,
            Status = ParticipantStatuses.Accepted,
            InvitedBy = LeaderHn,
            AssignedBy = LeaderHn,
            AssignedAt = Now,
            RespondedAt = Now,
            CreatedAt = Now,
            CreatedBy = LeaderHn,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Moves an approved instance to AFTER_VISIT — the news-writing window. The create service
    /// forbids a past schedule, so the row is created in the future and dragged into the past here, which
    /// is what a genuinely completed visit looks like by the time news is written. A DB trigger also
    /// requires at least one agenda item before AFTER_VISIT, so one is seeded first rather than the
    /// trigger being worked around.</summary>
    private static async Task MoveToAfterVisitAsync(ulong instanceId)
    {
        using var db = NewContext();
        db.VisitAgendas.Add(new VisitAgenda
        {
            VisitInstanceId = instanceId,
            Title = "[IT] Mục nghị trình",
            StartTime = Now.AddDays(-3),
            EndTime = Now.AddDays(-3).AddHours(1),
            SequenceOrder = 1,
            CreatedAt = Now,
            CreatedBy = LeaderHn,
        });
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = {0}, planned_start_at = {1}, planned_end_at = {2} WHERE visit_instance_id = {3}",
            VisitInstanceStatuses.AfterVisit, Now.AddDays(-3), Now.AddDays(-3).AddHours(2), instanceId);
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM news WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_agendas WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
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

    private static GetEligibleVisitInstancesForNewsQueryHandler Handler(ApplicationDbContext db, ulong userId)
        => new(db, new FakeUser(userId, RoleCodes.Staff, UserSubRoles.Staff, CampusHn));

    [Fact]
    public async Task Per_campus_consent_offers_only_the_campus_that_agreed_within_one_mixed_request()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var start = Now.AddDays(30); // create in the future (create forbids past); dragged to the past by MoveToAfterVisit
            requestId = await CreateAsync(
                Campus("HN", start, $"ĐoànHN{tag}", MediaConsentStatus.Agreed),
                Campus("HCM", start.AddDays(1), $"ĐoànHCM{tag}", MediaConsentStatus.Declined));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, HostHcm);

            // The author is an accepted participant on BOTH instances, so relation alone does not decide it.
            await AcceptParticipantAsync(instances[CampusHn], AuthorStaff);
            await AcceptParticipantAsync(instances[CampusHcm], AuthorStaff);
            await MoveToAfterVisitAsync(instances[CampusHn]);
            await MoveToAfterVisitAsync(instances[CampusHcm]);

            using var db = NewContext();
            var res = await Handler(db, AuthorStaff).Handle(
                new GetEligibleVisitInstancesForNewsQuery(), CancellationToken.None);

            var mine = res.Items.Where(i => i.VisitInstanceId == instances[CampusHn]
                                            || i.VisitInstanceId == instances[CampusHcm]).ToList();
            var only = Assert.Single(mine);
            Assert.Equal(instances[CampusHn], only.VisitInstanceId);      // the AGREED campus
            Assert.Contains($"ĐoànHN{tag}", only.VisitTitle);            // titled from its own detail
            Assert.DoesNotContain(res.Items, i => i.VisitInstanceId == instances[CampusHcm]); // DECLINED never offered
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task An_unrelated_staff_member_is_offered_nothing_from_this_request()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var start = Now.AddDays(30);
            requestId = await CreateAsync(Campus("HN", start, $"ĐoànHN{tag}", MediaConsentStatus.Agreed));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await MoveToAfterVisitAsync(instances[CampusHn]);

            using var db = NewContext();

            // The host (a participant by assignment) IS offered the visit...
            var hostRes = await Handler(db, HostHn).Handle(new GetEligibleVisitInstancesForNewsQuery(), CancellationToken.None);
            Assert.Contains(hostRes.Items, i => i.VisitInstanceId == instances[CampusHn]);

            // ...but a staff member with no relation to it is offered nothing from this request.
            var strangerRes = await Handler(db, AuthorStaff).Handle(new GetEligibleVisitInstancesForNewsQuery(), CancellationToken.None);
            Assert.DoesNotContain(strangerRes.Items, i => i.VisitInstanceId == instances[CampusHn]);
        }
        finally { await CleanupAsync(requestId); }
    }
}
