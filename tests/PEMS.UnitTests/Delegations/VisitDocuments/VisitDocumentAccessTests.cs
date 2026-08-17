using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.VisitDocuments.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.VisitDocuments;

/// <summary>
/// SEC-17 remediation. Visit Document upload used to share the broad, view-only
/// <c>VisitInstanceMediaAccessScope</c> (Host, ANY Staff Leader, or an ACCEPTED/ASSIGNED
/// participant). Chốt business rule: Visit Document Upload = Host of that EXACT visit instance
/// only — no participant, no Staff Leader, no Admin exception.
/// </summary>
public class VisitDocumentAccessTests
{
    [Fact]
    public async Task Host_Passes()
    {
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = DelegationsTestData.HostUserId, RoleId = DelegationsTestData.StaffRoleId,
            RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff,
        };

        var context = await VisitDocumentAccess.ResolveUploadAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None);

        Assert.Equal(DelegationsTestData.HostUserId, context.UserId);
    }

    [Fact]
    public async Task AcceptedParticipant_NotHost_IsForbidden()
    {
        const ulong participantId = 601;
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        db.Users.Add(DelegationsTestData.CreateUser(participantId, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, null));
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            701, participantId, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted, instance.VisitInstanceId));
        db.SaveChanges();
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = participantId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff,
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitDocumentAccess.ResolveUploadAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task StaffLeaderOfCampus_NotHost_IsForbidden()
    {
        const ulong leaderId = 602;
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        db.Users.Add(DelegationsTestData.CreateUser(leaderId, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, null));
        db.SaveChanges();
        var actor = new FakeDelegationsCurrentUser
        {
            UserId = leaderId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Staff,
            SubRole = UserSubRoles.Leader, PrimaryCampusId = instance.CampusId,
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitDocumentAccess.ResolveUploadAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_NotHost_IsForbidden()
    {
        const ulong adminId = 603;
        var db = DelegationsTestDbContext.Create();
        var (instance, _) = DelegationsTestData.SeedBase(db);
        db.Users.Add(DelegationsTestData.CreateUser(adminId, DelegationsTestData.StaffRoleId, null, null));
        db.SaveChanges();
        var actor = new FakeDelegationsCurrentUser { UserId = adminId, RoleId = DelegationsTestData.StaffRoleId, RoleCode = RoleCodes.Admin, SubRole = null };

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitDocumentAccess.ResolveUploadAsync(
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

        await Assert.ThrowsAsync<ForbiddenException>(() => VisitDocumentAccess.ResolveUploadAsync(
            db, actor, instance.VisitInstanceId, CancellationToken.None));
    }
}
