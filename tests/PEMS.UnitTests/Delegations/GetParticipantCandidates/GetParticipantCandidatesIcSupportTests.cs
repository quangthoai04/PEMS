using PEMS.Application.Delegations.Queries.GetParticipantCandidates;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.GetParticipantCandidates;

/// <summary>
/// IC_SUPPORT candidates: BOTH IC Staff (STAFF+STAFF) and Staff Leader (STAFF+LEADER) of an ACTIVE
/// IC department on the instance campus are eligible — never the current host, never users holding
/// an active participant slot; DECLINED/REMOVED rows may be re-invited.
/// </summary>
public class GetParticipantCandidatesIcSupportTests
{
    private const ulong IcStaffId = 101;
    private const ulong StaffLeaderId = 102;

    private static (DelegationsTestDbContext Db, GetParticipantCandidatesQueryHandler Handler, FakeDelegationsCurrentUser User) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        // Two valid candidates in the host's ACTIVE IC department (id 900, seeded by SeedBase).
        db.Users.Add(DelegationsTestData.CreateUser(IcStaffId, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, 900));
        db.Users.Add(DelegationsTestData.CreateUser(StaffLeaderId, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, 900));
        db.SaveChanges();
        var user = new FakeDelegationsCurrentUser();
        return (db, new GetParticipantCandidatesQueryHandler(db, user), user);
    }

    private static Task<IReadOnlyList<ParticipantCandidateDto>> Query(GetParticipantCandidatesQueryHandler handler)
        => handler.Handle(new GetParticipantCandidatesQuery(DelegationsTestData.VisitInstanceId, "IC_SUPPORT", null), default);

    [Fact]
    public async Task Returns_BothIcStaff_AndStaffLeader_WithTheirSubRoles()
    {
        var (_, handler, _) = CreateSut();

        var result = await Query(handler);

        Assert.Equal(2, result.Count);
        var staff = Assert.Single(result, c => c.UserId == IcStaffId);
        Assert.Equal(UserSubRoles.Staff, staff.SubRole);
        var leader = Assert.Single(result, c => c.UserId == StaffLeaderId);
        Assert.Equal(UserSubRoles.Leader, leader.SubRole);
    }

    [Fact]
    public async Task CurrentHost_IsExcluded_EvenWhenTheHostIsAStaffLeader()
    {
        var (db, handler, _) = CreateSut();
        // Make the host a Staff Leader — still never a candidate for their own instance.
        var host = db.Users.Single(u => u.UserId == DelegationsTestData.HostUserId);
        host.SubRole = UserSubRoles.Leader;
        db.SaveChanges();

        var result = await Query(handler);

        Assert.DoesNotContain(result, c => c.UserId == DelegationsTestData.HostUserId);
        Assert.Contains(result, c => c.UserId == StaffLeaderId);
    }

    [Theory]
    [InlineData(ParticipantStatuses.Invited)]
    [InlineData(ParticipantStatuses.Accepted)]
    [InlineData(ParticipantStatuses.Assigned)]
    public async Task ActiveParticipant_IsExcluded(string participantStatus)
    {
        var (db, handler, _) = CreateSut();
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(500, StaffLeaderId, ParticipantRoles.IcSupport, participantStatus));
        db.SaveChanges();

        var result = await Query(handler);

        Assert.DoesNotContain(result, c => c.UserId == StaffLeaderId);
        Assert.Contains(result, c => c.UserId == IcStaffId);
    }

    [Theory]
    [InlineData(ParticipantStatuses.Declined)]
    [InlineData(ParticipantStatuses.Removed)]
    public async Task ClosedParticipantSlot_ReappearsForReinvite(string participantStatus)
    {
        var (db, handler, _) = CreateSut();
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(500, StaffLeaderId, ParticipantRoles.IcSupport, participantStatus));
        db.SaveChanges();

        var result = await Query(handler);

        Assert.Contains(result, c => c.UserId == StaffLeaderId);
    }

    [Fact]
    public async Task OtherCampus_Inactive_NonIc_IcInactive_AreExcluded()
    {
        var (db, handler, _) = CreateSut();
        db.Departments.Add(DelegationsTestData.CreateDepartment(901, departmentType: "IC", campusId: DelegationsTestData.OtherCampusId));
        db.Departments.Add(DelegationsTestData.CreateDepartment(902)); // GENERAL, not IC
        db.Departments.Add(DelegationsTestData.CreateDepartment(903, departmentType: "IC", status: EntityStatuses.Inactive));
        db.Users.AddRange(
            DelegationsTestData.CreateUser(103, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, 901, campusId: DelegationsTestData.OtherCampusId),
            DelegationsTestData.CreateUser(104, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, 900, status: "INACTIVE"),
            DelegationsTestData.CreateUser(105, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, 902),
            DelegationsTestData.CreateUser(106, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, 903));
        db.SaveChanges();

        var result = await Query(handler);

        var ids = result.Select(c => c.UserId).ToHashSet();
        Assert.Equal(new HashSet<ulong> { IcStaffId, StaffLeaderId }, ids);
    }
}
