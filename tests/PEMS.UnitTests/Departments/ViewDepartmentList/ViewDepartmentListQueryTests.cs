using PEMS.Application.Departments.Queries.ViewDepartmentList;
using Xunit;

namespace PEMS.UnitTests.Departments.ViewDepartmentList;

/// <summary>
/// Unit tests for UC-104 View Department List.
///
/// Source-confirmed facts: there is no <c>ViewDepartmentListQueryValidator</c> (no FluentValidation
/// validator exists for this query) and <see cref="ViewDepartmentListQueryHandler"/> is a thin
/// pass-through to the internal <c>DepartmentListQueryExecutor.ExecuteAsync</c>, which requires a
/// real <c>IApplicationDbContext</c>/<c>ICurrentUserService</c> to do anything meaningful (campus
/// scope resolution, EF LINQ-to-SQL translation for keyword/status/sort, LEFT JOIN on head user,
/// pagination). None of that can be verified in isolation without a real MySQL connection — mocking
/// it would not prove the EF Core + Pomelo translation actually behaves as claimed (e.g.
/// case-insensitive Contains, ordering of nulls), so that behavior is covered by
/// ViewDepartmentListApiTests (Integration Test) instead.
///
/// The only pure, isolated contract worth a Unit Test is the query's own default property values —
/// this is real, testable behavior of production code (not a fabricated validator).
/// </summary>
public class ViewDepartmentListQueryTests
{
    [Fact]
    public void NewQuery_UsesExpectedDefaults()
    {
        var query = new ViewDepartmentListQuery();

        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Null(query.Keyword);
        Assert.Null(query.Status);
        Assert.Equal("name", query.SortBy);
        Assert.Equal("asc", query.SortDirection);
    }
}
