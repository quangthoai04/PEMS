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
/// Plan §25 items 9 and 10, against real MySQL: a visit that ENDS ON A LATER DAY must persist and
/// read back with the same wall-clock components it was submitted with.
///
/// This is the case the new schedule UI makes reachable — the old two-box form technically allowed
/// it, but nothing proved the round trip, and an overnight window is exactly where a stray
/// timezone conversion shows up as a one-day or seven-hour shift. PEMS stores DATETIME as Vietnam
/// local time, so the correct behaviour is that NOTHING converts: what goes in comes out.
///
/// Runs against disposable <c>pems_pr3_test</c>, each test inside a rolled-back transaction.
/// </summary>
public sealed class VisitScheduleMultiDayV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");
    private const ulong Registrant = 8; // a seeded VISITOR user
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

    private static VisitRequestV2CreateService Svc(ApplicationDbContext db) => new(db);
    private static readonly DateTime Now = DateTime.Now;

    private static CampusVisitFormDto Campus(string code, DateTime start, DateTime end, string delegation)
        => new(
            code, start, end, delegation, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null);

    private static VisitRequestFormDataV2 Form(params CampusVisitFormDto[] campuses)
        => new(
            Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491111", "registrant@example.com"),
            null,
            campuses.ToList());

    [Fact]
    public async Task An_overnight_visit_persists_and_reads_back_with_the_same_wall_clock()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // 22:00 on one day → 01:00 the next: three hours, two dates.
        var start = Now.AddDays(21).Date.AddHours(22);
        var end = start.AddHours(3);

        var req = await Svc(db).CreateV2Async(
            Form(Campus("HN", start, end, "Đoàn Qua Đêm")),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        await db.SaveChangesAsync();
        // Read back through a context with NO tracked entities, so this is the DATABASE's answer
        // and not the in-memory object that was just written.
        db.ChangeTracker.Clear();
        var stored = await db.VisitRequestCampuses.AsNoTracking()
            .FirstAsync(c => c.VisitRequestId == req.VisitRequestId);

        Assert.Equal(start, stored.PlannedStartAt);
        Assert.Equal(end, stored.PlannedEndAt);
        Assert.Equal(3, (stored.PlannedEndAt - stored.PlannedStartAt).TotalHours);
        // The date really did roll over — this is what a same-day-only reader would get wrong.
        Assert.NotEqual(stored.PlannedStartAt.Date, stored.PlannedEndAt.Date);
        Assert.Equal(22, stored.PlannedStartAt.Hour);
        Assert.Equal(1, stored.PlannedEndAt.Hour);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_visit_spanning_several_days_keeps_its_full_length()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var start = Now.AddDays(30).Date.AddHours(8);
        var end = start.AddDays(2).AddHours(6);   // 54 hours

        var req = await Svc(db).CreateV2Async(
            Form(Campus("HN", start, end, "Đoàn Dài Ngày")),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var stored = await db.VisitRequestCampuses.AsNoTracking()
            .FirstAsync(c => c.VisitRequestId == req.VisitRequestId);

        Assert.Equal(54, (stored.PlannedEndAt - stored.PlannedStartAt).TotalHours);
        Assert.Equal(start.Day, stored.PlannedStartAt.Day);
        Assert.Equal(end.Day, stored.PlannedEndAt.Day);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task The_stored_time_is_not_shifted_by_any_offset()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // A time whose components would be unmistakably mangled by a UTC round trip: 07:00 Vietnam
        // is 00:00 UTC, so a conversion would land on midnight — and 23:30 would move to the
        // previous day.
        var earlyStart = Now.AddDays(25).Date.AddHours(7);
        var lateStart = Now.AddDays(26).Date.AddHours(23).AddMinutes(30);

        var req = await Svc(db).CreateV2Async(
            Form(
                Campus("HN", earlyStart, earlyStart.AddHours(2), "Đoàn Sáng Sớm"),
                Campus("HCM", lateStart, lateStart.AddHours(2), "Đoàn Đêm Muộn")),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var stored = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == req.VisitRequestId)
            .OrderBy(c => c.PlannedStartAt)
            .ToListAsync();

        Assert.Equal(2, stored.Count);
        Assert.Equal(7, stored[0].PlannedStartAt.Hour);
        Assert.Equal(0, stored[0].PlannedStartAt.Minute);
        Assert.Equal(earlyStart.Day, stored[0].PlannedStartAt.Day);

        Assert.Equal(23, stored[1].PlannedStartAt.Hour);
        Assert.Equal(30, stored[1].PlannedStartAt.Minute);
        Assert.Equal(lateStart.Day, stored[1].PlannedStartAt.Day);
        // 23:30 + 2h crosses midnight, so the END is on the following day — stored as such.
        Assert.Equal(lateStart.AddHours(2).Day, stored[1].PlannedEndAt.Day);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_long_note_at_its_limit_survives_the_round_trip()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // The exact ceilings the validator now enforces; TEXT columns hold them comfortably, but
        // nothing had ever written one at full size.
        var purpose = new string('p', 2000);
        var workingContent = new string('w', 4000);
        var transportation = new string('t', 2000);
        var mediaNote = new string('m', 2000);
        var notes = new string('n', 2000);

        var start = Now.AddDays(22).Date.AddHours(9);
        var campus = new CampusVisitFormDto(
            "HN", start, start.AddHours(2), "Đoàn Dài Chữ", "MEETING", null, purpose, workingContent,
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", transportation, "AGREED", mediaNote, null);

        var req = await Svc(db).CreateV2Async(
            Form(campus), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var instance = await db.VisitRequestCampuses.AsNoTracking()
            .FirstAsync(c => c.VisitRequestId == req.VisitRequestId);
        var detail = await db.VisitInstanceFormDetails.AsNoTracking()
            .FirstAsync(d => d.VisitInstanceId == instance.VisitInstanceId);

        Assert.Equal(2000, detail.Purpose.Length);
        Assert.Equal(4000, detail.WorkingContent!.Length);
        Assert.Equal(2000, detail.TransportationNote!.Length);
        Assert.Equal(2000, detail.MediaConsentNote!.Length);

        await tx.RollbackAsync();
    }
}
