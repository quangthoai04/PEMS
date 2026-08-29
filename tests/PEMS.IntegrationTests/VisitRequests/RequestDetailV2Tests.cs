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
using PEMS.Application.DepartmentReceptionTasks.Queries.GetRequestDetail;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Dual-read migration tests for <c>GetRequestDetailQueryHandler</c> — the department reception-task detail
/// (route <c>GET .../request-detail/{logisticsItemId}</c>, key = logistics_item_id). A logistics item belongs
/// to exactly ONE campus instance, so this is INSTANCE-LEVEL: a MIXED v2 request returns 200 with the TARGET
/// instance's form content (delegation / purpose / working-content / operational contact), never a sibling
/// campus and never FORM_VERSION_UPGRADE_REQUIRED. Registrant identity fields stay request-level in both
/// versions. Runs against disposable <c>pems_pr3_test</c>, each test in a rolled-back transaction.
/// </summary>
public sealed class RequestDetailV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Campus1 = 1, Campus2 = 2, Campus3 = 3;

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

    // DB-AUTHZ-001: the handler now enforces department scope (see GetRequestDetailQueryHandler), so
    // this fake must carry a DepartmentId, and every seeded logistics item must be routed to the same
    // one (see NewLogisticsItem) — otherwise every call in this file would be refused before it ever
    // reached the V2 read-path behaviour these tests exist to verify. VisitFormReadService itself still
    // never reads ICurrentUserService. requested_to_department_id has a real FK to departments, so this
    // has to be an id that actually exists — resolved once per run and cached, same as _dbUp above.
    private static ulong? _fakeDepartmentId;

    private static async Task<ulong> ResolveFakeDepartmentIdAsync(ApplicationDbContext db)
    {
        _fakeDepartmentId ??= await db.Departments.AsNoTracking()
            .OrderBy(d => d.DepartmentId).Select(d => d.DepartmentId).FirstAsync();
        return _fakeDepartmentId.Value;
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId => 8;
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode => RoleCodes.Visitor;
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => _fakeDepartmentId;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static GetRequestDetailQueryHandler Handler(ApplicationDbContext db)
        => new(db, new VisitFormReadService(db, new FakeUser(), NullLogger<VisitFormReadService>.Instance), new FakeUser());

    private static Task<RequestDetailDto> Run(ApplicationDbContext db, ulong logisticsItemId)
        => Handler(db).Handle(new GetRequestDetailQuery { LogisticsItemId = logisticsItemId }, CancellationToken.None);

    // ── Tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// DECISION-01. The request row still carries a PRIMARY contact — a request-level relation — and each
    /// campus detail carries its own OPERATIONAL contact. This surface must show the operational one and
    /// must never reach past a detail to the primary contact.
    ///
    /// Replaces the former V1 test: with the global form columns dropped there is no V1 read path left,
    /// and the primary contact is now the only request-level value a contact field could wrongly fall back
    /// to. Seeding the two with different literals is what makes such a fallback visible.
    /// </summary>
    [Fact]
    public async Task Contact_is_the_campus_operational_contact_never_the_request_primary_contact()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, items) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, items[0]);

        Assert.Equal("Op-A", dto.OperationalContactFullName);
        Assert.Equal("+8410", dto.OperationalContactPhone);
        Assert.NotEqual("Primary Contact", dto.OperationalContactFullName);
        Assert.NotEqual("+8491", dto.OperationalContactPhone);

        // Registrant identity is genuinely request-level and must still come through unchanged — the rule
        // is "no fallback", not "ignore the request row".
        Assert.Equal("Reg", dto.RegistrantFullName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_single_reads_target_detail()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, items) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, items[0]);

        Assert.Equal("V2-DELEG", dto.DelegationName);
        Assert.NotEqual("GLOBAL-DELEG", dto.DelegationName);
        Assert.Equal("Op-A", dto.OperationalContactFullName);   // per-campus operational contact
        Assert.Equal("+8410", dto.OperationalContactPhone);
        Assert.Equal("Reg", dto.RegistrantFullName);       // registrant unchanged
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_nonmixed_returns_200()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, items) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: false);
        var dto = await Run(db, items[0]);

        Assert.Equal("V2-DELEG", dto.DelegationName);   // shared v2 content, 200 (not 409)
        Assert.Equal("Op-A", dto.OperationalContactFullName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_mixed_target_A_returns_200_with_A_only()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, items) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, items[0]); // logistics item on campus A

        Assert.Equal("DELEG-A", dto.DelegationName);   // 200 with target A (NOT 409-upgrade)
        Assert.Equal("PURPOSE-A", dto.Purpose);
        Assert.Equal("CONTENT-A", dto.WorkingContent);
        Assert.Equal("Op-A", dto.OperationalContactFullName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_mixed_target_B_returns_200_with_B_only()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, items) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, items[1]); // logistics item on campus B, SAME request

        Assert.Equal("DELEG-B", dto.DelegationName);   // 200 with target B
        Assert.Equal("PURPOSE-B", dto.Purpose);
        Assert.Equal("Op-B", dto.OperationalContactFullName);
        Assert.NotEqual("DELEG-A", dto.DelegationName); // no sibling A leak
        Assert.NotEqual("Op-A", dto.OperationalContactFullName);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// "Ghi chú gửi FPTU" reaches the department handling this item. The DTO carried Purpose and
    /// WorkingContent but no general note, so a department setting up a room never learned that the
    /// guest had asked for step-free access — the sentence existed and stopped at the contract.
    /// </summary>
    [Fact]
    public async Task NotesToFptu_comes_from_the_target_instance_detail()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, items) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, items[0]);

        Assert.Equal("V2-NOTE", dto.NotesToFptu);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// This DTO already carries five other things called a note — the item's Description, ProposalNote,
    /// ProposalResponseNote, BorrowNote and ReturnNote — and all of them belong to the logistics item,
    /// not to the guest. The guest's remark must never be filled from one of them, nor they from it.
    /// </summary>
    [Fact]
    public async Task NotesToFptu_is_never_one_of_the_logistics_item_notes()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, items) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, items[0]);

        Assert.Equal("V2-NOTE", dto.NotesToFptu);
        Assert.Equal("ITEM-DESCRIPTION", dto.Description);
        Assert.NotEqual(dto.Description, dto.NotesToFptu);
        // The item was seeded without any handover or proposal, so these stay empty — the guest's
        // note must not have been copied into them either.
        Assert.Null(dto.ProposalNote);
        Assert.Null(dto.BorrowNote);
        Assert.Null(dto.ReturnNote);
        await tx.RollbackAsync();
    }

    /// <summary>Blank is a real answer and the contract has to be able to express it.</summary>
    [Fact]
    public async Task NotesToFptu_is_empty_when_the_guest_wrote_nothing()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, items) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var instanceId = await db.VisitLogisticsItems.Where(i => i.LogisticsItemId == items[0])
            .Select(i => i.VisitInstanceId).FirstAsync();
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == instanceId);
        detail.Notes = null;
        await db.SaveChangesAsync();

        var dto = await Run(db, items[0]);

        Assert.Equal("", dto.NotesToFptu);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// MIXED multi-campus: the item is bound to one instance, so the department reading it must get
    /// that instance's note and never a sibling's.
    /// </summary>
    [Fact]
    public async Task NotesToFptu_never_leaks_across_campuses_of_a_mixed_request()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, items) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);

        var a = await Run(db, items[0]);
        var b = await Run(db, items[1]);

        Assert.Equal("NOTE-A", a.NotesToFptu);
        Assert.Equal("NOTE-B", b.NotesToFptu);
        Assert.NotEqual("NOTE-B", a.NotesToFptu);
        Assert.NotEqual("NOTE-A", b.NotesToFptu);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_missing_detail_throws_no_fallback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, items) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var instanceId = await db.VisitLogisticsItems.Where(l => l.LogisticsItemId == items[0])
            .Select(l => l.VisitInstanceId).FirstAsync();
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == instanceId);
        db.VisitInstanceFormDetails.Remove(detail);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(db, items[0]));
        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Query_count_constant_regardless_of_campus_count()
    {
        RequireDb();

        int small, large;
        var c1 = new CommandCounter();
        using (var db = NewContext(c1))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (_, items) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
            c1.Count = 0;
            await Run(db, items[0]);
            small = c1.Count;
            await tx.RollbackAsync();
        }
        var c3 = new CommandCounter();
        using (var db = NewContext(c3))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (_, items) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2, Campus3 }, mixed: true);
            c3.Count = 0;
            await Run(db, items[0]);
            large = c3.Count;
            await tx.RollbackAsync();
        }
        Assert.Equal(small, large);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static VisitRequest NewRequest(byte schemaVersion, string scope, bool mixed) => new()
    {
        RequestCode = "RQD-" + Guid.NewGuid().ToString("N")[..12],
        RegistrantUserId = 8,
        CreatedSource = "VISITOR_SUBMITTED",
        HasMixedCampusDetails = mixed,
        RegistrantFullName = "Reg", RegistrantOrganization = "Org", RegistrantJobTitle = "Job",
        RegistrantPhone = "+8490", RegistrantEmail = "reg@example.com", RegistrantNationality = "VN",
        VisitScope = scope,
        // Pure V2: form content is per campus (see the detail builder). The request row keeps only the
        // PRIMARY contact — a request-level relation, distinct from each campus's operational contact.
        Status = "PENDING_APPROVAL", SubmittedAt = DateTime.Now, CreatedAt = DateTime.Now,
    };

    private static VisitRequestCampus NewInstance(ulong campusId) => new()
    {
        CampusId = campusId,
        PlannedStartAt = DateTime.Now.AddDays(20),
        PlannedEndAt = DateTime.Now.AddDays(20).AddHours(2),
        Status = "WAITING_REQUEST_APPROVAL",
        // Self-matched: the registrant is this campus's operational contact, so the campus sits
        // past the confirmation gate. A campus beyond WAITING_CONTACT_CONFIRMATION with no
        // contact is refused by trg_visit_campuses_op_contact_guard_bi.
        OperationalContactUserId = 8,
        OperationalContactConfirmedAt = DateTime.Now,
        OperationalContactConfirmationSource = "REGISTRANT_SELF_MATCH",
        CreatedAt = DateTime.Now,
    };

    private static VisitInstanceFormDetail NewDetail(string tag, bool perCampus) => new()
    {
        DelegationName = perCampus ? $"DELEG-{tag}" : "V2-DELEG",
        VisitType = "MEETING",
        Purpose = perCampus ? $"PURPOSE-{tag}" : "V2-PURPOSE",
        WorkingContent = perCampus ? $"CONTENT-{tag}" : "V2-CONTENT",
        OperationalContactFullName = $"Op-{tag}", OperationalContactOrganization = $"OpOrg-{tag}", OperationalContactJobTitle = "Trưởng phòng Hợp tác",
        OperationalContactPhone = "+8410", OperationalContactEmail = $"op-{tag.ToLowerInvariant()}@example.com",
        WorkingLanguage = "EN", MediaConsentStatus = "AGREED",
        // "Ghi chú gửi FPTU" — the guest's remark about THIS campus. Tagged like the other per-campus
        // literals so a sibling-campus leak into NotesToFptu shows up as the wrong tag.
        Notes = perCampus ? $"NOTE-{tag}" : "V2-NOTE",
        FormRevision = 1, ApprovalRevision = 1, CreatedAt = DateTime.Now,
    };

    private static VisitLogisticsItem NewLogisticsItem(ulong instanceId, ulong departmentId) => new()
    {
        VisitInstanceId = instanceId,
        // Must match FakeUser.DepartmentId — GetRequestDetailQueryHandler now checks this (DB-AUTHZ-001).
        RequestedToDepartmentId = departmentId,
        ItemType = "ROOM",
        Title = "Phòng họp",
        // The ITEM's own free text. Distinct from every form literal: if this ever surfaces as
        // NotesToFptu, or the guest's note surfaces as Description, the assertions below catch it.
        Description = "ITEM-DESCRIPTION",
        Status = "REQUESTED",
        CoordinationMode = "SYSTEM_REQUEST",
        Quantity = 1,
        RequestedAt = DateTime.Now,
        CreatedAt = DateTime.Now,
    };

    /// <summary>Seeds one request with N campus instances (v2 → a per-campus detail each), plus one logistics
    /// item per instance. Returns the request and the logistics-item ids ordered by campus id (A, B, C…).</summary>
    private static async Task<(VisitRequest req, List<ulong> logisticsItemIds)> Seed(
        ApplicationDbContext db, byte schemaVersion, ulong[] campusIds, bool mixed)
    {
        var req = NewRequest(schemaVersion, campusIds.Length > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS", mixed);
        var isV2 = schemaVersion >= FormSchemaVersions.PerCampus;
        var tags = new[] { "A", "B", "C", "D", "E" };
        for (var i = 0; i < campusIds.Length; i++)
        {
            var inst = NewInstance(campusIds[i]);
            if (isV2) inst.FormDetail = NewDetail(tags[i], perCampus: mixed);
            req.CampusInstances.Add(inst);
        }
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();

        var departmentId = await ResolveFakeDepartmentIdAsync(db);
        var ordered = req.CampusInstances.OrderBy(c => c.CampusId).ToList();
        var itemIds = new List<ulong>();
        foreach (var inst in ordered)
        {
            var item = NewLogisticsItem(inst.VisitInstanceId, departmentId);
            db.VisitLogisticsItems.Add(item);
            await db.SaveChangesAsync();
            itemIds.Add(item.LogisticsItemId);
        }
        return (req, itemIds);
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
