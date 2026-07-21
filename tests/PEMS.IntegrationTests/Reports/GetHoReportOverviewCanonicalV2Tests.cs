using PEMS.Application.Common.Exceptions;
using PEMS.Application.Reports.Queries.GetHoReportOverview;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.IntegrationTests.Reports;

/// <summary>
/// Behavioural regressions for C2 — <see cref="GetHoReportOverviewQueryHandler"/>.
///
/// Same defect class as C1, but here it corrupted FILTERS rather than a display string: the four
/// <c>visit_type</c> predicates gated their canonical read on <c>HasMixedCampusDetails</c>, so a
/// uniform v2 request was filtered by the stale projection on <c>visit_requests</c>. That both
/// admits requests that should not match and drops requests that should — a wrong report, not a
/// cosmetic one.
///
/// The seed deliberately sets the projection and the canonical detail to DIFFERENT visit types, so
/// each assertion distinguishes which column the filter actually read. Runs on real MySQL/Pomelo.
/// </summary>
[Collection(CanonicalV2ReaderCollection.Name)]
public sealed class GetHoReportOverviewCanonicalV2Tests : IDisposable
{
    private readonly CanonicalV2ReaderFixture _fx;

    public GetHoReportOverviewCanonicalV2Tests(CanonicalV2ReaderFixture fx)
    {
        _fx = fx;
        CanonicalV2Seed.Reset(_fx.Db);
        CanonicalV2Seed.SeedOrganisation(_fx.Db);
    }

    public void Dispose() => CanonicalV2Seed.Reset(_fx.Db);

    private Task<HoReportOverviewDto> RunAsync(
        string? visitType = null, ulong? campusId = null, string roleCode = "HO")
    {
        var handler = new GetHoReportOverviewQueryHandler(
            _fx.Db, new FakeCurrentUser { RoleCode = roleCode });

        return handler.Handle(
            new GetHoReportOverviewQuery
            {
                Preset = "CUSTOM",
                FromDate = CanonicalV2Seed.PeriodStart,
                ToDate = CanonicalV2Seed.PeriodStart.AddMonths(1),
                VisitType = visitType,
                CampusId = campusId,
            },
            CancellationToken.None);
    }

    /// <summary>Uniform v2 whose projection says CAMPUS_TOUR but whose canonical detail says MOU_SIGNING.</summary>
    private void SeedUniformV2WithDivergentVisitType() =>
        CanonicalV2Seed.SeedRequest(
            _fx.Db, requestId: 100, formSchemaVersion: FormSchemaVersions.PerCampus,
            hasMixedCampusDetails: false,
            globalDelegationName: CanonicalV2Seed.StaleGlobalName,
            globalVisitType: CanonicalV2Seed.StaleGlobalVisitType,
            campusDetails: new[]
            {
                (101UL, 1UL, 10UL, (string?)CanonicalV2Seed.CanonicalNameA, (string?)CanonicalV2Seed.CanonicalVisitType),
            });

    [Fact]
    public async Task Uniform_v2_matches_the_filter_on_its_canonical_visit_type()
    {
        SeedUniformV2WithDivergentVisitType();

        var report = await RunAsync(visitType: CanonicalV2Seed.CanonicalVisitType);

        Assert.Equal(1, report.Kpis.TotalRequests);
        Assert.Equal(1, report.LifecyclePipeline.Single(p => p.Status == VisitInstanceStatus.Closed).Count);
    }

    [Fact]
    public async Task Uniform_v2_does_not_match_the_filter_on_its_stale_projection_visit_type()
    {
        SeedUniformV2WithDivergentVisitType();

        var report = await RunAsync(visitType: CanonicalV2Seed.StaleGlobalVisitType);

        // Filtering by the projection value must find nothing: for v2 that column is not business content.
        Assert.Equal(0, report.Kpis.TotalRequests);
        Assert.Equal(0, report.LifecyclePipeline.Sum(p => p.Count));
    }

    [Fact]
    public async Task Mixed_v2_matches_at_request_level_but_only_the_matching_instance_at_instance_level()
    {
        CanonicalV2Seed.SeedRequest(
            _fx.Db, requestId: 200, formSchemaVersion: FormSchemaVersions.PerCampus,
            hasMixedCampusDetails: true,
            globalDelegationName: CanonicalV2Seed.StaleGlobalName,
            globalVisitType: CanonicalV2Seed.StaleGlobalVisitType,
            campusDetails: new[]
            {
                (201UL, 1UL, 10UL, (string?)CanonicalV2Seed.CanonicalNameA, (string?)CanonicalV2Seed.CanonicalVisitType),
                (202UL, 2UL, 20UL, (string?)CanonicalV2Seed.CanonicalNameB, (string?)"CAMPUS_TOUR"),
            });

        var report = await RunAsync(visitType: CanonicalV2Seed.CanonicalVisitType);

        // Request level: the request matches because ANY campus detail matches — counted once.
        Assert.Equal(1, report.Kpis.TotalRequests);
        // Instance level: only the campus whose OWN detail matches is counted. Counting both would
        // mean the non-matching sibling leaked in through the shared parent.
        Assert.Equal(1, report.LifecyclePipeline.Sum(p => p.Count));
        Assert.Equal(1, report.CampusPerformance.Single(c => c.CampusId == 1).TotalInstances);
        Assert.Equal(0, report.CampusPerformance.Single(c => c.CampusId == 2).TotalInstances);
    }

