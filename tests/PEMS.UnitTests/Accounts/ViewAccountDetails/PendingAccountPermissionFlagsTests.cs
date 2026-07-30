using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Accounts.Queries.ViewAccountDetails;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.ViewAccountDetails;

/// <summary>
/// The <c>canResendEmailConfirmation</c> / <c>canEditPendingEmail</c> flags on the UC-98 detail
/// projection — what decides whether the detail modal offers "Gửi lại email xác nhận".
///
/// <para>
/// These are display hints, and getting them wrong is still a real defect either way: a button offered
/// and then refused with a 403 is worse than no button, and one withheld from somebody entitled to it
/// hides the only way forward for a pending account. The rule must match what the mutations enforce —
/// which is what these tests pin down, one refusal reason at a time.
/// </para>
/// </summary>
public class PendingAccountPermissionFlagsTests
{
    private const ulong Campus = Uc106TestData.CampusId;   // 1 — the fake actor's campus
    private const ulong OtherCampus = 2;
    private const ulong TargetId = 700;
    private const ulong ActorId = 900;                     // the fake Staff Leader's own id

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();   // Staff Leader, id 900, campus 1

        public Harness()
        {
            Db.Roles.AddRange(
                Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff),
                Uc106TestData.CreateRole(Uc106TestData.DepartmentRoleId, RoleCodes.Department),
                Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
            Db.SaveChanges();
        }

        public Task<ViewAccountDetailsDto> Run(ulong userId)
            => new ViewAccountDetailsQueryHandler(Db, Actor)
                .Handle(new ViewAccountDetailsQuery { UserId = userId }, CancellationToken.None);
    }

    private static User Seed(
        Harness h, ulong roleId, string? subRole, ulong campus = Campus,
        string status = UserStatuses.PendingEmailConfirmation, ulong id = TargetId)
    {
        var user = Uc106TestData.CreateUser(id, roleId, subRole, null, campus);
        user.Status = status;
        h.Db.Users.Add(user);
        h.Db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task Staff_leader_may_act_on_a_pending_ic_staff_of_their_own_campus()
    {
        var h = new Harness();
        Seed(h, Uc106TestData.StaffRoleId, UserSubRoles.Staff);

        var dto = await h.Run(TargetId);

        Assert.True(dto.CanResendEmailConfirmation);
        Assert.True(dto.CanEditPendingEmail);
    }

    [Theory]
    [InlineData(UserStatuses.Active)]
    [InlineData(UserStatuses.Inactive)]
    [InlineData(UserStatuses.Locked)]
    public async Task Nothing_is_offered_for_an_account_that_is_not_pending(string status)
    {
        // There is no link to re-issue: this account either confirmed its address or was stopped for
        // another reason entirely, and neither is fixed by mailing an activation link.
        var h = new Harness();
        Seed(h, Uc106TestData.StaffRoleId, UserSubRoles.Staff, status: status);

        var dto = await h.Run(TargetId);

        Assert.False(dto.CanResendEmailConfirmation);
        Assert.False(dto.CanEditPendingEmail);
    }

    [Fact]
    public async Task Staff_leader_may_act_on_a_pending_department_head_and_student()
    {
        var h = new Harness();
        Seed(h, Uc106TestData.DepartmentRoleId, UserSubRoles.Leader);
        Seed(h, Uc106TestData.StudentRoleId, null, id: 701);

        Assert.True((await h.Run(TargetId)).CanResendEmailConfirmation);
        Assert.True((await h.Run(701)).CanResendEmailConfirmation);
    }

    [Fact]
    public async Task Staff_leader_may_not_act_on_a_peer_leader()
    {
        // Same campus, and still out of reach: one campus leader has no authority over the other.
        var h = new Harness();
        Seed(h, Uc106TestData.StaffRoleId, UserSubRoles.Leader);

        var dto = await h.Run(TargetId);

        Assert.False(dto.CanResendEmailConfirmation);
        Assert.False(dto.CanEditPendingEmail);
    }

    [Fact]
    public async Task Staff_leader_may_not_act_on_a_department_staff_member()
    {
        // DEPARTMENT/STAFF is a department head's personnel, not the campus leader's to re-role or
        // re-address — it is absent from the three manageable shapes for that reason.
        var h = new Harness();
        Seed(h, Uc106TestData.DepartmentRoleId, UserSubRoles.Staff);

        Assert.False((await h.Run(TargetId)).CanResendEmailConfirmation);
    }

    [Fact]
    public async Task Staff_leader_may_not_act_on_an_account_in_another_campus()
    {
        var h = new Harness();
        Seed(h, Uc106TestData.StaffRoleId, UserSubRoles.Staff, campus: OtherCampus);
        h.Actor.RoleCode = RoleCodes.Admin;   // ADMIN sees every campus, so the query returns the row

        var dto = await h.Run(TargetId);

        // ADMIN is not part of the account-management scope this flow defines, so no action is offered
        // even though the row is visible.
        Assert.False(dto.CanResendEmailConfirmation);
    }

    [Fact]
    public async Task Nobody_is_offered_the_action_on_their_own_account()
    {
        var h = new Harness();
        Seed(h, Uc106TestData.StaffRoleId, UserSubRoles.Staff, id: ActorId);

        var dto = await h.Run(ActorId);

        Assert.False(dto.CanResendEmailConfirmation);
        Assert.False(dto.CanEditPendingEmail);
    }

    [Fact]
    public async Task Ho_may_act_on_a_pending_staff_leader()
    {
        var h = new Harness();
        Seed(h, Uc106TestData.StaffRoleId, UserSubRoles.Leader, campus: OtherCampus);
        h.Actor.RoleCode = RoleCodes.Ho;
        h.Actor.SubRole = null;

        var dto = await h.Run(TargetId);

        // HO is not campus-bound: an incoming leader anywhere is theirs to activate.
        Assert.True(dto.CanResendEmailConfirmation);
        Assert.True(dto.CanEditPendingEmail);
    }
}
