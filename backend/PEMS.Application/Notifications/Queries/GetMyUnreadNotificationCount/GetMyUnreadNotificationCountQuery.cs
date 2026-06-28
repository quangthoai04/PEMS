using MediatR;

namespace PEMS.Application.Notifications.Queries.GetMyUnreadNotificationCount;

public sealed record GetMyUnreadNotificationCountQuery : IRequest<UnreadNotificationCountResponse>;
