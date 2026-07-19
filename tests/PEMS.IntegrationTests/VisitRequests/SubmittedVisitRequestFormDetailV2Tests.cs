using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.GetSubmittedVisitRequestFormDetail;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Characterization + dual-read migration tests for GetSubmittedVisitRequestFormDetailQueryHandler
/// (the flat, single-snapshot submitted-form endpoint). Runs against the DISPOSABLE MySQL database
/// <c>pems_pr3_test</c> (PR-2 fresh-create master) — never the real pems_db / pems_test. Each test
/// seeds inside a transaction it rolls back, so the DB stays clean.
///
/// Contract under migration (plan §6 / PR-3 follow-up):
///  • v1 (form_schema_version=1)      → flat response byte-for-byte unchanged (global projection).
///  • v2 non-mixed                    → flat form content DERIVED from visit_instance_form_details +
///                                       visit_instance_guest_members (NEVER the global fields).
///  • v2 mixed                        → 409 FORM_VERSION_UPGRADE_REQUIRED (flat DTO can't represent it).
///  • v2 visible instance w/o detail  → 409 VISIT_FORM_DETAIL_MISSING (no silent global fallback).
///  • scope applied before projection → hidden campus never in Campuses[] / CampusDecisionSummary.
/// </summary>
public sealed class SubmittedVisitRequestFormDetailV2Tests
{
    private const string ConnString =
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None";

    private const ulong VisitorOwner = 8, VisitorOther = 22, SlCampus1 = 3, SlCampus2 = 9,
                        IcStaffC1 = 4, HoUser = 2, AdminUser = 1;
    private const ulong Campus1 = 1, Campus2 = 2;

    private static bool? _dbUp;

