using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using PEMS.Shared;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// DB-TXN-009: <c>VisitExpenseReport.RowVersion</c> (and <c>VisitExpenseItem.RowVersion</c>) were plain
/// <c>int</c> columns, manually bumped (<c>report.RowVersion++</c>) but never configured as EF
/// concurrency tokens — so the generated <c>UPDATE</c> never included <c>row_version</c> in its
/// <c>WHERE</c> clause. <c>SaveExpenseReportCommandHandler</c> already had a manual
/// <c>report.RowVersion != request.RowVersion</c> check AND a
/// <c>catch (DbUpdateConcurrencyException)</c> block ready to convert a real conflict into a
/// <c>ConflictException</c> — but that exception could never actually fire: two editors who both
/// loaded the report before either saved would both pass the manual check (each against their own
/// correct-at-the-time snapshot) and both saves would silently succeed, the second unconditionally
/// overwriting the first (lost update).
///
/// <para>
/// The fix is a two-line EF configuration change (<c>ApplicationDbContext.cs</c>:
/// <c>b.Property(r =&gt; r.RowVersion).IsConcurrencyToken()</c> for both entities) — no handler code
/// changed. This test proves the mechanism directly, deterministically, without needing real thread
/// concurrency: it loads the same row into two separate <c>DbContext</c> instances (both see the same
/// starting RowVersion, exactly as two editors opening the same report at the same time would), saves
/// the first (succeeds), then saves the second (must now fail rather than silently overwrite).
/// </para>
/// </summary>
public sealed class SaveExpenseReportConcurrencyTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "IT-EXPENSE-TXN009";

    private readonly PemsWebApplicationFactory _factory;
    private ulong _visitRequestId;
    private ulong _visitInstanceId;
    private ulong _hostUserId;
    private ulong _registrantUserId;
    private ulong _expenseReportId;

    public SaveExpenseReportConcurrencyTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var staffRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Staff).Select(r => r.RoleId).FirstAsync();
        var visitorRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Visitor).Select(r => r.RoleId).FirstAsync();
        var dept = await db.Departments.AsNoTracking()
            .Where(d => d.DepartmentType == "IC" && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.DepartmentId).FirstAsync();
        var campusId = dept.CampusId;

        // Leader, not regular Staff: a self-hosting Staff Leader is the simplest fixture that
        // satisfies trg_visit_campuses_assignment_validate_bu's "decided_by must be Staff Leader of
        // the same campus" check below (self-host is an explicitly supported case).
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var host = new User
        {
            FullName = $"{TestPrefix} Host",
            Email = $"host_{suffix}@pems.test",
            RoleId = staffRoleId,
            SubRole = UserSubRoles.Leader,
            PrimaryCampusId = campusId,
            DepartmentId = dept.DepartmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        var registrant = new User
        {
            FullName = $"{TestPrefix} Registrant",
            Email = $"reg_{suffix}@pems.test",
            RoleId = visitorRoleId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.AddRange(host, registrant);
        await db.SaveChangesAsync();
        _hostUserId = host.UserId;
        _registrantUserId = registrant.UserId;

        var visit = new VisitRequest
        {
            RequestCode = $"IT-EXP-{suffix}",
            RegistrantUserId = registrant.UserId,
            RegistrantFullName = "Registrant",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Org",
            RegistrantJobTitle = "Manager",
            RegistrantPhone = "0900000000",
            RegistrantEmail = registrant.Email,
            VisitScope = VisitScopes.SingleCampus,
            Status = VisitRequestStatuses.Approved,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequests.Add(visit);
        await db.SaveChangesAsync();
        _visitRequestId = visit.VisitRequestId;

        var instance = new VisitRequestCampus
        {
            VisitRequestId = _visitRequestId,
            CampusId = campusId,
            OperationalContactUserId = registrant.UserId,
            CurrentHostUserId = host.UserId,
            HostAssignedBy = host.UserId,
            HostAssignedAt = DateTime.Now.AddDays(-4),
            DecidedBy = host.UserId,
            DecidedAt = DateTime.Now.AddDays(-4),
            DecisionActorRole = "STAFF_LEADER",
            DecisionSource = "STANDARD_CAMPUS_REVIEW",
            PlannedStartAt = DateTime.Now.AddDays(-1),
            PlannedEndAt = DateTime.Now.AddDays(-1).AddHours(2),
            Status = VisitInstanceStatus.BeforeVisit,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();
        _visitInstanceId = instance.VisitInstanceId;

        var report = new VisitExpenseReport
        {
            VisitInstanceId = _visitInstanceId,
            ReportScope = "GENERAL",
            Status = "DRAFT",
            CurrencyCode = "VND",
            RowVersion = 0,
            CreatedAt = DateTime.Now,
            CreatedBy = _hostUserId,
        };
        db.VisitExpenseReports.Add(report);
        await db.SaveChangesAsync();
        _expenseReportId = report.ExpenseReportId;
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await FixtureCleanup.For(db)
            .Root("visit_requests", $"visit_request_id = {_visitRequestId}")
            .Root("users", $"user_id IN ({_hostUserId}, {_registrantUserId})")
            .RunAsync();
    }

    [Fact]
    public async Task A_concurrent_save_from_a_stale_load_cannot_silently_overwrite_a_committed_edit()
    {
        // Both scopes load the SAME starting row — exactly as two editors opening the report at the
        // same time would, before either has saved.
        using var scopeA = _factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reportA = await dbA.VisitExpenseReports.FirstAsync(r => r.ExpenseReportId == _expenseReportId);

        using var scopeB = _factory.Services.CreateScope();
        var dbB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reportB = await dbB.VisitExpenseReports.FirstAsync(r => r.ExpenseReportId == _expenseReportId);

        Assert.Equal(0, reportA.RowVersion);
        Assert.Equal(0, reportB.RowVersion);

        // A saves first — succeeds.
        reportA.ReportNote = "Saved by A";
        reportA.RowVersion++;
        await dbA.SaveChangesAsync();

        // B, still holding its stale (pre-A) RowVersion, tries to save next — must be rejected rather
        // than silently overwriting A's committed edit.
        reportB.ReportNote = "Saved by B";
        reportB.RowVersion++;
        var thrown = await Record.ExceptionAsync(() => dbB.SaveChangesAsync());

        Assert.NotNull(thrown);
        Assert.IsType<DbUpdateConcurrencyException>(thrown);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await checkDb.VisitExpenseReports.AsNoTracking()
            .FirstAsync(r => r.ExpenseReportId == _expenseReportId);

        // A's edit survived intact; B's never landed.
        Assert.Equal("Saved by A", row.ReportNote);
        Assert.Equal(1, row.RowVersion);
    }

    [Fact]
    public async Task An_uncontended_save_still_succeeds_and_bumps_row_version()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var report = await db.VisitExpenseReports.FirstAsync(r => r.ExpenseReportId == _expenseReportId);

        report.ReportNote = "Solo edit";
        report.RowVersion++;
        await db.SaveChangesAsync();

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await checkDb.VisitExpenseReports.AsNoTracking()
            .FirstAsync(r => r.ExpenseReportId == _expenseReportId);
        Assert.Equal("Solo edit", row.ReportNote);
        Assert.Equal(1, row.RowVersion);
    }
}
