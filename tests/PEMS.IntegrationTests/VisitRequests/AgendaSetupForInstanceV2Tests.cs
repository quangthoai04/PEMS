using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.AgendaTemplates.Queries.GetAgendaSetupForInstance;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Dual-read migration tests for <c>GetAgendaSetupForInstanceQueryHandler</c> — the host/staff-leader/HO agenda
/// setup screen (key = visit_instance_id). Its only submitted-form field is <c>visit_type</c> (it drives the
/// default template + template ordering). This is INSTANCE-LEVEL: a MIXED v2 request returns 200 with THIS
/// instance's per-campus visit_type, never the global field and never a sibling. Runs against disposable
/// <c>pems_pr3_test</c>, each test in a rolled-back transaction.
/// </summary>
public sealed class AgendaSetupForInstanceV2Tests
{
    private const string ConnString =
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None";

    private const ulong Campus1 = 1, Campus2 = 2, Campus3 = 3;
    private const string GlobalType = "MEETING";
    private const string V2NonMixedType = "WORKSHOP";
    // Distinct valid visit types per campus for the mixed case (avoid OTHER — it needs visit_type_other).
    private static readonly string[] TypeByTag = { "CAMPUS_TOUR", "SIGNING_CEREMONY", "EXCHANGE", "WORKSHOP", "MEETING" };

    private static bool? _dbUp;

