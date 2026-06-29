using MediatR;
using PEMS.Application.Common.Models;
using PEMS.Application.Notifications.Common;

namespace PEMS.Application.Notifications.Queries.GetMyNotifications;

public sealed record GetMyNotificationsQuery(
    int Page,
    int PageSize,
    bool? IsRead) : IRequest<PaginatedResult<NotificationDto>>;
