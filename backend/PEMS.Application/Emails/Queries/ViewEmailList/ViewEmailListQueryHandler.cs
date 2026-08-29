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

        // RelatedType/StartDate/EndDate map straight to root SentEmail columns (unlike Keyword/Status
        // below, which read derived DTO fields), so they are pushed down here — applied identically to
        // both sent and received before the per-row Recipients lookup, instead of after Union() on the
        // already-projected+joined rows. Same predicates as before, just evaluated earlier.
        var baseQuery = _context.SentEmails.AsQueryable();

        if (!string.IsNullOrEmpty(request.RelatedType))
        {
            baseQuery = request.RelatedType == "VISIT_REQUEST"
                ? baseQuery.Where(e => e.RelatedType == "VISIT_REQUEST")
                : baseQuery.Where(e => e.RelatedType != "VISIT_REQUEST");
        }

        if (request.StartDate.HasValue)
        {
            baseQuery = baseQuery.Where(e => (e.SentAt ?? e.CreatedAt) >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            var endPushedDown = request.EndDate.Value.AddDays(1).AddSeconds(-1);
            baseQuery = baseQuery.Where(e => (e.SentAt ?? e.CreatedAt) <= endPushedDown);
        }

        // Two-stage Select: the correlated Recipients lookup is evaluated ONCE per row into `To`/`Mine`
        // here, then read multiple times below — instead of the original repeating
        // e.Recipients.FirstOrDefault(...) 3-4 times per row (a separate correlated subquery execution
        // each time). No Include: it was already dead here (EF drops eager-load Include once a
        // Select projects a shape of its own — the fields below come from the FirstOrDefault subquery,
        // not from a materialized Recipients collection).
        var sentQuery = baseQuery
            .Where(e => e.SentBy == currentUserId)
            .Select(e => new { Email = e, To = e.Recipients.FirstOrDefault(r => r.RecipientType == "TO") })
            .Select(x => new EmailListItemDto
            {
                Id = x.Email.SentEmailId,
                SourceType = "SENT",
                Subject = x.Email.Subject,
                Snippet = x.Email.BodySnapshot != null ? (x.Email.BodySnapshot.Length > 100 ? x.Email.BodySnapshot.Substring(0, 100) : x.Email.BodySnapshot) : null,
                CounterpartName = x.To != null ? x.To.RecipientName : null,
                CounterpartEmail = x.To != null ? x.To.RecipientEmail : null,
                SentAt = x.Email.SentAt,
                CreatedAt = x.Email.CreatedAt,
                SendStatus = x.Email.Status,
                DeliveryStatus = x.To != null ? x.To.DeliveryStatus : null,
                ProcessStatus = x.Email.DeliveredAt.HasValue ? "COMPLETED" : (x.Email.Status == "FAILED" ? "FAILED" : "PROCESSING"),
                RelatedType = x.Email.RelatedType,
                RelatedId = x.Email.RelatedId,
                CanReply = false,
                CanConfirm = false,
                CanMarkComplete = !x.Email.DeliveredAt.HasValue && x.Email.Status != "FAILED"
            });

        var receivedQuery = baseQuery
            .Where(e => e.Recipients.Any(r => r.RecipientEmail == currentUserEmail))
            .Select(e => new { Email = e, Mine = e.Recipients.FirstOrDefault(r => r.RecipientEmail == currentUserEmail) })
            .Select(x => new EmailListItemDto
            {
                Id = x.Email.SentEmailId,
                SourceType = "RECEIVED",
                Subject = x.Email.Subject,
                Snippet = x.Email.BodySnapshot != null ? (x.Email.BodySnapshot.Length > 100 ? x.Email.BodySnapshot.Substring(0, 100) : x.Email.BodySnapshot) : null,
                CounterpartName = "System/Sender", // Could join with Users on SentBy if needed
                CounterpartEmail = "sender@pems.local", // Simplification
                SentAt = x.Email.SentAt,
                CreatedAt = x.Email.CreatedAt,
                SendStatus = x.Email.Status,
                DeliveryStatus = x.Mine != null ? x.Mine.DeliveryStatus : null,
                ProcessStatus = x.Email.DeliveredAt.HasValue ? "COMPLETED" : (x.Email.Status == "FAILED" ? "FAILED" : "PROCESSING"),
                RelatedType = x.Email.RelatedType,
                RelatedId = x.Email.RelatedId,
                CanReply = true,
                CanConfirm = !x.Email.DeliveredAt.HasValue && x.Email.Status != "FAILED",
                CanMarkComplete = !x.Email.DeliveredAt.HasValue && x.Email.Status != "FAILED"
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

        // RelatedType/StartDate/EndDate already applied above, on baseQuery before the Union.

        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        // DB-PAGE-002: bound page/pageSize the same way the other list queries already do
        // (GetAdminAuditLogsQueryHandler etc.) - unbounded PageSize let a client request an
        // arbitrarily large result set in one query.
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await combinedQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            Page = page,
            PageSize = pageSize
        };
    }
}
