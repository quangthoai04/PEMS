using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.Delegations.ExportScheduleReport;

/// <summary>
/// Pure V2 contract for the schedule report.
///
/// The report is produced for ONE campus instance, so every piece of its content has to come from that
/// instance's own <c>visit_instance_form_details</c> row and its own
/// <c>visit_instance_guest_members</c> links. The feature originally shipped reading the delegation name
/// and purpose straight off <c>VisitRequest</c> behind a <c>form_schema_version</c> check — columns that
/// no longer exist. These tests pin the replacement so the request row can never become the source again.
///
/// The fixture is deliberately MIXED across three campuses: with a shared value there would be nothing to
/// distinguish "read the target campus" from "read whichever campus came first".
/// </summary>
public class ScheduleReportPerCampusTests
{
    private static VisitFormReadService FormReadService(
        ScheduleReportTestDbContext db, FakeScheduleReportCurrentUser currentUser)
        => new(db, currentUser, NullLogger<VisitFormReadService>.Instance);

    private static Task<ScheduleReportDto> BuildAsync(
        ScheduleReportTestDbContext db, Domain.Entities.Delegations.VisitRequestCampus instance)
        => ScheduleReportDataBuilder.BuildAsync(
            db, FormReadService(db, new FakeScheduleReportCurrentUser()), instance, default);

    [Theory]
    [InlineData(0, "A")]
    [InlineData(1, "B")]
    [InlineData(2, "C")]
    public async Task Report_content_comes_from_the_campus_it_was_asked_for(int index, string tag)
    {
        using var db = ScheduleReportTestDbContext.Create();
        var instances = ScheduleReportTestData.SeedMixedThreeCampuses(db);

        var dto = await BuildAsync(db, instances[index]);

        Assert.Equal($"Đoàn {tag}", dto.DelegationName);
        Assert.Equal($"Mục đích {tag}", dto.Purpose);
    }

    /// <summary>
    /// The C case is what a two-campus pair cannot catch: a builder that silently used the first campus
    /// still looks correct for A, and can look correct for B by accident. Asserting that the other two
    /// campuses' values are absent is the part that bites.
    /// </summary>
    [Fact]
    public async Task Third_campus_report_contains_nothing_from_the_first_two()
    {
        using var db = ScheduleReportTestDbContext.Create();
        var instances = ScheduleReportTestData.SeedMixedThreeCampuses(db);

        var dto = await BuildAsync(db, instances[2]);

        Assert.Equal("Đoàn C", dto.DelegationName);
        Assert.NotEqual("Đoàn A", dto.DelegationName);
        Assert.NotEqual("Đoàn B", dto.DelegationName);
        Assert.NotEqual("Mục đích A", dto.Purpose);
        Assert.NotEqual("Mục đích B", dto.Purpose);
    }

    /// <summary>
    /// Three campuses, three reports, three different documents — the request they share contributes
    /// nothing to any of them.
    /// </summary>
    [Fact]
    public async Task One_mixed_request_produces_three_independent_reports()
    {
        using var db = ScheduleReportTestDbContext.Create();
        var instances = ScheduleReportTestData.SeedMixedThreeCampuses(db);

        var names = new List<string>();
        var purposes = new List<string?>();
        foreach (var instance in instances)
        {
            var dto = await BuildAsync(db, instance);
            names.Add(dto.DelegationName);
            purposes.Add(dto.Purpose);
        }

        Assert.Equal(new[] { "Đoàn A", "Đoàn B", "Đoàn C" }, names);
        Assert.Equal(3, purposes.Distinct().Count());
    }

    /// <summary>
    /// Guests belong to the request but reach a report only through their campus link, so a report must
    /// list its own campus's guest and no one else's.
    /// </summary>
    [Fact]
    public async Task Guest_side_only_contains_guests_linked_to_that_campus()
    {
        using var db = ScheduleReportTestDbContext.Create();
        var instances = ScheduleReportTestData.SeedMixedThreeCampuses(db);

        var dto = await BuildAsync(db, instances[1]);

        var guest = Assert.Single(dto.GuestSide);
        Assert.Equal("Khách B", guest.FullName);
        Assert.DoesNotContain(dto.GuestSide, p => p.FullName == "Khách A");
        Assert.DoesNotContain(dto.GuestSide, p => p.FullName == "Khách C");
    }

    /// <summary>
    /// DECISION-01. The request row still carries a PRIMARY contact ("Đầu mối"); each campus carries its
    /// own operational contact. Nothing on the report may be the request-level one.
    /// </summary>
    [Fact]
    public async Task Report_never_surfaces_the_request_level_primary_contact()
    {
        using var db = ScheduleReportTestDbContext.Create();
        var instances = ScheduleReportTestData.SeedMixedThreeCampuses(db);

        var dto = await BuildAsync(db, instances[0]);

        var everyName = dto.GuestSide.Concat(dto.FptSide).Select(p => p.FullName).ToList();
        Assert.DoesNotContain("Đầu mối", everyName);      // request-level primary contact
        Assert.DoesNotContain("Đầu mối B", everyName);    // another campus's operational contact
        Assert.DoesNotContain("Đầu mối C", everyName);
    }

    /// <summary>
    /// Pure V2 has no global snapshot to fall back to, so a campus with no detail row is a data defect,
    /// not a blank report. It has to fail with the business error rather than render something empty.
    /// </summary>
    [Fact]
    public async Task Missing_form_detail_fails_loudly_instead_of_rendering_a_blank_report()
    {
        using var db = ScheduleReportTestDbContext.Create();
        var instances = ScheduleReportTestData.SeedMixedThreeCampuses(db);

        var detail = await db.Set<Domain.Entities.Delegations.VisitInstanceFormDetail>()
            .FirstAsync(d => d.VisitInstanceId == instances[0].VisitInstanceId);
        db.Remove(detail);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => BuildAsync(db, instances[0]));
        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
    }
}
