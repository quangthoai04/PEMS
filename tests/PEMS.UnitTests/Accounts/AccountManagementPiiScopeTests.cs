using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Accounts.Queries.ViewAccountDetails;
using PEMS.Application.Accounts.Queries.ViewAccountList;
using PEMS.Application.Accounts.Queries.ViewAccountStatistics;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Accounts;

/// <summary>
/// SEC-02/03/04 remediation. <c>ViewAccountDetails</c>, the account list/search executor and
/// <c>ViewAccountStatistics</c> each had a "campus-scoped caller" branch reachable by ANY
/// authenticated same-campus user — a Student or Department caller got full PII (or account-count
/// statistics) for their whole campus. All three now require Admin/HO/Staff Leader
/// (<c>IRoleAccessPolicy.CanAccessAccountManagement</c>); everyone else is refused.
/// </summary>
public class AccountManagementPiiScopeTests
{
    private const ulong Campus = Uc106TestData.CampusId; // 1
    private const ulong TargetId = 700;

    private static FakeCurrentUserService StudentActor(ulong id = 800) => new()
    {
        UserId = id,
        RoleCode = RoleCodes.Student,
        SubRole = null,
        PrimaryCampusId = Campus,
    };

    private static TestApplicationDbContext SeedDb()
    {
        var db = TestApplicationDbContext.Create();
        db.Roles.AddRange(
            Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff),
            Uc106TestData.CreateRole(Uc106TestData.DepartmentRoleId, RoleCodes.Department),
            Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
        db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, null, Campus));
        db.SaveChanges();
        return db;
    }

    // ── SEC-02: ViewAccountDetails ──────────────────────────────────────────

    [Fact]
    public async Task ViewAccountDetails_SameCampusStudent_IsRefused()
    {
        var db = SeedDb();
        var handler = new ViewAccountDetailsQueryHandler(db, StudentActor());

        // NotFoundException, not ForbiddenException — the established anti-enumeration convention
        // for this handler (a forbidden target must not be distinguishable from a missing one).
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new ViewAccountDetailsQuery { UserId = TargetId }, CancellationToken.None));
    }

    [Fact]
    public async Task ViewAccountDetails_SameCampusStaffLeader_StillSucceeds()
    {
        var db = SeedDb();
        var actor = new FakeCurrentUserService
        {
            UserId = 900, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader, PrimaryCampusId = Campus,
        };
        var handler = new ViewAccountDetailsQueryHandler(db, actor);

        var dto = await handler.Handle(new ViewAccountDetailsQuery { UserId = TargetId }, CancellationToken.None);

        Assert.Equal(TargetId, dto.UserId);
    }

    // ── SEC-03: account list / search ───────────────────────────────────────

    [Fact]
    public async Task ViewAccountList_SameCampusStudent_IsRefused()
    {
        var db = SeedDb();
        var handler = new ViewAccountListQueryHandler(db, StudentActor(), new RoleAccessPolicy());

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => handler.Handle(
            new ViewAccountListQuery(), CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ViewAccountList_SameCampusStaffLeader_StillSucceeds()
    {
        var db = SeedDb();
        var actor = new FakeCurrentUserService
        {
            UserId = 900, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader, PrimaryCampusId = Campus,
        };
        var handler = new ViewAccountListQueryHandler(db, actor, new RoleAccessPolicy());

        var result = await handler.Handle(new ViewAccountListQuery(), CancellationToken.None);

        Assert.Contains(result.Items, i => i.UserId == TargetId);
    }

    [Fact]
    public async Task ViewAccountList_Ho_StillSucceeds()
    {
        var db = SeedDb();
        var actor = new FakeCurrentUserService { UserId = 950, RoleCode = RoleCodes.Ho, SubRole = null, PrimaryCampusId = null };
        var handler = new ViewAccountListQueryHandler(db, actor, new RoleAccessPolicy());

        // HO scope is HO + Staff Leader rows only — the seeded target is STAFF/STAFF, so the call
        // must succeed (not throw) even though it legitimately returns zero rows.
        var result = await handler.Handle(new ViewAccountListQuery(), CancellationToken.None);

        Assert.NotNull(result);
    }

    // ── SEC-04: account statistics ──────────────────────────────────────────

    [Fact]
    public async Task ViewAccountStatistics_SameCampusStudent_IsRefused()
    {
        var db = SeedDb();
        var handler = new ViewAccountStatisticsQueryHandler(db, StudentActor());

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => handler.Handle(
            new ViewAccountStatisticsQuery(), CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ViewAccountStatistics_SameCampusStaffLeader_StillSucceeds()
    {
        var db = SeedDb();
        var actor = new FakeCurrentUserService
        {
            UserId = 900, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader, PrimaryCampusId = Campus,
        };
        var handler = new ViewAccountStatisticsQueryHandler(db, actor);

        var dto = await handler.Handle(new ViewAccountStatisticsQuery(), CancellationToken.None);

        Assert.Equal(1, dto.TotalAccounts); // the seeded STAFF/STAFF target, own-campus scope
    }

    [Fact]
    public async Task ViewAccountStatistics_Ho_StillSucceeds()
    {
        var db = SeedDb();
        var actor = new FakeCurrentUserService { UserId = 950, RoleCode = RoleCodes.Ho, SubRole = null, PrimaryCampusId = null };
        var handler = new ViewAccountStatisticsQueryHandler(db, actor);

        var dto = await handler.Handle(new ViewAccountStatisticsQuery(), CancellationToken.None);

        Assert.Equal(0, dto.TotalAccounts); // HO scope is HO/StaffLeader rows only; none seeded
    }
}
