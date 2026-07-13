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
}
