using PEMS.Application.Authentication.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Users;
using Xunit;

namespace PEMS.UnitTests.Authentication;

/// <summary>
/// Patch 7 (P7.1) — <see cref="AuthUserMapper.ToDto"/> is used by login (<c>AuthResultBuilder</c>),
/// <c>GetCurrentUserQueryHandler</c> and <c>RefreshTokenCommandHandler</c>: every ordinary
/// authenticated request. Before this patch it called <c>EffectiveRole.Resolve</c> unguarded, so an
/// account whose (role_code, sub_role) pair is not a valid combination — a data defect every OTHER
/// caller of <c>EffectiveRole.Resolve</c> already fails closed on with 403
/// (<c>RoleAuthorizeAttribute</c>, <c>RoleAccessPolicy</c>, <c>DepartmentPersonnelManagementScope</c>)
/// — instead let a raw <c>InvalidOperationException</c> fall through to a generic 500. A user with
/// this exact account defect could not even sign in, see their own profile, or refresh a session to
/// find out why.
/// </summary>
public class AuthUserMapperTests
{
    private static User UserWith(string roleCode, string? subRole) => new()
    {
        UserId = 1,
        FullName = "Test User",
        Email = "test@example.com",
        Status = "ACTIVE",
        SubRole = subRole,
        Role = new Role { RoleId = 1, RoleCode = roleCode, Name = roleCode },
    };

    [Fact]
    public void An_invalid_role_subrole_combination_fails_closed_as_Forbidden_not_a_raw_500()
    {
        // STAFF with no sub_role — a real, previously-seen data defect (EffectiveRole.Resolve's own
        // switch has no case for it), not a hypothetical.
        var user = UserWith("STAFF", null);

        var ex = Assert.Throws<ForbiddenException>(() => AuthUserMapper.ToDto(user));
        Assert.Equal(AuthErrorCodes.InvalidRoleCombination, ex.ErrorCode);
    }

    [Theory]
    [InlineData("ADMIN", null, EffectiveRole.Admin)]
    [InlineData("HO", null, EffectiveRole.Ho)]
    [InlineData("STAFF", "LEADER", EffectiveRole.StaffLeader)]
    [InlineData("STAFF", "STAFF", EffectiveRole.Staff)]
    [InlineData("DEPARTMENT", "LEADER", EffectiveRole.DepartmentLead)]
    [InlineData("DEPARTMENT", "STAFF", EffectiveRole.Department)]
    [InlineData("STUDENT", null, EffectiveRole.Student)]
    [InlineData("VISITOR", null, EffectiveRole.Visitor)]
    public void Every_valid_combination_still_maps_through_unaffected(string roleCode, string? subRole, string expected)
    {
        var dto = AuthUserMapper.ToDto(UserWith(roleCode, subRole));
        Assert.Equal(expected, dto.EffectiveRole);
    }
}
