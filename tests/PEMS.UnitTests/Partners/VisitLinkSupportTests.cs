using PEMS.Application.Common.Exceptions;
using PEMS.Application.Partners.VisitLinks.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Partners;

/// <summary>
/// SEC-18 remediation. ADMIN used to be hardcoded into the allow expression for partner-link
/// operations on a visit instance. Fixed with an explicit, unconditional early-deny before any
/// relationship check, so a historical Host/Participant relationship on an Admin account can never
/// substitute for the removed allow-list entry.
/// </summary>
public class VisitLinkSupportTests
{
    [Fact]
    public async Task Admin_WithNoRelationship_IsForbidden()
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = 900, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Admin, SubRole = null,
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitLinkSupport.LoadInstanceWithAccessAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_WhoIsTheHistoricalHost_IsStillForbidden()
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db); // Host = DelegationsTestData.HostUserId
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = DelegationsTestData.HostUserId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Admin, SubRole = null,
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitLinkSupport.LoadInstanceWithAccessAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_WhoHoldsAnAcceptedParticipantRow_IsStillForbidden()
    {
        const ulong adminId = 950;
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            700, adminId, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted, instance.VisitInstanceId));
        db.SaveChanges();
        var actor = new FakeDelegationsCurrentUser { UserId = adminId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Admin, SubRole = null };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitLinkSupport.LoadInstanceWithAccessAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task Ho_StillPasses()
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        var actor = new FakeDelegationsCurrentUser { UserId = 951, RoleId = 0, RoleCode = RoleCodes.Ho, SubRole = null };

        var result = await VisitLinkSupport.LoadInstanceWithAccessAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None);

        Assert.Equal(instance.VisitInstanceId, result.VisitInstanceId);
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

        var result = await VisitLinkSupport.LoadInstanceWithAccessAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None);

        Assert.Equal(instance.VisitInstanceId, result.VisitInstanceId);
    }

    [Fact]
    public async Task StaffLeaderOfCampus_StillPasses()
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = 952, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff,
            SubRole = UserSubRoles.Leader, PrimaryCampusId = instance.CampusId,
        };

        var result = await VisitLinkSupport.LoadInstanceWithAccessAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None);

        Assert.Equal(instance.VisitInstanceId, result.VisitInstanceId);
    }

    [Fact]
    public async Task UnresolvableRoleSubRole_FailsClosed()
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = 953, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff, SubRole = "NOT_A_REAL_SUBROLE",
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitLinkSupport.LoadInstanceWithAccessAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }
}
