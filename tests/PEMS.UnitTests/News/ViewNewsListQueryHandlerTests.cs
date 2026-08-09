using PEMS.Application.News.Queries.ViewNewsList;
using PEMS.Domain.Constants;
using PEMS.Shared;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.NewsManagementTests;

/// <summary>
/// "Thêm tin tức mới" on the News Management page (Quản lý tin tức). ResolveViewerMode puts a Staff
/// Leader into REVIEWER mode (campus-wide approve/reject) while plain Staff/Student go into AUTHOR
/// mode — but ResolveCanCreateNewsAsync used to say the button exists ONLY for AUTHOR, so a Staff
/// Leader NEVER got an entry point here even when they host a visit of their own (the exact scenario
/// VisitNewsAccess.Evaluate already allows on the per-visit process page — see
/// GetVisitInstanceNewsQueryHandlerTests). The fix makes REVIEWER eligible too, same as AUTHOR;
/// HO stays excluded (HoReadonly), unaffected by this fix.
/// </summary>
public class ViewNewsListQueryHandlerTests
{
    private static (DelegationsTestDbContext Db, ViewNewsListQueryHandler Handler, FakeDelegationsCurrentUser CurrentUser)
        CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        var currentUser = new FakeDelegationsCurrentUser();
        var handler = new ViewNewsListQueryHandler(db, currentUser);
        return (db, handler, currentUser);
    }

    [Fact]
    public async Task Staff_leader_now_gets_the_create_button_same_as_plain_staff()
    {
        var (_, handler, currentUser) = CreateSut();
        currentUser.UserId = DelegationsTestData.HostUserId;
        currentUser.RoleCode = RoleCodes.Staff;
        currentUser.SubRole = UserSubRoles.Leader;
        currentUser.PrimaryCampusId = DelegationsTestData.CampusId;

        var result = await handler.Handle(new ViewNewsListQuery(), default);

        Assert.Equal(NewsConstants.ViewerMode.Reviewer, result.ViewerMode);
        Assert.True(result.CanCreateNews);
    }

    [Fact]
    public async Task Plain_staff_still_gets_the_create_button_unaffected()
    {
        var (_, handler, currentUser) = CreateSut();
        currentUser.UserId = DelegationsTestData.HostUserId;
        currentUser.RoleCode = RoleCodes.Staff;
        currentUser.SubRole = UserSubRoles.Staff;
        currentUser.PrimaryCampusId = DelegationsTestData.CampusId;

        var result = await handler.Handle(new ViewNewsListQuery(), default);

        Assert.Equal(NewsConstants.ViewerMode.Author, result.ViewerMode);
        Assert.True(result.CanCreateNews);
    }

    [Fact]
    public async Task Ho_never_gets_the_create_button()
    {
        var (db, handler, currentUser) = CreateSut();
        db.Roles.Add(DelegationsTestData.CreateRole(6, RoleCodes.Ho));
        db.Users.Add(DelegationsTestData.CreateUser(900, 6, null, null));
        db.SaveChanges();
        currentUser.UserId = 900;
        currentUser.RoleCode = RoleCodes.Ho;
        currentUser.SubRole = null;
        currentUser.PrimaryCampusId = null;

        var result = await handler.Handle(new ViewNewsListQuery(), default);

        Assert.Equal(NewsConstants.ViewerMode.HoReadonly, result.ViewerMode);
        Assert.False(result.CanCreateNews);
    }
}
