using PEMS.Application.Common.Exceptions;
using PEMS.Application.Reports.Queries.GetStaffLeaderReportV2;
using PEMS.Domain.Constants;

namespace PEMS.IntegrationTests.Reports;

/// <summary>
/// Behavioural regressions for C1 — <see cref="GetStaffLeaderDeptInvoiceItemsQueryHandler"/>.
///
/// The defect these lock down: the reader used to gate its canonical read on
/// <c>FormSchemaVersion >= PerCampus &amp;&amp; HasMixedCampusDetails</c>, so a UNIFORM v2 request
/// silently fell back to <c>visit_requests.delegation_name</c> — the compatibility projection that
/// Phase I is supposed to be able to drop. A green build never showed this; only a row where the
/// projection and the canonical detail disagree does.
///
/// Runs on real MySQL/Pomelo (see <see cref="CanonicalV2ReaderFixture"/>) so translation is proven
/// too, not just the LINQ shape.
/// </summary>
[Collection(CanonicalV2ReaderCollection.Name)]
public sealed class GetStaffLeaderDeptInvoiceItemsCanonicalV2Tests : IDisposable
{
    private readonly CanonicalV2ReaderFixture _fx;

    public GetStaffLeaderDeptInvoiceItemsCanonicalV2Tests(CanonicalV2ReaderFixture fx)
    {
        _fx = fx;
        CanonicalV2Seed.Reset(_fx.Db);
        CanonicalV2Seed.SeedOrganisation(_fx.Db);
    }

    public void Dispose() => CanonicalV2Seed.Reset(_fx.Db);

    private Task<List<StaffLeaderInvoiceItemDto>> RunAsync(
        ulong departmentId = 10, string roleCode = "STAFF", string subRole = "LEADER", ulong? campusId = 1)
    {
        var handler = new GetStaffLeaderDeptInvoiceItemsQueryHandler(
            _fx.Db,
            new FakeCurrentUser { RoleCode = roleCode, SubRole = subRole, PrimaryCampusId = campusId });

        return handler.Handle(
            new GetStaffLeaderDeptInvoiceItemsQuery
            {
                DepartmentId = departmentId,
                FromDate = CanonicalV2Seed.PeriodStart,
                ToDate = CanonicalV2Seed.PeriodStart.AddMonths(1),
            },
            CancellationToken.None);
    }

    [Fact]
    public async Task Uniform_v2_reads_the_canonical_detail_not_the_stale_projection()
    {
        // has_mixed_campus_details = false is exactly the case the old gate mishandled.
        CanonicalV2Seed.SeedRequest(
            _fx.Db, requestId: 100,
            hasMixedCampusDetails: false,
            campusDetails: new[] { (101UL, 1UL, 10UL, (string?)CanonicalV2Seed.CanonicalNameA, (string?)null) });

        var rows = await RunAsync();

        var row = Assert.Single(rows);
        Assert.Equal(CanonicalV2Seed.CanonicalNameA, row.DelegationName);
        Assert.DoesNotContain(rows, r => r.DelegationName == CanonicalV2Seed.AbsentDelegationName);
    }

    /// <summary>Campus 1 (department 10) holds name A, campus 2 (department 20) holds name B.</summary>
    private void SeedMixedTwoCampusRequest() =>
        CanonicalV2Seed.SeedRequest(
            _fx.Db, requestId: 200,
            hasMixedCampusDetails: true,
            campusDetails: new[]
            {
                (201UL, 1UL, 10UL, (string?)CanonicalV2Seed.CanonicalNameA, (string?)null),
                (202UL, 2UL, 20UL, (string?)CanonicalV2Seed.CanonicalNameB, (string?)null),
            });

    [Fact]
    public async Task Mixed_v2_returns_only_the_targeted_campus_detail_and_never_a_sibling()
    {
        SeedMixedTwoCampusRequest();

        var rows = await RunAsync();

        var row = Assert.Single(rows);
        Assert.Equal(CanonicalV2Seed.CanonicalNameA, row.DelegationName);
        // The hidden sibling campus must not surface through the shared parent request.
        Assert.DoesNotContain(rows, r => r.DelegationName == CanonicalV2Seed.CanonicalNameB);
    }

    [Fact]
    public async Task Missing_v2_detail_does_not_fall_back_to_any_request_level_value()
    {
        CanonicalV2Seed.SeedRequest(
            _fx.Db, requestId: 300,
            hasMixedCampusDetails: false,
            campusDetails: new[] { (301UL, 1UL, 10UL, (string?)null, (string?)null) });

        var rows = await RunAsync();

        var row = Assert.Single(rows);
        // The DTO coalesces a missing canonical name to "" — the point is that it invents nothing.
        Assert.NotEqual(CanonicalV2Seed.AbsentDelegationName, row.DelegationName);
        Assert.Equal(string.Empty, row.DelegationName);
    }

    /// <summary>
    /// Mirror of the mixed test, read by the campus-2 Staff Leader over department 20.
    ///
    /// Replaces the former V1 test: there is no global name column left to read, so the live risk is a
    /// reader that treats one campus as representative of the request. The A-side test alone cannot see
    /// that — a first-campus reader returns name A here, where only name B is correct.
    /// </summary>
    [Fact]
    public async Task Mixed_v2_read_from_the_second_campus_returns_that_campus_detail()
    {
        SeedMixedTwoCampusRequest();

        var rows = await RunAsync(departmentId: 20, campusId: 2);

        var row = Assert.Single(rows);
        Assert.Equal(CanonicalV2Seed.CanonicalNameB, row.DelegationName);
        Assert.DoesNotContain(rows, r => r.DelegationName == CanonicalV2Seed.CanonicalNameA);
    }

    [Fact]
    public async Task Department_in_another_campus_is_still_rejected()
    {
        CanonicalV2Seed.SeedRequest(
            _fx.Db, requestId: 500,
            hasMixedCampusDetails: false,
            campusDetails: new[] { (501UL, 2UL, 20UL, (string?)CanonicalV2Seed.CanonicalNameB, (string?)null) });

        // Department 20 belongs to campus 2; the caller is the campus-1 Staff Leader.
        await Assert.ThrowsAsync<NotFoundException>(() => RunAsync(departmentId: 20));
    }

    [Theory]
    [InlineData("STAFF", null)]      // Staff, but not a Leader
    [InlineData("DEPARTMENT", "LEADER")]
    [InlineData("HO", "LEADER")]
    public async Task Non_staff_leader_callers_are_forbidden(string roleCode, string? subRole)
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => RunAsync(roleCode: roleCode, subRole: subRole!));
    }

    [Fact]
    public async Task Staff_leader_without_a_campus_is_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => RunAsync(campusId: null));
    }
}
