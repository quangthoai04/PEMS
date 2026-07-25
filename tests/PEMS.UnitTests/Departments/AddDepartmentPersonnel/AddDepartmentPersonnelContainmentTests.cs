using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Departments.Commands.AddDepartmentPersonnel;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Departments.AddDepartmentPersonnel;

/// <summary>
/// P0 #2 for <see cref="AddDepartmentPersonnelCommandHandler"/>: an authenticated, in-scope actor (HO /
/// the campus Staff Leader / the department's own Leader) provisions new department personnel through the
/// SHARED confirmation flow — the account is created PENDING_EMAIL_CONFIRMATION (never a direct ACTIVE
/// bypass), a department-head slot is reserved for a Leader, and a confirmation link is issued. Out-of-scope
/// actors are refused with 403; there is no hardcoded URL.
/// </summary>
public class AddDepartmentPersonnelContainmentTests
{
    private const ulong CampusA = 1;
    private const ulong CampusB = 2;
    private const ulong TargetDeptId = 10;
    private const ulong OtherDeptId = 11;
    private const ulong DeptLeaderId = 900;   // == FakeCurrentUserService default UserId
    private const string NewEmail = "new.person@fpt.edu.vn";

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();   // Staff Leader, campus 1, id 900
        public Mock<IEmailService> Email { get; } = new();
        public Mock<IAccountEmailConfirmationService> Confirmations { get; } = new();
        public FakeDateTimeService Clock { get; } = new();
        public AddDepartmentPersonnelCommandHandler Handler { get; }

        public Harness()
        {
            Email.Setup(e => e.TrySendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(EmailDeliveryResult.Sent());
            Confirmations.Setup(c => c.IssuePendingAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("raw");
            Confirmations.Setup(c => c.BuildConfirmUrl(It.IsAny<string>())).Returns("http://x/confirm-email?token=raw");
            Handler = new AddDepartmentPersonnelCommandHandler(Db, Actor, Email.Object, Confirmations.Object, Clock);
        }

        public Task<AddDepartmentPersonnelResponse> Run(AddDepartmentPersonnelCommand cmd) => Handler.Handle(cmd, CancellationToken.None);
    }

    private static Harness CreateHarness(ulong? targetDeptHeadUserId = null)
    {
        var h = new Harness();
        h.Db.Campuses.Add(Uc106TestData.CreateCampus(CampusA));
        h.Db.Campuses.Add(Uc106TestData.CreateCampus(CampusB));
        h.Db.Roles.Add(Uc106TestData.CreateRole(Uc106TestData.DepartmentRoleId, RoleCodes.Department));
        var dept = Uc106TestData.CreateGeneralDepartment(TargetDeptId, CampusA);
        dept.HeadUserId = targetDeptHeadUserId;
        h.Db.Departments.Add(dept);
        h.Db.SaveChanges();
        return h;
    }

    private static AddDepartmentPersonnelCommand Command(ulong departmentId = TargetDeptId, string role = "Nhân viên") => new()
    {
        DepartmentId = departmentId,
        FullName = "Nguoi Moi",
        Email = NewEmail,
        Phone = "+84900000000",
        Role = role,
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
        h.Actor.RoleCode = RoleCodes.Department;
        h.Actor.SubRole = UserSubRoles.Staff;   // a department staff, not a leader/authority

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(Command()));
    }

    [Fact]
    public async Task Right_role_wrong_campus_is_forbidden()
    {
        var h = CreateHarness();
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = CampusB;   // Staff Leader of a different campus

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(Command()));
    }

    [Fact]
    public async Task Right_campus_wrong_department_is_forbidden()
    {
        var h = CreateHarness(targetDeptHeadUserId: 999);
        h.Actor.RoleCode = RoleCodes.Department;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = CampusA;
        h.Actor.UserId = DeptLeaderId;   // heads OtherDeptId, not the target
        h.Db.Departments.Add(Uc106TestData.CreateGeneralDepartment(OtherDeptId, CampusA));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(Command()));
    }

    [Fact]
    public async Task Staff_leader_creates_department_staff_as_pending_not_active()
    {
        var h = CreateHarness();
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = CampusA;

        var res = await h.Run(Command());

        Assert.True(res.Success);
        Assert.Equal("SENT", res.EmailNotificationStatus);
        var user = await h.Db.Users.SingleAsync(u => u.Email == NewEmail);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, user.Status);   // NOT active — no direct bypass
        Assert.Equal(UserSubRoles.Staff, user.SubRole);
        h.Confirmations.Verify(c => c.IssuePendingAsync(user.UserId, NewEmail, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Staff_leader_creating_a_leader_reserves_the_department_head_slot()
    {
        var h = CreateHarness();   // target department has no head
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = CampusA;

        var res = await h.Run(Command(role: "Trưởng phòng"));

        Assert.True(res.Success);
        var user = await h.Db.Users.SingleAsync(u => u.Email == NewEmail);
        Assert.Equal(UserSubRoles.Leader, user.SubRole);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, user.Status);
        Assert.Equal(user.UserId, (await h.Db.Departments.SingleAsync(d => d.DepartmentId == TargetDeptId)).HeadUserId);
    }

    [Fact]
    public async Task Ho_same_campus_creates_pending_personnel()
    {
        var h = CreateHarness();
        h.Actor.RoleCode = RoleCodes.Ho;
        h.Actor.SubRole = null;
        h.Actor.PrimaryCampusId = CampusA;

        var res = await h.Run(Command());

        Assert.True(res.Success);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, (await h.Db.Users.SingleAsync(u => u.Email == NewEmail)).Status);
    }

    [Fact]
    public async Task Department_head_of_target_creates_pending_staff()
    {
        var h = CreateHarness(targetDeptHeadUserId: DeptLeaderId);
        h.Actor.RoleCode = RoleCodes.Department;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = CampusA;
        h.Actor.UserId = DeptLeaderId;

        var res = await h.Run(Command());

        Assert.True(res.Success);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, (await h.Db.Users.SingleAsync(u => u.Email == NewEmail)).Status);
    }

    [Fact]
    public async Task Duplicate_email_conflicts()
    {
        var h = CreateHarness();
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = CampusA;
        var existing = Uc106TestData.CreateUser(555, Uc106TestData.StudentRoleId, null, CampusA);
        existing.Email = NewEmail;
        h.Db.Users.Add(existing);
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => h.Run(Command()));
    }

    [Fact]
    public async Task Missing_or_inactive_department_returns_unsuccessful_without_creating_anything()
    {
        var h = CreateHarness();
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = CampusA;

        var res = await h.Run(Command(departmentId: 9999));

        Assert.False(res.Success);
        Assert.DoesNotContain(h.Db.Users, u => u.Email == NewEmail);
    }
}
