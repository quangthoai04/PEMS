using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Departments.Commands.AddDepartmentPersonnel;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Departments.AddDepartmentPersonnel;

/// <summary>
/// P0 #2 containment for <see cref="AddDepartmentPersonnelCommandHandler"/>: the legacy path created an
/// <c>ACTIVE</c> account with no actor authorization and a hardcoded login URL. After containment the
/// handler (a) requires an authenticated, in-scope actor (403 otherwise) and (b) refuses to create a new
/// active account directly (422) — there is NO direct-ACTIVE bypass until the shared confirmation
/// provisioning is wired in.
/// </summary>
public class AddDepartmentPersonnelContainmentTests
{
    private const ulong CampusA = 1;
    private const ulong CampusB = 2;
    private const ulong TargetDeptId = 10;
    private const ulong OtherDeptId = 11;
    private const ulong DeptLeaderId = 900;

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();
        public Mock<IEmailService> Email { get; } = new();
        public AddDepartmentPersonnelCommandHandler Handler { get; }

        public Harness()
        {
            Handler = new AddDepartmentPersonnelCommandHandler(Db, Actor, Email.Object);
        }

        public Task<AddDepartmentPersonnelResponse> Run(AddDepartmentPersonnelCommand cmd)
            => Handler.Handle(cmd, CancellationToken.None);
    }

    /// <summary>Seeds two campuses and an ACTIVE target department on campus A (head = <paramref name="headUserId"/>).</summary>
    private static Harness CreateHarness(ulong? targetDeptHeadUserId = null)
    {
        var h = new Harness();
        h.Db.Campuses.Add(Uc106TestData.CreateCampus(CampusA));
        h.Db.Campuses.Add(Uc106TestData.CreateCampus(CampusB));
        var dept = Uc106TestData.CreateGeneralDepartment(TargetDeptId, CampusA);
        dept.HeadUserId = targetDeptHeadUserId;
        h.Db.Departments.Add(dept);
        h.Db.SaveChanges();
        return h;
    }

    private static AddDepartmentPersonnelCommand Command(ulong departmentId = TargetDeptId) => new()
    {
        DepartmentId = departmentId,
        FullName = "Nguoi Moi",
        Email = "new.person@fpt.edu.vn",
        Phone = "+84900000000",
        Role = "Nhân viên",
    };

    [Fact]
    public async Task Unauthenticated_actor_is_forbidden()
    {
        var h = CreateHarness();
        h.Actor.IsAuthenticated = false;
        h.Actor.UserId = null;

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(Command()));
    }

    [Fact]
    public async Task Out_of_scope_role_is_forbidden()
    {
        var h = CreateHarness();
        // A department STAFF (not a leader, not a campus authority) may not add personnel.
        h.Actor.RoleCode = RoleCodes.Department;
        h.Actor.SubRole = UserSubRoles.Staff;
        h.Actor.PrimaryCampusId = CampusA;

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(Command()));
    }

    [Fact]
    public async Task Right_role_wrong_campus_is_forbidden()
    {
        var h = CreateHarness();
        // Staff Leader, but of a DIFFERENT campus than the target department.
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = CampusB;

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(Command()));
    }

    [Fact]
    public async Task Right_campus_wrong_department_is_forbidden()
    {
        // Target department is headed by someone else; the actor heads a different department.
        var h = CreateHarness(targetDeptHeadUserId: 999);
        h.Actor.RoleCode = RoleCodes.Department;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = CampusA;
        h.Actor.UserId = DeptLeaderId;      // heads OtherDeptId, not the target
        h.Db.Departments.Add(Uc106TestData.CreateGeneralDepartment(OtherDeptId, CampusA));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(Command()));
    }

    [Fact]
    public async Task Staff_leader_same_campus_cannot_create_active_account()
    {
        var h = CreateHarness();
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = CampusA;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(Command()));
        Assert.Equal(AddDepartmentPersonnelErrorCodes.RequiresConfirmationProvisioning, ex.ErrorCode);
        // The unsafe direct-ACTIVE insert never happened.
        Assert.DoesNotContain(h.Db.Users, u => u.Email == "new.person@fpt.edu.vn");
        h.Email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ho_same_campus_cannot_create_active_account()
    {
        var h = CreateHarness();
        h.Actor.RoleCode = RoleCodes.Ho;
        h.Actor.SubRole = null;
        h.Actor.PrimaryCampusId = CampusA;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(Command()));
        Assert.Equal(AddDepartmentPersonnelErrorCodes.RequiresConfirmationProvisioning, ex.ErrorCode);
        Assert.DoesNotContain(h.Db.Users, u => u.Email == "new.person@fpt.edu.vn");
    }

    [Fact]
    public async Task Department_head_of_target_is_authorized_but_blocked_by_containment()
    {
        // Proves authorization ALLOWS the department's own head — the 422 is the containment block,
        // not an authorization failure.
        var h = CreateHarness(targetDeptHeadUserId: DeptLeaderId);
        h.Actor.RoleCode = RoleCodes.Department;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = CampusA;
        h.Actor.UserId = DeptLeaderId;

        await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(Command()));
    }

    [Fact]
    public async Task Missing_or_inactive_department_returns_unsuccessful_without_creating_anything()
    {
        var h = CreateHarness();
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = CampusA;

        var res = await h.Run(Command(departmentId: 9999));   // not seeded

        Assert.False(res.Success);
        Assert.DoesNotContain(h.Db.Users, u => u.Email == "new.person@fpt.edu.vn");
    }
}
