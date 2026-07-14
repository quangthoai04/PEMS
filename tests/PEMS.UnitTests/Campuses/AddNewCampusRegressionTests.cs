using PEMS.Application.Campuses.Commands.AddNewCampus;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Campuses;

/// <summary>
/// UC-81 regression (doc §28.1): the Create Campus flow is deliberately UNCHANGED by UC-86 —
/// new campus = ACTIVE + ic_head NULL + auto IC department ACTIVE with head NULL, no Staff
/// Leader required; the campus is simply not operationally available yet.
/// </summary>
public class AddNewCampusRegressionTests
{
    private readonly FakeCurrentUserService _currentUser = new()
    {
        UserId = 900,
        RoleCode = RoleCodes.Ho,
        SubRole = null,
        PrimaryCampusId = 999,
    };

    private static AddNewCampusCommand ValidCommand() => new()
    {
        CampusCode = "QN",
        Name = "FPT University Quy Nhơn",
        City = "Gia Lai",
        Address = "Khu đô thị mới An Phú Thịnh",
        Phone = "0256 7300 999",
        Email = "qn@fpt.edu.vn",
    };

    [Fact]
    public async Task Create_ProducesActiveCampus_WithNullIcHead_AndActiveIcDepartment()
    {
        using var db = CampusTestDbContext.Create();
        var handler = new AddNewCampusCommandHandler(db, _currentUser, new RoleAccessPolicy(), new FakeDateTimeService());

        var response = await handler.Handle(ValidCommand(), CancellationToken.None);

        var campus = db.Campuses.Single();
        Assert.Equal(EntityStatuses.Active, campus.Status);
        Assert.Null(campus.IcHeadUserId);

        var icDept = db.Departments.Single();
        Assert.Equal(campus.CampusId, icDept.CampusId);
        Assert.Equal("IC", icDept.DepartmentType);
        Assert.Equal(EntityStatuses.Active, icDept.Status);
        Assert.Null(icDept.HeadUserId);

        Assert.Equal(EntityStatuses.Active, response.Status);
        Assert.Null(response.IcHeadUserId);
    }

    [Fact]
    public async Task Create_WithoutStaffLeader_Succeeds_ButCampusIsNotAvailableForRegistration()
    {
        using var db = CampusTestDbContext.Create();
        db.Roles.Add(CampusUcTestData.CreateRole(CampusUcTestData.StaffRoleId, RoleCodes.Staff));
        db.SaveChanges();
        var handler = new AddNewCampusCommandHandler(db, _currentUser, new RoleAccessPolicy(), new FakeDateTimeService());

        var response = await handler.Handle(ValidCommand(), CancellationToken.None);

        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(db, response.CampusId, CancellationToken.None);
        Assert.False(snapshot!.IsAvailableForVisitRegistration);
        Assert.Contains(CampusReadinessIssues.ActiveStaffLeaderMissing, snapshot.Readiness.ReadinessIssues);
        Assert.True(snapshot.Readiness.ActiveIcDepartmentExists);
    }
}
