using Microsoft.EntityFrameworkCore;
using PEMS.Application.Campuses.Commands.ManageCampusStatus;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Campuses;

/// <summary>
/// UC-86 disable/enable handler behaviour (doc §28.3/§28.4/§28.5): every non-terminal campus
/// visit instance blocks disable with 409 and NO state change; terminal instances never block;
/// enable requires master data + ACTIVE IC department but NOT a Staff Leader; only HO/ADMIN may
/// act; same-status requests are idempotent no-ops.
/// </summary>
public class ManageCampusStatusCommandHandlerTests
{
    private const ulong CampusId = 1;
    private const ulong IcDeptId = 10;

    private readonly FakeCurrentUserService _currentUser;
    private readonly FakeDateTimeService _clock = new();

    public ManageCampusStatusCommandHandlerTests()
    {
        // ACTIVE HO from another campus (999) so the own-campus guard is not tripped.
        _currentUser = new FakeCurrentUserService
        {
            UserId = 900,
            RoleCode = RoleCodes.Ho,
            SubRole = null,
            PrimaryCampusId = 999,
        };
    }

    private ManageCampusStatusCommandHandler CreateHandler(CampusTestDbContext db) =>
        CreateHandler(db, out _);

    private ManageCampusStatusCommandHandler CreateHandler(
        CampusTestDbContext db, out CampusRecordingSessionService sessions)
    {
        sessions = new CampusRecordingSessionService(db);
        return new(db, _currentUser, new RoleAccessPolicy(), _clock, sessions);
    }

    private static CampusTestDbContext CreateContext(string campusStatus = EntityStatuses.Active)
    {
        var db = CampusTestDbContext.Create();
        db.Roles.Add(CampusUcTestData.CreateRole(CampusUcTestData.StaffRoleId, RoleCodes.Staff));
        db.Campuses.Add(CampusUcTestData.CreateCampus(CampusId, campusStatus));
        db.Departments.Add(CampusUcTestData.CreateIcDepartment(IcDeptId, CampusId));
        db.SaveChanges();
        return db;
    }

    private static void AddInstance(CampusTestDbContext db, ulong id, string status, ulong campusId = CampusId)
    {
        db.VisitRequests.Add(CampusUcTestData.CreateVisitRequest(id));
        db.VisitRequestCampuses.Add(CampusUcTestData.CreateVisitInstance(id, id, campusId, status));
        db.SaveChanges();
    }

    private static ManageCampusStatusCommand Disable(ulong campusId = CampusId) =>
        new() { CampusId = campusId, Status = EntityStatuses.Inactive };

    private static ManageCampusStatusCommand Enable(ulong campusId = CampusId) =>
        new() { CampusId = campusId, Status = EntityStatuses.Active };

    // ── §28.3 Disable: every non-terminal instance status blocks with 409 + no change ──

