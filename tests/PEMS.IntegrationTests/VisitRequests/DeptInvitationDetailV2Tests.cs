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
using PEMS.Application.DepartmentReceptionTasks.Queries.GetInvitationDetail;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Dual-read migration tests for <c>GetInvitationDetailQueryHandler</c> (DepartmentReceptionTasks) — the
/// department invitation detail (route <c>GET .../invitation-detail/{participantId}</c>, key = participant_id).
/// A participant is bound to exactly ONE campus instance, so this is INSTANCE-LEVEL: a MIXED v2 request returns
/// 200 with the TARGET instance's form content (delegation / purpose / working-content / operational contact),
/// never a sibling campus and never FORM_VERSION_UPGRADE_REQUIRED. Registrant identity fields stay
/// request-level. Runs against disposable <c>pems_pr3_test</c>, each test in a rolled-back transaction.
/// </summary>
public sealed class DeptInvitationDetailV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Campus1 = 1, Campus2 = 2, Campus3 = 3;
    private const ulong ParticipantUser = 8;

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

    // The handler injects ICurrentUserService but does not read it; only VisitFormReadService needs one.
    private sealed class FakeUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId => ParticipantUser;
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode => RoleCodes.Visitor;
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static GetInvitationDetailQueryHandler Handler(ApplicationDbContext db)
        => new(db, new FakeUser(), new VisitFormReadService(db, new FakeUser(), NullLogger<VisitFormReadService>.Instance));

    private static Task<InvitationDetailDto> Run(ApplicationDbContext db, ulong participantId)
        => Handler(db).Handle(new GetInvitationDetailQuery { ParticipantId = participantId }, CancellationToken.None);

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

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, parts[0]);

        Assert.Equal("Op-A", dto.OperationalContactFullName);
        Assert.Equal("+8410", dto.OperationalContactPhone);
        Assert.NotEqual("Primary Contact", dto.OperationalContactFullName);
        Assert.NotEqual("+8491", dto.OperationalContactPhone);

        // Registrant identity is genuinely request-level and must still come through unchanged.
        Assert.Equal("Reg", dto.RegistrantFullName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_single_reads_target_detail()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, parts[0]);

        Assert.Equal("V2-DELEG", dto.DelegationName);
        Assert.NotEqual("GLOBAL-DELEG", dto.DelegationName);
        Assert.Equal("Op-A", dto.OperationalContactFullName);
        Assert.Equal("+8410", dto.OperationalContactPhone);
        Assert.Equal("Reg", dto.RegistrantFullName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_nonmixed_returns_200()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: false);
        var dto = await Run(db, parts[0]);

        Assert.Equal("V2-DELEG", dto.DelegationName);
        Assert.Equal("Op-A", dto.OperationalContactFullName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_mixed_target_A_returns_200_with_A_only()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, parts[0]); // participant on campus A

        Assert.Equal("DELEG-A", dto.DelegationName);
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

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, parts[1]); // participant on campus B, SAME request

        Assert.Equal("DELEG-B", dto.DelegationName);
        Assert.Equal("PURPOSE-B", dto.Purpose);
        Assert.Equal("Op-B", dto.OperationalContactFullName);
        Assert.NotEqual("DELEG-A", dto.DelegationName); // no sibling A leak
        Assert.NotEqual("Op-A", dto.OperationalContactFullName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_missing_detail_throws_no_fallback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var instanceId = await db.VisitParticipants.Where(p => p.ParticipantId == parts[0])
            .Select(p => p.VisitInstanceId).FirstAsync();
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == instanceId);
        db.VisitInstanceFormDetails.Remove(detail);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(db, parts[0]));
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
            var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
            c1.Count = 0;
            await Run(db, parts[0]);
            small = c1.Count;
            await tx.RollbackAsync();
        }
        var c3 = new CommandCounter();
        using (var db = NewContext(c3))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2, Campus3 }, mixed: true);
            c3.Count = 0;
            await Run(db, parts[0]);
            large = c3.Count;
            await tx.RollbackAsync();
        }
        Assert.Equal(small, large);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static VisitRequest NewRequest(byte schemaVersion, string scope, bool mixed) => new()
    {
        RequestCode = "INV-" + Guid.NewGuid().ToString("N")[..12],
        RegistrantUserId = ParticipantUser,
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
        OperationalContactUserId = ParticipantUser,
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
        FormRevision = 1, ApprovalRevision = 1, CreatedAt = DateTime.Now,
    };

    private static VisitParticipant NewParticipant(ulong instanceId) => new()
    {
        VisitInstanceId = instanceId,
        UserId = ParticipantUser,
        ParticipantRole = "IC_SUPPORT",
        IsHost = false,
        Status = "INVITED",
        InvitedAt = DateTime.Now,
        CreatedAt = DateTime.Now,
    };

    /// <summary>Seeds one request with N campus instances (v2 → a per-campus detail each), plus one participant
    /// per instance. Returns the request and the participant ids ordered by campus id (A, B, C…).</summary>
    private static async Task<(VisitRequest req, List<ulong> participantIds)> Seed(
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

        var ordered = req.CampusInstances.OrderBy(c => c.CampusId).ToList();
        var partIds = new List<ulong>();
        foreach (var inst in ordered)
        {
            var part = NewParticipant(inst.VisitInstanceId);
            db.VisitParticipants.Add(part);
            await db.SaveChangesAsync();
            partIds.Add(part.ParticipantId);
        }
        return (req, partIds);
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
