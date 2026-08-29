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

    // ── DB-PAGE-001: Page/PageSize are bounded, not used exactly as received ─────────────────────

    /// <summary>A request for a huge page size does not turn into an unbounded fetch — it is capped
    /// at 100, the same ceiling GetAdminAuditLogsQueryHandler and its siblings already use.</summary>
    [Fact]
    public async Task PageSize_AboveMaximum_IsClampedTo100_AndFetchIsActuallyBounded()
    {
        const ulong userId = 101;
        var (db, handler, _) = CreateSut(userId, RoleCodes.Staff, UserSubRoles.Staff);

        for (ulong i = 1; i <= 150; i++)
        {
            var visitRequestId = 1000 + i;
            var visitInstanceId = 2000 + i;
            db.VisitRequests.Add(DelegationsTestData.CreateVisitRequest(visitRequestId));
            db.VisitRequestCampuses.Add(DelegationsTestData.CreateVisitInstance(
                visitInstanceId: visitInstanceId, visitRequestId: visitRequestId, currentHostUserId: userId));
        }
        db.SaveChanges();

        var result = await handler.Handle(
            new ViewGuestDelegationListQuery { Page = 1, PageSize = 100_000 }, CancellationToken.None);

        Assert.Equal(100, result.PageSize);
        Assert.True(result.Items.Count <= 100,
            $"Expected at most 100 items, got {result.Items.Count} — PageSize was not bounded.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task PageSize_ZeroOrNegative_IsClampedToOne(int requestedPageSize)
    {
        const ulong userId = 101;
        var (db, handler, _) = CreateSut(userId, RoleCodes.Staff, UserSubRoles.Staff);
        db.VisitRequests.Add(DelegationsTestData.CreateVisitRequest(20));
        db.VisitRequestCampuses.Add(DelegationsTestData.CreateVisitInstance(
            visitInstanceId: 200, visitRequestId: 20, currentHostUserId: userId));
        db.SaveChanges();

        var result = await handler.Handle(
            new ViewGuestDelegationListQuery { Page = 1, PageSize = requestedPageSize }, CancellationToken.None);

        Assert.Equal(1, result.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Page_ZeroOrNegative_IsClampedToOne(int requestedPage)
    {
        const ulong userId = 101;
        var (db, handler, _) = CreateSut(userId, RoleCodes.Staff, UserSubRoles.Staff);
        db.VisitRequests.Add(DelegationsTestData.CreateVisitRequest(20));
        db.VisitRequestCampuses.Add(DelegationsTestData.CreateVisitInstance(
            visitInstanceId: 200, visitRequestId: 20, currentHostUserId: userId));
        db.SaveChanges();

        var result = await handler.Handle(
            new ViewGuestDelegationListQuery { Page = requestedPage, PageSize = 10 }, CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.NotEmpty(result.Items);
    }

    /// <summary>An ordinary in-range request is completely unaffected by the clamp — same Page/PageSize
    /// echoed back, same rows returned as before this fix.</summary>
    [Fact]
    public async Task PageSize_WithinRange_IsUnchanged()
    {
        const ulong userId = 101;
        var (db, handler, _) = CreateSut(userId, RoleCodes.Staff, UserSubRoles.Staff);
        db.VisitRequests.Add(DelegationsTestData.CreateVisitRequest(20));
        db.VisitRequestCampuses.Add(DelegationsTestData.CreateVisitInstance(
            visitInstanceId: 200, visitRequestId: 20, currentHostUserId: userId));
        db.SaveChanges();

        var result = await handler.Handle(
            new ViewGuestDelegationListQuery { Page = 1, PageSize = 25 }, CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.NotEmpty(result.Items);
    }
}
