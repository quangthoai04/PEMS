using MediatR;

namespace PEMS.Application.Notifications.Commands.MarkNotificationAsRead;

public sealed record MarkNotificationAsReadCommand(ulong NotificationId) : IRequest<bool>;
