using System.Collections.Generic;
using MediatR;
using PEMS.Application.Common.Models;
using PEMS.Application.Notifications.Common;

namespace PEMS.Application.Notifications.Queries.GetMyNotifications;

public sealed record GetMyNotificationsQuery(
    int Page,
    int PageSize,
    bool? IsRead,
    IReadOnlyCollection<string>? Categories = null,
    bool? IsActionRequired = null) : IRequest<PaginatedResult<NotificationDto>>;
