using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Accounts.Queries.ViewAccountList;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.ViewAccountList;

/// <summary>
/// The capability flags the account list hands the UI for an ADMIN caller — ADMIN_ACCOUNT_MANAGEMENT
/// spec §23/§30/§43.4.
///
/// <para>
/// Two things have to hold at once, and they pull in opposite directions: ADMIN keeps reading every
/// account on every campus (nothing about narrowing its powers may narrow its VIEW), while every
/// business capability it used to carry now answers false and the two security ones take their
/// place. The security flags are asserted against the same state matrix the handler enforces — a
/// flag that disagrees with the handler produces either a button that 403s or a missing button that
/// hides ADMIN's only action on that account.
/// </para>
/// </summary>
public class AdminAccountListCapabilityTests
{
    private const ulong Campus = Uc106TestData.CampusId;   // 1
    private const ulong OtherCampus = 2;
    private const ulong AdminRoleId = 1;
    private const ulong HoRoleId = 2;
    private const ulong AdminActorId = 700;

    private static readonly Mock<IRoleAccessPolicy> AllowAll = BuildPolicy();

    private static Mock<IRoleAccessPolicy> BuildPolicy()
    {
        var policy = new Mock<IRoleAccessPolicy>();
        policy.Setup(p => p.CanAccessAccountManagement(It.IsAny<ICurrentUserService>())).Returns(true);
        return policy;
    }

    /// <summary>
    /// Four accounts on ANOTHER campus, one per status, plus the ADMIN's own row on its own campus —
    /// so "ADMIN sees everything" and "ADMIN cannot act on itself" are both exercised by one fixture.
    /// </summary>
    private static (TestApplicationDbContext Db, FakeCurrentUserService Actor) CreateFixture()
    {
        var db = TestApplicationDbContext.Create();
        db.Campuses.AddRange(Uc106TestData.CreateCampus(), Uc106TestData.CreateCampus(OtherCampus));
        db.Roles.AddRange(
            Uc106TestData.CreateRole(AdminRoleId, RoleCodes.Admin),
            Uc106TestData.CreateRole(HoRoleId, RoleCodes.Ho),
            Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff),
            Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));

        db.Users.Add(Uc106TestData.CreateUser(AdminActorId, AdminRoleId, null, null, Campus));
        db.Users.Add(Row(1001, UserStatuses.Active));
        db.Users.Add(Row(1002, UserStatuses.Locked));
        db.Users.Add(Row(1003, UserStatuses.Inactive));
        db.Users.Add(Row(1004, UserStatuses.PendingEmailConfirmation));
        db.SaveChanges();

        var actor = new FakeCurrentUserService
        {
            UserId = AdminActorId, RoleId = AdminRoleId, RoleCode = RoleCodes.Admin,
            SubRole = null, PrimaryCampusId = Campus,
        };
        return (db, actor);
    }

    private static User Row(ulong id, string status)
    {
        var u = Uc106TestData.CreateUser(id, Uc106TestData.StudentRoleId, null, null, OtherCampus);
        u.Status = status;
        return u;
    }

    private static async Task<Dictionary<ulong, AccountListItemDto>> ListAsync(
        TestApplicationDbContext db, ICurrentUserService actor)
    {
        var handler = new ViewAccountListQueryHandler(db, actor, AllowAll.Object);
        var page = await handler.Handle(new ViewAccountListQuery { PageSize = 100 }, CancellationToken.None);
        return page.Items.ToDictionary(i => i.UserId);
    }

    [Fact]
    public async Task AdminStillReadsEveryCampus()
    {
        var (db, actor) = CreateFixture();

        var rows = await ListAsync(db, actor);

        // Global read is the half of ADMIN's role that did NOT change.
        Assert.Equal(5, rows.Count);
        Assert.All(rows.Values, r => Assert.True(r.CanViewDetails));
        Assert.Contains(rows.Values, r => r.CampusId == OtherCampus);
    }

    [Fact]
    public async Task AdminHasNoBusinessCapabilityOnAnyRow()
    {
        var (db, actor) = CreateFixture();

        var rows = await ListAsync(db, actor);

        Assert.All(rows.Values, r =>
        {
            Assert.False(r.CanUpdateRole);
            Assert.False(r.CanEditBasicInfo);
            Assert.False(r.CanManageStatus);
            Assert.Equal("ADMIN_SECURITY_ONLY", r.HideStatusToggleReason);
        });
    }

    [Fact]
    public async Task ActiveRowOffersLockOnly_AndLockedRowOffersUnlockOnly()
    {
        var (db, actor) = CreateFixture();

        var rows = await ListAsync(db, actor);

        Assert.True(rows[1001].CanSecurityLock);
        Assert.False(rows[1001].CanSecurityUnlock);
        Assert.Null(rows[1001].SecurityActionDisabledReason);

        Assert.False(rows[1002].CanSecurityLock);
        Assert.True(rows[1002].CanSecurityUnlock);
        Assert.Null(rows[1002].SecurityActionDisabledReason);
    }

    [Theory]
    [InlineData(1003ul, "ACCOUNT_INACTIVE")]
    [InlineData(1004ul, "ACCOUNT_PENDING_EMAIL_CONFIRMATION")]
    public async Task NonSecurityStatesOfferNothing_AndSayWhy(ulong userId, string expectedReason)
    {
        var (db, actor) = CreateFixture();

        var rows = await ListAsync(db, actor);

        Assert.False(rows[userId].CanSecurityLock);
        Assert.False(rows[userId].CanSecurityUnlock);
        Assert.Equal(expectedReason, rows[userId].SecurityActionDisabledReason);
    }

    [Fact]
    public async Task AdminsOwnRowOffersNoSecurityAction()
    {
        var (db, actor) = CreateFixture();

        var rows = await ListAsync(db, actor);

        var self = rows[AdminActorId];
        Assert.True(self.IsCurrentUser);
        Assert.False(self.CanSecurityLock);
        Assert.False(self.CanSecurityUnlock);
        Assert.Equal("SELF_ACCOUNT", self.SecurityActionDisabledReason);
    }

    [Fact]
    public async Task StaffLeaderKeepsItsBusinessToggleAndGetsNoSecurityAction()
    {
        // Regression: narrowing ADMIN must not hand security control to anybody else, nor take the
        // ACTIVE↔INACTIVE toggle away from the role that owns it.
        var (db, _) = CreateFixture();
        var leader = Uc106TestData.CreateUser(900, Uc106TestData.StaffRoleId, UserSubRoles.Leader, null, OtherCampus);
        db.Users.Add(leader);
        db.SaveChanges();

        var actor = new FakeCurrentUserService
        {
            UserId = 900, RoleId = Uc106TestData.StaffRoleId, RoleCode = RoleCodes.Staff,
            SubRole = UserSubRoles.Leader, PrimaryCampusId = OtherCampus,
        };

        var rows = await ListAsync(db, actor);

        Assert.True(rows[1001].CanManageStatus);
        Assert.All(rows.Values, r =>
        {
            Assert.False(r.CanSecurityLock);
            Assert.False(r.CanSecurityUnlock);
            Assert.Null(r.SecurityActionDisabledReason);
        });
    }
}
