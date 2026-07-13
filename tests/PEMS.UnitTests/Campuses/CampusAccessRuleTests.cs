using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.Campuses;

/// <summary>
/// UC-86 campus force-logout gate (doc 08 §18): STAFF/DEPARTMENT/STUDENT accounts are blocked
/// once their primary campus is not ACTIVE; HO/ADMIN/VISITOR are never blocked by campus status.
/// </summary>
public class CampusAccessRuleTests
{
    [Theory]
    [InlineData(RoleCodes.Staff)]
    [InlineData(RoleCodes.Department)]
    [InlineData(RoleCodes.Student)]
    public void CampusScopedRole_WithInactiveCampus_IsBlocked(string roleCode)
    {
        Assert.True(CampusAccessRule.IsBlocked(roleCode, EntityStatuses.Inactive));
        Assert.True(CampusAccessRule.IsBlocked(roleCode, null));
    }

    [Theory]
    [InlineData(RoleCodes.Staff)]
    [InlineData(RoleCodes.Department)]
    [InlineData(RoleCodes.Student)]
    public void CampusScopedRole_WithActiveCampus_IsNotBlocked(string roleCode)
    {
        Assert.False(CampusAccessRule.IsBlocked(roleCode, EntityStatuses.Active));
    }

    [Theory]
    [InlineData(RoleCodes.Ho)]
    [InlineData(RoleCodes.Admin)]
    [InlineData(RoleCodes.Visitor)]
    public void NonCampusScopedRole_IsNeverBlocked_EvenWhenCampusInactive(string roleCode)
    {
        Assert.False(CampusAccessRule.IsBlocked(roleCode, EntityStatuses.Inactive));
        Assert.False(CampusAccessRule.IsBlocked(roleCode, null));
    }
}
