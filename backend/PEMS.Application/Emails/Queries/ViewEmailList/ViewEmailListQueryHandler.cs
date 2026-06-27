using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Domain.Entities.Emails;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PEMS.Application.Emails.Queries.ViewEmailList;

public class ViewEmailListQueryHandler : IRequestHandler<ViewEmailListQuery, ViewEmailListResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ViewEmailListQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ViewEmailListResponse> Handle(ViewEmailListQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        var currentUserEmail = _currentUserService.Email;

        if (string.IsNullOrEmpty(currentUserEmail))
        {
            var user = await _context.Users.FindAsync(currentUserId);
            currentUserEmail = user?.Email ?? "";
        }

        var sentQuery = _context.SentEmails
            .Include(e => e.Recipients)
            .Where(e => e.SentBy == currentUserId)
            .Select(e => new EmailListItemDto
            {
                Id = e.SentEmailId,
                SourceType = "SENT",
                Subject = e.Subject,
                Snippet = e.BodySnapshot != null ? (e.BodySnapshot.Length > 100 ? e.BodySnapshot.Substring(0, 100) : e.BodySnapshot) : null,
                CounterpartName = e.Recipients.FirstOrDefault(r => r.RecipientType == "TO") != null ? e.Recipients.FirstOrDefault(r => r.RecipientType == "TO").RecipientName : null,
                CounterpartEmail = e.Recipients.FirstOrDefault(r => r.RecipientType == "TO") != null ? e.Recipients.FirstOrDefault(r => r.RecipientType == "TO").RecipientEmail : null,
                SentAt = e.SentAt,
                CreatedAt = e.CreatedAt,
                SendStatus = e.Status,
                DeliveryStatus = e.Recipients.FirstOrDefault(r => r.RecipientType == "TO") != null ? e.Recipients.FirstOrDefault(r => r.RecipientType == "TO").DeliveryStatus : null,
                ProcessStatus = e.DeliveredAt.HasValue ? "COMPLETED" : (e.Status == "FAILED" ? "FAILED" : "PROCESSING"),
                RelatedType = e.RelatedType,
                RelatedId = e.RelatedId,
                CanReply = false,
                CanConfirm = false,
                CanMarkComplete = !e.DeliveredAt.HasValue && e.Status != "FAILED"
            });

        var receivedQuery = _context.SentEmails
            .Include(e => e.Recipients)
            .Where(e => e.Recipients.Any(r => r.RecipientEmail == currentUserEmail))
            .Select(e => new EmailListItemDto
            {
                Id = e.SentEmailId,
                SourceType = "RECEIVED",
                Subject = e.Subject,
                Snippet = e.BodySnapshot != null ? (e.BodySnapshot.Length > 100 ? e.BodySnapshot.Substring(0, 100) : e.BodySnapshot) : null,
                CounterpartName = "System/Sender", // Could join with Users on SentBy if needed
                CounterpartEmail = "sender@pems.local", // Simplification
                SentAt = e.SentAt,
                CreatedAt = e.CreatedAt,
                SendStatus = e.Status,
                DeliveryStatus = e.Recipients.FirstOrDefault(r => r.RecipientEmail == currentUserEmail) != null ? e.Recipients.FirstOrDefault(r => r.RecipientEmail == currentUserEmail).DeliveryStatus : null,
                ProcessStatus = e.DeliveredAt.HasValue ? "COMPLETED" : (e.Status == "FAILED" ? "FAILED" : "PROCESSING"),
                RelatedType = e.RelatedType,
                RelatedId = e.RelatedId,
                CanReply = true,
                CanConfirm = !e.DeliveredAt.HasValue && e.Status != "FAILED",
                CanMarkComplete = !e.DeliveredAt.HasValue && e.Status != "FAILED"
            });

        IQueryable<EmailListItemDto> combinedQuery;

        if (request.MailBox.ToLower() == "sent")
        {
            combinedQuery = sentQuery;
        }
        else if (request.MailBox.ToLower() == "received")
        {
            combinedQuery = receivedQuery;
        }
        else
        {
            combinedQuery = sentQuery.Union(receivedQuery);
        }

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            var keyword = request.Keyword.ToLower();
            combinedQuery = combinedQuery.Where(x => x.Subject.ToLower().Contains(keyword) || (x.CounterpartName != null && x.CounterpartName.ToLower().Contains(keyword)));
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            if (request.Status == "COMPLETED")
                combinedQuery = combinedQuery.Where(x => x.ProcessStatus == "COMPLETED");
            else if (request.Status == "FAILED")
                combinedQuery = combinedQuery.Where(x => x.ProcessStatus == "FAILED");
            else
                combinedQuery = combinedQuery.Where(x => x.ProcessStatus != "COMPLETED" && x.ProcessStatus != "FAILED");
        }

        if (!string.IsNullOrEmpty(request.RelatedType))
        {
            if (request.RelatedType == "VISIT_REQUEST")
                combinedQuery = combinedQuery.Where(x => x.RelatedType == "VISIT_REQUEST");
            else
                combinedQuery = combinedQuery.Where(x => x.RelatedType != "VISIT_REQUEST");
        }

        if (request.StartDate.HasValue)
        {
            combinedQuery = combinedQuery.Where(x => (x.SentAt ?? x.CreatedAt) >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            var end = request.EndDate.Value.AddDays(1).AddSeconds(-1);
            combinedQuery = combinedQuery.Where(x => (x.SentAt ?? x.CreatedAt) <= end);
        }

        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await combinedQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Enhance Counterpart Name/Email for received emails
        var userIdsToFetch = items.Where(x => x.SourceType == "RECEIVED").Select(x => x.Id).ToList();
        if (userIdsToFetch.Any())
        {
            var senders = await _context.SentEmails
                .Where(e => userIdsToFetch.Contains(e.SentEmailId) && e.SentBy != null)
                .Select(e => new { e.SentEmailId, e.SentBy })
                .ToListAsync(cancellationToken);

            var sentByArray = senders.Select(s => s.SentBy).Distinct().ToList();
            var users = await _context.Users.Where(u => sentByArray.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => new { u.FullName, u.Email }, cancellationToken);

            foreach (var item in items.Where(x => x.SourceType == "RECEIVED"))
            {
                var senderId = senders.FirstOrDefault(s => s.SentEmailId == item.Id)?.SentBy;
                if (senderId.HasValue && users.ContainsKey(senderId.Value))
                {
                    item.CounterpartName = users[senderId.Value].FullName;
                    item.CounterpartEmail = users[senderId.Value].Email;
                }
            }
        }

        return new ViewEmailListResponse 
        { 
            Items = items, 
            TotalCount = totalCount, 
            Page = request.Page, 
            PageSize = request.PageSize 
        };
    }
}
