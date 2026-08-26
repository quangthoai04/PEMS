using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PEMS.Api.Controllers;
using PEMS.Application.Common.Models;
using PEMS.Application.Notifications.Common;
using PEMS.Application.Notifications.Queries.GetMyNotifications;

namespace PEMS.UnitTests.Notifications;

public sealed class NotificationsControllerTests
{
    [Fact]
    public async Task GetMyNotifications_parses_grouped_categories_and_action_filter()
    {
        var mediator = new Mock<IMediator>();
        GetMyNotificationsQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetMyNotificationsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<PaginatedResult<NotificationDto>>, CancellationToken>((request, _) =>
                captured = (GetMyNotificationsQuery)request)
            .ReturnsAsync(PaginatedResult<NotificationDto>.Create(Array.Empty<NotificationDto>(), 2, 10, 0));

        var controller = new NotificationsController(mediator.Object);

        var response = await controller.GetMyNotifications(
            page: 2,
            pageSize: 10,
            isRead: false,
            category: "visit, reminder, VISIT",
            isActionRequired: true);

        Assert.IsType<OkObjectResult>(response);
        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Page);
        Assert.Equal(10, captured.PageSize);
        Assert.False(captured.IsRead);
        Assert.True(captured.IsActionRequired);
        Assert.Equal(new[] { "VISIT", "REMINDER" }, captured.Categories);
    }

    [Fact]
    public async Task GetMyNotifications_leaves_categories_null_when_category_is_blank()
    {
        var mediator = new Mock<IMediator>();
        GetMyNotificationsQuery? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<GetMyNotificationsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<PaginatedResult<NotificationDto>>, CancellationToken>((request, _) =>
                captured = (GetMyNotificationsQuery)request)
            .ReturnsAsync(PaginatedResult<NotificationDto>.Create(Array.Empty<NotificationDto>(), 1, 20, 0));

        var controller = new NotificationsController(mediator.Object);

        await controller.GetMyNotifications(
            page: 1,
            pageSize: 20,
            isRead: null,
            category: "   ",
            isActionRequired: null);

        Assert.NotNull(captured);
        Assert.Null(captured!.Categories);
        Assert.Null(captured.IsActionRequired);
    }
}
