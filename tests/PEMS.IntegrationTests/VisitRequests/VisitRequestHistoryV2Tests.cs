using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The scoped history timeline. These exist because the previous shape shipped assembled audit strings
/// ("source=CREATE;approvalRevision=1", "Cơ sở: REJECTED") straight to whoever opened the page, and
/// because the handler built an actor-name dictionary and then never applied it — so every entry went
/// out with ActorName = null and nothing failed. Both are the kind of defect only a test catches.
///
/// Seed ids in pems_pr3_test: visitor owner = 8, Staff Leader campus1 = 3, Staff Leader campus2 = 9,
/// IC Staff campus1 (host) = 4, HO = 2.
/// </summary>
public sealed class VisitRequestHistoryV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong VisitorOwner = 8, SlCampus1 = 3, SlCampus2 = 9, IcStaffC1 = 4, HoUser = 2;
    /// <summary>ACTIVE Student — invited to SUPPORT a campus, and nothing more than that.</summary>
    private const ulong SupportingStudent = 152;
    /// <summary>ACTIVE IC Staff who hosts nothing in this fixture — a pure supporting participant.</summary>
    private const ulong IcStaffSupporter = 101;
    private const ulong Campus1 = 1, Campus2 = 2;

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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master to run these tests.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public bool IsAuthenticated => UserId is not null;
        public ulong? UserId { get; init; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; init; }
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId { get; init; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static FakeUser Owner() => new() { UserId = VisitorOwner, RoleCode = RoleCodes.Visitor };
    private static FakeUser Ho() => new() { UserId = HoUser, RoleCode = RoleCodes.Ho };
    private static FakeUser StaffLeader(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader, PrimaryCampusId = campusId };
    private static FakeUser Host(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff, PrimaryCampusId = campusId };
    private static FakeUser Student(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Student, PrimaryCampusId = campusId };

    private static GetVisitRequestHistoryQueryHandler Handler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new PerCampusFormV2Options { Enabled = true });

    /// <summary>
    /// The V2 detail read model — the OTHER half of every capability test here. The capability and the
    /// endpoint have to be asked in the same test, because the bug being locked down is precisely that
    /// they disagreed.
    /// </summary>
    private static VisitFormReadService Resolver(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, NullLogger<VisitFormReadService>.Instance);

    /// <summary>Two campuses, campus 1 decided (approved + hosted) with a note, campus 2 still waiting.</summary>
    private static async Task<(VisitRequest Request, List<VisitRequestCampus> Instances)> SeedAsync(ApplicationDbContext db)
    {
        var now = DateTime.Now;
        var req = new VisitRequest
        {
            RequestCode = "HIST-" + Guid.NewGuid().ToString("N")[..12],
            RegistrantUserId = VisitorOwner,
            CreatedSource = "VISITOR_SUBMITTED",
            HasMixedCampusDetails = true,
            RegistrantFullName = "Reg", RegistrantOrganization = "Org", RegistrantJobTitle = "Job",
            RegistrantPhone = "+8490", RegistrantEmail = "reg@example.com", RegistrantNationality = "VN",
            VisitScope = "MULTI_CAMPUS",
            Status = "PARTIALLY_APPROVED", SubmittedAt = now, CreatedAt = now,
        };

        foreach (var (campusId, host) in new[] { (Campus1, (ulong?)IcStaffC1), (Campus2, (ulong?)null) })
        {
            req.CampusInstances.Add(new VisitRequestCampus
            {
                CampusId = campusId,
                PlannedStartAt = now.AddDays(20),
                PlannedEndAt = now.AddDays(20).AddHours(2),
                Status = host is null ? "WAITING_REQUEST_APPROVAL" : "ASSIGNED",
                // Self-matched: the registrant is each campus's operational contact, so both campuses sit
                // past the confirmation gate. A campus beyond WAITING_CONTACT_CONFIRMATION with no
                // contact is refused by trg_visit_campuses_op_contact_guard_bi.
                OperationalContactUserId = VisitorOwner,
                OperationalContactConfirmedAt = now,
                OperationalContactConfirmationSource = "REGISTRANT_SELF_MATCH",
                CurrentHostUserId = host,
                HostAssignedBy = host is null ? null : SlCampus1,
                HostAssignedAt = host is null ? null : now,
                DecidedBy = host is null ? null : SlCampus1,
                DecidedAt = host is null ? null : now,
                DecisionActorRole = host is null ? null : "STAFF_LEADER",
                DecisionSource = host is null ? null : "STANDARD_CAMPUS_REVIEW",
                DecisionNote = host is null ? null : "Tiếp nhận bình thường",
                CreatedAt = now,
                FormDetail = new VisitInstanceFormDetail
                {
                    DelegationName = $"DELEG-{campusId}", VisitType = "MEETING", Purpose = "P",
                    WorkingContent = "C",
                    OperationalContactFullName = "Op", OperationalContactOrganization = "OpOrg",
                    OperationalContactJobTitle = "Trưởng phòng Hợp tác",
                    OperationalContactPhone = "+8410", OperationalContactEmail = "op@example.com",
                    WorkingLanguage = "VI", MediaConsentStatus = "AGREED",
                    FormRevision = 1, ApprovalRevision = 1, CreatedAt = now,
                },
            });
        }

        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();

        var ordered = req.CampusInstances.OrderBy(c => c.CampusId).ToList();

        // Every campus gets a CREATE revision when the request is written, so seed one each — otherwise
        // a campus with no decision produces no timeline entry at all and the fixture is unrealistic.
        foreach (var inst in ordered)
        {
            db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = req.VisitRequestId,
                VisitInstanceId = inst.VisitInstanceId,
                FormRevision = 1,
                ApprovalRevision = 1,
                SourceType = "CREATE",
                // The snapshot is what the row archives; the timeline never reads or exposes it.
                SnapshotJson = "{}",
                AppliedBy = VisitorOwner,
                AppliedAt = now,
            });
        }
        await db.SaveChangesAsync();

        return (req, ordered);
    }

    // ── Structure, not prose ─────────────────────────────────────────────────

    [Fact]
    public async Task Decision_entries_carry_the_actor_name_the_handler_looked_up()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        var decision = Assert.Single(result.Entries.Where(
            e => e.EventCode == VisitHistoryEventCodes.InstanceApproved));
        // The name dictionary used to be built and then dropped on the floor.
        Assert.False(string.IsNullOrWhiteSpace(decision.ActorName));
        Assert.Equal("Tiếp nhận bình thường", decision.Reason);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Decision_entries_name_their_campus_so_multi_campus_rows_differ()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        var perInstance = result.Entries
            .Where(e => e.VisitInstanceId != null)
            .GroupBy(e => e.VisitInstanceId!.Value)
            .ToDictionary(g => g.Key, g => g.First().CampusName);

        Assert.Equal(2, perInstance.Count);
        Assert.All(perInstance.Values, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        // Two campuses, two DIFFERENT names — otherwise the rows are indistinguishable to a reader.
        Assert.Equal(2, perInstance.Values.Distinct().Count());
        Assert.Contains(instances[0].VisitInstanceId, perInstance.Keys);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Entries_expose_facts_not_pre_assembled_audit_strings()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        Assert.NotEmpty(result.Entries);
        // Every code is a known member of the vocabulary the client maps to a sentence.
        Assert.All(result.Entries, e => Assert.False(string.IsNullOrWhiteSpace(e.EventCode)));
        // The old glued fragments are gone from every string-bearing field.
        var strings = result.Entries.SelectMany(e => new[] { e.Reason, e.CampusName, e.ActorName })
            .Where(s => s is not null)!;
        Assert.All(strings, s =>
        {
            Assert.DoesNotContain("source=", s);
            Assert.DoesNotContain("approvalRevision=", s);
            Assert.DoesNotContain("→", s);
        });
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Content_creation_is_reported_per_campus_with_its_revision()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        var created = result.Entries
            .Where(e => e.EventCode == VisitHistoryEventCodes.InstanceContentCreated).ToList();
        Assert.Equal(2, created.Count);
        Assert.All(created, e =>
        {
            Assert.Equal(1u, e.FormRevision);
            Assert.Equal("CREATE", e.SourceType);      // a FACT the client may or may not render
            Assert.False(string.IsNullOrWhiteSpace(e.CampusName));
        });
        await tx.RollbackAsync();
    }

    // ── The three events the timeline used to be blind to ────────────────────
    // The rows were already being written by VisitSafeEditService / VisitRequestV2EditService and the
    // cancellation columns were already on the request — the handler simply collapsed every revision
    // into "created / revised" by its NUMBER and never looked at the request row at all. So a quick
    // update, a resubmission and a cancellation were invisible or indistinguishable.

    /// <summary>
    /// Adds a request-level revision with the given source type, plus the matching per-campus row when
    /// an instance is supplied. `(visit_instance_id, form_revision)` is unique, so SeedAsync's own
    /// revision 1 must never be re-used here.
    /// </summary>
    private static async Task SeedRevisionAsync(
        ApplicationDbContext db, VisitRequest req, VisitRequestCampus? instance,
        string sourceType, uint formRevision, uint requestRevision, ulong actorId)
    {
        var now = DateTime.Now;
        if (instance is not null)
        {
            db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = req.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                FormRevision = formRevision,
                ApprovalRevision = 1,
                SourceType = sourceType,
                SnapshotJson = "{}",
                AppliedBy = actorId,
                AppliedAt = now,
                // The writers store a correlation id here. It must never reach the reader as a "reason".
                Reason = Guid.NewGuid().ToString("N"),
            });
        }
        db.VisitRequestRevisionHistories.Add(new VisitRequestRevisionHistory
        {
            VisitRequestId = req.VisitRequestId,
            RequestRevision = requestRevision,
            SourceType = sourceType,
            SnapshotJson = "{}",
            AppliedBy = actorId,
            AppliedAt = now,
            Reason = Guid.NewGuid().ToString("N"),
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_safe_edit_is_reported_as_a_quick_update_at_both_levels()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        await SeedRevisionAsync(db, req, instances[0], "SAFE_EDIT", 2, 2, VisitorOwner);

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        var instanceEdit = Assert.Single(result.Entries.Where(
            e => e.EventCode == VisitHistoryEventCodes.InstanceSafeEditApplied));
        Assert.Equal(instances[0].VisitInstanceId, instanceEdit.VisitInstanceId);
        Assert.Equal(2u, instanceEdit.FormRevision);
        Assert.False(string.IsNullOrWhiteSpace(instanceEdit.ActorName));
        Assert.False(string.IsNullOrWhiteSpace(instanceEdit.CampusName));

        var requestEdit = Assert.Single(result.Entries.Where(
            e => e.EventCode == VisitHistoryEventCodes.RequestSafeEditApplied));
        Assert.False(string.IsNullOrWhiteSpace(requestEdit.ActorName));

        // The correlation id the writer parks in `reason` is plumbing, not a business reason.
        Assert.Null(instanceEdit.Reason);
        Assert.Null(requestEdit.Reason);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_resubmission_is_reported_once_for_the_request_and_once_per_campus()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        await SeedRevisionAsync(db, req, instances[0], "RESUBMIT", 2, 2, VisitorOwner);
        await SeedRevisionAsync(db, req, instances[1], "RESUBMIT", 2, 3, VisitorOwner);

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        // The request-level sentence is the one a reader is looking for ("đã gửi lại đơn"); the
        // per-campus rows say WHICH content went back in.
        Assert.Equal(2, result.Entries.Count(
            e => e.EventCode == VisitHistoryEventCodes.RequestResubmitted));
        var perCampus = result.Entries
            .Where(e => e.EventCode == VisitHistoryEventCodes.InstanceContentResubmitted).ToList();
        Assert.Equal(2, perCampus.Count);
        Assert.Equal(2, perCampus.Select(e => e.VisitInstanceId).Distinct().Count());
        Assert.All(perCampus, e => Assert.False(string.IsNullOrWhiteSpace(e.ActorName)));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_cancelled_request_reports_who_cancelled_it_when_and_why()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        var cancelledAt = DateTime.Now.AddMinutes(-5);
        req.Status = VisitRequestStatuses.Cancelled;
        req.CancelledBy = VisitorOwner;
        req.CancelledAt = cancelledAt;
        req.CancellationReason = "Đoàn thay đổi lịch bay";
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        var cancelled = Assert.Single(result.Entries.Where(
            e => e.EventCode == VisitHistoryEventCodes.RequestCancelled));
        Assert.False(string.IsNullOrWhiteSpace(cancelled.ActorName));
        Assert.Equal("Đoàn thay đổi lịch bay", cancelled.Reason);
        Assert.Equal(VisitRequestStatuses.Cancelled, cancelled.StatusCode);
        Assert.Null(cancelled.VisitInstanceId); // request-level, not a campus event
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_scoped_leader_does_not_receive_the_request_level_cancellation()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        req.Status = VisitRequestStatuses.Cancelled;
        req.CancelledBy = VisitorOwner;
        req.CancelledAt = DateTime.Now;
        req.CancellationReason = "Lý do nội bộ của đoàn";
        await db.SaveChangesAsync();

        var result = await Handler(db, StaffLeader(SlCampus2, Campus2)).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        // Request-level entries stay with the managers/HO — a campus leader's timeline is their campus.
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.RequestCancelled);
        Assert.DoesNotContain(result.Entries, e => e.Reason == "Lý do nội bộ của đoàn");
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task The_first_request_revision_reads_as_a_submission_not_an_update()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        // Request level only: SeedAsync already wrote each campus's own CREATE revision 1.
        await SeedRevisionAsync(db, req, null, "CREATE", 1, 1, VisitorOwner);

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        Assert.Contains(result.Entries, e => e.EventCode == VisitHistoryEventCodes.RequestCreated);
        // "đã cập nhật thông tin chung" was the sentence a CREATE row used to produce.
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.RequestRevision);
        await tx.RollbackAsync();
    }

    // ── Scope ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_scoped_leader_sees_only_their_own_campus_and_no_identity_events()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var result = await Handler(db, StaffLeader(SlCampus2, Campus2)).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        // Campus 1's decision belongs to a campus this leader cannot see.
        Assert.DoesNotContain(result.Entries, e => e.VisitInstanceId == instances[0].VisitInstanceId);
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactIdentityChanged);
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.RequestRevision);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task An_unrelated_actor_is_refused()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        var stranger = new FakeUser { UserId = 22, RoleCode = RoleCodes.Visitor };

        await Assert.ThrowsAsync<ForbiddenException>(() => Handler(db, stranger).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None));
        await tx.RollbackAsync();
    }

    // ── Detail visibility and history visibility are two permissions ──────────
    //
    // They have always had different answers on the backend; what did not exist was any way for the
    // detail screen to KNOW that, so it mounted the history section for everyone who could open the
    // page and painted the resulting 403 as a technical failure with a Retry button. These tests pin
    // the mismatch as the intended rule and pin the capability that reports it, in the same test —
    // asserting either one alone is how the two drifted apart in the first place.

    /// <summary>Invites a user to SUPPORT one campus. Support, and nothing else.</summary>
    private static async Task SeedSupportingParticipantAsync(
        ApplicationDbContext db, VisitRequestCampus instance, ulong userId, string role)
    {
        var now = DateTime.Now;
        db.VisitParticipants.Add(new VisitParticipant
        {
            VisitInstanceId = instance.VisitInstanceId,
            UserId = userId,
            ParticipantRole = role,
            IsHost = false,
            Status = ParticipantStatuses.Accepted,
            InvitedBy = SlCampus1, InvitedAt = now,
            AssignedBy = SlCampus1, AssignedAt = now,
            RespondedAt = now, CreatedAt = now, CreatedBy = SlCampus1,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_supporting_participant_reads_the_detail_and_is_refused_the_history()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        await SeedSupportingParticipantAsync(db, instances[0], SupportingStudent, ParticipantRoles.Student);
        var participant = Student(SupportingStudent, Campus1);

        // The detail they were invited to is theirs to read — that half must keep working.
        var resolved = await Resolver(db, participant).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.NotEmpty(resolved.CampusVisits);
        Assert.Contains(VisitFormActions.View, resolved.Viewer.AllowedActions);

        // The history is not. The capability says so, so the client never mounts the section...
        Assert.DoesNotContain(VisitFormActions.ViewChangeHistory, resolved.Viewer.AllowedActions);

        // ...and the endpoint says the same thing, which is the point: a capability that disagreed
        // with the API would just move the bug from one side of the wire to the other.
        await Assert.ThrowsAsync<ForbiddenException>(() => Handler(db, participant).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None));

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task An_ic_support_participant_gains_no_history_either()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        // IC_SUPPORT on campus 2 — a STAFF account, so this also proves the refusal is about the
        // RELATION and not about the role name on the badge. Deliberately NOT IcStaffC1, who hosts
        // campus 1 in this fixture and therefore has history there on merit.
        await SeedSupportingParticipantAsync(db, instances[1], IcStaffSupporter, ParticipantRoles.IcSupport);
        // Not the host of campus 2 and not its leader: supporting it is their whole connection to it.
        var supporter = Host(IcStaffSupporter, Campus2);

        var resolved = await Resolver(db, supporter).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.NotEmpty(resolved.CampusVisits);
        Assert.DoesNotContain(VisitFormActions.ViewChangeHistory, resolved.Viewer.AllowedActions);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task The_current_host_is_granted_the_capability_and_their_scoped_history()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var host = Host(IcStaffC1, Campus1);   // hosts campus 1 only

        var resolved = await Resolver(db, host).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.Contains(VisitFormActions.ViewChangeHistory, resolved.Viewer.AllowedActions);

        var result = await Handler(db, host).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.NotEmpty(result.Entries);
        // Scoping is unchanged: the campus they do NOT host stays out, and so do the request-level
        // events. Granting the capability must not have widened what it grants access TO.
        Assert.DoesNotContain(result.Entries, e => e.VisitInstanceId == instances[1].VisitInstanceId);
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.RequestRevision);

        await tx.RollbackAsync();
    }

    [Theory]
    [InlineData("REGISTRANT")]
    [InlineData("HO")]
    [InlineData("STAFF_LEADER")]
    public async Task The_existing_history_actors_keep_the_capability(string who)
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        FakeUser actor = who switch
        {
            "REGISTRANT" => Owner(),
            "HO" => Ho(),
            _ => StaffLeader(SlCampus1, Campus1),
        };

        var resolved = await Resolver(db, actor).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.Contains(VisitFormActions.ViewChangeHistory, resolved.Viewer.AllowedActions);

        // And the endpoint still serves them, so the capability is not a claim nobody checked.
        var result = await Handler(db, actor).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.NotEmpty(result.Entries);

        await tx.RollbackAsync();
    }
}
