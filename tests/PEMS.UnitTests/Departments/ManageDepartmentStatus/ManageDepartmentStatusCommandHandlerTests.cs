using PEMS.Application.Common.Exceptions;
using PEMS.Application.Departments.Commands.ManageDepartmentStatus;
using PEMS.Application.Departments.Common;
using PEMS.Domain.Constants;
using PEMS.Shared;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Departments.ManageDepartmentStatus;

/// <summary>
/// Unit tests for <see cref="ManageDepartmentStatusCommandHandler"/> (UC-106) covering the
/// participant blocker integration (doc §21 groups A–D): a blocked disable changes nothing
/// (department, users, participants, sessions, audit), blockers aggregate with the existing
/// logistics blockers, a clean disable still revokes sessions, and the enable path never runs
/// the participant check. Runs on EF InMemory (no MySQL / HTTP / WebApplicationFactory).
/// </summary>
public class ManageDepartmentStatusCommandHandlerTests
{
    private const ulong DeptId = 10;
    private const ulong OtherDeptId = 20;

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();
        public FakeDateTimeService Clock { get; } = new();
        public RecordingSessionService Sessions { get; }
        public ManageDepartmentStatusCommandHandler Handler { get; }

        public Harness()
        {
            Sessions = new RecordingSessionService(Db);
            Handler = new ManageDepartmentStatusCommandHandler(Db, Actor, Clock, Sessions);
        }

        public Task<ManageDepartmentStatusResponse> DisableAsync(ulong departmentId = DeptId)
            => Handler.Handle(
                new ManageDepartmentStatusCommand { DepartmentId = departmentId, Status = "INACTIVE" },
                CancellationToken.None);

