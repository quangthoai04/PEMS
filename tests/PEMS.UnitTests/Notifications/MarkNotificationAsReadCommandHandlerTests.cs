using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Notifications.Commands.MarkNotificationAsRead;
using PEMS.Domain.Entities.Notifications;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Notifications;

/// <summary>
/// SEC-19 remediation. A missing notification threw a bare <c>Exception</c> and an unauthenticated
/// caller threw <c>UnauthorizedAccessException</c> — both unmatched by
/// <c>ExceptionHandlingMiddleware</c>'s switch and surfaced as a generic 500 instead of a clean HTTP
/// status. Fixed with <c>NotFoundException</c> (404) and <c>ForbiddenException</c> (403)
/// respectively — the not-found/wrong-owner cases stay merged into one exception, on purpose,
/// preserving the existing anti-enumeration property (a notification id the caller may not touch
/// stays indistinguishable from one that does not exist at all).
/// </summary>
public class MarkNotificationAsReadCommandHandlerTests
{
    private const ulong OwnerId = 900;
    private const ulong OtherUserId = 901;
    private const ulong NotificationId = 5001;

    private static DbSet<Notification> Notifications(DelegationsTestDbContext db)
        => ((IApplicationDbContext)db).Notifications;

    private static (DelegationsTestDbContext Db, MarkNotificationAsReadCommandHandler Handler)
        CreateSut(ulong actorId = OwnerId)
    {
        var db = DelegationsTestDbContext.Create();
        Notifications(db).Add(new Notification
        {
            NotificationId = NotificationId,
            RecipientUserId = OwnerId,
            NotificationType = "TEST",
            Title = "Test notification",
            IsRead = false,
            CreatedAt = new DateTime(2026, 8, 1),
        });
        db.SaveChanges();

        var actor = new FakeDelegationsCurrentUser { UserId = actorId };
        var handler = new MarkNotificationAsReadCommandHandler(db, actor);
        return (db, handler);
    }

    [Fact]
    public async Task Owner_MarksItRead()
    {
        var (db, handler) = CreateSut();

        var result = await handler.Handle(new MarkNotificationAsReadCommand(NotificationId), CancellationToken.None);

        Assert.True(result);
        var notification = Notifications(db).Single(n => n.NotificationId == NotificationId);
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
    }

    [Fact]
    public async Task MissingNotification_Is404NotFound_NotAGeneric500()
    {
        var (_, handler) = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new MarkNotificationAsReadCommand(999999), CancellationToken.None));
    }

    [Fact]
    public async Task NotificationBelongingToSomeoneElse_Is404NotFound_NotAGeneric500()
    {
        // Same exception as "missing" — deliberately, so the response cannot be used to enumerate
        // which notification ids exist for other users.
        var (_, handler) = CreateSut(actorId: OtherUserId);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new MarkNotificationAsReadCommand(NotificationId), CancellationToken.None));
    }

    [Fact]
    public async Task Unauthenticated_Is403Forbidden_NotAGeneric500()
    {
        var db = DelegationsTestDbContext.Create();
        var actor = new FakeDelegationsCurrentUser { UserId = null };
        var handler = new MarkNotificationAsReadCommandHandler(db, actor);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new MarkNotificationAsReadCommand(NotificationId), CancellationToken.None));
    }

    [Fact]
    public async Task AlreadyRead_IsANoOp_DoesNotOverwriteReadAt()
    {
        var (db, handler) = CreateSut();
        var readAt = new DateTime(2026, 8, 2, 10, 0, 0);
        var notification = Notifications(db).Single(n => n.NotificationId == NotificationId);
        notification.IsRead = true;
        notification.ReadAt = readAt;
        db.SaveChanges();

        var result = await handler.Handle(new MarkNotificationAsReadCommand(NotificationId), CancellationToken.None);

        Assert.True(result);
        Assert.Equal(readAt, Notifications(db).Single(n => n.NotificationId == NotificationId).ReadAt);
    }
}
