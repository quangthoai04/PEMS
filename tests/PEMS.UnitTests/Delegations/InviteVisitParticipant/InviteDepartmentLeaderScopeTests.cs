using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Commands.InviteVisitParticipant;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.InviteVisitParticipant;

/// <summary>
/// The backend half of the department-invitation scope.
///
/// <para>
/// A DEPT_SUPPORT invitation names a DEPARTMENT; the invitee is the department's active leader, and the
/// handler resolves them itself — the frontend never supplies that id, so an approval cannot be aimed at
/// an arbitrary account by editing a request body. The scope the send verifies an edited message against
/// is built from THAT resolved id, which is why a preview bound to anything else is refused.
/// </para>
/// <para>
/// These tests state the id the send actually uses, so the frontend's matching assertion
/// (participantInvitationPreviewScope.test.tsx) is anchored to something rather than to itself. They
/// also pin the tie-break: with two active leaders and no configured head, the handler must pick the
/// same one the host was shown in the department list, or the two ends disagree by name order alone.
/// </para>
/// </summary>
public class InviteDepartmentLeaderScopeTests
{
    private const ulong GeneralDeptId = 800;

    private static (DelegationsTestDbContext Db, InviteVisitParticipantCommandHandler Handler,
        StubApprovedEmailContentResolver Approved) CreateSut(
        Action<DelegationsTestDbContext>? seedExtra = null, ulong? departmentHeadUserId = null)
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        db.Departments.Add(DelegationsTestData.CreateDepartment(
            GeneralDeptId, departmentType: "GENERAL", headUserId: departmentHeadUserId));
        db.SaveChanges();
        seedExtra?.Invoke(db);
        db.SaveChanges();

        var mocks = new DelegationsHandlerMocks();
        var formRead = new Mock<PEMS.Application.Delegations.Services.VisitFormRead.IVisitFormReadService>();
        formRead
            .Setup(f => f.ResolveCampusFormContentAsync(
                It.IsAny<PEMS.Domain.Entities.Delegations.VisitRequest>(),
                It.IsAny<IReadOnlyList<ulong>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PEMS.Domain.Entities.Delegations.VisitRequest _, IReadOnlyList<ulong> ids, CancellationToken _) =>
                ids.ToDictionary(
                        id => id,
                        _ => new PEMS.Application.Delegations.Services.VisitFormRead.VisitCampusFormContent
                        {
                            DelegationName = "Đoàn khách kiểm thử",
                        })
                   as IReadOnlyDictionary<ulong, PEMS.Application.Delegations.Services.VisitFormRead.VisitCampusFormContent>);

        var approved = new StubApprovedEmailContentResolver(mocks.Sanitizer.Object);
        var handler = new InviteVisitParticipantCommandHandler(
            db, new FakeDelegationsCurrentUser(), mocks.Clock, mocks.DispatcherFor(db), mocks.Tokens.Object,
            mocks.Sanitizer.Object, mocks.Storage.Object, mocks.Normalizer.Object, mocks.Notifications.Object,
            formRead.Object, new RecordingUserMutationLockService(), approved);
        return (db, handler, approved);
    }

    /// <summary>An active DEPARTMENT+LEADER of the general department, on the instance's campus.</summary>
    private static void AddLeader(DelegationsTestDbContext db, ulong userId, string fullName)
    {
        var user = DelegationsTestData.CreateUser(
            userId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Leader, GeneralDeptId);
        user.FullName = fullName;
        db.Users.Add(user);
    }

    private static InviteVisitParticipantCommand DeptCommand() =>
        new(DelegationsTestData.VisitInstanceId, "DEPT_SUPPORT", null, GeneralDeptId, null);

    [Fact]
    public async Task TheApprovalScope_IsTheResolvedLeader_NotTheDepartment()
    {
        var (db, handler, approved) = CreateSut(d => AddLeader(d, 600, "Phạm Thị Trưởng Phòng"));

        var response = await handler.Handle(DeptCommand(), default);

        Assert.Equal(ParticipantRoles.DeptSupport, response.ParticipantRole);
        var call = Assert.Single(approved.Calls);
        Assert.Equal(SystemEmailTemplates.VisitDepartmentLeaderInvitation, call.TemplateCode);
        // The exact string the frontend has to produce for an edited message to be accepted. It names
        // the leader; the department id appears nowhere in it.
        Assert.Equal($"visitInstance:{DelegationsTestData.VisitInstanceId}|participant:600", call.ScopeKey);
        Assert.DoesNotContain("department:", call.ScopeKey);
        // …and the participant row is the leader's, so scope and invitee are the same person.
        Assert.Equal(600UL, Assert.Single(db.VisitParticipants).UserId);
    }

    /// <summary>
    /// The configured department head wins when they are a valid active leader — the same rule the
    /// department list applies when it shows the host who they are about to invite.
    /// </summary>
    [Fact]
    public async Task TheConfiguredHead_IsPreferredOverOtherActiveLeaders()
    {
        var (_, handler, approved) = CreateSut(
            d => { AddLeader(d, 601, "An"); AddLeader(d, 602, "Bích"); },
            departmentHeadUserId: 602);

        await handler.Handle(DeptCommand(), default);

        Assert.Equal($"visitInstance:{DelegationsTestData.VisitInstanceId}|participant:602",
            Assert.Single(approved.Calls).ScopeKey);
    }

    /// <summary>
    /// Two active leaders, no configured head: the pick must be by name, matching
    /// GetSupportDepartments. Left to database order the two ends could resolve different people, and
    /// the host would sign an approval for the leader they were shown while the send addressed another.
    /// </summary>
    [Fact]
    public async Task WithNoConfiguredHead_TheFirstLeaderByName_IsTheOneInvited()
    {
        var (_, handler, approved) = CreateSut(d =>
        {
            // Name order and id order disagree on purpose: 611 is first alphabetically, 612 is what an
            // unordered query returns here. Only a handler that sorts by name answers 611.
            AddLeader(d, 611, "An Nhiên");
            AddLeader(d, 612, "Bích Ngọc");
        });

        await handler.Handle(DeptCommand(), default);

        Assert.Equal($"visitInstance:{DelegationsTestData.VisitInstanceId}|participant:611",
            Assert.Single(approved.Calls).ScopeKey);
    }

    /// <summary>
    /// No active leader, no invitation — and so no scope for an approval to be bound to. This is why
    /// the frontend offers no editable preview for such a department.
    /// </summary>
    [Fact]
    public async Task ADepartmentWithNoActiveLeader_CannotBeInvited()
    {
        var (_, handler, approved) = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(DeptCommand(), default));
        Assert.Empty(approved.Calls);
    }
}
