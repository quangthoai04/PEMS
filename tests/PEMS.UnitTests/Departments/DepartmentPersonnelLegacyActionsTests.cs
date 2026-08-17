using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Departments.Commands.ReassignDepartmentLead;
using PEMS.Application.Departments.Commands.RemovePersonnel;
using PEMS.Application.Departments.Commands.UpdateDepartmentPersonnel;
using PEMS.Application.Departments.Queries.SearchPersonnel;
using PEMS.Application.Departments.Queries.ViewPersonnelDetails;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Enums;
using PEMS.UnitTests.DepartmentLeaderPersonnel;
using Xunit;

namespace PEMS.UnitTests.Departments;

/// <summary>
/// SEC-05..09 remediation. The legacy <c>/api/Departments</c> personnel actions (searchpersonnel,
/// viewpersonneldetails, updatedepartmentpersonnel, removepersonnel, reassigndepartmentlead) used to
/// trust a client-supplied departmentId/userId with NO scope check at all — any authenticated user
/// could read or mutate any department's personnel. These tests exercise the new
/// <see cref="DepartmentPersonnelManagementScope"/> gate through each handler.
///
/// Reuses <see cref="DepartmentLeaderTestHarness"/> purely as DB/lock/session/email scaffolding — a
/// campus, the DEPARTMENT/STAFF roles, and a GENERAL department (id 10, campus 1) headed by user 900.
/// The harness's own default actor (a Department Lead) is overwritten per test as needed.
/// </summary>
public class DepartmentPersonnelLegacyActionsTests
{
    private const ulong OutOfScopeStaffLeaderId = 5001;
    private const ulong HoId = 5002;

    // ── SearchPersonnel (SEC-05) ────────────────────────────────────────────

    [Fact]
    public async Task SearchPersonnel_CrossCampusStaffLeader_Throws()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.Actor.UserId = OutOfScopeStaffLeaderId;
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = DepartmentLeaderTestHarness.OtherCampusId; // department is on CampusId

        var handler = new SearchPersonnelQueryHandler(h.Db, h.Actor);

