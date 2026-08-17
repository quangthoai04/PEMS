using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.VisitPhotos;

/// <summary>
/// SEC-16 remediation. <c>ResolveStaffAsync</c> used to be a pure pass-through to the broad,
/// view-only <c>VisitInstanceMediaAccessScope</c> — any Staff Leader of any campus, or an
/// ACCEPTED/ASSIGNED participant, could reach face-scan/tagging. The chốt business rule is
/// narrower: Staff or Staff Leader role AND must be the Host of that exact visit instance.
/// </summary>
public class VisitPhotoFaceScanAccessTests
{
    private static (DelegationsTestDbContext Db, PEMS.Domain.Entities.Delegations.VisitRequestCampus Instance)
        SeedInstance(string hostRoleCode = RoleCodes.Staff, string? hostSubRole = UserSubRoles.Staff)
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        return (db, instance);
    }

    [Fact]
    public async Task Staff_WhoIsHost_Passes()
    {
        var (db, instance) = SeedInstance();
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = DelegationsTestData.HostUserId, RoleId = DelegationsTestData.StaffRoleId,
            RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff,
        };

        var context = await VisitPhotoFaceScanAccess.ResolveStaffAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None);

        Assert.Equal(DelegationsTestData.HostUserId, context.UserId);
    }

    [Fact]
    public async Task StaffLeader_WhoIsHost_Passes()
    {
        var (db, instance) = SeedInstance();
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = DelegationsTestData.HostUserId, RoleId = DelegationsTestData.StaffRoleId,
            RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader,
        };

        var context = await VisitPhotoFaceScanAccess.ResolveStaffAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None);

        Assert.Equal(DelegationsTestData.HostUserId, context.UserId);
    }

    [Fact]
    public async Task Staff_WhoIsNotHost_IsForbidden()
    {
        const ulong notHostId = 555;
        var (db, instance) = SeedInstance();
        db.Users.Add(DelegationsTestData.CreateUser(notHostId, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, null));
        db.SaveChanges();
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = notHostId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff,
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitPhotoFaceScanAccess.ResolveStaffAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task AcceptedParticipant_WhoIsNotStaffRole_IsForbidden()
    {
        // The former broad scope would have admitted an accepted participant regardless of role or
        // Host status — the narrowed rule requires BOTH the role AND Host, so this must now fail.
        const ulong participantId = 556;
        var (db, instance) = SeedInstance();
        db.Users.Add(DelegationsTestData.CreateUser(participantId, DelegationsTestData.StudentRoleId, null, null));
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            700, participantId, ParticipantRoles.Student, ParticipantStatuses.Accepted, instance.VisitInstanceId));
        db.SaveChanges();
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = participantId, RoleId = DelegationsTestData.StudentRoleId, RoleCode = RoleCodes.Student, SubRole = null,
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitPhotoFaceScanAccess.ResolveStaffAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_WhoIsHost_IsForbidden()
    {
        // Demonstrates the fix is "Staff/StaffLeader role AND Host" — not merely "add a Host check"
        // — since Admin-as-Host must still be denied by the role half of the rule.
        var (db, instance) = SeedInstance();
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = DelegationsTestData.HostUserId, RoleId = DelegationsTestData.StaffRoleId,
            RoleCode = RoleCodes.Admin, SubRole = null,
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitPhotoFaceScanAccess.ResolveStaffAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task Ho_WhoIsHost_IsForbidden()
    {
        var (db, instance) = SeedInstance();
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = DelegationsTestData.HostUserId, RoleId = DelegationsTestData.StaffRoleId,
            RoleCode = RoleCodes.Ho, SubRole = null,
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitPhotoFaceScanAccess.ResolveStaffAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task UnresolvableRoleSubRoleCombination_FailsClosed_NotACrash()
    {
        // An invalid (role_code, sub_role) pair is a data defect, not a server fault — must deny
        // (ForbiddenException), never bubble EffectiveRole.Resolve's InvalidOperationException as a 500.
        var (db, instance) = SeedInstance();
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = DelegationsTestData.HostUserId, RoleId = DelegationsTestData.StaffRoleId,
            RoleCode = RoleCodes.Staff, SubRole = "NOT_A_REAL_SUBROLE",
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitPhotoFaceScanAccess.ResolveStaffAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }
}