    [Fact]
    public async Task V1_still_filters_on_the_global_visit_type()
    {
        CanonicalV2Seed.SeedRequest(
            _fx.Db, requestId: 300, formSchemaVersion: FormSchemaVersions.Legacy,
            hasMixedCampusDetails: false,
            globalDelegationName: CanonicalV2Seed.V1GlobalName,
            globalVisitType: CanonicalV2Seed.StaleGlobalVisitType,
            campusDetails: new[] { (301UL, 1UL, 10UL, (string?)null, (string?)null) });

        Assert.Equal(1, (await RunAsync(visitType: CanonicalV2Seed.StaleGlobalVisitType)).Kpis.TotalRequests);
        Assert.Equal(0, (await RunAsync(visitType: CanonicalV2Seed.CanonicalVisitType)).Kpis.TotalRequests);
    }

    [Fact]
    public async Task Missing_v2_detail_never_matches_via_the_projection()
    {
        CanonicalV2Seed.SeedRequest(
            _fx.Db, requestId: 400, formSchemaVersion: FormSchemaVersions.PerCampus,
            hasMixedCampusDetails: false,
            globalDelegationName: CanonicalV2Seed.StaleGlobalName,
            globalVisitType: CanonicalV2Seed.StaleGlobalVisitType,
            campusDetails: new[] { (401UL, 1UL, 10UL, (string?)null, (string?)null) });

        // No canonical detail exists, so no v2 filter value exists — the projection must not stand in.
        Assert.Equal(0, (await RunAsync(visitType: CanonicalV2Seed.StaleGlobalVisitType)).Kpis.TotalRequests);
        Assert.Equal(0, (await RunAsync(visitType: CanonicalV2Seed.CanonicalVisitType)).Kpis.TotalRequests);
    }

    [Fact]
    public async Task Campus_filter_does_not_surface_a_hidden_sibling_campus()
    {
        CanonicalV2Seed.SeedRequest(
            _fx.Db, requestId: 500, formSchemaVersion: FormSchemaVersions.PerCampus,
            hasMixedCampusDetails: true,
            globalDelegationName: CanonicalV2Seed.StaleGlobalName,
            globalVisitType: CanonicalV2Seed.StaleGlobalVisitType,
            campusDetails: new[]
            {
                (501UL, 1UL, 10UL, (string?)CanonicalV2Seed.CanonicalNameA, (string?)CanonicalV2Seed.CanonicalVisitType),
                (502UL, 2UL, 20UL, (string?)CanonicalV2Seed.CanonicalNameB, (string?)CanonicalV2Seed.CanonicalVisitType),
            });

        var report = await RunAsync(campusId: 1);

        Assert.Single(report.CampusPerformance);
        Assert.Equal(1UL, report.CampusPerformance[0].CampusId);
        Assert.Equal(1, report.LifecyclePipeline.Sum(p => p.Count));
    }

    [Fact]
    public async Task Unfiltered_report_does_not_double_count_a_multi_campus_request()
    {
        CanonicalV2Seed.SeedRequest(
            _fx.Db, requestId: 600, formSchemaVersion: FormSchemaVersions.PerCampus,
            hasMixedCampusDetails: true,
            globalDelegationName: CanonicalV2Seed.StaleGlobalName,
            globalVisitType: CanonicalV2Seed.StaleGlobalVisitType,
            campusDetails: new[]
            {
                (601UL, 1UL, 10UL, (string?)CanonicalV2Seed.CanonicalNameA, (string?)CanonicalV2Seed.CanonicalVisitType),
                (602UL, 2UL, 20UL, (string?)CanonicalV2Seed.CanonicalNameB, (string?)CanonicalV2Seed.CanonicalVisitType),
            });

        var report = await RunAsync();

        // One request, two instances — the request-level KPI and the monthly trend must not inflate.
        Assert.Equal(1, report.Kpis.TotalRequests);
        Assert.Equal(1, report.MonthlyTrend.Sum(m => m.TotalRequests));
        Assert.Equal(2, report.LifecyclePipeline.Sum(p => p.Count));
    }

    [Theory]
    [InlineData("STAFF")]
    [InlineData("DEPARTMENT")]
    [InlineData("STUDENT")]
    public async Task Non_ho_callers_are_forbidden(string roleCode)
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => RunAsync(roleCode: roleCode));
    }
}
