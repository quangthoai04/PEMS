using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Queries.GetRoleAssignmentOptions;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Departments;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.GetRoleAssignmentOptions;

/// <summary>
/// Unit tests for <see cref="GetRoleAssignmentOptionsQueryHandler"/> (UC-100-SL). The endpoint is
/// Staff-Leader-only and campus-scoped from the authenticated caller (id 900, campus 1): it returns
/// the campus IC department and the active GENERAL departments, flagging each with hasHead /
/// isCurrentTargetHead / selectable. Runs on EF InMemory.
/// </summary>
public class GetRoleAssignmentOptionsQueryHandlerTests
{
    private const ulong Campus = Uc106TestData.CampusId;   // 1
    private const ulong IcDeptId = 50;
    private const ulong TargetId = 100;

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();   // Staff Leader, id 900, campus 1
        public GetRoleAssignmentOptionsQueryHandler Handler { get; }

        public Harness() => Handler = new GetRoleAssignmentOptionsQueryHandler(Db, Actor);

        public Task<RoleAssignmentOptionsDto> Run(ulong targetUserId)
            => Handler.Handle(new GetRoleAssignmentOptionsQuery { TargetUserId = targetUserId }, CancellationToken.None);
    }

    private static Department IcDept(bool active = true) => new()
    {
        DepartmentId = IcDeptId,
        CampusId = Campus,
        Name = "Phòng Hợp tác Quốc tế",
        DepartmentType = "IC",
        Status = active ? EntityStatuses.Active : EntityStatuses.Inactive,
        CreatedAt = new DateTime(2026, 1, 1),
    };

    private static Department General(ulong id, string name, ulong? head = null, ulong campus = Campus, string status = EntityStatuses.Active)
        => new()
        {
            DepartmentId = id,
            CampusId = campus,
            Name = name,
            DepartmentType = "GENERAL",
            Status = status,
            HeadUserId = head,
            CreatedAt = new DateTime(2026, 1, 1),
        };

    private static Harness CreateHarness(bool withIc = true)
    {
        var h = new Harness();
        h.Db.Campuses.Add(Uc106TestData.CreateCampus());
        if (withIc) h.Db.Departments.Add(IcDept());
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId));
        h.Db.SaveChanges();
        return h;
    }

    [Fact]
    public async Task Returns_IcDepartment_AndGeneralDepartments_SortedByName_WithFlags()
    {
        var h = CreateHarness();
        h.Db.Departments.AddRange(
            General(61, "Phòng B", head: null),
            General(62, "Phòng A", head: 999),          // has another head
            General(63, "Phòng C", head: TargetId));    // headed by the target itself
        h.Db.SaveChanges();

        var result = await h.Run(TargetId);

        Assert.Equal(Campus, result.CampusId);
        Assert.NotNull(result.IcDepartment);
        Assert.Equal(IcDeptId, result.IcDepartment!.DepartmentId);

        // Sorted by name: Phòng A, Phòng B, Phòng C.
        Assert.Equal(new[] { "Phòng A", "Phòng B", "Phòng C" }, result.GeneralDepartments.Select(d => d.Name).ToArray());

        var a = result.GeneralDepartments.Single(d => d.Name == "Phòng A");
        Assert.True(a.HasHead);
        Assert.False(a.IsCurrentTargetHead);
        Assert.False(a.Selectable);

        var b = result.GeneralDepartments.Single(d => d.Name == "Phòng B");
        Assert.False(b.HasHead);
        Assert.True(b.Selectable);

        var c = result.GeneralDepartments.Single(d => d.Name == "Phòng C");
        Assert.True(c.HasHead);
        Assert.True(c.IsCurrentTargetHead);
        Assert.True(c.Selectable);      // the target may keep its own department
    }

    [Fact]
    public async Task Excludes_OtherCampus_Inactive_AndIcFromGeneralList()
    {
        var h = CreateHarness();
        h.Db.Departments.AddRange(
            General(61, "Phòng cùng cơ sở"),
            General(62, "Phòng khác cơ sở", campus: 2),
            General(63, "Phòng ngưng hoạt động", status: EntityStatuses.Inactive));
        h.Db.SaveChanges();

        var result = await h.Run(TargetId);

        var names = result.GeneralDepartments.Select(d => d.Name).ToArray();
        Assert.Contains("Phòng cùng cơ sở", names);
        Assert.DoesNotContain("Phòng khác cơ sở", names);
        Assert.DoesNotContain("Phòng ngưng hoạt động", names);
    }

    [Fact]
    public async Task NoActiveIc_ReturnsNullIcDepartment()
    {
        var h = CreateHarness(withIc: false);
        h.Db.Departments.Add(IcDept(active: false));
        h.Db.SaveChanges();

        var result = await h.Run(TargetId);

        Assert.Null(result.IcDepartment);
    }

    [Fact]
    public async Task NonStaffLeaderCaller_Throws()
    {
        var h = CreateHarness();
        h.Actor.SubRole = UserSubRoles.Staff;   // IC Staff, not Leader

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(TargetId));
    }

    [Fact]
    public async Task TargetInAnotherCampus_ThrowsNotFound()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(200, Uc106TestData.StaffRoleId, UserSubRoles.Staff, departmentId: null, campusId: 2));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<NotFoundException>(() => h.Run(200));
    }

    [Fact]
    public async Task TargetIsCaller_Throws()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(h.Actor.UserId!.Value, Uc106TestData.StaffRoleId, UserSubRoles.Leader, IcDeptId));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(h.Actor.UserId!.Value));
    }
}
