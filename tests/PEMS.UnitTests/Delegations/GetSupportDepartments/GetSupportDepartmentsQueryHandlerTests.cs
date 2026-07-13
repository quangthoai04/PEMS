using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Queries.GetSupportDepartments;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.GetSupportDepartments;

/// <summary>
/// Capability split of the support-department list: inviting the leader as a participant and
/// sending the department a system logistics request are INDEPENDENT businesses. A leader who
/// already occupies an active participant slot blocks re-invitation only — never logistics.
/// </summary>
public class GetSupportDepartmentsQueryHandlerTests
{
    private const ulong DeptId = 20;
    private const ulong LeaderId = 200;

    private static (DelegationsTestDbContext Db, GetSupportDepartmentsQueryHandler Handler, FakeDelegationsCurrentUser User) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        var user = new FakeDelegationsCurrentUser();
        return (db, new GetSupportDepartmentsQueryHandler(db, user), user);
    }

    private static void SeedDeptWithLeader(DelegationsTestDbContext db, ulong deptId = DeptId, ulong leaderId = LeaderId)
    {
        db.Departments.Add(DelegationsTestData.CreateDepartment(deptId, headUserId: leaderId));
        db.Users.Add(DelegationsTestData.CreateUser(leaderId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Leader, deptId));
        db.SaveChanges();
    }

    [Fact]
    public async Task ActiveLeader_NotParticipant_AllowsBothInvitationAndLogistics()
    {
        var (db, handler, _) = CreateSut();
        SeedDeptWithLeader(db);

        var result = await handler.Handle(new GetSupportDepartmentsQuery(DelegationsTestData.VisitInstanceId, null), default);

        var dept = Assert.Single(result);
        Assert.Equal(DeptId, dept.DepartmentId);
        Assert.Equal(LeaderId, dept.LeaderUserId);
        Assert.True(dept.CanInviteParticipant);
        Assert.Null(dept.ParticipantDisabledReason);
        Assert.True(dept.CanReceiveLogistics);
        Assert.Null(dept.LogisticsDisabledReason);
    }

    [Theory]
    [InlineData(ParticipantStatuses.Invited)]
    [InlineData(ParticipantStatuses.Accepted)]
    [InlineData(ParticipantStatuses.Assigned)]
    public async Task LeaderWithActiveParticipantSlot_BlocksInvitationOnly_LogisticsStaysAvailable(string participantStatus)
    {
        var (db, handler, _) = CreateSut();
        SeedDeptWithLeader(db);
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(500, LeaderId, ParticipantRoles.DeptSupport, participantStatus));
        db.SaveChanges();

        var result = await handler.Handle(new GetSupportDepartmentsQuery(DelegationsTestData.VisitInstanceId, null), default);

        var dept = Assert.Single(result);
        Assert.False(dept.CanInviteParticipant);
        Assert.Equal("Trưởng phòng này đã có trong danh sách tham gia của đoàn.", dept.ParticipantDisabledReason);
        Assert.True(dept.CanReceiveLogistics);
        Assert.Null(dept.LogisticsDisabledReason);
    }

    [Theory]
    [InlineData(ParticipantStatuses.Declined)]
    [InlineData(ParticipantStatuses.Removed)]
    public async Task LeaderWithClosedParticipantSlot_BlocksNothing(string participantStatus)
    {
        var (db, handler, _) = CreateSut();
        SeedDeptWithLeader(db);
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(500, LeaderId, ParticipantRoles.DeptSupport, participantStatus));
        db.SaveChanges();

        var result = await handler.Handle(new GetSupportDepartmentsQuery(DelegationsTestData.VisitInstanceId, null), default);

        var dept = Assert.Single(result);
        Assert.True(dept.CanInviteParticipant);
        Assert.True(dept.CanReceiveLogistics);
    }

    [Fact]
    public async Task NoActiveLeader_DisablesBothWithContextualReasons()
    {
        var (db, handler, _) = CreateSut();
        db.Departments.Add(DelegationsTestData.CreateDepartment(DeptId)); // no leader at all
        db.SaveChanges();

        var result = await handler.Handle(new GetSupportDepartmentsQuery(DelegationsTestData.VisitInstanceId, null), default);

        var dept = Assert.Single(result);
        Assert.Null(dept.LeaderUserId);
        Assert.False(dept.CanInviteParticipant);
        Assert.Equal("Phòng này chưa có trưởng phòng đang hoạt động.", dept.ParticipantDisabledReason);
        Assert.False(dept.CanReceiveLogistics);
        Assert.Equal("Phòng này chưa có trưởng phòng đang hoạt động, không thể nhận yêu cầu hậu cần.", dept.LogisticsDisabledReason);
    }

    [Fact]
    public async Task OtherCampus_Inactive_NonGeneral_DepartmentsAreNeverReturned()
    {
        var (db, handler, _) = CreateSut();
        SeedDeptWithLeader(db); // the only valid one
        db.Departments.Add(DelegationsTestData.CreateDepartment(21, campusId: DelegationsTestData.OtherCampusId));
        db.Departments.Add(DelegationsTestData.CreateDepartment(22, status: EntityStatuses.Inactive));
        db.Departments.Add(DelegationsTestData.CreateDepartment(23, departmentType: "IC"));
        db.SaveChanges();

        var result = await handler.Handle(new GetSupportDepartmentsQuery(DelegationsTestData.VisitInstanceId, null), default);

        var dept = Assert.Single(result);
        Assert.Equal(DeptId, dept.DepartmentId);
    }

    [Fact]
    public async Task NonHostActor_IsForbidden()
    {
        var (db, handler, user) = CreateSut();
        SeedDeptWithLeader(db);
        user.UserId = 999; // authenticated but not the instance host

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetSupportDepartmentsQuery(DelegationsTestData.VisitInstanceId, null), default));
    }
}
