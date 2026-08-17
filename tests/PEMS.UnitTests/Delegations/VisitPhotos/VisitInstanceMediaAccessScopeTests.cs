using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.VisitPhotos;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.VisitPhotos;

/// <summary>
/// SEC-15/P2-2 remediation. Two fixes to the shared VIEW scope for the visit-photo folder/gallery
/// (folder listing, face-scan, News cover image): (1) the Staff Leader clause was missing a campus
/// comparison entirely — ANY Staff Leader of ANY campus passed for ANY instance; (2) ADMIN was
/// folded into the same allow clause — now an explicit, unconditional early-deny before any
/// relationship check.
/// </summary>
public class VisitInstanceMediaAccessScopeTests
{
    [Fact]
    public async Task StaffLeaderOfADifferentCampus_IsForbidden()
    {
        const ulong otherCampusLeaderId = 300;
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db); // instance is on CampusId (1)
        db.Users.Add(DelegationsTestData.CreateUser(
            otherCampusLeaderId, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, null, DelegationsTestData.OtherCampusId));
        db.SaveChanges();
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = otherCampusLeaderId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff,
            SubRole = UserSubRoles.Leader, PrimaryCampusId = DelegationsTestData.OtherCampusId,
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitInstanceMediaAccessScope.ResolveAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task StaffLeaderOfTheSameCampus_StillPasses()
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = 301, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff,
            SubRole = UserSubRoles.Leader, PrimaryCampusId = instance.CampusId,
        };

        var context = await VisitInstanceMediaAccessScope.ResolveAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None);

        Assert.Equal(instance.VisitInstanceId, context.Instance.VisitInstanceId);
    }

    [Fact]
    public async Task Admin_WithNoRelationship_IsForbidden()
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        var actor = new FakeDelegationsCurrentUser { UserId = 302, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Admin, SubRole = null };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitInstanceMediaAccessScope.ResolveAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_WhoIsTheHistoricalHost_IsStillForbidden()
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = DelegationsTestData.HostUserId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Admin, SubRole = null,
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitInstanceMediaAccessScope.ResolveAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task Host_StillPasses()
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = DelegationsTestData.HostUserId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff,
        };

        var context = await VisitInstanceMediaAccessScope.ResolveAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None);

        Assert.Equal(instance.VisitInstanceId, context.Instance.VisitInstanceId);
    }
}
