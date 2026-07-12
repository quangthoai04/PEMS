using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.UnitTests.Authentication;

/// <summary>
/// Unit tests for <see cref="DepartmentAccessRule"/> — the UC-106 shared eligibility rule used by
/// credentials login, Google SSO, refresh token and the session validation middleware.
/// Only DEPARTMENT-role accounts are blocked by an INACTIVE/missing department; every other role
/// keeps access even if abnormal data gives it a department_id (BR-UC106-09..11, §8).
/// </summary>
public class DepartmentAccessRuleTests
{
    [Fact]
    public void Department_Role_With_Inactive_Department_Is_Blocked()
        => Assert.True(DepartmentAccessRule.IsBlocked(RoleCodes.Department, EntityStatuses.Inactive));

    [Fact]
    public void Department_Role_With_Active_Department_Is_Allowed()
        => Assert.False(DepartmentAccessRule.IsBlocked(RoleCodes.Department, EntityStatuses.Active));

    [Fact]
    public void Department_Role_Without_Linked_Department_Is_Blocked()
        => Assert.True(DepartmentAccessRule.IsBlocked(RoleCodes.Department, null));

    [Theory]
    [InlineData(RoleCodes.Staff)]
    [InlineData(RoleCodes.Ho)]
    [InlineData(RoleCodes.Admin)]
    [InlineData(RoleCodes.Student)]
    [InlineData(RoleCodes.Visitor)]
    public void Other_Roles_Are_Never_Blocked_By_Department_Status(string roleCode)
    {
        // Even with an INACTIVE department linked by abnormal data, only DEPARTMENT is blocked.
        Assert.False(DepartmentAccessRule.IsBlocked(roleCode, EntityStatuses.Inactive));
        Assert.False(DepartmentAccessRule.IsBlocked(roleCode, null));
    }

    [Fact]
    public void Null_Role_Is_Not_Blocked_By_This_Rule()
        => Assert.False(DepartmentAccessRule.IsBlocked(null, EntityStatuses.Inactive));
}