    private static ApplicationDbContext NewContext(CommandCounter? counter = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString));
        if (counter is not null) builder.AddInterceptors(counter);
        return new ApplicationDbContext(builder.Options);
    }

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master into it to run these tests.");
    }

    private static GetSubmittedVisitRequestFormDetailQueryHandler Handler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, NullLogger<GetSubmittedVisitRequestFormDetailQueryHandler>.Instance,
            new VisitFormReadService(db, user, NullLogger<VisitFormReadService>.Instance));

    private static Task<SubmittedVisitRequestFormDetailDto> Run(ApplicationDbContext db, ICurrentUserService user, ulong reqId)
        => Handler(db, user).Handle(new GetSubmittedVisitRequestFormDetailQuery(reqId), CancellationToken.None);

    // ── Fixture builders (proven to insert against the real PR-2 schema in PerCampusFormV2ReadTests) ──

    private static VisitRequest NewRequest(byte schemaVersion, string scope, bool mixed) => new()
    {
        RequestCode = "SFD-" + Guid.NewGuid().ToString("N")[..12],
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

    // perCampus=false → identical content for every campus (a non-mixed v2 request).
    private static VisitInstanceFormDetail NewDetail(string tag, bool perCampus) => new()
    {
        DelegationName = perCampus ? $"DELEG-{tag}" : "V2-DELEG",
        VisitType = "MEETING",
        Purpose = perCampus ? $"PURPOSE-{tag}" : "V2-PURPOSE",
        WorkingContent = perCampus ? $"CONTENT-{tag}" : "V2-CONTENT",
        OperationalContactFullName = $"Op-{tag}", OperationalContactOrganization = $"OpOrg-{tag}",
        OperationalContactPhone = "+8410", OperationalContactEmail = $"op-{tag}@example.com",
        WorkingLanguage = perCampus && tag == "B" ? "VI" : "EN", MediaConsentStatus = "AGREED",
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

    // ── 1. v1 compatibility — the flat global projection is unchanged ─────────

    [Fact]
    public async Task V1_single_campus_returns_global_flat_snapshot()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var req = await SeedV1(db, new[] { Campus1 });
        var dto = await Run(db, Owner(), req.VisitRequestId);

        Assert.Equal(1, dto.FormSchemaVersion); // exposed so a shared detail surface stays on v1 UI
        Assert.Equal("GLOBAL-DELEG", dto.DelegationName);
        Assert.Equal("GLOBAL-PURPOSE", dto.Purpose);
        Assert.Equal("GLOBAL-CONTENT", dto.WorkingContent);
        Assert.Contains(dto.GuestMembers, m => m.FullName == "G1");
        Assert.Contains(dto.ExternalSupportMembers, m => m.FullName == "S1");
        var c = Assert.Single(dto.Campuses);
        Assert.Equal((long)Campus1, c.CampusId);
        Assert.Equal(1, dto.CampusDecisionSummary.Total);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V1_multi_campus_summary_counts_all_campuses_unchanged()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var req = await SeedV1(db, new[] { Campus1, Campus2 });
        var dto = await Run(db, Owner(), req.VisitRequestId);

        Assert.Equal("GLOBAL-DELEG", dto.DelegationName);
        Assert.Equal(2, dto.Campuses.Count);
        Assert.Equal(2, dto.CampusDecisionSummary.Total); // v1 rollup over the whole request (unchanged)
        await tx.RollbackAsync();
    }

    // ── 2. v2 non-mixed — flat content DERIVED from per-campus detail, not global ──

    [Fact]
    public async Task V2_single_campus_derives_flat_from_detail_not_global()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2(db, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, Owner(), req.VisitRequestId);

        // A uniform v2 request LOOKS flat but must drive the v2 UI — the version says so, not the scope.
        Assert.Equal(2, dto.FormSchemaVersion);
        Assert.Equal("V2-DELEG", dto.DelegationName);      // per-campus detail
        Assert.NotEqual("GLOBAL-DELEG", dto.DelegationName); // never the global field
        Assert.Equal("V2-PURPOSE", dto.Purpose);
        Assert.Contains(dto.GuestMembers, m => m.FullName == "A-guest0"); // per-campus link, not request-level
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_nonmixed_derives_flat_from_detail()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2(db, new[] { Campus1, Campus2 }, mixed: false);
        var dto = await Run(db, Owner(), req.VisitRequestId);

        Assert.Equal("V2-DELEG", dto.DelegationName);
        Assert.Equal("V2-PURPOSE", dto.Purpose);
        Assert.Equal(2, dto.Campuses.Count);
        Assert.Equal(2, dto.CampusDecisionSummary.Total);
        await tx.RollbackAsync();
    }

    // ── 3. v2 mixed — flat endpoint returns a stable upgrade-required 409 ─────

    [Fact]
    public async Task V2_multi_mixed_flat_endpoint_throws_upgrade_required()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2(db, new[] { Campus1, Campus2 }, mixed: true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(db, Owner(), req.VisitRequestId));
        Assert.Equal(VisitFormV2ErrorCodes.FormVersionUpgradeRequired, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_mixed_resolver_v2_endpoint_returns_percampus_campusvisits()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // The canonical v2 read path (IVisitFormReadService.ResolveAsync, served by /api/v2/visit-requests/{id})
        // DOES represent mixed data — each campus keeps its own content. This is what clients use after the 409.
        var (req, _) = await SeedV2(db, new[] { Campus1, Campus2 }, mixed: true);
        var resolved = await new VisitFormReadService(db, Owner(), NullLogger<VisitFormReadService>.Instance)
            .ResolveAsync(req.VisitRequestId, CancellationToken.None);

        var a = resolved.CampusVisits.Single(c => c.CampusId == (long)Campus1);
        var b = resolved.CampusVisits.Single(c => c.CampusId == (long)Campus2);
        Assert.Equal("DELEG-A", a.DelegationName);
        Assert.Equal("DELEG-B", b.DelegationName);
        await tx.RollbackAsync();
    }

    // ── 4. v2 missing detail — 409, never a silent global fallback ────────────

    [Fact]
    public async Task V2_missing_detail_throws_no_global_fallback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2(db, new[] { Campus1 }, mixed: false);
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == instances[0].VisitInstanceId);
        db.VisitInstanceFormDetails.Remove(detail);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(db, Owner(), req.VisitRequestId));
        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    // ── 5. Scope applied before projection — hidden campus never leaks ────────

    [Fact]
    public async Task StaffLeader_v2_sees_only_own_campus_no_aggregate_or_member_leak()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // Non-mixed FORM content, but a DISTINCT member per campus, so a cross-campus member leak is detectable.
        var (req, _) = await SeedV2(db, new[] { Campus1, Campus2 }, mixed: false);

        var dtoA = await Run(db, StaffLeader(SlCampus1, Campus1), req.VisitRequestId);
        var a = Assert.Single(dtoA.Campuses);
        Assert.Equal((long)Campus1, a.CampusId);
        Assert.DoesNotContain(dtoA.Campuses, c => c.CampusId == (long)Campus2); // hidden campus absent
        Assert.Equal(1, dtoA.CampusDecisionSummary.Total);                       // no aggregate leak (not 2)
        Assert.Contains(dtoA.GuestMembers, m => m.FullName == "A-guest0");
        Assert.DoesNotContain(dtoA.GuestMembers, m => m.FullName == "B-guest0");  // no cross-campus member leak

        var dtoB = await Run(db, StaffLeader(SlCampus2, Campus2), req.VisitRequestId);
        var b = Assert.Single(dtoB.Campuses);
        Assert.Equal((long)Campus2, b.CampusId);
        Assert.Contains(dtoB.GuestMembers, m => m.FullName == "B-guest0");
        Assert.DoesNotContain(dtoB.GuestMembers, m => m.FullName == "A-guest0");
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Ho_v2_sees_all_campuses()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2(db, new[] { Campus1, Campus2 }, mixed: false);
        var dto = await Run(db, Ho(), req.VisitRequestId);

        Assert.Equal(2, dto.Campuses.Count);
        Assert.Equal(2, dto.CampusDecisionSummary.Total);
        Assert.Equal("V2-DELEG", dto.DelegationName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Host_v2_sees_only_hosted_instance()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedV2(db, new[] { Campus1, Campus2 }, mixed: false, host0: IcStaffC1);
        var dto = await Run(db, Host(IcStaffC1, Campus1), req.VisitRequestId);

        var c = Assert.Single(dto.Campuses);
        Assert.Equal(instances[0].VisitInstanceId, (ulong)c.VisitInstanceId);
        Assert.Equal(1, dto.CampusDecisionSummary.Total);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Admin_and_unrelated_v2_are_forbidden()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedV2(db, new[] { Campus1 }, mixed: false);

        await Assert.ThrowsAsync<ForbiddenException>(() => Run(db, Admin(), req.VisitRequestId));
        await Assert.ThrowsAsync<ForbiddenException>(() => Run(db, Unrelated(), req.VisitRequestId));
        await tx.RollbackAsync();
    }

    // ── 6. No per-campus / per-member N+1 (constant DB command count) ─────────

    [Fact]
    public async Task V2_flat_handler_issues_constant_query_count_no_n_plus_1()
    {
        RequireDb();

        int small, large;
        var counterSmall = new CommandCounter();
        using (var db = NewContext(counterSmall))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (req, _) = await SeedV2(db, new[] { Campus1 }, mixed: false, membersPerCampus: 1);
            counterSmall.Count = 0;
            await Run(db, Owner(), req.VisitRequestId);
            small = counterSmall.Count;
            await tx.RollbackAsync();
        }

        var counterLarge = new CommandCounter();
        using (var db = NewContext(counterLarge))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (req, _) = await SeedV2(db, new[] { Campus1, Campus2 }, mixed: false, membersPerCampus: 3);
            counterLarge.Count = 0;
            await Run(db, Owner(), req.VisitRequestId);
            large = counterLarge.Count;
            await tx.RollbackAsync();
        }

        // 2 campuses × 3 members must cost the SAME number of DB commands as 1 campus × 1 member.
        Assert.Equal(small, large);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static async Task<VisitRequest> SeedV1(ApplicationDbContext db, ulong[] campusIds)
    {
        var req = NewRequest(FormSchemaVersions.Legacy, campusIds.Length > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS", false);
        foreach (var cid in campusIds) req.CampusInstances.Add(NewInstance(cid));
        req.GuestMembers.Add(NewMember(0, "G1"));
        req.GuestMembers.Add(NewMember(0, "S1", "EXTERNAL_SUPPORT"));
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();
        return req;
    }

    private static async Task<(VisitRequest req, List<VisitRequestCampus> instances)> SeedV2(
        ApplicationDbContext db, ulong[] campusIds, bool mixed, ulong? host0 = null, int membersPerCampus = 1)
    {
        var req = NewRequest(FormSchemaVersions.PerCampus, campusIds.Length > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS", mixed);
        var tags = new[] { "A", "B", "C", "D", "E" };
        for (var i = 0; i < campusIds.Length; i++)
        {
            var inst = NewInstance(campusIds[i], i == 0 ? host0 : null);
            inst.FormDetail = NewDetail(tags[i], perCampus: mixed);
            req.CampusInstances.Add(inst);
        }
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();

        var ordered = req.CampusInstances.OrderBy(c => c.CampusId).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            for (var j = 0; j < membersPerCampus; j++)
            {
                var member = NewMember(req.VisitRequestId, $"{tags[i]}-guest{j}");
                db.VisitGuestMembers.Add(member);
                await db.SaveChangesAsync();
                db.VisitInstanceGuestMembers.Add(new VisitInstanceGuestMember
                {
                    VisitRequestId = req.VisitRequestId,
                    VisitInstanceId = ordered[i].VisitInstanceId,
                    GuestMemberId = member.GuestMemberId,
                    DisplayOrder = (uint)j, CreatedAt = DateTime.Now,
                });
            }
        }
        await db.SaveChangesAsync();
        return (req, ordered);
    }

    /// <summary>Counts every DB command executed on the context — used to assert no per-campus N+1.</summary>
    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Count++;
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
