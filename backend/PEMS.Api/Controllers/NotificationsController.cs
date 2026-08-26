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
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? isRead = null,
        [FromQuery] string? category = null,
        [FromQuery] bool? isActionRequired = null)
    {
        // `category` accepts one category (e.g. INVITATION) or a comma-separated group
        // (e.g. VISIT,REMINDER). Filtering must happen in the database BEFORE Count/Skip/Take;
        // otherwise the frontend receives a 10-row mixed page, removes non-matching rows locally,
        // and can display only 1-5 items while pagination still says "10 / page" and "1 / 14".
        var categories = string.IsNullOrWhiteSpace(category)
            ? null
            : category
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        var result = await _mediator.Send(new GetMyNotificationsQuery(
            page,
            pageSize,
            isRead,
            categories,
            isActionRequired));
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
