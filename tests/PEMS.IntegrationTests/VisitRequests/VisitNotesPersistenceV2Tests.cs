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
/// "Ghi chú gửi FPTU" (<c>visit_instance_form_details.notes</c>) against real MySQL.
///
/// This field existed on the form and nowhere else: the payload mapper never sent it, no request
/// DTO carried it, and no column stored it — so a guest could type a note, submit successfully, and
/// have it vanish with no error anywhere. These tests pin the whole path now that the column is
/// real: what is submitted is what the row holds, blank means NULL rather than an empty string, and
/// media consent neither gates the note nor is gated by it.
///
/// Runs against disposable <c>pems_pr3_test</c>, each test inside a rolled-back transaction.
/// </summary>
public sealed class VisitNotesPersistenceV2Tests
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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the canonical script to run these tests.");
    }

    private static VisitRequestV2CreateService Svc(ApplicationDbContext db) => new(db);
    private static readonly DateTime Now = DateTime.Now;

    private static CampusVisitFormDto Campus(string mediaConsentStatus, string? notes, int dayOffset = 30)
    {
        var start = Now.AddDays(dayOffset).Date.AddHours(9);
        return new(
            "HN", start, start.AddHours(2), "Đoàn Ghi Chú", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", "op@example.com"),
            "EN", null, mediaConsentStatus, notes, null);
    }

    private static VisitRequestFormDataV2 Form(params CampusVisitFormDto[] campuses)
        => new(
            Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491111", "registrant@example.com"),
            null, campuses.ToList());

    /// <summary>Creates one campus and returns the persisted detail row, read back from the DB.</summary>
    private static async Task<PEMS.Domain.Entities.Delegations.VisitInstanceFormDetail> CreateAndReadAsync(
        ApplicationDbContext db, CampusVisitFormDto campus)
    {
        var req = await Svc(db).CreateV2Async(
            Form(campus), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var instance = await db.VisitRequestCampuses.AsNoTracking()
            .FirstAsync(c => c.VisitRequestId == req.VisitRequestId);
        return await db.VisitInstanceFormDetails.AsNoTracking()
            .FirstAsync(d => d.VisitInstanceId == instance.VisitInstanceId);
    }

    [Fact]
    public async Task A_submitted_note_is_stored_verbatim()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        const string note = "Đoàn có 2 khách lớn tuổi, vui lòng hỗ trợ xe điện.";
        var detail = await CreateAndReadAsync(db, Campus("DECLINED", note));

        // Verbatim: no trimming of interior text, no re-encoding of the Vietnamese diacritics.
        Assert.Equal(note, detail.Notes);

        await tx.RollbackAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n  ")]
    public async Task A_blank_note_is_stored_as_NULL_not_as_an_empty_string(string? submitted)
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var detail = await CreateAndReadAsync(db, Campus("AGREED", submitted));

        // "" and NULL would render identically but compare differently — an empty string makes every
        // downstream diff, "has a note" check and change-detection treat a blank as content.
        Assert.Null(detail.Notes);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Surrounding_whitespace_is_trimmed_off_a_real_note()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var detail = await CreateAndReadAsync(db, Campus("DECLINED", "   Cần phiên dịch buổi chiều.  "));

        Assert.Equal("Cần phiên dịch buổi chiều.", detail.Notes);

        await tx.RollbackAsync();
    }

    [Theory]
    [InlineData("AGREED", null)]
    [InlineData("DECLINED", null)]
    [InlineData("AGREED", "Xin hỗ trợ suất ăn chay cho 3 khách.")]
    [InlineData("DECLINED", "Xin hỗ trợ suất ăn chay cho 3 khách.")]
    public async Task Media_consent_and_the_note_are_stored_independently(string status, string? notes)
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var detail = await CreateAndReadAsync(db, Campus(status, notes));

        // The consent answer is untouched by the note, and vice versa: all four combinations persist
        // exactly as submitted. The note is NOT a justification for the consent answer.
        Assert.Equal(status, detail.MediaConsentStatus);
        Assert.Equal(notes, detail.Notes);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Each_campus_keeps_its_OWN_note()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var start = Now.AddDays(31).Date.AddHours(9);
        CampusVisitFormDto At(string code, string? note) => new(
            code, start, start.AddHours(2), $"Đoàn {code}", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new($"Guest {code}", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", "op@example.com"),
            "EN", null, "DECLINED", note, null);

        var req = await Svc(db).CreateV2Async(
            Form(At("HN", "Ghi chú riêng của Hà Nội."), At("HCM", null)),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var instances = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == req.VisitRequestId)
            .OrderBy(c => c.VisitInstanceId).ToListAsync();
        var ids = instances.Select(i => i.VisitInstanceId).ToList();
        var details = await db.VisitInstanceFormDetails.AsNoTracking()
            .Where(d => ids.Contains(d.VisitInstanceId)).ToListAsync();

        // The note is per-campus content, so one campus's note must never be projected onto its
        // sibling — the same rule the rest of the form snapshot follows.
        Assert.Equal(2, details.Count);
        Assert.Contains(details, d => d.Notes == "Ghi chú riêng của Hà Nội.");
        Assert.Contains(details, d => d.Notes is null);

        await tx.RollbackAsync();
    }
}
