using MediatR;

namespace PEMS.Application.Notifications.Commands.MarkAllNotificationsAsRead;

public sealed record MarkAllNotificationsAsReadCommand : IRequest<MarkAllNotificationsAsReadResponse>;
