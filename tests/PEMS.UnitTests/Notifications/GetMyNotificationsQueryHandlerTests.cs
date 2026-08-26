using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Notifications.Common;
using PEMS.Application.Notifications.Queries.GetMyNotifications;
using PEMS.Domain.Entities.Notifications;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Notifications;

/// <summary>
/// PEMS_NOTIFICATION pagination fix: category/isActionRequired must be applied BEFORE
/// CountAsync/Skip/Take, never as a client-side filter over an already-paginated page. Pins the
/// exact scenario reported live — a tab whose category is a small fraction of the recipient's total
/// notifications used to show a sparse page ("2 items") against a stale total-pages count computed
/// over the unfiltered set ("Trang 1/14").
/// </summary>
public class GetMyNotificationsQueryHandlerTests
{
    private const ulong UserId = 900;

    private static DbSet<Notification> Notifications(DelegationsTestDbContext db)
        => ((IApplicationDbContext)db).Notifications;

    private static (DelegationsTestDbContext Db, GetMyNotificationsQueryHandler Handler) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        var actor = new FakeDelegationsCurrentUser { UserId = UserId };
        var handler = new GetMyNotificationsQueryHandler(db, actor);
        return (db, handler);
    }

    private static Notification Make(ulong id, string category, bool isActionRequired = false, bool isRead = false)
        => new()
        {
            NotificationId = id,
            RecipientUserId = UserId,
            NotificationType = "TEST",
            Category = category,
            IsActionRequired = isActionRequired,
            Title = $"Notification {id}",
            IsRead = isRead,
            CreatedAt = new DateTime(2026, 8, 1).AddMinutes(id),
        };

    [Fact]
    public async Task Category_IsFilteredBeforeCounting_TotalsMatchTheFilteredSet()
    {
        var (db, handler) = CreateSut();
        for (ulong i = 1; i <= 10; i++) Notifications(db).Add(Make(i, NotificationCategories.Visit));
        for (ulong i = 11; i <= 20; i++) Notifications(db).Add(Make(i, NotificationCategories.Invitation));
        await db.SaveChangesAsync();

        var result = await handler.Handle(
            new GetMyNotificationsQuery(1, 5, null, new[] { NotificationCategories.Visit }, null),
            CancellationToken.None);

        Assert.Equal(5, result.Items.Count);
        Assert.Equal(10, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.All(result.Items, i => Assert.Equal(NotificationCategories.Visit, i.Category));
    }

    [Fact]
    public async Task MultiCategoryFilter_PaginatesTheUnionAsOneDataset()
    {
        var (db, handler) = CreateSut();
        ulong id = 1;
        for (var i = 0; i < 7; i++) Notifications(db).Add(Make(id++, NotificationCategories.Visit));
        for (var i = 0; i < 4; i++) Notifications(db).Add(Make(id++, NotificationCategories.Reminder));
        for (var i = 0; i < 9; i++) Notifications(db).Add(Make(id++, NotificationCategories.Invitation));
        await db.SaveChangesAsync();

        var categories = new[] { NotificationCategories.Visit, NotificationCategories.Reminder };
        var page1 = await handler.Handle(new GetMyNotificationsQuery(1, 10, null, categories, null), CancellationToken.None);
        var page2 = await handler.Handle(new GetMyNotificationsQuery(2, 10, null, categories, null), CancellationToken.None);

        Assert.Equal(11, page1.TotalItems);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(1, page2.Items.Count);
        Assert.All(page1.Items.Concat(page2.Items), i => Assert.Contains(i.Category, categories));
    }

    [Fact]
    public async Task IsActionRequiredFilter_CountsOnlyActionRequiredRows()
    {
        var (db, handler) = CreateSut();
        for (ulong i = 1; i <= 3; i++) Notifications(db).Add(Make(i, NotificationCategories.Visit, isActionRequired: true));
        for (ulong i = 4; i <= 9; i++) Notifications(db).Add(Make(i, NotificationCategories.Visit, isActionRequired: false));
        await db.SaveChangesAsync();

        var result = await handler.Handle(
            new GetMyNotificationsQuery(1, 10, null, null, true), CancellationToken.None);

        Assert.Equal(3, result.TotalItems);
        Assert.All(result.Items, i => Assert.True(i.IsActionRequired));
    }

    [Fact]
    public async Task IsRead_And_Category_AreBothAppliedBeforePagination()
    {
        var (db, handler) = CreateSut();
        Notifications(db).Add(Make(1, NotificationCategories.Visit, isRead: false));
        Notifications(db).Add(Make(2, NotificationCategories.Visit, isRead: true));
        Notifications(db).Add(Make(3, NotificationCategories.Invitation, isRead: false));
        await db.SaveChangesAsync();

        var result = await handler.Handle(
            new GetMyNotificationsQuery(1, 10, false, new[] { NotificationCategories.Visit }, null),
            CancellationToken.None);

        Assert.Equal(1, result.TotalItems);
        Assert.Equal((ulong)1, result.Items.Single().NotificationId);
    }

    [Fact]
    public async Task NoFilters_ReturnsEveryNotificationForTheRecipientOnly()
    {
        var (db, handler) = CreateSut();
        Notifications(db).Add(Make(1, NotificationCategories.Visit));
        Notifications(db).Add(new Notification
        {
            NotificationId = 2,
            RecipientUserId = UserId + 1,
            NotificationType = "TEST",
            Category = NotificationCategories.Visit,
            Title = "Someone else's notification",
            CreatedAt = new DateTime(2026, 8, 1),
        });
        await db.SaveChangesAsync();

        var result = await handler.Handle(new GetMyNotificationsQuery(1, 10, null), CancellationToken.None);

        Assert.Equal(1, result.TotalItems);
        Assert.Equal((ulong)1, result.Items.Single().NotificationId);
    }
}