    private static ApplicationDbContext NewContext(CommandCounter? counter = null)
    {
        var b = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString));
        if (counter is not null) b.AddInterceptors(counter);
        return new ApplicationDbContext(b.Options);
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

    // HO passes authorization for any instance — sufficient to exercise the visit_type dual-read.
    private static FakeUser Ho() => new() { UserId = 500, RoleCode = RoleCodes.Ho };
    private static FakeUser Visitor() => new() { UserId = 8, RoleCode = RoleCodes.Visitor };

    private static GetAgendaSetupForInstanceQueryHandler Handler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new VisitFormReadService(db, user, NullLogger<VisitFormReadService>.Instance));

    private static Task<GetAgendaSetupForInstanceDto> Run(ApplicationDbContext db, ICurrentUserService user, ulong instanceId)
        => Handler(db, user).Handle(new GetAgendaSetupForInstanceQuery(instanceId), CancellationToken.None);

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task V1_returns_global_visit_type_byte_identical()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.Legacy, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, Ho(), inst[0]);

        Assert.Equal(GlobalType, dto.VisitType);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_single_reads_target_visit_type()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, Ho(), inst[0]);

        Assert.Equal(V2NonMixedType, dto.VisitType);
        Assert.NotEqual(GlobalType, dto.VisitType);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_nonmixed_returns_200()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: false);
        var dto = await Run(db, Ho(), inst[0]);

        Assert.Equal(V2NonMixedType, dto.VisitType);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_mixed_target_A_returns_200_with_A_type()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, Ho(), inst[0]); // campus A

        Assert.Equal(TypeByTag[0], dto.VisitType); // CAMPUS_TOUR
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_mixed_target_B_returns_200_with_B_type()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, Ho(), inst[1]); // campus B, SAME request

        Assert.Equal(TypeByTag[1], dto.VisitType);    // SIGNING_CEREMONY
        Assert.NotEqual(TypeByTag[0], dto.VisitType);  // no sibling A leak
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_missing_detail_throws_no_fallback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == inst[0]);
        db.VisitInstanceFormDetails.Remove(detail);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(db, Ho(), inst[0]));
        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Unauthorized_role_is_forbidden()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        // A plain visitor is neither host, staff-leader-of-campus, nor HO → forbidden (before any projection).
        await Assert.ThrowsAsync<ForbiddenException>(() => Run(db, Visitor(), inst[0]));
        // …HO succeeds.
        var ok = await Run(db, Ho(), inst[0]);
        Assert.Equal(V2NonMixedType, ok.VisitType);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Query_count_constant_regardless_of_campus_count()
    {
        RequireDb();

        // Both cases target Campus1 with the SAME visit type (non-mixed → V2NonMixedType), so the only variable
        // is how many OTHER campus instances the request carries. AgendaDefaultResolver's query count depends on
        // the visit_type/templates (not on my dual-read), so it must be held constant to isolate per-campus N+1.
        int small, large;
        var c1 = new CommandCounter();
        using (var db = NewContext(c1))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
            c1.Count = 0;
            await Run(db, Ho(), inst[0]);
            small = c1.Count;
            await tx.RollbackAsync();
        }
        var c3 = new CommandCounter();
        using (var db = NewContext(c3))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2, Campus3 }, mixed: false);
            c3.Count = 0;
            await Run(db, Ho(), inst[0]);
            large = c3.Count;
            await tx.RollbackAsync();
        }
        Assert.Equal(small, large);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static VisitRequest NewRequest(byte schemaVersion, string scope, bool mixed) => new()
    {
        RequestCode = "AGS-" + Guid.NewGuid().ToString("N")[..12],
        VisitorUserId = 8,
        RegistrantUserId = 8,
        CreatedSource = "VISITOR_SUBMITTED",
        FormSchemaVersion = schemaVersion,
        HasMixedCampusDetails = mixed,
        RegistrantFullName = "Reg", RegistrantOrganization = "Org", RegistrantJobTitle = "Job",
        RegistrantPhone = "+8490", RegistrantEmail = "reg@example.com", RegistrantNationality = "VN",
        DelegationName = "GLOBAL-DELEG", VisitScope = scope, VisitType = GlobalType,
        Purpose = "GLOBAL-PURPOSE", WorkingContent = "GLOBAL-CONTENT",
        ContactPersonFullName = "Primary Contact", ContactPersonOrganization = "COrg",
        ContactPersonPhone = "+8491", ContactPersonEmail = "contact@example.com",
        WorkingLanguage = "EN", MediaConsentStatus = "DECLINED",
        PrimaryContactAccessStatus = "ACTIVE", PrimaryContactVerifiedAt = DateTime.Now,
        Status = "PENDING_APPROVAL", SubmittedAt = DateTime.Now, CreatedAt = DateTime.Now,
    };

    private static VisitRequestCampus NewInstance(ulong campusId) => new()
    {
        CampusId = campusId,
        PlannedStartAt = DateTime.Now.AddDays(20),
        PlannedEndAt = DateTime.Now.AddDays(20).AddHours(2),
        Status = "WAITING_REQUEST_APPROVAL",
        CreatedAt = DateTime.Now,
    };

    private static VisitInstanceFormDetail NewDetail(int index, bool perCampus) => new()
    {
        DelegationName = perCampus ? $"DELEG-{TypeByTag[index]}" : "V2-DELEG",
        VisitType = perCampus ? TypeByTag[index] : V2NonMixedType,
        Purpose = "V2-PURPOSE",
        WorkingContent = "V2-CONTENT",
        OperationalContactFullName = "Op", OperationalContactOrganization = "OpOrg",
        OperationalContactPhone = "+8410", OperationalContactEmail = "op@example.com",
        WorkingLanguage = "EN", MediaConsentStatus = "AGREED",
        FormRevision = 1, ApprovalRevision = 1, CreatedAt = DateTime.Now,
    };

    /// <summary>Seeds one request with N campus instances (v2 → a per-campus detail each). Returns instance ids
    /// ordered by campus id (A, B, C…).</summary>
    private static async Task<(VisitRequest req, List<ulong> instanceIds)> Seed(
        ApplicationDbContext db, byte schemaVersion, ulong[] campusIds, bool mixed)
    {
        var req = NewRequest(schemaVersion, campusIds.Length > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS", mixed);
        var isV2 = schemaVersion >= FormSchemaVersions.PerCampus;
        for (var i = 0; i < campusIds.Length; i++)
        {
            var inst = NewInstance(campusIds[i]);
            if (isV2) inst.FormDetail = NewDetail(i, perCampus: mixed);
            req.CampusInstances.Add(inst);
        }
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();

        var ordered = req.CampusInstances.OrderBy(c => c.CampusId).Select(c => c.VisitInstanceId).ToList();
        return (req, ordered);
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count;
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        { Count++; return base.ReaderExecuting(command, eventData, result); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        { Count++; return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }
    }
}