        public Task<ManageDepartmentStatusResponse> EnableAsync(ulong departmentId = DeptId)
            => Handler.Handle(
                new ManageDepartmentStatusCommand { DepartmentId = departmentId, Status = "ACTIVE" },
                CancellationToken.None);
    }

    /// <summary>Campus + roles + one GENERAL ACTIVE department with a known updated_at/updated_by.</summary>
    private static Harness CreateHarness(string departmentStatus = EntityStatuses.Active)
    {
        var h = new Harness();
        h.Db.Campuses.Add(Uc106TestData.CreateCampus());
        h.Db.Roles.AddRange(
            Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff),
            Uc106TestData.CreateRole(Uc106TestData.DepartmentRoleId, RoleCodes.Department),
            Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
        h.Db.Departments.Add(Uc106TestData.CreateGeneralDepartment(DeptId, status: departmentStatus));
        h.Db.SaveChanges();
        return h;
    }

    private static void SeedDeptStaffParticipant(
        Harness h, string participantStatus, string visitStatus,
        ulong userId = 100, ulong visitInstanceId = 500, ulong participantId = 700, ulong departmentId = DeptId)
    {
        h.Db.Users.Add(Uc106TestData.CreateDepartmentStaff(userId, departmentId));
        h.Db.VisitRequestCampuses.Add(Uc106TestData.CreateVisitInstance(visitInstanceId, visitStatus));
        h.Db.VisitParticipants.Add(
            Uc106TestData.CreateDeptSupportParticipant(participantId, visitInstanceId, userId, participantStatus));
        h.Db.SaveChanges();
    }

    private static IReadOnlyList<DepartmentStatusBlocker> GetBlockers(ConflictException ex)
    {
        Assert.NotNull(ex.Data);
        var property = ex.Data!.GetType().GetProperty("blockers");
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<IReadOnlyList<DepartmentStatusBlocker>>(property!.GetValue(ex.Data));
    }

    // ── Group A — participant blocker rejects disable and changes nothing (doc 31–40) ──

    [Fact]
    public async Task Disable_PendingInvitation_ThrowsBlockedByDependencies()
    {
        var h = CreateHarness();
        SeedDeptStaffParticipant(h, ParticipantStatuses.Invited, VisitInstanceStatuses.Assigned);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.DisableAsync());

        Assert.Equal(DepartmentErrorCodes.DepartmentStatusBlockedByDependencies, ex.ErrorCode);
        var blocker = Assert.Single(GetBlockers(ex));
        Assert.Equal(DepartmentParticipantDependencyRule.PendingInvitationsBlockerType, blocker.Type);
        Assert.Equal(1, blocker.Count);
        Assert.Equal(1, blocker.AffectedUserCount);
        Assert.Equal(1, blocker.AffectedVisitCount);
    }

    [Fact]
    public async Task Disable_ActiveParticipation_ThrowsBlockedByDependencies()
    {
        var h = CreateHarness();
        SeedDeptStaffParticipant(h, ParticipantStatuses.Accepted, VisitInstanceStatuses.DuringVisit);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.DisableAsync());

        Assert.Equal(DepartmentErrorCodes.DepartmentStatusBlockedByDependencies, ex.ErrorCode);
        var blocker = Assert.Single(GetBlockers(ex));
        Assert.Equal(DepartmentParticipantDependencyRule.ActiveParticipationsBlockerType, blocker.Type);
    }

    [Fact]
    public async Task Disable_Blocked_DepartmentAndUpdatedFieldsUnchanged()
    {
        var h = CreateHarness();
        SeedDeptStaffParticipant(h, ParticipantStatuses.Invited, VisitInstanceStatuses.BeforeVisit);

        await Assert.ThrowsAsync<ConflictException>(() => h.DisableAsync());

        var dept = h.Db.Departments.Single(d => d.DepartmentId == DeptId);
        Assert.Equal(EntityStatuses.Active, dept.Status);
        Assert.Equal(new DateTime(2026, 6, 1), dept.UpdatedAt); // seeded baseline (doc 34)
        Assert.Equal(111ul, dept.UpdatedBy);                    // seeded baseline (doc 35)
    }

    [Fact]
    public async Task Disable_Blocked_SessionServiceNeverCalled()
    {
        var h = CreateHarness();
        SeedDeptStaffParticipant(h, ParticipantStatuses.Accepted, VisitInstanceStatuses.Assigned);
        h.Db.UserSessions.Add(Uc106TestData.CreateActiveSession(1, 100));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => h.DisableAsync());

        Assert.Empty(h.Sessions.RevokeAllCalls);
        Assert.Null(h.Db.UserSessions.Single(s => s.SessionId == 1).RevokedAt);
    }

    [Fact]
    public async Task Disable_Blocked_UserAndParticipantStatusesUnchanged()
    {
        var h = CreateHarness();
        SeedDeptStaffParticipant(h, ParticipantStatuses.Invited, VisitInstanceStatuses.DuringVisit);

        await Assert.ThrowsAsync<ConflictException>(() => h.DisableAsync());

        Assert.Equal(EntityStatuses.Active, h.Db.Users.Single(u => u.UserId == 100).Status);      // doc 37
        Assert.Equal(ParticipantStatuses.Invited,
            h.Db.VisitParticipants.Single(p => p.ParticipantId == 700).Status);                   // doc 38
    }

    [Fact]
    public async Task Disable_Blocked_NoSuccessAuditAndNoPartialUpdate()
    {
        var h = CreateHarness();
        SeedDeptStaffParticipant(h, ParticipantStatuses.Assigned, VisitInstanceStatuses.AfterVisit);

        await Assert.ThrowsAsync<ConflictException>(() => h.DisableAsync());

        Assert.Empty(h.Db.AuditLogs);                                                             // doc 39
        Assert.False(h.Db.ChangeTracker.HasChanges());                                            // doc 40
    }

    // ── Group B — aggregation with the existing logistics blockers (doc 41–42) ──

    [Fact]
    public async Task Disable_LogisticsAndParticipantBlockers_ReturnsBothGroups()
    {
        var h = CreateHarness();
        SeedDeptStaffParticipant(h, ParticipantStatuses.Accepted, VisitInstanceStatuses.Assigned,
            visitInstanceId: 500);
        h.Db.VisitLogisticsItems.Add(
            Uc106TestData.CreateOpenLogisticsItem(1, 500, DeptId, LogisticsItemStatus.Requested));
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.DisableAsync());

        var blockers = GetBlockers(ex);
        Assert.Equal(2, blockers.Count);
        Assert.Contains(blockers, b => b.Type == DepartmentStatusImpactCalculator.OpenLogisticsBlockerType);
        Assert.Contains(blockers, b => b.Type == DepartmentParticipantDependencyRule.ActiveParticipationsBlockerType);
    }

    [Theory]
    [InlineData(LogisticsItemStatus.Requested)]
    [InlineData(LogisticsItemStatus.ChangeProposed)]
    [InlineData(LogisticsItemStatus.Assigned)]
    [InlineData(LogisticsItemStatus.Accepted)]
    [InlineData(LogisticsItemStatus.InProgress)]
    public async Task Disable_OpenLogistics_StillBlocksWithoutParticipants(string logisticsStatus)
    {
        var h = CreateHarness();
        h.Db.VisitRequestCampuses.Add(Uc106TestData.CreateVisitInstance(500, VisitInstanceStatuses.Assigned));
        h.Db.VisitLogisticsItems.Add(Uc106TestData.CreateOpenLogisticsItem(1, 500, DeptId, logisticsStatus));
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.DisableAsync());

        var blocker = Assert.Single(GetBlockers(ex));
        Assert.Equal(DepartmentStatusImpactCalculator.OpenLogisticsBlockerType, blocker.Type);
    }

    // ── Group C — no participant blocker → disable flow proceeds (doc 43–48) ──

    [Theory]
    [InlineData("DECLINED", "ASSIGNED")]  // doc 43: declined invitation
    [InlineData("REMOVED", "DURING_VISIT")] // doc 43: removed participant
    [InlineData("ACCEPTED", "CLOSED")]    // doc 44: terminal visit
    [InlineData("ASSIGNED", "CANCELLED")] // doc 44
    [InlineData("INVITED", "REJECTED")]   // doc 44
    [InlineData("INVITED", "WAITING_REQUEST_APPROVAL")] // BR-UC106-32 anomaly, no canonical blocker
    public async Task Disable_NonBlockingParticipant_Succeeds(string participantStatus, string visitStatus)
    {
        var h = CreateHarness();
        SeedDeptStaffParticipant(h, participantStatus, visitStatus);

        var response = await h.DisableAsync();

        Assert.True(response.Changed);
        Assert.Equal(EntityStatuses.Inactive, h.Db.Departments.Single(d => d.DepartmentId == DeptId).Status);
    }

    [Fact]
    public async Task Disable_ParticipantOfOtherDepartment_DoesNotBlockTarget()
    {
        var h = CreateHarness();
        h.Db.Departments.Add(Uc106TestData.CreateGeneralDepartment(OtherDeptId));
        h.Db.SaveChanges();
        SeedDeptStaffParticipant(h, ParticipantStatuses.Accepted, VisitInstanceStatuses.Assigned,
            userId: 200, departmentId: OtherDeptId);

        var response = await h.DisableAsync(DeptId);

        Assert.True(response.Changed);
        Assert.Equal(EntityStatuses.Inactive, h.Db.Departments.Single(d => d.DepartmentId == DeptId).Status);
        Assert.Equal(EntityStatuses.Active, h.Db.Departments.Single(d => d.DepartmentId == OtherDeptId).Status);
    }

    [Fact]
    public async Task Disable_NoBlockers_RevokesSessionsAndAuditsButKeepsUserStatus()
    {
        var h = CreateHarness();
        h.Db.Users.AddRange(
            Uc106TestData.CreateDepartmentLeader(100, DeptId),
            Uc106TestData.CreateDepartmentStaff(101, DeptId));
        h.Db.UserSessions.AddRange(
            Uc106TestData.CreateActiveSession(1, 100),
            Uc106TestData.CreateActiveSession(2, 101),
            Uc106TestData.CreateActiveSession(3, 101));
        h.Db.SaveChanges();

        var response = await h.DisableAsync();

        // Session revocation flow still runs (doc 46) with the department-disabled reason.
        Assert.Equal(2, h.Sessions.RevokeAllCalls.Count);
        Assert.All(h.Sessions.RevokeAllCalls, c => Assert.Equal(SessionRevokeReasons.DepartmentDisabled, c.Reason));
        Assert.Equal(new ulong[] { 100, 101 },
            h.Sessions.RevokeAllCalls.Select(c => c.UserId).OrderBy(id => id).ToArray());

        Assert.Equal(EntityStatuses.Inactive, h.Db.Departments.Single(d => d.DepartmentId == DeptId).Status); // doc 47
        Assert.Equal(2, response.AffectedAccountCount);
        Assert.Equal(3, response.RevokedSessionCount);
        Assert.All(h.Db.Users.Where(u => u.DepartmentId == DeptId).ToList(),
            u => Assert.Equal(EntityStatuses.Active, u.Status));                                              // doc 48
        Assert.Single(h.Db.AuditLogs.ToList()); // success audit IS written when not blocked
    }

    // ── Group D — enable path is unaffected by participants (doc 49–50) ──

    [Fact]
    public async Task Enable_WithOperationalParticipants_SucceedsAndLeavesParticipantsUntouched()
    {
        var h = CreateHarness(departmentStatus: EntityStatuses.Inactive);
        // Would block a DISABLE — must be irrelevant for enable (participant checker not run).
        SeedDeptStaffParticipant(h, ParticipantStatuses.Invited, VisitInstanceStatuses.Assigned);

        var response = await h.EnableAsync();

        Assert.True(response.Changed);
        Assert.Equal(EntityStatuses.Active, h.Db.Departments.Single(d => d.DepartmentId == DeptId).Status);
        Assert.Equal(ParticipantStatuses.Invited,
            h.Db.VisitParticipants.Single(p => p.ParticipantId == 700).Status);                   // doc 50
        Assert.Empty(h.Sessions.RevokeAllCalls); // enable never touches sessions
    }
}