    [Theory]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval)]
    [InlineData(VisitInstanceStatuses.Assigned)]
    [InlineData(VisitInstanceStatuses.BeforeVisit)]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.AfterVisit)]
    public async Task Disable_WithNonTerminalInstance_Throws409_AndCampusStaysActive(string status)
    {
        using var db = CreateContext();
        AddInstance(db, 50, status);
        var handler = CreateHandler(db);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(Disable(), CancellationToken.None));

        Assert.Equal(CampusErrorCodes.CampusHasActiveVisits, ex.ErrorCode);
        Assert.Equal(EntityStatuses.Active, db.Campuses.Single().Status);
        Assert.Empty(db.AuditLogs); // never audit a change that did not happen
    }

    [Theory]
    [InlineData(VisitInstanceStatuses.Closed)]
    [InlineData(VisitInstanceStatuses.Cancelled)]
    [InlineData(VisitInstanceStatuses.Rejected)]
    public async Task Disable_WithOnlyTerminalInstances_Succeeds(string status)
    {
        using var db = CreateContext();
        AddInstance(db, 50, status);
        var handler = CreateHandler(db);

        var response = await handler.Handle(Disable(), CancellationToken.None);

        Assert.Equal(EntityStatuses.Inactive, response.Status);
        var campus = db.Campuses.Single();
        Assert.Equal(EntityStatuses.Inactive, campus.Status);
        Assert.Equal(900UL, campus.UpdatedBy);
        Assert.Equal(_clock.VietnamNow, campus.UpdatedAt);
        Assert.Equal("DISABLE_CAMPUS", db.AuditLogs.Single().Action);
        // No cascade: the terminal instance is untouched (BR-86-11).
        Assert.Equal(status, db.VisitRequestCampuses.Single().Status);
    }

    [Fact]
    public async Task Disable_WithNoInstances_Succeeds()
    {
        using var db = CreateContext();
        var handler = CreateHandler(db);

        var response = await handler.Handle(Disable(), CancellationToken.None);

        Assert.Equal(EntityStatuses.Inactive, response.Status);
        Assert.False(response.Readiness!.IsAvailableForVisitRegistration);
    }

    // ── Session revocation on disable (STAFF/DEPARTMENT of the campus lose access) ──

    [Fact]
    public async Task Disable_RevokesSessionsOfCampusStaffAndDepartmentUsers()
    {
        using var db = CreateContext();
        db.Roles.Add(CampusUcTestData.CreateRole(CampusUcTestData.DepartmentRoleId, RoleCodes.Department));
        db.Departments.Add(CampusUcTestData.CreateGeneralDepartment(20, CampusId));
        // Staff Leader (STAFF) + a DEPARTMENT staff, both in the disabled campus.
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, CampusId, IcDeptId));
        db.Users.Add(CampusUcTestData.CreateDepartmentStaff(101, CampusId, 20));
        db.UserSessions.Add(CampusUcTestData.CreateActiveSession(1000, 100));
        db.UserSessions.Add(CampusUcTestData.CreateActiveSession(1001, 101));
        db.SaveChanges();
        var handler = CreateHandler(db, out var sessions);

        var response = await handler.Handle(Disable(), CancellationToken.None);

        Assert.Equal(EntityStatuses.Inactive, response.Status);
        Assert.Equal(2, response.AffectedAccountCount);
        Assert.Equal(2, response.RevokedSessionCount);
        Assert.Equal(2, sessions.RevokeAllCalls.Count);
        Assert.All(sessions.RevokeAllCalls, c => Assert.Equal(SessionRevokeReasons.CampusDisabled, c.Reason));
        // Sessions actually revoked; users.status untouched (org-level lock).
        Assert.All(db.UserSessions.ToList(), s => Assert.NotNull(s.RevokedAt));
        Assert.All(db.Users.Where(u => u.UserId == 100 || u.UserId == 101).ToList(),
            u => Assert.Equal(UserStatuses.Active, u.Status));
    }

    [Fact]
    public async Task Disable_DoesNotRevoke_HoAdminOrOtherCampusSessions()
    {
        using var db = CreateContext();
        db.Roles.Add(CampusUcTestData.CreateRole(CampusUcTestData.HoRoleId, RoleCodes.Ho));
        db.Roles.Add(CampusUcTestData.CreateRole(CampusUcTestData.AdminRoleId, RoleCodes.Admin));
        db.Campuses.Add(CampusUcTestData.CreateCampus(2));
        db.Departments.Add(CampusUcTestData.CreateIcDepartment(21, 2));
        // HO + ADMIN whose primary campus IS the disabled one — must NOT be revoked.
        db.Users.Add(CampusUcTestData.CreateUser(200, CampusUcTestData.HoRoleId, null, CampusId, null));
        db.Users.Add(CampusUcTestData.CreateUser(201, CampusUcTestData.AdminRoleId, null, CampusId, null));
        // STAFF of ANOTHER campus — must NOT be revoked.
        db.Users.Add(CampusUcTestData.CreateStaffLeader(202, 2, 21));
        db.UserSessions.Add(CampusUcTestData.CreateActiveSession(2000, 200));
        db.UserSessions.Add(CampusUcTestData.CreateActiveSession(2001, 201));
        db.UserSessions.Add(CampusUcTestData.CreateActiveSession(2002, 202));
        db.SaveChanges();
        var handler = CreateHandler(db, out var sessions);

        var response = await handler.Handle(Disable(), CancellationToken.None);

        Assert.Equal(0, response.AffectedAccountCount);
        Assert.Empty(sessions.RevokeAllCalls);
        Assert.All(db.UserSessions.ToList(), s => Assert.Null(s.RevokedAt));
    }

    [Fact]
    public async Task Enable_DoesNotRevokeSessions()
    {
        using var db = CreateContext(EntityStatuses.Inactive);
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, CampusId, IcDeptId));
        db.UserSessions.Add(CampusUcTestData.CreateActiveSession(1000, 100));
        db.SaveChanges();
        var handler = CreateHandler(db, out var sessions);

        var response = await handler.Handle(Enable(), CancellationToken.None);

        Assert.Equal(EntityStatuses.Active, response.Status);
        Assert.Equal(0, response.RevokedSessionCount);
        Assert.Empty(sessions.RevokeAllCalls);
        Assert.Null(db.UserSessions.Single().RevokedAt);
    }

    [Fact]
    public async Task Disable_AfterVisitPastItsDate_StillBlocks_DatesNeverDecide()
    {
        using var db = CreateContext();
        db.VisitRequests.Add(CampusUcTestData.CreateVisitRequest(50));
        var instance = CampusUcTestData.CreateVisitInstance(50, 50, CampusId, VisitInstanceStatuses.AfterVisit);
        instance.PlannedStartAt = new DateTime(2025, 1, 1, 9, 0, 0); // long past
        instance.PlannedEndAt = new DateTime(2025, 1, 1, 11, 0, 0);
        db.VisitRequestCampuses.Add(instance);
        db.SaveChanges();
        var handler = CreateHandler(db);

        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(Disable(), CancellationToken.None));
    }

    [Fact]
    public async Task Disable_BlockerOnAnotherCampus_DoesNotBlockThisOne()
    {
        using var db = CreateContext();
        db.Campuses.Add(CampusUcTestData.CreateCampus(2));
        db.SaveChanges();
        // Multi-campus request: campus 1's instance is terminal, campus 2's is operational.
        db.VisitRequests.Add(CampusUcTestData.CreateVisitRequest(50));
        db.VisitRequestCampuses.Add(CampusUcTestData.CreateVisitInstance(51, 50, CampusId, VisitInstanceStatuses.Cancelled));
        db.VisitRequestCampuses.Add(CampusUcTestData.CreateVisitInstance(52, 50, 2, VisitInstanceStatuses.Assigned));
        db.SaveChanges();
        var handler = CreateHandler(db);

        var response = await handler.Handle(Disable(CampusId), CancellationToken.None);

        Assert.Equal(EntityStatuses.Inactive, response.Status);
    }

    // ── §28.4 Enable ──

    [Fact]
    public async Task Enable_WithIncompleteMasterData_IsRejected()
    {
        using var db = CreateContext(EntityStatuses.Inactive);
        var campus = db.Campuses.Single();
        campus.Address = " ";
        db.SaveChanges();
        var handler = CreateHandler(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => handler.Handle(Enable(), CancellationToken.None));

        Assert.Equal(CampusErrorCodes.CampusActivationMasterDataIncomplete, ex.ErrorCode);
        Assert.Equal(EntityStatuses.Inactive, db.Campuses.Single().Status);
    }

    [Fact]
    public async Task Enable_WithoutActiveIcDepartment_IsRejected()
    {
        using var db = CreateContext(EntityStatuses.Inactive);
        db.Departments.Single().Status = EntityStatuses.Inactive;
        db.SaveChanges();
        var handler = CreateHandler(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => handler.Handle(Enable(), CancellationToken.None));

        Assert.Equal(CampusErrorCodes.CampusActivationMissingIcDepartment, ex.ErrorCode);
    }

    [Fact]
    public async Task Enable_WithoutStaffLeader_Succeeds_ButNotReady()
    {
        using var db = CreateContext(EntityStatuses.Inactive);
        var handler = CreateHandler(db);

        var response = await handler.Handle(Enable(), CancellationToken.None);

        // BR-86-15: ACTIVE without a leader is legal — readiness stays false.
        Assert.Equal(EntityStatuses.Active, response.Status);
        Assert.Equal(EntityStatuses.Active, db.Campuses.Single().Status);
        Assert.False(response.Readiness!.IsAvailableForVisitRegistration);
        Assert.Contains(CampusReadinessIssues.ActiveStaffLeaderMissing, response.Readiness.ReadinessIssues);
        Assert.Equal("ENABLE_CAMPUS", db.AuditLogs.Single().Action);
    }

    [Fact]
    public async Task Enable_WithValidStaffLeader_Succeeds_AndReady()
    {
        using var db = CreateContext(EntityStatuses.Inactive);
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, CampusId, IcDeptId));
        db.SaveChanges();
        var handler = CreateHandler(db);

        var response = await handler.Handle(Enable(), CancellationToken.None);

        Assert.Equal(EntityStatuses.Active, response.Status);
        Assert.True(response.Readiness!.IsAvailableForVisitRegistration);
    }

    // ── §17 Idempotency ──

    [Fact]
    public async Task SameStatusRequest_IsIdempotentNoOp()
    {
        using var db = CreateContext();
        var handler = CreateHandler(db);

        var response = await handler.Handle(Enable(), CancellationToken.None); // already ACTIVE

        Assert.Equal(EntityStatuses.Active, response.Status);
        Assert.Null(db.Campuses.Single().UpdatedAt); // updated_at untouched
        Assert.Empty(db.AuditLogs);                  // no fake audit row
    }

    // ── §28.5 Authorization ──

    [Fact]
    public async Task NonHo_IsForbidden()
    {
        using var db = CreateContext();
        _currentUser.RoleCode = RoleCodes.Staff;
        _currentUser.SubRole = UserSubRoles.Leader;
        var handler = CreateHandler(db);

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(
            () => handler.Handle(Disable(), CancellationToken.None));

        Assert.Equal(CampusErrorCodes.CampusManagementForbidden, ex.ErrorCode);
        Assert.Equal(EntityStatuses.Active, db.Campuses.Single().Status);
    }

    [Fact]
    public async Task Ho_CannotChangeOwnCampus()
    {
        using var db = CreateContext();
        _currentUser.PrimaryCampusId = CampusId;
        var handler = CreateHandler(db);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(Disable(), CancellationToken.None));
    }

    [Fact]
    public async Task UnknownCampus_Is404()
    {
        using var db = CreateContext();
        var handler = CreateHandler(db);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(Disable(12345), CancellationToken.None));
    }

    [Fact]
    public async Task InvalidStatusValue_IsRejected()
    {
        using var db = CreateContext();
        var handler = CreateHandler(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => handler.Handle(
                new ManageCampusStatusCommand { CampusId = CampusId, Status = "ARCHIVED" },
                CancellationToken.None));

        Assert.Equal(CampusErrorCodes.InvalidCampusStatus, ex.ErrorCode);
    }
}
