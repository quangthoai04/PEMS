using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// PR-3 persistence + dual-read + authorization tests for per-campus visit form v2. Runs against a
/// DISPOSABLE MySQL database <c>pems_pr3_test</c> built from the PR-2 fresh-create master — never the
/// real pems_db / pems_test. Each test seeds inside a transaction it rolls back, so the DB stays clean
/// and tests are independent. The whole class self-skips (throws a clear Skip) when the DB is absent.
///
/// Seed ids in pems_pr3_test: visitor owner = 8, other visitor = 22, Staff Leader campus1 = 3,
/// Staff Leader campus2 = 9, IC Staff campus1 (host) = 4, HO = 2, Admin = 1, VISITOR role_id = 6.
/// </summary>
public sealed class PerCampusFormV2ReadTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong VisitorOwner = 8, VisitorOther = 22, SlCampus1 = 3, SlCampus2 = 9,
                        IcStaffC1 = 4, HoUser = 2, AdminUser = 1;
    private const ulong Campus1 = 1, Campus2 = 2;

    private static bool? _dbUp;

    private static ApplicationDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString))
            .Options;
        return new ApplicationDbContext(options);
    }

    // These are MySQL-backed integration tests (like the repo's other pems_test tests). They require
    // the disposable pems_pr3_test database (PR-2 master imported). A clear, non-silent failure if absent.
    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master into it to run these tests.");
    }

    private static VisitFormReadService Resolver(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, NullLogger<VisitFormReadService>.Instance);

    // ── Fixture builders ────────────────────────────────────────────────────

    private static VisitRequest NewRequest(byte schemaVersion, string scope, bool mixed = false) => new()
    {
        RequestCode = "PR3-" + Guid.NewGuid().ToString("N")[..12],
        VisitorUserId = VisitorOwner,
        RegistrantUserId = VisitorOwner,
        CreatedSource = "VISITOR_SUBMITTED",
        FormSchemaVersion = schemaVersion,
        HasMixedCampusDetails = mixed,
        RegistrantFullName = "Reg", RegistrantOrganization = "Org", RegistrantJobTitle = "Job",
        RegistrantPhone = "+8490", RegistrantEmail = "reg@example.com", RegistrantNationality = "VN",
        DelegationName = "GLOBAL-DELEG", VisitScope = scope, VisitType = "MEETING",
        Purpose = "GLOBAL-PURPOSE", WorkingContent = "GLOBAL-CONTENT",
        ContactPersonFullName = "Primary Contact", ContactPersonOrganization = "COrg",
        ContactPersonPhone = "+8491", ContactPersonEmail = "contact@example.com",
        WorkingLanguage = "EN", MediaConsentStatus = "DECLINED",
        PrimaryContactAccessStatus = "ACTIVE", PrimaryContactVerifiedAt = DateTime.Now,
        Status = "PENDING_APPROVAL", SubmittedAt = DateTime.Now, CreatedAt = DateTime.Now,
    };

    private static VisitRequestCampus NewInstance(ulong campusId, ulong? hostUserId = null) => new()
    {
        CampusId = campusId,
        PlannedStartAt = DateTime.Now.AddDays(20),
        PlannedEndAt = DateTime.Now.AddDays(20).AddHours(2),
        Status = hostUserId is null ? "WAITING_REQUEST_APPROVAL" : "ASSIGNED",
        CurrentHostUserId = hostUserId,
        HostAssignedBy = hostUserId is null ? null : SlCampus1,
        HostAssignedAt = hostUserId is null ? null : DateTime.Now,
        DecidedBy = hostUserId is null ? null : SlCampus1,
        DecidedAt = hostUserId is null ? null : DateTime.Now,
        DecisionActorRole = hostUserId is null ? null : "STAFF_LEADER",
        DecisionSource = hostUserId is null ? null : "STANDARD_CAMPUS_REVIEW",
        CreatedAt = DateTime.Now,
    };

    private static VisitInstanceFormDetail NewDetail(string tag) => new()
    {
        DelegationName = $"DELEG-{tag}", VisitType = "MEETING", Purpose = $"PURPOSE-{tag}",
        WorkingContent = $"CONTENT-{tag}",
        OperationalContactFullName = $"Op-{tag}", OperationalContactOrganization = $"OpOrg-{tag}",
        OperationalContactPhone = "+8410", OperationalContactEmail = $"op-{tag}@example.com",
        WorkingLanguage = tag == "B" ? "VI" : "EN", MediaConsentStatus = "AGREED",
        FormRevision = 1, ApprovalRevision = 1, CreatedAt = DateTime.Now,
    };

    private static VisitGuestMember NewMember(ulong requestId, string name, string type = "GUEST") => new()
    {
        VisitRequestId = requestId, MemberType = type, FullName = name,
        Organization = "GOrg", JobTitle = "GJob", Nationality = "VN", CreatedAt = DateTime.Now,
    };

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
    private static FakeUser Admin() => new() { UserId = AdminUser, RoleCode = RoleCodes.Admin };
    private static FakeUser Unrelated() => new() { UserId = VisitorOther, RoleCode = RoleCodes.Visitor };
    private static FakeUser StaffLeader(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader, PrimaryCampusId = campusId };
    private static FakeUser Host(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff, PrimaryCampusId = campusId };

    // ── 1. Persistence / model mapping against the real PR-2 MySQL schema ─────

    [Fact]
    public async Task Model_builds_and_new_dbsets_query_against_pr2_schema()
    {
        RequireDb();
        using var db = NewContext();
        // Touching each new DbSet forces EF to build the model against the real schema (composite
        // FKs, alternate keys, one-to-one shared PK). A model error would throw here.
        Assert.Equal(0, await db.VisitInstanceFormDetails.CountAsync(d => d.VisitInstanceId == 0));
        Assert.Equal(0, await db.VisitInstanceGuestMembers.CountAsync(l => l.VisitInstanceId == 0));
        Assert.Equal(0, await db.VisitRequestIdentityChanges.CountAsync(c => c.IdentityChangeId == 0));
        Assert.Equal(0, await db.VisitRequestIdentityChangeEvents.CountAsync(e => e.IdentityChangeEventId == 0));
        Assert.Equal(0, await db.VisitInstanceAmendments.CountAsync(a => a.AmendmentId == 0));
        Assert.Equal(0, await db.VisitInstanceAmendmentChanges.CountAsync(a => a.AmendmentChangeId == 0));
        Assert.Equal(0, await db.VisitInstanceFormRevisionHistories.CountAsync(h => h.RevisionHistoryId == 0));
        Assert.Equal(0, await db.VisitRequestRevisionHistories.CountAsync(h => h.RequestRevisionHistoryId == 0));
    }

    [Fact]
    public async Task Generated_guard_columns_are_not_written_on_insert()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);

        // Insert a PENDING_APPROVAL amendment + a PENDING identity change. If EF tried to write the
        // VIRTUAL guard columns (amendment_pending_guard / pending_guard) MySQL would reject the
        // INSERT (error 3105). A clean SaveChanges proves EF excludes the generated columns.
        db.VisitInstanceAmendments.Add(new VisitInstanceAmendment
        {
            VisitRequestId = req.VisitRequestId, VisitInstanceId = instances[0].VisitInstanceId,
            AmendmentNo = 1, Status = AmendmentStatuses.PendingApproval,
            BaseFormRevision = 1, BaseApprovalRevision = 1, RequestedBy = VisitorOwner,
            RequestedAt = DateTime.Now, ExpectedInstanceRowVersion = 0, CreatedAt = DateTime.Now,
        });
        db.VisitRequestIdentityChanges.Add(new VisitRequestIdentityChange
        {
            VisitRequestId = req.VisitRequestId, ChangeKind = IdentityChangeKinds.InitialClaim,
            TargetRelation = IdentityChangeTargetRelations.PrimaryContact, NewEmailMasked = "n***@e.com",
            Status = IdentityChangeStatuses.Pending, ExpectedRequestRowVersion = 0,
            RequestedBy = VisitorOwner, RequestedAt = DateTime.Now, ExpiresAt = DateTime.Now.AddHours(72),
            CreatedAt = DateTime.Now,
        });

        var affected = await db.SaveChangesAsync();
        Assert.True(affected >= 2);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Resolved_campus_rowversion_is_the_instance_token_not_the_form_detail()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);

        // Simulate a campus-approve: the CAMPUS INSTANCE row_version is bumped, the form-detail's is not.
        // The read model must surface the instance token — that is what pending-edit / safe-edit / amendment
        // all check against — so a safe-edit/amendment on a freshly-loaded ASSIGNED detail never 409s. (Caught
        // by the real-stack member-amendment journey: exposing the form-detail version here 409'd the submit.)
        var instance = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == instances[0].VisitInstanceId);
        instance.RowVersion += 5;
        await db.SaveChangesAsync();
        var detail = await db.VisitInstanceFormDetails.AsNoTracking()
            .SingleAsync(d => d.VisitInstanceId == instances[0].VisitInstanceId);

        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        var campus = Assert.Single(dto.CampusVisits);
        Assert.Equal(instance.RowVersion, campus.RowVersion);   // the instance token the write paths check
        Assert.NotEqual(detail.RowVersion, campus.RowVersion);  // the diverged form-detail version is NOT surfaced

        await tx.RollbackAsync();
    }

    // ── 2. Dual-read v1 ───────────────────────────────────────────────────────

    [Fact]
    public async Task V1_single_campus_resolves_from_global_projection()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV1Async(db, new[] { Campus1 });
        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        Assert.Equal(FormSchemaVersions.Legacy, dto.FormSchemaVersion);
        var c = Assert.Single(dto.CampusVisits);
        Assert.Equal("GLOBAL-DELEG", c.DelegationName);
        Assert.Equal("GLOBAL-PURPOSE", c.Purpose);
        Assert.Equal("contact@example.com", c.OperationalContact.Email); // v1: primary contact projected
        Assert.Contains(c.Visitors, m => m.FullName == "G1"); // request-level member
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V1_multi_campus_gives_every_campus_the_same_snapshot()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV1Async(db, new[] { Campus1, Campus2 });
        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        Assert.Equal(2, dto.CampusVisits.Count);
        Assert.All(dto.CampusVisits, c => Assert.Equal("GLOBAL-DELEG", c.DelegationName));
        Assert.All(dto.CampusVisits, c => Assert.Equal("GLOBAL-PURPOSE", c.Purpose));
        await tx.RollbackAsync();
    }

    // ── 3. Dual-read v2 ───────────────────────────────────────────────────────

    [Fact]
    public async Task V2_single_campus_reads_detail_and_links()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);
        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        Assert.Equal(FormSchemaVersions.PerCampus, dto.FormSchemaVersion);
        var c = Assert.Single(dto.CampusVisits);
        Assert.Equal("DELEG-A", c.DelegationName);
        Assert.Equal("PURPOSE-A", c.Purpose);
        Assert.Equal("op-A@example.com", c.OperationalContact.Email); // per-campus operational contact
        Assert.Contains(c.Visitors, m => m.FullName == "A-guest");     // per-campus link
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_campus_mixed_keeps_each_campus_independent()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        Assert.True(dto.HasMixedCampusDetails);
        var a = dto.CampusVisits.Single(c => c.CampusId == (long)Campus1);
        var b = dto.CampusVisits.Single(c => c.CampusId == (long)Campus2);
        Assert.Equal("DELEG-A", a.DelegationName);
        Assert.Equal("DELEG-B", b.DelegationName);            // campus B not equal to campus A
        Assert.Equal("PURPOSE-A", a.Purpose);
        Assert.Equal("PURPOSE-B", b.Purpose);
        Assert.Equal("EN", a.WorkingLanguage);
        Assert.Equal("VI", b.WorkingLanguage);
        Assert.Contains(a.Visitors, m => m.FullName == "A-guest");
        Assert.Contains(b.Visitors, m => m.FullName == "B-guest");
        Assert.DoesNotContain(a.Visitors, m => m.FullName == "B-guest"); // no cross-campus member leak
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_missing_detail_throws_consistency_error_no_fallback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);
        // Delete the only detail row → a v2 instance with no per-campus detail.
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == instances[0].VisitInstanceId);
        db.VisitInstanceFormDetails.Remove(detail);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None));
        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    // ── 4. Authorization / scope ─────────────────────────────────────────────

    [Fact]
    public async Task StaffLeader_sees_only_own_campus_hidden_campus_not_in_payload()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true);

        var dtoA = await Resolver(db, StaffLeader(SlCampus1, Campus1)).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        var a = Assert.Single(dtoA.CampusVisits);
        Assert.Equal((long)Campus1, a.CampusId);
        Assert.DoesNotContain(dtoA.CampusVisits, c => c.CampusId == (long)Campus2); // hidden campus absent
        Assert.False(dtoA.Viewer.CanViewAllCampuses);
        Assert.Equal(VisitInstanceAccessRelations.StaffLeader, dtoA.Viewer.Relation);

        var dtoB = await Resolver(db, StaffLeader(SlCampus2, Campus2)).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        var b = Assert.Single(dtoB.CampusVisits);
        Assert.Equal((long)Campus2, b.CampusId);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Ho_sees_all_read_only()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Resolver(db, Ho()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        Assert.Equal(2, dto.CampusVisits.Count);
        Assert.True(dto.Viewer.CanViewAllCampuses);
        Assert.True(dto.Viewer.IsReadOnly);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Host_sees_only_hosted_instance()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // campus1 hosted by IcStaffC1, campus2 not.
        var (req, instances) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true, host0: IcStaffC1);
        var dto = await Resolver(db, Host(IcStaffC1, Campus1)).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        var c = Assert.Single(dto.CampusVisits);
        Assert.Equal(instances[0].VisitInstanceId, (ulong)c.VisitInstanceId);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Admin_and_unrelated_are_forbidden()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => Resolver(db, Admin()).ResolveAsync(req.VisitRequestId, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => Resolver(db, Unrelated()).ResolveAsync(req.VisitRequestId, CancellationToken.None));
        await tx.RollbackAsync();
    }

    // ── 5. allowedActions (mirror the command-handler authorization) ─────────

    [Fact]
    public async Task Owner_pending_request_offers_edit_and_safe_edit_not_amendment()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false); // instance WAITING_REQUEST_APPROVAL
        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        Assert.Contains(VisitFormActions.EditPendingRequest, dto.Viewer.AllowedActions);
        Assert.Contains(VisitFormActions.SubmitSafeEdit, dto.Viewer.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.ResubmitRejectedRequest, dto.Viewer.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.SubmitAmendment, dto.CampusVisits.Single().AllowedActions);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Owner_assigned_instance_offers_submit_amendment()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: IcStaffC1); // ASSIGNED, +20d
        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);

        var campus = dto.CampusVisits.Single();
        Assert.Contains(VisitFormActions.SubmitAmendment, campus.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.WithdrawAmendment, campus.AllowedActions); // no pending amendment
        Assert.Contains(VisitFormActions.SubmitSafeEdit, dto.Viewer.AllowedActions);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Pending_amendment_moves_owner_to_withdraw_and_leader_to_decide()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: IcStaffC1);
        await SeedPendingAmendmentAsync(db, req.VisitRequestId, instances[0].VisitInstanceId);

        var ownerCampus = (await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None))
            .CampusVisits.Single();
        Assert.Contains(VisitFormActions.WithdrawAmendment, ownerCampus.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.SubmitAmendment, ownerCampus.AllowedActions);  // one pending only
        Assert.DoesNotContain(VisitFormActions.ApproveAmendment, ownerCampus.AllowedActions); // owner never decides

        var leaderCampus = (await Resolver(db, StaffLeader(SlCampus1, Campus1)).ResolveAsync(req.VisitRequestId, CancellationToken.None))
            .CampusVisits.Single();
        Assert.Contains(VisitFormActions.ApproveAmendment, leaderCampus.AllowedActions);
        Assert.Contains(VisitFormActions.RejectAmendment, leaderCampus.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.WithdrawAmendment, leaderCampus.AllowedActions);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Leader_of_other_campus_cannot_decide_sibling_amendment()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1, Campus2 }, mixed: true, host0: IcStaffC1);
        await SeedPendingAmendmentAsync(db, req.VisitRequestId, instances[0].VisitInstanceId); // campus1

        // Leader of campus2 sees ONLY campus2 (campus1 hidden) → can never approve campus1's amendment.
        var dtoB = await Resolver(db, StaffLeader(SlCampus2, Campus2)).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        var only = Assert.Single(dtoB.CampusVisits);
        Assert.Equal((long)Campus2, only.CampusId);
        Assert.DoesNotContain(VisitFormActions.ApproveAmendment, only.AllowedActions);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Ho_gets_only_view_and_no_instance_actions()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2Async(db, new[] { Campus1 }, mixed: false, host0: IcStaffC1);
        await SeedPendingAmendmentAsync(db, req.VisitRequestId, instances[0].VisitInstanceId);

        var dto = await Resolver(db, Ho()).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.True(dto.Viewer.IsReadOnly);
        Assert.Equal(new[] { VisitFormActions.View }, dto.Viewer.AllowedActions);
        Assert.Empty(dto.CampusVisits.Single().AllowedActions);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Rejected_request_offers_resubmit_not_edit()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2Async(db, new[] { Campus1 }, mixed: false);
        var entity = await db.VisitRequests.FirstAsync(v => v.VisitRequestId == req.VisitRequestId);
        entity.Status = "REJECTED";
        await db.SaveChangesAsync();

        var dto = await Resolver(db, Owner()).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.Contains(VisitFormActions.ResubmitRejectedRequest, dto.Viewer.AllowedActions);
        Assert.DoesNotContain(VisitFormActions.EditPendingRequest, dto.Viewer.AllowedActions);
        await tx.RollbackAsync();
    }

    private static async Task SeedPendingAmendmentAsync(ApplicationDbContext db, ulong requestId, ulong instanceId)
    {
        db.VisitInstanceAmendments.Add(new VisitInstanceAmendment
        {
            VisitRequestId = requestId,
            VisitInstanceId = instanceId,
            AmendmentNo = 1,
            Status = AmendmentStatuses.PendingApproval,
            BaseFormRevision = 1,
            BaseApprovalRevision = 1,
            RequestedBy = VisitorOwner,
            RequestedAt = DateTime.Now,
            ExpectedInstanceRowVersion = 0,
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static async Task<(VisitRequest, List<VisitRequestCampus>)> SeedV1Async(
        ApplicationDbContext db, ulong[] campusIds)
    {
        var req = NewRequest(FormSchemaVersions.Legacy, campusIds.Length > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS");
        foreach (var cid in campusIds) req.CampusInstances.Add(NewInstance(cid));
        req.GuestMembers.Add(NewMember(0, "G1"));
        req.GuestMembers.Add(NewMember(0, "S1", "EXTERNAL_SUPPORT"));
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();
        return (req, req.CampusInstances.OrderBy(c => c.CampusId).ToList());
    }

    private static async Task<(VisitRequest, List<VisitRequestCampus>)> SeedV2Async(
        ApplicationDbContext db, ulong[] campusIds, bool mixed, ulong? host0 = null)
    {
        var req = NewRequest(FormSchemaVersions.PerCampus, campusIds.Length > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS", mixed);
        var tags = new[] { "A", "B", "C", "D", "E" };
        for (var i = 0; i < campusIds.Length; i++)
        {
            var inst = NewInstance(campusIds[i], i == 0 ? host0 : null);
            inst.FormDetail = NewDetail(tags[i]);
            req.CampusInstances.Add(inst);
        }
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();

        var ordered = req.CampusInstances.OrderBy(c => c.CampusId).ToList();

        // Per-campus members + links (one distinct member per campus so cross-leak is detectable).
        for (var i = 0; i < ordered.Count; i++)
        {
            var member = NewMember(req.VisitRequestId, $"{tags[i]}-guest");
            db.VisitGuestMembers.Add(member);
            await db.SaveChangesAsync();
            db.VisitInstanceGuestMembers.Add(new VisitInstanceGuestMember
            {
                VisitRequestId = req.VisitRequestId,
                VisitInstanceId = ordered[i].VisitInstanceId,
                GuestMemberId = member.GuestMemberId,
                DisplayOrder = 0, CreatedAt = DateTime.Now,
            });
        }
        await db.SaveChangesAsync();
        return (req, ordered);
    }
}

/// <summary>Mirror of the string relation constants used by the resolver, so tests need no
/// dependency on the internal VisitInstanceAccess type.</summary>
internal static class VisitInstanceAccessRelations
{
    public const string StaffLeader = "STAFF_LEADER";
    public const string Ho = "HO";
    public const string VisitorOwner = "VISITOR_OWNER";
    public const string Host = "HOST";
}
