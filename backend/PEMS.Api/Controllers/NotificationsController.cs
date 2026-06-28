using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Notifications.Commands.MarkAllNotificationsAsRead;
using PEMS.Application.Notifications.Commands.MarkNotificationAsRead;
using PEMS.Application.Notifications.Queries.GetMyNotifications;
using PEMS.Application.Notifications.Queries.GetMyUnreadNotificationCount;

namespace PEMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] bool? isRead = null)
    {
        var result = await _mediator.Send(new GetMyNotificationsQuery(page, pageSize, isRead));
        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetMyUnreadNotificationCount()
    {
        var result = await _mediator.Send(new GetMyUnreadNotificationCountQuery());
        return Ok(result);
    }

    [HttpPatch("{notificationId}/read")]
    public async Task<IActionResult> MarkNotificationAsRead(ulong notificationId)
    {
        var result = await _mediator.Send(new MarkNotificationAsReadCommand(notificationId));
        return Ok(new { success = result });
    }

    [HttpPatch("mark-all-read")]
    public async Task<IActionResult> MarkAllNotificationsAsRead()
    {
        var result = await _mediator.Send(new MarkAllNotificationsAsReadCommand());
        return Ok(result);
    }
}
