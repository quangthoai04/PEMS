using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Notifications;

namespace PEMS.Application.Notifications.Common;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;

    public NotificationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(
        ulong recipientUserId,
        string title,
        string? message,
        string notificationType,
        string? relatedType,
        ulong? relatedId,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == recipientUserId && u.Status == "ACTIVE", cancellationToken);

        if (user == null)
        {
            return; // Only send to active users
        }

        var notification = new Notification
        {
            RecipientUserId = recipientUserId,
            Title = title.Trim().Length > 255 ? title.Trim().Substring(0, 255) : title.Trim(),
            Message = message,
            NotificationType = notificationType,
            RelatedType = relatedType,
            RelatedId = relatedId,
            IsRead = false,
            ReadAt = null,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateManyAsync(
        IEnumerable<CreateNotificationItem> items,
        CancellationToken cancellationToken)
    {
        var validItems = items.DistinctBy(i => new { i.RecipientUserId, i.NotificationType, i.RelatedType, i.RelatedId }).ToList();
        
        var recipientIds = validItems.Select(i => i.RecipientUserId).Distinct().ToList();
        
        var activeUsers = await _context.Users
            .Where(u => recipientIds.Contains(u.UserId) && u.Status == "ACTIVE")
            .Select(u => u.UserId)
            .ToListAsync(cancellationToken);

        var notifications = new List<Notification>();

        foreach (var item in validItems)
        {
            if (!activeUsers.Contains(item.RecipientUserId))
                continue;

            notifications.Add(new Notification
            {
                RecipientUserId = item.RecipientUserId,
                Title = item.Title.Trim().Length > 255 ? item.Title.Trim().Substring(0, 255) : item.Title.Trim(),
                Message = item.Message,
                NotificationType = item.NotificationType,
                RelatedType = item.RelatedType,
                RelatedId = item.RelatedId,
                IsRead = false,
                ReadAt = null,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (notifications.Any())
        {
            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
