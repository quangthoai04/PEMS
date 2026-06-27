using MediatR;

namespace PEMS.Application.PublicContent.Commands.MarkNotificationsRead;

/// <summary>
/// Marks notifications as read for the current user.
/// If <see cref="NotificationId"/> is provided, marks only that one.
/// Otherwise marks all unread notifications for the current user.
/// </summary>
public sealed record MarkNotificationsReadCommand(ulong? NotificationId) : IRequest;
