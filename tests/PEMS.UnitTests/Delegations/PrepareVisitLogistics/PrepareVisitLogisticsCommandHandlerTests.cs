using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Commands.PrepareVisitLogistics;
using PEMS.Domain.Constants;
using PEMS.Shared;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.PrepareVisitLogistics;

/// <summary>
/// Regression guard for the invitation/logistics capability split: a system logistics request MUST
/// still be created when the department's active leader already holds a participant slot
/// (INVITED/ACCEPTED/ASSIGNED) — the two businesses are independent. Department scope + active
/// leader remain revalidated server-side.
/// </summary>
public class PrepareVisitLogisticsCommandHandlerTests
{
    private const ulong DeptId = 20;
    private const ulong LeaderId = 200;

    private static (DelegationsTestDbContext Db, PrepareVisitLogisticsCommandHandler Handler,
        FakeDelegationsCurrentUser User, DelegationsHandlerMocks Mocks) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        db.Departments.Add(DelegationsTestData.CreateDepartment(DeptId, headUserId: LeaderId));
        db.Users.Add(DelegationsTestData.CreateUser(LeaderId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Leader, DeptId));
        db.SaveChanges();

        var user = new FakeDelegationsCurrentUser();
        var mocks = new DelegationsHandlerMocks();
        var handler = new PrepareVisitLogisticsCommandHandler(
            db, user, mocks.Clock, mocks.Email.Object, mocks.Tokens.Object, mocks.Sanitizer.Object,
            mocks.Storage.Object, mocks.Normalizer.Object, mocks.Notifications.Object);
        return (db, handler, user, mocks);
    }

    private static PrepareVisitLogisticsCommand SystemRequest(ulong? departmentId = DeptId, string title = "Welcome LED") =>
        new(DelegationsTestData.VisitInstanceId, departmentId, "LED", title, null, 1,
            "2026-08-01T08:00", "2026-08-01T12:00");

    [Theory]
    [InlineData(ParticipantStatuses.Invited)]
    [InlineData(ParticipantStatuses.Accepted)]
    [InlineData(ParticipantStatuses.Assigned)]
    public async Task LeaderAlreadyAParticipant_NeverBlocksTheSystemLogisticsRequest(string participantStatus)
    {
        var (db, handler, _, mocks) = CreateSut();
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(500, LeaderId, ParticipantRoles.DeptSupport, participantStatus));
        db.SaveChanges();

        var response = await handler.Handle(SystemRequest(), default);

        Assert.True(response.BusinessCreated);
        var item = Assert.Single(db.VisitLogisticsItems);
        Assert.Equal(LogisticsItemStatus.Requested, item.Status);
        Assert.Equal(DeptId, item.RequestedToDepartmentId);
        Assert.Equal(new DateTime(2026, 8, 1, 8, 0, 0), item.UsageStartAt);
        Assert.Equal(new DateTime(2026, 8, 1, 12, 0, 0), item.UsageEndAt);
        // The email still goes to the department's active leader.
        var mail = Assert.Single(mocks.SentEmails);
        Assert.Equal($"user{LeaderId}@test.local", mail.ToEmail);
    }

    [Fact]
    public async Task NoActiveLeader_RejectsTheSystemRequest()
    {
        var (db, handler, _, _) = CreateSut();
        db.Departments.Add(DelegationsTestData.CreateDepartment(21)); // GENERAL, no leader
        db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(SystemRequest(departmentId: 21), default));
        Assert.Empty(db.VisitLogisticsItems);
    }

    [Fact]
    public async Task DepartmentOutsideScope_IsRejected()
    {
        var (db, handler, _, _) = CreateSut();
        db.Departments.AddRange(
            DelegationsTestData.CreateDepartment(22, campusId: DelegationsTestData.OtherCampusId),
            DelegationsTestData.CreateDepartment(23, departmentType: "IC"),
            DelegationsTestData.CreateDepartment(24, status: EntityStatuses.Inactive));
        db.SaveChanges();

        foreach (var invalidDeptId in new ulong[] { 22, 23, 24 })
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                handler.Handle(SystemRequest(departmentId: invalidDeptId, title: $"LED {invalidDeptId}"), default));
        }
        Assert.Empty(db.VisitLogisticsItems);
    }

    [Fact]
    public async Task NonHostActor_IsForbidden()
    {
        var (_, handler, user, _) = CreateSut();
        user.UserId = 999;

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(SystemRequest(), default));
    }
}
