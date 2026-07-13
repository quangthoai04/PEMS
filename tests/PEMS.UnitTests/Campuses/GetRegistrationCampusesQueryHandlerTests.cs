using PEMS.Application.Campuses.Queries.GetRegistrationCampuses;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Campuses;

/// <summary>
/// UC-86 §10: the registration-form endpoint returns ONLY fully-available campuses —
/// ACTIVE + active IC department + exactly one valid Staff Leader (BR-86-04/05).
/// </summary>
public class GetRegistrationCampusesQueryHandlerTests
{
    [Fact]
    public async Task ReturnsOnlyFullyAvailableCampuses()
    {
        using var db = CampusTestDbContext.Create();
        db.Roles.Add(CampusUcTestData.CreateRole(CampusUcTestData.StaffRoleId, RoleCodes.Staff));

        // Campus 1: fully available.
        db.Campuses.Add(CampusUcTestData.CreateCampus(1));
        db.Departments.Add(CampusUcTestData.CreateIcDepartment(10, 1));
        db.Users.Add(CampusUcTestData.CreateStaffLeader(100, 1, 10));

        // Campus 2: ACTIVE but no Staff Leader (BR-86-05 — hidden from the form).
        db.Campuses.Add(CampusUcTestData.CreateCampus(2));
        db.Departments.Add(CampusUcTestData.CreateIcDepartment(20, 2));

        // Campus 3: INACTIVE (even with an otherwise valid setup).
        db.Campuses.Add(CampusUcTestData.CreateCampus(3, EntityStatuses.Inactive));
        db.Departments.Add(CampusUcTestData.CreateIcDepartment(30, 3));
        db.Users.Add(CampusUcTestData.CreateStaffLeader(300, 3, 30));

        // Campus 4: ACTIVE but two valid leaders (configuration error — hidden).
        db.Campuses.Add(CampusUcTestData.CreateCampus(4));
        db.Departments.Add(CampusUcTestData.CreateIcDepartment(40, 4));
        db.Users.Add(CampusUcTestData.CreateStaffLeader(400, 4, 40));
        db.Users.Add(CampusUcTestData.CreateStaffLeader(401, 4, 40));

        db.SaveChanges();
        var handler = new GetRegistrationCampusesQueryHandler(db);

        var result = await handler.Handle(new GetRegistrationCampusesQuery(), CancellationToken.None);

        var only = Assert.Single(result);
        Assert.Equal(1UL, only.CampusId);
        Assert.Equal("C1", only.CampusCode);
        Assert.Equal("Campus 1", only.CampusName);
    }

    [Fact]
    public async Task NoAvailableCampuses_ReturnsEmptyList()
    {
        using var db = CampusTestDbContext.Create();
        db.Roles.Add(CampusUcTestData.CreateRole(CampusUcTestData.StaffRoleId, RoleCodes.Staff));
        db.Campuses.Add(CampusUcTestData.CreateCampus(1)); // ACTIVE, no IC dept, no leader
        db.SaveChanges();
        var handler = new GetRegistrationCampusesQueryHandler(db);

        var result = await handler.Handle(new GetRegistrationCampusesQuery(), CancellationToken.None);

        Assert.Empty(result);
    }
}
