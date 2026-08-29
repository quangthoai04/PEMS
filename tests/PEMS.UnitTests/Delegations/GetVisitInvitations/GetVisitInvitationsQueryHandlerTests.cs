using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Delegations.Queries.GetVisitInvitations;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;
using Moq;
using PEMS.Application.Common.Interfaces;
using System;

namespace PEMS.UnitTests.Delegations.GetVisitInvitations;

public class GetVisitInvitationsQueryHandlerTests
{
    private static (DelegationsTestDbContext Db, GetVisitInvitationsQueryHandler Handler, FakeDelegationsCurrentUser User) CreateSut(ulong userId, string roleCode, string subRole)
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        db.Users.Add(DelegationsTestData.CreateUser(userId, 
            roleCode == RoleCodes.Staff ? DelegationsTestData.StaffRoleId : DelegationsTestData.StudentRoleId, 
            subRole, 900));
        db.SaveChanges();
        var user = new FakeDelegationsCurrentUser { UserId = userId, RoleCode = roleCode, SubRole = subRole, IsAuthenticated = true };
        
        var clockMock = new Mock<IDateTimeService>();
        clockMock.Setup(c => c.VietnamNow).Returns(DateTime.UtcNow);
        
        return (db, new GetVisitInvitationsQueryHandler(db, user, clockMock.Object), user);
    }

    [Fact]
    public async Task StaffLeader_WithIcSupport_AndAcceptedStatus_HasOpenContributionAction()
    {
        const ulong userId = 101;
        var (db, handler, _) = CreateSut(userId, RoleCodes.Staff, UserSubRoles.Leader);
        
        var participant = DelegationsTestData.CreateParticipant(500, userId, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted);
        participant.VisitInstanceId = DelegationsTestData.VisitInstanceId;
        db.VisitParticipants.Add(participant);
        
        // Ensure VisitRequest/VisitInstance are in valid states for ACTIVE invitation
        var vr = db.VisitRequests.First(r => r.VisitRequestId == DelegationsTestData.VisitRequestId);
        vr.Status = VisitRequestStatuses.Approved;
        
        var vi = db.VisitRequestCampuses.First(i => i.VisitInstanceId == DelegationsTestData.VisitInstanceId);
        vi.Status = VisitInstanceStatuses.BeforeVisit;
        
        db.SaveChanges();

        var result = await handler.Handle(new GetVisitInvitationsQuery(), CancellationToken.None);

        var invite = Assert.Single(result.Items);
        Assert.Contains("OPEN_CONTRIBUTION", invite.AllowedActions);
    }

    // DB-PAGE-002: Page/PageSize were passed straight through to Skip/Take with no bound, so a client
    // could request an arbitrarily large page (or a non-positive Page/PageSize) in one query.
    [Theory]
    [InlineData(0, 20, 1, 20)]      // Page < 1 floors to 1
    [InlineData(-5, 20, 1, 20)]     // negative Page floors to 1
    [InlineData(1, 0, 1, 1)]        // PageSize < 1 floors to 1
    [InlineData(1, 100000, 1, 100)] // PageSize > 100 ceilings to 100
    public async Task PageAndPageSize_AreClamped(int requestedPage, int requestedPageSize, int expectedPage, int expectedPageSize)
    {
        const ulong userId = 102;
        var (db, handler, _) = CreateSut(userId, RoleCodes.Staff, UserSubRoles.Leader);

        var participant = DelegationsTestData.CreateParticipant(501, userId, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted);
        participant.VisitInstanceId = DelegationsTestData.VisitInstanceId;
        db.VisitParticipants.Add(participant);
        db.SaveChanges();

        var result = await handler.Handle(
            new GetVisitInvitationsQuery { Page = requestedPage, PageSize = requestedPageSize },
            CancellationToken.None);

        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedPageSize, result.PageSize);
    }
}