        await Assert.ThrowsAsync<AuthBusinessException>(() => handler.Handle(
            new SearchPersonnelQuery { DepartmentId = DepartmentLeaderTestHarness.DepartmentId },
            CancellationToken.None));
    }

    [Fact]
    public async Task SearchPersonnel_Ho_Succeeds()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.Actor.UserId = HoId;
        h.Actor.RoleCode = RoleCodes.Ho;
        h.Actor.SubRole = null;
        h.Actor.PrimaryCampusId = null;

        var handler = new SearchPersonnelQueryHandler(h.Db, h.Actor);

        var result = await handler.Handle(
            new SearchPersonnelQuery { DepartmentId = DepartmentLeaderTestHarness.DepartmentId },
            CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SearchPersonnel_DepartmentLeadClaimButNotActualHead_Throws()
    {
        // JWT claims DepartmentId=10, but department 10's real HeadUserId is 900 (the harness's seeded
        // leader) — a stale/forged claim must not be trusted.
        var h = DepartmentLeaderTestHarness.Create();
        h.Actor.UserId = 777; // not 900
        h.Actor.RoleCode = RoleCodes.Department;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.DepartmentId = DepartmentLeaderTestHarness.DepartmentId;

        var handler = new SearchPersonnelQueryHandler(h.Db, h.Actor);

        await Assert.ThrowsAsync<AuthBusinessException>(() => handler.Handle(
            new SearchPersonnelQuery { DepartmentId = DepartmentLeaderTestHarness.DepartmentId },
            CancellationToken.None));
    }

    // ── ViewPersonnelDetails (SEC-06) ───────────────────────────────────────

    [Fact]
    public async Task ViewPersonnelDetails_CrossCampusStaffLeader_Throws()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId);
        h.Actor.UserId = OutOfScopeStaffLeaderId;
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = DepartmentLeaderTestHarness.OtherCampusId;

        var handler = new ViewPersonnelDetailsQueryHandler(h.Db, h.Actor);

        await Assert.ThrowsAsync<AuthBusinessException>(() => handler.Handle(
            new ViewPersonnelDetailsQuery
            {
                DepartmentId = DepartmentLeaderTestHarness.DepartmentId,
                UserId = DepartmentLeaderTestHarness.StaffId,
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task ViewPersonnelDetails_SameCampusStaffLeader_Succeeds()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId);
        h.Actor.UserId = OutOfScopeStaffLeaderId;
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = DepartmentLeaderTestHarness.CampusId; // same campus this time

        var handler = new ViewPersonnelDetailsQueryHandler(h.Db, h.Actor);

        var result = await handler.Handle(
            new ViewPersonnelDetailsQuery
            {
                DepartmentId = DepartmentLeaderTestHarness.DepartmentId,
                UserId = DepartmentLeaderTestHarness.StaffId,
            },
            CancellationToken.None);

        Assert.NotNull(result);
    }

    // ── UpdateDepartmentPersonnel (SEC-07) ──────────────────────────────────

    [Fact]
    public async Task UpdateDepartmentPersonnel_CrossDepartmentDepartmentLead_Throws()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddOtherDepartment(headUserId: 700);
        h.AddStaff(DepartmentLeaderTestHarness.StaffId,
            departmentId: DepartmentLeaderTestHarness.OtherDepartmentId,
            campusId: DepartmentLeaderTestHarness.OtherCampusId);

        // Actor is the REAL head of department 10 (per the harness default), but the target
        // personnel and departmentId in the request belong to the OTHER department.
        var handler = new UpdateDepartmentPersonnelCommandHandler(h.Db, h.Actor);

        await Assert.ThrowsAsync<AuthBusinessException>(() => handler.Handle(
            new UpdateDepartmentPersonnelCommand
            {
                DepartmentId = DepartmentLeaderTestHarness.OtherDepartmentId,
                UserId = DepartmentLeaderTestHarness.StaffId,
                FullName = "Hacked Name",
                Phone = "0900000000",
                Gender = Gender.Male,
            },
            CancellationToken.None));

        var untouched = h.GetUser(DepartmentLeaderTestHarness.StaffId);
        Assert.NotEqual("Hacked Name", untouched.FullName);
    }

    [Fact]
    public async Task UpdateDepartmentPersonnel_ActualHead_Succeeds()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId);

        var handler = new UpdateDepartmentPersonnelCommandHandler(h.Db, h.Actor); // default actor == real head (900)

        var response = await handler.Handle(
            new UpdateDepartmentPersonnelCommand
            {
                DepartmentId = DepartmentLeaderTestHarness.DepartmentId,
                UserId = DepartmentLeaderTestHarness.StaffId,
                FullName = "New Name",
                Phone = "0911111111",
                Gender = Gender.Male,
            },
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("New Name", h.GetUser(DepartmentLeaderTestHarness.StaffId).FullName);
    }

    // ── RemovePersonnel (SEC-08) ────────────────────────────────────────────

    [Fact]
    public async Task RemovePersonnel_CrossCampusStaffLeader_Throws()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId);
        h.Actor.UserId = OutOfScopeStaffLeaderId;
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = DepartmentLeaderTestHarness.OtherCampusId;

        var handler = new RemovePersonnelCommandHandler(h.Db, h.Actor);

        await Assert.ThrowsAsync<AuthBusinessException>(() => handler.Handle(
            new RemovePersonnelCommand
            {
                DepartmentId = DepartmentLeaderTestHarness.DepartmentId,
                UserId = DepartmentLeaderTestHarness.StaffId,
            },
            CancellationToken.None));

        Assert.Equal(UserStatuses.Active, h.GetUser(DepartmentLeaderTestHarness.StaffId).Status);
    }

    [Fact]
    public async Task RemovePersonnel_Ho_Succeeds()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId);
        h.Actor.UserId = HoId;
        h.Actor.RoleCode = RoleCodes.Ho;
        h.Actor.SubRole = null;
        h.Actor.PrimaryCampusId = null;

        var handler = new RemovePersonnelCommandHandler(h.Db, h.Actor);

        var response = await handler.Handle(
            new RemovePersonnelCommand
            {
                DepartmentId = DepartmentLeaderTestHarness.DepartmentId,
                UserId = DepartmentLeaderTestHarness.StaffId,
            },
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(UserStatuses.Inactive, h.GetUser(DepartmentLeaderTestHarness.StaffId).Status);
    }

    // ── ReassignDepartmentLead (SEC-09) ─────────────────────────────────────

    private static ReassignDepartmentLeadCommandHandler ReassignHandler(DepartmentLeaderTestHarness h)
        => new(h.Db, h.Actor, new DepartmentLeadershipTransferService(h.Db, h.Locks, h.Sessions, h.Dispatcher, h.Clock));

    [Fact]
    public async Task ReassignDepartmentLead_DepartmentLeadCaller_IsOutOfScope()
    {
        // Department Lead is deliberately excluded from this legacy route — self-service goes through
        // /api/department-leader/transfer-leadership instead. The harness's default actor (900) is
        // already the real head of department 10, which would pass the general personnel-management
        // gate, but must still be refused by the narrower reassignment-only gate.
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId);

        var handler = ReassignHandler(h);

        await Assert.ThrowsAsync<AuthBusinessException>(() => handler.Handle(
            new ReassignDepartmentLeadCommand
            {
                DepartmentId = DepartmentLeaderTestHarness.DepartmentId,
                NewLeaderUserId = DepartmentLeaderTestHarness.StaffId,
            },
            CancellationToken.None));

        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, h.GetDepartment().HeadUserId);
    }

    [Fact]
    public async Task ReassignDepartmentLead_StaffLeaderOwnCampus_Succeeds()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId);
        h.Actor.UserId = OutOfScopeStaffLeaderId;
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = DepartmentLeaderTestHarness.CampusId;

        var handler = ReassignHandler(h);

        var response = await handler.Handle(
            new ReassignDepartmentLeadCommand
            {
                DepartmentId = DepartmentLeaderTestHarness.DepartmentId,
                NewLeaderUserId = DepartmentLeaderTestHarness.StaffId,
            },
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(DepartmentLeaderTestHarness.StaffId, h.GetDepartment().HeadUserId);
        // Parity with the canonical self-service flow: both accounts must sign in again.
        Assert.Equal(2, h.Sessions.RevokeAllCalls.Count);
    }

    [Fact]
    public async Task ReassignDepartmentLead_HoCrossCampus_Succeeds()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId);
        h.Actor.UserId = HoId;
        h.Actor.RoleCode = RoleCodes.Ho;
        h.Actor.SubRole = null;
        h.Actor.PrimaryCampusId = null;

        var handler = ReassignHandler(h);

        var response = await handler.Handle(
            new ReassignDepartmentLeadCommand
            {
                DepartmentId = DepartmentLeaderTestHarness.DepartmentId,
                NewLeaderUserId = DepartmentLeaderTestHarness.StaffId,
            },
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(DepartmentLeaderTestHarness.StaffId, h.GetDepartment().HeadUserId);
    }

    [Fact]
    public async Task ReassignDepartmentLead_NoSeatedHead_IsRefusedBeforeAnyLock()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId);
        var department = h.GetDepartment();
        department.HeadUserId = null; // no seated head at all
        h.Db.SaveChanges();

        h.Actor.UserId = HoId;
        h.Actor.RoleCode = RoleCodes.Ho;
        h.Actor.SubRole = null;
        h.Actor.PrimaryCampusId = null;

        var handler = ReassignHandler(h);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            new ReassignDepartmentLeadCommand
            {
                DepartmentId = DepartmentLeaderTestHarness.DepartmentId,
                NewLeaderUserId = DepartmentLeaderTestHarness.StaffId,
            },
            CancellationToken.None));

        Assert.Contains("Trưởng phòng", ex.Message);
        Assert.Empty(h.Locks.LockedUserBatches);
    }

    [Fact]
    public async Task ReassignDepartmentLead_UnusableCandidate_DoesNotSilentlySucceed()
    {
        // SEC-20 regression: the old handler swallowed this into Success=false with a raw exception
        // message and HTTP 200. It must now throw and let the middleware answer with the real status.
        var h = DepartmentLeaderTestHarness.Create();
        h.Actor.UserId = HoId;
        h.Actor.RoleCode = RoleCodes.Ho;
        h.Actor.SubRole = null;
        h.Actor.PrimaryCampusId = null;

        var handler = ReassignHandler(h);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            new ReassignDepartmentLeadCommand
            {
                DepartmentId = DepartmentLeaderTestHarness.DepartmentId,
                NewLeaderUserId = 999999, // does not exist
            },
            CancellationToken.None));

        Assert.Equal(DepartmentLeaderErrorCodes.LeaderCandidateInvalid, ex.ErrorCode);
        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, h.GetDepartment().HeadUserId);
    }
}
