using PEMS.Application.Departments.Queries.ViewDepartmentDetails;
using Xunit;

namespace PEMS.UnitTests.Departments.ViewDepartmentDetails;

/// <summary>
/// Unit tests for UC-105 View Department Details.
///
/// Source-confirmed facts: there is no <c>ViewDepartmentDetailsQueryValidator</c> (no FluentValidation
/// validator exists for this query) and <see cref="ViewDepartmentDetailsQueryHandler"/> requires a real
/// <c>IApplicationDbContext</c>/<c>ICurrentUserService</c> to do anything meaningful (campus scope guard,
/// lookup/projection, 404/403 branching, IC-flag computation). None of that can be verified in isolation
/// without a real MySQL connection, so that behavior is covered by ViewDepartmentDetailsApiTests
/// (Integration Test) instead.
///
/// The only pure, isolated contract worth a Unit Test is the query's own default property value —
/// this is real, testable behavior of production code (not a fabricated validator).
/// </summary>
public class ViewDepartmentDetailsQueryTests
{
    [Fact]
    public void NewQuery_DefaultsDepartmentIdToZero()
    {
        var query = new ViewDepartmentDetailsQuery();

        Assert.Equal(0UL, query.DepartmentId);
    }
}
