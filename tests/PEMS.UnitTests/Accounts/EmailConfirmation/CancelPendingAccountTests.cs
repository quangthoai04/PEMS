using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Commands.CancelPendingAccount;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.EmailConfirmation;

/// <summary>
/// P0 #1 cancel: cancelling a pending account releases any Head slot it reserved (campus IC-head /
/// department-head pointer cleared), cancels its confirmation token(s) and deactivates it — so a
/// reservation is never held forever. Authorized to HO / the account's Staff Leader; non-pending accounts
/// are refused.
/// </summary>
public class CancelPendingAccountTests
{
    private const ulong CampusA = 1;
    private const ulong TargetUserId = 700;

    private static (CancelPendingAccountCommandHandler handler, TestApplicationDbContext db, FakeCurrentUserService actor) Build()
    {
        var db = TestApplicationDbContext.Create();
        var actor = new FakeCurrentUserService();   // Staff Leader, campus 1
        var handler = new CancelPendingAccountCommandHandler(db, actor, new FakeDateTimeService());
        return (handler, db, actor);
    }

    /// <summary>
    /// The account these tests cancel is a pending STAFF/LEADER — a campus's incoming IC head. That is
    /// an HO's target, not another Staff Leader's: a leader has no authority over a peer, so the actor
    /// is promoted here rather than the scope rule being relaxed for the test's convenience.
    /// </summary>
    private static (CancelPendingAccountCommandHandler handler, TestApplicationDbContext db, FakeCurrentUserService actor) BuildForHo()
    {
        var (handler, db, actor) = Build();
        actor.RoleCode = RoleCodes.Ho;
        actor.SubRole = null;
        return (handler, db, actor);
    }

    private static User SeedPending(TestApplicationDbContext db, ulong campus = CampusA)
    {
        var user = Uc106TestData.CreateUser(TargetUserId, Uc106TestData.StaffRoleId, UserSubRoles.Leader, campus);
        user.Email = "pending.head@fpt.edu.vn";
        user.Status = UserStatuses.PendingEmailConfirmation;
        db.Users.Add(user);
        db.AccountEmailConfirmations.Add(new AccountEmailConfirmation
        {
            UserId = TargetUserId,
            TargetEmail = user.Email,
            TokenHash = new string('a', 64),
            Status = AccountEmailConfirmationStatuses.Pending,
            ExpiresAt = new System.DateTime(2026, 8, 1),
            CreatedAt = new System.DateTime(2026, 7, 12),
        });
        return user;
    }

    [Fact]
    public async Task Cancel_releases_the_reserved_campus_ic_head_slot()
    {
        var (handler, db, _) = BuildForHo();
        SeedPending(db);
        var campus = Uc106TestData.CreateCampus(CampusA);
        campus.IcHeadUserId = TargetUserId;         // reserved by the pending user
        db.Campuses.Add(campus);
        db.SaveChanges();

        var res = await handler.Handle(new CancelPendingAccountCommand { UserId = TargetUserId }, CancellationToken.None);

        Assert.True(res.Success);
        Assert.True(res.ReleasedHeadReservation);
        Assert.Null((await db.Campuses.SingleAsync()).IcHeadUserId);                 // slot freed
        Assert.Equal(UserStatuses.Inactive, (await db.Users.SingleAsync()).Status);  // deactivated
        Assert.Equal(AccountEmailConfirmationStatuses.Cancelled, (await db.AccountEmailConfirmations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Cancel_releases_the_reserved_department_head_slot()
    {
        var (handler, db, _) = BuildForHo();
        SeedPending(db);
        var dept = Uc106TestData.CreateGeneralDepartment(10, CampusA);
        dept.HeadUserId = TargetUserId;
        db.Departments.Add(dept);
        db.SaveChanges();

        var res = await handler.Handle(new CancelPendingAccountCommand { UserId = TargetUserId }, CancellationToken.None);

        Assert.True(res.ReleasedHeadReservation);
        Assert.Null((await db.Departments.SingleAsync()).HeadUserId);
    }

    [Fact]
    public async Task Unauthorized_actor_cannot_cancel()
    {
        var (handler, db, actor) = Build();
        SeedPending(db);
        db.SaveChanges();
        actor.PrimaryCampusId = 2;   // Staff Leader of a different campus

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new CancelPendingAccountCommand { UserId = TargetUserId }, CancellationToken.None));
    }

    [Fact]
    public async Task A_staff_leader_cannot_cancel_another_staff_leader_on_their_own_campus()
    {
        // Sharing a campus is not authority over a peer: the seeded target is a pending STAFF/LEADER,
        // and cancelling it would let one campus leader retire the other's account.
        var (handler, db, _) = Build();
        SeedPending(db);
        db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new CancelPendingAccountCommand { UserId = TargetUserId }, CancellationToken.None));
    }

    [Fact]
    public async Task Cannot_cancel_an_already_active_account()
    {
        var (handler, db, _) = BuildForHo();
        var user = SeedPending(db);
        user.Status = UserStatuses.Active;
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => handler.Handle(new CancelPendingAccountCommand { UserId = TargetUserId }, CancellationToken.None));
        Assert.Equal("ACCOUNT_NOT_PENDING", ex.ErrorCode);
    }
}
