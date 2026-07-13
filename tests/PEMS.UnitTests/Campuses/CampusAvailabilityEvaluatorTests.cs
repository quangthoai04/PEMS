using PEMS.Application.Campuses.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Campuses;

/// <summary>
/// UC-86 §8 availability matrix against the real EF query shape (doc §28.2): the "valid Staff
/// Leader" definition is verified from user rows (role/sub-role/status/campus/IC department),
/// never from campuses.ic_head_user_id.
/// </summary>
public class CampusAvailabilityEvaluatorTests
{
    private const ulong CampusId = 1;
    private const ulong IcDeptId = 10;

    private static CampusTestDbContext CreateBaseContext(
        string campusStatus = EntityStatuses.Active,
        string icDeptStatus = EntityStatuses.Active)
    {
        var db = CampusTestDbContext.Create();
        db.Roles.Add(CampusUcTestData.CreateRole(CampusUcTestData.StaffRoleId, RoleCodes.Staff));
        db.Campuses.Add(CampusUcTestData.CreateCampus(CampusId, campusStatus));
        db.Departments.Add(CampusUcTestData.CreateIcDepartment(IcDeptId, CampusId, icDeptStatus));
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task ActiveCampus_ActiveIcDept_ExactlyOneLeader_IsAvailable()
    {
        using var db = CreateBaseContext();
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, CampusId, IcDeptId));
        db.SaveChanges();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, CampusId, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.True(snapshot!.IsAvailableForVisitRegistration);
        Assert.Equal(100UL, snapshot.ValidStaffLeaderUserId);
        Assert.Equal(IcDeptId, snapshot.ActiveIcDepartmentId);
    }

    [Fact]
    public async Task InactiveCampus_IsNotAvailable()
    {
        using var db = CreateBaseContext(campusStatus: EntityStatuses.Inactive);
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, CampusId, IcDeptId));
        db.SaveChanges();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, CampusId, CancellationToken.None);

        Assert.False(snapshot!.IsAvailableForVisitRegistration);
        Assert.Contains(CampusReadinessIssues.CampusInactive, snapshot.Readiness.ReadinessIssues);
    }

    [Fact]
    public async Task NoActiveIcDepartment_IsNotAvailable()
    {
        using var db = CreateBaseContext(icDeptStatus: EntityStatuses.Inactive);
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, CampusId, IcDeptId));
        db.SaveChanges();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, CampusId, CancellationToken.None);

        Assert.False(snapshot!.IsAvailableForVisitRegistration);
        Assert.Contains(CampusReadinessIssues.ActiveIcDepartmentMissing, snapshot.Readiness.ReadinessIssues);
        // The leader's department is INACTIVE ⇒ they are not a valid leader either.
        Assert.Equal(0, snapshot.ValidStaffLeaderCount);
    }

    [Fact]
    public async Task MultipleActiveIcDepartments_ConfigurationError_NotAvailable()
    {
        using var db = CreateBaseContext();
        db.Departments.Add(CampusUcTestData.CreateIcDepartment(11, CampusId));
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, CampusId, IcDeptId));
        db.SaveChanges();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, CampusId, CancellationToken.None);

        Assert.False(snapshot!.IsAvailableForVisitRegistration);
        Assert.Contains(CampusReadinessIssues.MultipleActiveIcDepartments, snapshot.Readiness.ReadinessIssues);
        Assert.Null(snapshot.ActiveIcDepartmentId); // never picked arbitrarily
    }

    [Fact]
    public async Task NoLeader_IsNotAvailable()
    {
        using var db = CreateBaseContext();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, CampusId, CancellationToken.None);

        Assert.False(snapshot!.IsAvailableForVisitRegistration);
        Assert.Contains(CampusReadinessIssues.ActiveStaffLeaderMissing, snapshot.Readiness.ReadinessIssues);
    }

    [Fact]
    public async Task InactiveLeader_DoesNotCount()
    {
        using var db = CreateBaseContext();
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, CampusId, IcDeptId, status: UserStatuses.Inactive));
        db.SaveChanges();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, CampusId, CancellationToken.None);

        Assert.False(snapshot!.IsAvailableForVisitRegistration);
        Assert.Equal(0, snapshot.ValidStaffLeaderCount);
    }

    [Fact]
    public async Task StaffWithSubRoleStaff_DoesNotCount()
    {
        using var db = CreateBaseContext();
        db.Users.Add(CampusUcTestData.CreateUser(100, CampusUcTestData.StaffRoleId, UserSubRoles.Staff, CampusId, IcDeptId));
        db.SaveChanges();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, CampusId, CancellationToken.None);

        Assert.False(snapshot!.IsAvailableForVisitRegistration);
        Assert.Equal(0, snapshot.ValidStaffLeaderCount);
    }

    [Fact]
    public async Task LeaderOfAnotherCampus_DoesNotCount()
    {
        using var db = CreateBaseContext();
        db.Campuses.Add(CampusUcTestData.CreateCampus(2));
        db.Departments.Add(CampusUcTestData.CreateIcDepartment(20, 2));
        // Leader whose primary campus is campus 2 (their own IC dept) — must not satisfy campus 1.
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, 2, 20));
        db.SaveChanges();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, CampusId, CancellationToken.None);

        Assert.False(snapshot!.IsAvailableForVisitRegistration);
        Assert.Equal(0, snapshot.ValidStaffLeaderCount);
    }

    [Fact]
    public async Task LeaderInGeneralDepartment_DoesNotCount()
    {
        using var db = CreateBaseContext();
        db.Departments.Add(CampusUcTestData.CreateGeneralDepartment(30, CampusId));
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, CampusId, 30));
        db.SaveChanges();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, CampusId, CancellationToken.None);

        Assert.False(snapshot!.IsAvailableForVisitRegistration);
        Assert.Equal(0, snapshot.ValidStaffLeaderCount);
    }

    [Fact]
    public async Task LeaderInIcDepartmentOfAnotherCampus_DoesNotCount()
    {
        using var db = CreateBaseContext();
        db.Campuses.Add(CampusUcTestData.CreateCampus(2));
        db.Departments.Add(CampusUcTestData.CreateIcDepartment(20, 2));
        // Primary campus 1 but department is campus 2's IC dept (mismatch) — invalid.
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, CampusId, 20));
        db.SaveChanges();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, CampusId, CancellationToken.None);

        Assert.False(snapshot!.IsAvailableForVisitRegistration);
        Assert.Equal(0, snapshot.ValidStaffLeaderCount);
    }

    [Fact]
    public async Task MultipleValidLeaders_ConfigurationError_NotAvailable_NoArbitraryPick()
    {
        using var db = CreateBaseContext();
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, CampusId, IcDeptId));
        db.Users.Add(CampusUcTestData.CreateStaffLeader(101, CampusId, IcDeptId));
        db.SaveChanges();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, CampusId, CancellationToken.None);

        Assert.False(snapshot!.IsAvailableForVisitRegistration);
        Assert.Equal(2, snapshot.ValidStaffLeaderCount);
        Assert.Null(snapshot.ValidStaffLeaderUserId); // BR-86-20: never FirstOrDefault among several
        Assert.Contains(CampusReadinessIssues.MultipleActiveStaffLeaders, snapshot.Readiness.ReadinessIssues);
    }

    [Fact]
    public async Task IcHeadPointsAtSomeoneElse_WarningOnly_StillAvailable()
    {
        using var db = CreateBaseContext();
        var campus = db.Campuses.Single();
        campus.IcHeadUserId = 999; // stale mapping — §8.5: display/consistency only
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, CampusId, IcDeptId));
        db.SaveChanges();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, CampusId, CancellationToken.None);

        Assert.True(snapshot!.IsAvailableForVisitRegistration);
        Assert.Contains(CampusReadinessIssues.IcHeadMappingInconsistent, snapshot.Readiness.ReadinessIssues);
    }

    [Fact]
    public async Task UnknownCampusId_ReturnsNull()
    {
        using var db = CreateBaseContext();

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, 12345, CancellationToken.None);

        Assert.Null(snapshot);
    }
}
