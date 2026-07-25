using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.VisitAmendments;
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

    private const ulong VisitorOwner = 8, SlCampus1 = 3, SlCampus2 = 9, IcStaffC1 = 4;
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
    private static FakeUser StaffLeader(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader, PrimaryCampusId = campusId };

    private static GetVisitRequestHistoryQueryHandler Handler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new PerCampusFormV2Options { Enabled = true });

    /// <summary>Two campuses, campus 1 decided (approved + hosted) with a note, campus 2 still waiting.</summary>
    private static async Task<(VisitRequest Request, List<VisitRequestCampus> Instances)> SeedAsync(ApplicationDbContext db)
    {
        var now = DateTime.Now;
        var req = new VisitRequest
        {
            RequestCode = "HIST-" + Guid.NewGuid().ToString("N")[..12],
            VisitorUserId = VisitorOwner,
            RegistrantUserId = VisitorOwner,
            CreatedSource = "VISITOR_SUBMITTED",
            HasMixedCampusDetails = true,
            RegistrantFullName = "Reg", RegistrantOrganization = "Org", RegistrantJobTitle = "Job",
            RegistrantPhone = "+8490", RegistrantEmail = "reg@example.com", RegistrantNationality = "VN",
            VisitScope = "MULTI_CAMPUS",
            ContactPersonFullName = "Primary Contact", ContactPersonOrganization = "COrg",
            ContactPersonPhone = "+8491", ContactPersonEmail = "contact@example.com",
            PrimaryContactAccessStatus = "ACTIVE", PrimaryContactVerifiedAt = now,
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
}
