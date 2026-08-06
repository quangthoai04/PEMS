using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Plan §23 items 6 and 7 — what the per-campus operational contact actually stores.
///
/// The form now searches known organizations for this field instead of taking free text. That is a
/// presentation change and must stay one: the column is a SNAPSHOT of who a campus calls on the day,
/// and it has no relation to the request's partner. These tests hold that line at the database —
/// choosing a known organization here must not link, unlink or otherwise disturb `partner_id`, and
/// each campus must keep its own value.
///
/// Runs against disposable <c>pems_pr3_test</c> inside a rolled-back transaction; nothing commits.
/// </summary>
public sealed class OperationalContactSnapshotV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");
    private const ulong Registrant = 8;
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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master to run these tests.");
    }

    private static CampusVisitFormDto Campus(string code, string operationalOrganization)
    {
        var start = Now.AddDays(20);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(120), "Đoàn Snapshot", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", operationalOrganization, "+84911111111", "op@example.com"),
            "EN", null, "DECLINED", null, null);
    }

    private static VisitRequestFormDataV2 Form(ulong? partnerId, params CampusVisitFormDto[] campuses)
        => new(
            Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+84912345678", "registrant@example.com"),
            partnerId,
            campuses.ToList());

    private static async Task<string?> OperationalOrganizationAsync(
        ApplicationDbContext db, ulong visitRequestId, string campusCode)
    {
        var campusId = await db.Campuses
            .Where(c => c.CampusCode == campusCode)
            .Select(c => c.CampusId)
            .FirstAsync();
        var instanceId = await db.VisitRequestCampuses
            .Where(c => c.VisitRequestId == visitRequestId && c.CampusId == campusId)
            .Select(c => c.VisitInstanceId)
            .FirstAsync();
        return await db.VisitInstanceFormDetails
            .Where(d => d.VisitInstanceId == instanceId)
            .Select(d => d.OperationalContactOrganization)
            .FirstAsync();
    }

    [Fact]
    public async Task Operational_organization_is_stored_verbatim_as_a_snapshot()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        const string picked = "Trường Đại học Khoa học Tự nhiên";
        var req = await new VisitRequestV2CreateService(db).CreateV2Async(
            Form(null, Campus("HN", picked)), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        Assert.Equal(picked, await OperationalOrganizationAsync(db, req.VisitRequestId, "HN"));
    }

    [Fact]
    public async Task Choosing_a_known_organization_here_leaves_the_request_partner_alone()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // No partner on the request, and an operational organization that IS a real partner name.
        var partner = await db.Partners.OrderBy(p => p.PartnerId).FirstOrDefaultAsync();
        var knownName = partner?.Name ?? "Đại học Đối Tác";

        var req = await new VisitRequestV2CreateService(db).CreateV2Async(
            Form(null, Campus("HN", knownName)), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        var stored = await db.VisitRequests
            .Where(v => v.VisitRequestId == req.VisitRequestId)
            .Select(v => v.PartnerId)
            .FirstAsync();

        // The name matched a partner exactly and STILL did not link one: the per-campus contact has
        // no partner relation, so nothing about it can create or change the request's own.
        Assert.Null(stored);
        Assert.Equal(knownName, await OperationalOrganizationAsync(db, req.VisitRequestId, "HN"));
    }

    [Fact]
    public async Task A_partner_chosen_on_the_request_survives_a_different_operational_organization()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var partner = await db.Partners.OrderBy(p => p.PartnerId).FirstOrDefaultAsync();
        if (partner is null) return; // no seeded partner in this database — nothing to assert against

        var req = await new VisitRequestV2CreateService(db).CreateV2Async(
            Form(partner.PartnerId, Campus("HN", "Một Đơn Vị Khác Hẳn")),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        var stored = await db.VisitRequests
            .Where(v => v.VisitRequestId == req.VisitRequestId)
            .Select(v => v.PartnerId)
            .FirstAsync();

        Assert.Equal(partner.PartnerId, stored);
        Assert.Equal("Một Đơn Vị Khác Hẳn", await OperationalOrganizationAsync(db, req.VisitRequestId, "HN"));
    }

    [Fact]
    public async Task Each_campus_keeps_its_own_operational_organization()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // The quick-fill copies into ONE card; a second campus filled differently must stay different.
        var req = await new VisitRequestV2CreateService(db).CreateV2Async(
            Form(null, Campus("HN", "Đơn Vị Hà Nội"), Campus("HCM", "Đơn Vị Hồ Chí Minh")),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        Assert.Equal("Đơn Vị Hà Nội", await OperationalOrganizationAsync(db, req.VisitRequestId, "HN"));
        Assert.Equal("Đơn Vị Hồ Chí Minh", await OperationalOrganizationAsync(db, req.VisitRequestId, "HCM"));
    }

    [Fact]
    public async Task An_organization_at_the_full_200_characters_reaches_the_column_intact()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // The form caps this at 200 and so does the validator; the column has to actually hold 200,
        // or the limit the user is shown is one MySQL will not honour.
        var longName = new string('x', 200);
        var req = await new VisitRequestV2CreateService(db).CreateV2Async(
            Form(null, Campus("HN", longName)), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        var stored = await OperationalOrganizationAsync(db, req.VisitRequestId, "HN");
        Assert.Equal(200, stored!.Length);
        Assert.Equal(longName, stored);
    }
}
