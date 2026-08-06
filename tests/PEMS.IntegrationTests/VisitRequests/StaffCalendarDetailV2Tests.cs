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
using PEMS.Application.Dashboard.Queries.GetStaffCalendarDetail;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Dual-read migration tests for GetStaffCalendarDetailQueryHandler — an INSTANCE-LEVEL modal
/// (route <c>GET /api/dashboard/staff-calendar/{visitInstanceId}</c>, key = visit_instance_id). A MIXED
/// v2 request must return 200 with the TARGET instance's form content (never
/// FORM_VERSION_UPGRADE_REQUIRED, never a sibling campus). The DTO exposes a guest COUNT (not the member
/// list), so per-instance guest scoping is proven by the count. Runs against disposable
/// <c>pems_pr3_test</c>, each test in a rolled-back transaction.
/// </summary>
public sealed class StaffCalendarDetailV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Campus1 = 1, Campus2 = 2, Campus3 = 3;
    // Fabricated actor ids — the handler authorizes purely from ICurrentUserService (role/subrole/campus),
    // it never looks the current user up in the DB, so these need not be seeded rows.
    private const ulong StaffC1 = 901, StaffC2 = 902;

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

    private static FakeUser StaffMember(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff, PrimaryCampusId = campusId };
    private static FakeUser StaffLeader(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader, PrimaryCampusId = campusId };
    private static FakeUser Visitor() => new() { UserId = 8, RoleCode = RoleCodes.Visitor };

    private static GetStaffCalendarDetailQueryHandler Handler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new FixedClock(),
            new VisitFormReadService(db, user, NullLogger<VisitFormReadService>.Instance));

    private static Task<StaffCalendarDetailDto> Run(ApplicationDbContext db, ICurrentUserService user, ulong instanceId)
        => Handler(db, user).Handle(new GetStaffCalendarDetailQuery(instanceId), CancellationToken.None);

    // ── Tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// DECISION-01. The request row still carries a PRIMARY contact — a request-level relation — and each
    /// campus detail carries its own OPERATIONAL contact. This surface must show the operational one and
    /// must never reach past a detail to the primary contact.
    ///
    /// Replaces the former V1 test: with the global form columns dropped there is no V1 read path left,
    /// and the primary contact is now the only request-level value a contact field could wrongly fall back
    /// to. The secondary form fields are asserted here too, because they were the other half of the old
    /// global projection and must now come from the detail as well.
    /// </summary>
    [Fact]
    public async Task Contact_and_form_fields_come_from_the_detail_never_the_request_row()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, StaffLeader(StaffC1, Campus1), inst[0].VisitInstanceId);

        Assert.Equal("Op-A", dto.OperationalContactFullName);
        Assert.Equal("op-a@example.com", dto.OperationalContactEmail);
        Assert.NotEqual("Primary Contact", dto.OperationalContactFullName);
        Assert.NotEqual("contact@example.com", dto.OperationalContactEmail);

        // Everything the old global projection used to supply now comes from the campus detail.
        Assert.Equal("MEETING", dto.VisitType);
        Assert.Equal("EN", dto.WorkingLanguage);
        Assert.Equal("AGREED", dto.MediaConsentStatus);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_single_reads_target_detail()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, StaffLeader(StaffC1, Campus1), inst[0].VisitInstanceId);

        Assert.Equal("V2-DELEG", dto.DelegationName);
        Assert.NotEqual("GLOBAL-DELEG", dto.DelegationName);
        Assert.Equal("Op-A", dto.OperationalContactFullName);            // per-campus operational contact
        Assert.Equal("op-a@example.com", dto.OperationalContactEmail);
        Assert.Equal(1, dto.GuestCount);                            // only this instance's linked member
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_nonmixed_returns_200()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: false);
        var dto = await Run(db, StaffMember(StaffC1, Campus1), inst[0].VisitInstanceId);

        Assert.Equal("V2-DELEG", dto.DelegationName);               // shared v2 content, 200 (not 409)
        Assert.Equal(1, dto.GuestCount);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_mixed_target_A_returns_200_with_A_only()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, StaffMember(StaffC1, Campus1), inst[0].VisitInstanceId); // campus A

        Assert.Equal("DELEG-A", dto.DelegationName);                // 200 with target A (NOT 409-upgrade)
        Assert.Equal("PURPOSE-A", dto.Purpose);
        Assert.Equal("Op-A", dto.OperationalContactFullName);
        Assert.Equal("op-a@example.com", dto.OperationalContactEmail);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_mixed_target_B_returns_200_with_B_only()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, StaffMember(StaffC2, Campus2), inst[1].VisitInstanceId); // campus B, SAME request

        Assert.Equal("DELEG-B", dto.DelegationName);                // 200 with target B
        Assert.Equal("PURPOSE-B", dto.Purpose);
        Assert.Equal("Op-B", dto.OperationalContactFullName);
        Assert.Equal("op-b@example.com", dto.OperationalContactEmail);
        Assert.NotEqual("DELEG-A", dto.DelegationName);             // no sibling A leak
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_guest_count_is_per_instance_no_cross_leak()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        // Seed links 1 member each; give campus B a second linked member so the counts differ.
        await LinkExtraMember(db, req.VisitRequestId, inst[1].VisitInstanceId, "B-guest-2");

        var a = await Run(db, StaffMember(StaffC1, Campus1), inst[0].VisitInstanceId);
        var b = await Run(db, StaffMember(StaffC2, Campus2), inst[1].VisitInstanceId);

        Assert.Equal(1, a.GuestCount);   // only campus A's linked member
        Assert.Equal(2, b.GuestCount);   // only campus B's linked members
        Assert.NotEqual(3, a.GuestCount); // never the request-wide total
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_missing_detail_throws_no_fallback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == inst[0].VisitInstanceId);
        db.VisitInstanceFormDetails.Remove(detail);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => Run(db, StaffLeader(StaffC1, Campus1), inst[0].VisitInstanceId));
        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Hidden_sibling_campus_and_non_staff_are_forbidden()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);

        // Staff of campus 1 cannot open the campus-2 instance (hidden sibling), even by direct id.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => Run(db, StaffMember(StaffC1, Campus1), inst[1].VisitInstanceId));
        // …but can open their own campus-1 instance.
        var ok = await Run(db, StaffMember(StaffC1, Campus1), inst[0].VisitInstanceId);
        Assert.Equal("DELEG-A", ok.DelegationName);
        // A non-staff role is rejected outright.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => Run(db, Visitor(), inst[0].VisitInstanceId));
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
            var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
            c1.Count = 0;
            await Run(db, StaffMember(StaffC1, Campus1), inst[0].VisitInstanceId);
            small = c1.Count;
            await tx.RollbackAsync();
        }
        var c3 = new CommandCounter();
        using (var db = NewContext(c3))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2, Campus3 }, mixed: true);
            c3.Count = 0;
            await Run(db, StaffMember(StaffC1, Campus1), inst[0].VisitInstanceId);
            large = c3.Count;
            await tx.RollbackAsync();
        }
        Assert.Equal(small, large);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static VisitRequest NewRequest(byte schemaVersion, string scope, bool mixed) => new()
    {
        RequestCode = "SCD-" + Guid.NewGuid().ToString("N")[..12],
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
        FormRevision = 1, ApprovalRevision = 1, CreatedAt = DateTime.Now,
    };

    private static VisitGuestMember NewMember(ulong requestId, string name, string type = "GUEST") => new()
    {
        VisitRequestId = requestId, MemberType = type, FullName = name,
        Organization = "GOrg", JobTitle = "GJob", Nationality = "VN", CreatedAt = DateTime.Now,
    };

    private static async Task LinkExtraMember(ApplicationDbContext db, ulong requestId, ulong instanceId, string name)
    {
        var member = NewMember(requestId, name);
        db.VisitGuestMembers.Add(member);
        await db.SaveChangesAsync();
        db.VisitInstanceGuestMembers.Add(new VisitInstanceGuestMember
        {
            VisitRequestId = requestId,
            VisitInstanceId = instanceId,
            GuestMemberId = member.GuestMemberId,
            DisplayOrder = 1, CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<(VisitRequest req, List<VisitRequestCampus> instances)> Seed(
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
        if (!isV2)
        {
            req.GuestMembers.Add(NewMember(0, "G1"));
            req.GuestMembers.Add(NewMember(0, "S1", "EXTERNAL_SUPPORT"));
        }
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();

        var ordered = req.CampusInstances.OrderBy(c => c.CampusId).ToList();
        if (isV2)
        {
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
        }
        return (req, ordered);
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => DateTime.Now;
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
