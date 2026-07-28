using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.DepartmentLeaderPersonnel;

/// <summary>
/// The authorization gate behind every /api/department-leader route (spec §4/§7).
///
/// The point these tests protect: holding a DEPARTMENT+LEADER token is NOT sufficient. The service
/// re-reads the account and the department on every request, so a demoted leader, a deactivated
/// account or a department that has since been handed to someone else is refused even though the
/// token still says LEADER.
/// </summary>
public class DepartmentLeaderScopeServiceTests
{
    private static Task<DepartmentLeaderScope> Run(DepartmentLeaderTestHarness h)
        => h.Scope.EnsureCurrentUserIsActualDepartmentLeaderAsync(CancellationToken.None);

    [Fact]
    public async Task Seated_leader_resolves_their_own_department()
    {
        var h = DepartmentLeaderTestHarness.Create();

        var scope = await Run(h);

        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, scope.ActorUserId);
        Assert.Equal(DepartmentLeaderTestHarness.DepartmentId, scope.DepartmentId);
        Assert.Equal(DepartmentLeaderTestHarness.CampusId, scope.CampusId);
        Assert.Equal("Phòng ban 10", scope.DepartmentName);
    }

    [Fact]
    public async Task Anonymous_caller_is_rejected_with_401()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.Actor.IsAuthenticated = false;
        h.Actor.UserId = null;

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal(DepartmentLeaderErrorCodes.DepartmentLeaderRequired, ex.ErrorCode);
    }

    [Fact]
    public async Task Department_staff_is_forbidden()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.Actor.SubRole = UserSubRoles.Staff;

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(DepartmentLeaderErrorCodes.DepartmentLeaderRequired, ex.ErrorCode);
    }

    [Fact]
    public async Task Staff_leader_is_forbidden()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Ho_is_forbidden()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.Actor.RoleCode = RoleCodes.Ho;
        h.Actor.SubRole = null;

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(403, ex.StatusCode);
    }

    /// <summary>
    /// The case a JWT-only check cannot catch: the token still claims LEADER, but the database says
    /// the account was demoted to STAFF. Without the DB re-read this caller would keep full access
    /// until their token expired.
    /// </summary>
    [Fact]
    public async Task Token_says_leader_but_database_says_staff_is_forbidden()
    {
        var h = DepartmentLeaderTestHarness.Create();
        var leader = h.GetUser(DepartmentLeaderTestHarness.LeaderId);
        leader.SubRole = UserSubRoles.Staff;
        h.Db.SaveChanges();
        h.Detach();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(DepartmentLeaderErrorCodes.DepartmentLeaderRequired, ex.ErrorCode);
    }

    /// <summary>
    /// The leadership-transfer case: the account is still DEPARTMENT+LEADER, but the department's
    /// head_user_id now points at somebody else. Access must stop immediately.
    /// </summary>
    [Fact]
    public async Task Leader_who_is_no_longer_head_is_forbidden()
    {
        var h = DepartmentLeaderTestHarness.Create();
        var department = h.GetDepartment();
        department.HeadUserId = 999;
        h.Db.SaveChanges();
        h.Detach();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(DepartmentLeaderErrorCodes.DepartmentScopeForbidden, ex.ErrorCode);
    }

    [Fact]
    public async Task Inactive_leader_account_is_forbidden()
    {
        var h = DepartmentLeaderTestHarness.Create();
        var leader = h.GetUser(DepartmentLeaderTestHarness.LeaderId);
        leader.Status = UserStatuses.Inactive;
        h.Db.SaveChanges();
        h.Detach();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Leader_without_department_gets_context_missing()
    {
        var h = DepartmentLeaderTestHarness.Create();
        var leader = h.GetUser(DepartmentLeaderTestHarness.LeaderId);
        leader.DepartmentId = null;
        h.Db.SaveChanges();
        h.Detach();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(422, ex.StatusCode);
        Assert.Equal(DepartmentLeaderErrorCodes.DepartmentContextMissing, ex.ErrorCode);
    }

    [Fact]
    public async Task Ic_department_is_out_of_scope_for_this_flow()
    {
        var h = DepartmentLeaderTestHarness.Create();
        var department = h.GetDepartment();
        department.DepartmentType = DepartmentTypes.Ic;
        h.Db.SaveChanges();
        h.Detach();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(DepartmentLeaderErrorCodes.DepartmentScopeForbidden, ex.ErrorCode);
    }

    [Fact]
    public async Task Inactive_department_is_rejected()
    {
        var h = DepartmentLeaderTestHarness.Create();
        var department = h.GetDepartment();
        department.Status = EntityStatuses.Inactive;
        h.Db.SaveChanges();
        h.Detach();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(422, ex.StatusCode);
        Assert.Equal(DepartmentLeaderErrorCodes.DepartmentNotActive, ex.ErrorCode);
    }

    [Fact]
    public async Task Inactive_campus_is_rejected()
    {
        var h = DepartmentLeaderTestHarness.Create();
        var campus = h.Db.Campuses.Find(DepartmentLeaderTestHarness.CampusId)!;
        campus.Status = EntityStatuses.Inactive;
        h.Db.SaveChanges();
        h.Detach();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(422, ex.StatusCode);
        Assert.Equal(DepartmentLeaderErrorCodes.DepartmentNotActive, ex.ErrorCode);
    }

    // ── Target membership ────────────────────────────────────────────────────

    [Fact]
    public async Task Scoped_personnel_lookup_returns_a_member_of_the_department()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff();
        var scope = await Run(h);

        var target = await h.Scope.GetScopedPersonnelAsync(
            scope, DepartmentLeaderTestHarness.StaffId, CancellationToken.None);

        Assert.Equal(DepartmentLeaderTestHarness.StaffId, target.UserId);
    }

    /// <summary>
    /// A member of ANOTHER department answers 404 — the same response a non-existent id gets — so the
    /// endpoint cannot be walked to discover which user ids exist elsewhere.
    /// </summary>
    [Fact]
    public async Task Personnel_of_another_department_answers_the_same_404_as_a_missing_id()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddOtherDepartment();
        h.AddStaff(
            userId: 950,
            departmentId: DepartmentLeaderTestHarness.OtherDepartmentId,
            campusId: DepartmentLeaderTestHarness.OtherCampusId);

        var scope = await Run(h);

        var foreign = await Assert.ThrowsAsync<AuthBusinessException>(
            () => h.Scope.GetScopedPersonnelAsync(scope, 950, CancellationToken.None));
        var missing = await Assert.ThrowsAsync<AuthBusinessException>(
            () => h.Scope.GetScopedPersonnelAsync(scope, 123456, CancellationToken.None));

        Assert.Equal(404, foreign.StatusCode);
        Assert.Equal(DepartmentLeaderErrorCodes.PersonnelNotFound, foreign.ErrorCode);
        // Identical shape — that identity is the anti-enumeration property.
        Assert.Equal(missing.StatusCode, foreign.StatusCode);
        Assert.Equal(missing.ErrorCode, foreign.ErrorCode);
        Assert.Equal(missing.Message, foreign.Message);
    }

    [Fact]
    public async Task Non_department_role_in_the_same_department_is_out_of_scope()
    {
        var h = DepartmentLeaderTestHarness.Create();

        // A STAFF-role account sitting in the department is not "department personnel".
        var outsider = Uc106TestData.CreateUser(
            960, Uc106TestData.StaffRoleId, UserSubRoles.Staff,
            DepartmentLeaderTestHarness.DepartmentId, DepartmentLeaderTestHarness.CampusId);
        h.Db.Users.Add(outsider);
        h.Db.SaveChanges();

        var scope = await Run(h);

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(
            () => h.Scope.GetScopedPersonnelAsync(scope, 960, CancellationToken.None));
        Assert.Equal(404, ex.StatusCode);
    }
}
