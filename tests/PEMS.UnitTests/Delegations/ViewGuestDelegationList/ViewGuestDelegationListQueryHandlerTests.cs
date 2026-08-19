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

    /// <summary>
    /// The VisitRequestId filter exists for one reason: a notification deep link names a request/
    /// instance id, and the resolver that opens it must find that ONE row regardless of whatever
    /// other rows this caller also happens to see — see
    /// PEMS_Notification_Visit_DeepLink_OneShot_Fix_Plan.md §24.
    /// </summary>
    [Fact]
    public async Task VisitRequestIdFilter_ReturnsOnlyThatRequest_IgnoringOtherVisibleRows()
    {
        const ulong userId = 101;
        var (db, handler, _) = CreateSut(userId, RoleCodes.Staff, UserSubRoles.Staff);

        db.VisitRequests.Add(DelegationsTestData.CreateVisitRequest(20));
        db.VisitRequestCampuses.Add(DelegationsTestData.CreateVisitInstance(
            visitInstanceId: 200, visitRequestId: 20, currentHostUserId: userId));

        db.VisitRequests.Add(DelegationsTestData.CreateVisitRequest(30));
        db.VisitRequestCampuses.Add(DelegationsTestData.CreateVisitInstance(
            visitInstanceId: 300, visitRequestId: 30, currentHostUserId: userId));
        db.SaveChanges();

        // Sanity: without the filter this caller hosts both — the filter is what narrows it.
        var unfiltered = await handler.Handle(
            new ViewGuestDelegationListQuery { Page = 1, PageSize = 10 }, CancellationToken.None);
        Assert.Equal(2, unfiltered.Items.Count);

        var scoped = await handler.Handle(
            new ViewGuestDelegationListQuery { Page = 1, PageSize = 10, VisitRequestId = 20 },
            CancellationToken.None);

        var only = Assert.Single(scoped.Items);
        Assert.Equal(20UL, only.VisitRequestId);
    }

    /// <summary>
    /// The filter narrows WHICH request, never WHETHER this caller may see it — a notification
    /// deep link can never bypass authorization just because it names an id (plan §10, §26).
    /// </summary>
    [Fact]
    public async Task VisitRequestIdFilter_StillEnforcesAuthorization_EmptyWhenCallerHasNoRelation()
    {
        const ulong userId = 101;
        var (db, handler, _) = CreateSut(userId, RoleCodes.Staff, UserSubRoles.Staff);
        // SeedBase's request/instance (id 10) is hosted by user 100, not 101.

        var result = await handler.Handle(
            new ViewGuestDelegationListQuery
            {
                Page = 1,
                PageSize = 10,
                VisitRequestId = DelegationsTestData.VisitRequestId,
            },
            CancellationToken.None);

        Assert.Empty(result.Items);
    }
}
