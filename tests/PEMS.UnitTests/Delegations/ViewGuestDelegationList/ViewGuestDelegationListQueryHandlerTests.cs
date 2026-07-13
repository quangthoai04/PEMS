using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Delegations.Queries.ViewGuestDelegationList;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;
using Moq;
using PEMS.Application.Common.Interfaces;
using System;

namespace PEMS.UnitTests.Delegations.ViewGuestDelegationList;

public class ViewGuestDelegationListQueryHandlerTests
{
    private static (DelegationsTestDbContext Db, ViewGuestDelegationListQueryHandler Handler, FakeDelegationsCurrentUser User) CreateSut(ulong userId, string roleCode, string subRole)
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
        
        return (db, new ViewGuestDelegationListQueryHandler(db, user, clockMock.Object), user);
    }

    [Fact]
    public async Task StaffLeader_CanViewAttendingTab_LegacyRuleBypassed()
    {
        const ulong userId = 101;
        var (db, handler, _) = CreateSut(userId, RoleCodes.Staff, UserSubRoles.Leader);
        
        var participant = DelegationsTestData.CreateParticipant(500, userId, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted);
        participant.VisitInstanceId = DelegationsTestData.VisitInstanceId;
        db.VisitParticipants.Add(participant);
        db.SaveChanges();

        var result = await handler.Handle(new ViewGuestDelegationListQuery { Tab = "attending", Page = 1, PageSize = 10 }, CancellationToken.None);

        // Expect it not to be blocked by the legacy rule, should return the assigned invitation/item.
        Assert.NotEmpty(result.Items);
    }
}
