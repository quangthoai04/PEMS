using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Commands.InviteVisitParticipant;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.InviteVisitParticipant;

/// <summary>
/// IC_SUPPORT invitations: both IC Staff and Staff Leader are valid invitees (server-side
/// revalidation — a direct API call can't bypass the candidate query), the current host and users
/// holding an active slot are rejected, and DECLINED/REMOVED rows are reused on re-invite. The
/// participant row is committed BEFORE the email attempt.
/// </summary>
public class InviteVisitParticipantCommandHandlerTests
{
    private const ulong IcStaffId = 101;
    private const ulong StaffLeaderId = 102;

    private static (DelegationsTestDbContext Db, InviteVisitParticipantCommandHandler Handler,
        FakeDelegationsCurrentUser User, DelegationsHandlerMocks Mocks) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        db.Users.Add(DelegationsTestData.CreateUser(IcStaffId, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, 900));
        db.Users.Add(DelegationsTestData.CreateUser(StaffLeaderId, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, 900));
        db.SaveChanges();

        var user = new FakeDelegationsCurrentUser();
        var mocks = new DelegationsHandlerMocks();
        var handler = new InviteVisitParticipantCommandHandler(
            db, user, mocks.Clock, mocks.Email.Object, mocks.Tokens.Object, mocks.Sanitizer.Object,
            mocks.Storage.Object, mocks.Normalizer.Object, mocks.Notifications.Object);
        return (db, handler, user, mocks);
    }

    private static InviteVisitParticipantCommand IcSupportCommand(ulong userId) =>
        new(DelegationsTestData.VisitInstanceId, "IC_SUPPORT", userId, null, null);

    [Fact]
    public async Task InviteStaffLeader_CreatesIcSupportParticipant_AndSendsEmail()
    {
        var (db, handler, _, mocks) = CreateSut();

        var response = await handler.Handle(IcSupportCommand(StaffLeaderId), default);

        Assert.Equal(ParticipantRoles.IcSupport, response.ParticipantRole);
        Assert.Equal(ParticipantStatuses.Invited, response.Status);
        Assert.True(response.EmailQueued);
        Assert.Equal("SENT", response.EmailStatus);

        var row = Assert.Single(db.VisitParticipants.Where(p => p.UserId == StaffLeaderId));
        Assert.Equal(ParticipantRoles.IcSupport, row.ParticipantRole);
        Assert.Equal(ParticipantStatuses.Invited, row.Status);
        Assert.False(row.IsHost);
        Assert.Equal(DelegationsTestData.HostUserId, row.InvitedBy);

        var mail = Assert.Single(mocks.SentEmails);
        Assert.Equal($"user{StaffLeaderId}@test.local", mail.ToEmail);
        // ACCEPT + DECLINE one-time tokens persisted for the participant.
        Assert.Equal(2, db.EmailActionTokens.Count(t => t.TargetId == row.ParticipantId));
        mocks.Notifications.Verify(n => n.CreateAsync(
            It.Is<PEMS.Application.Notifications.Common.CreateNotificationRequest>(r => r.RecipientUserId == StaffLeaderId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InviteIcStaff_StillWorks()
    {
        var (db, handler, _, _) = CreateSut();

        var response = await handler.Handle(IcSupportCommand(IcStaffId), default);

        Assert.Equal(ParticipantRoles.IcSupport, response.ParticipantRole);
        Assert.Equal(ParticipantStatuses.Invited, response.Status);
        Assert.Single(db.VisitParticipants.Where(p => p.UserId == IcStaffId));
    }

    [Fact]
    public async Task InvitingTheCurrentHost_IsRejected_EvenWhenTheHostIsAStaffLeader()
    {
        var (db, handler, _, _) = CreateSut();
        var host = db.Users.Single(u => u.UserId == DelegationsTestData.HostUserId);
        host.SubRole = UserSubRoles.Leader;
        db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(IcSupportCommand(DelegationsTestData.HostUserId), default));
        Assert.Empty(db.VisitParticipants);
    }

    [Theory]
    [InlineData(ParticipantStatuses.Invited)]
    [InlineData(ParticipantStatuses.Accepted)]
    [InlineData(ParticipantStatuses.Assigned)]
    public async Task DuplicateActiveParticipant_ReturnsConflict(string participantStatus)
    {
        var (db, handler, _, mocks) = CreateSut();
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(500, StaffLeaderId, ParticipantRoles.IcSupport, participantStatus));
        db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(IcSupportCommand(StaffLeaderId), default));
        Assert.Empty(mocks.SentEmails);
    }

    [Fact]
    public async Task StaffLeaderOutsideScope_IsRejected()
    {
        var (db, handler, _, _) = CreateSut();
        db.Departments.AddRange(
            DelegationsTestData.CreateDepartment(901, departmentType: "IC", campusId: DelegationsTestData.OtherCampusId),
            DelegationsTestData.CreateDepartment(902), // GENERAL — not IC
            DelegationsTestData.CreateDepartment(903, departmentType: "IC", status: EntityStatuses.Inactive));
        db.Users.AddRange(
            DelegationsTestData.CreateUser(103, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, 901, campusId: DelegationsTestData.OtherCampusId),
            DelegationsTestData.CreateUser(104, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, 900, status: "INACTIVE"),
            DelegationsTestData.CreateUser(105, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, 902),
            DelegationsTestData.CreateUser(106, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, 903));
        db.SaveChanges();

        foreach (var invalidUserId in new ulong[] { 103, 104, 105, 106 })
        {
            await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(IcSupportCommand(invalidUserId), default));
        }
        Assert.Empty(db.VisitParticipants);
    }

    [Theory]
    [InlineData(ParticipantStatuses.Declined)]
    [InlineData(ParticipantStatuses.Removed)]
    public async Task ReinvitingAClosedSlot_ReusesTheExistingRow(string previousStatus)
    {
        var (db, handler, _, _) = CreateSut();
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(500, StaffLeaderId, ParticipantRoles.IcSupport, previousStatus));
        db.SaveChanges();

        var response = await handler.Handle(IcSupportCommand(StaffLeaderId), default);

        Assert.Equal(500ul, response.ParticipantId);
        var row = Assert.Single(db.VisitParticipants.Where(p => p.UserId == StaffLeaderId));
        Assert.Equal(500ul, row.ParticipantId);
        Assert.Equal(ParticipantStatuses.Invited, row.Status);
        Assert.Null(row.RespondedAt);
    }

    [Fact]
    public async Task EmailFailure_DoesNotLoseTheCommittedInvitation()
    {
        var (db, handler, _, mocks) = CreateSut();
        mocks.FailEmail = true;

        var response = await handler.Handle(IcSupportCommand(StaffLeaderId), default);

        Assert.False(response.EmailQueued);
        Assert.Equal("FAILED", response.EmailStatus);
        var row = Assert.Single(db.VisitParticipants.Where(p => p.UserId == StaffLeaderId));
        Assert.Equal(ParticipantStatuses.Invited, row.Status);
        Assert.Equal("FAILED", db.SentEmails.Single().Status);
    }
}
