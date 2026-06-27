using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceSentEmails;

public sealed class GetVisitInstanceSentEmailsQueryHandler
    : IRequestHandler<GetVisitInstanceSentEmailsQuery, GetVisitInstanceSentEmailsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitInstanceSentEmailsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GetVisitInstanceSentEmailsResponse> Handle(
        GetVisitInstanceSentEmailsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Host of the instance, the campus Staff Leader, or HO — same rule as the rest of VisitProcess.
        if (!VisitReminderAccess.CanView(_currentUser, instance))
            throw new ForbiddenException("Bạn không có quyền xem lịch sử email của chuyến tiếp khách này.");

        // The targets that belong to THIS instance (so we never surface another instance's emails).
        var participantIds = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId)
            .Select(p => p.ParticipantId)
            .ToListAsync(cancellationToken);
        var logisticsIds = await _db.VisitLogisticsItems
            .Where(l => l.VisitInstanceId == instance.VisitInstanceId)
            .Select(l => l.LogisticsItemId)
            .ToListAsync(cancellationToken);

        // Optional narrowing to a single target — and validate it really belongs to this instance.
        var wantType = string.IsNullOrWhiteSpace(request.RelatedType)
            ? null : request.RelatedType.Trim().ToUpperInvariant();
        if (wantType is not null && request.RelatedId is { } rid)
        {
            var belongs = wantType == EmailActionTargetTypes.VisitParticipant
                ? participantIds.Contains(rid)
                : wantType == EmailActionTargetTypes.LogisticsItem && logisticsIds.Contains(rid);
            if (!belongs)
                throw new NotFoundException("SentEmailTarget", rid);

            participantIds = wantType == EmailActionTargetTypes.VisitParticipant ? new List<ulong> { rid } : new List<ulong>();
            logisticsIds = wantType == EmailActionTargetTypes.LogisticsItem ? new List<ulong> { rid } : new List<ulong>();
        }

        if (participantIds.Count == 0 && logisticsIds.Count == 0)
            return new GetVisitInstanceSentEmailsResponse();

        // Root the query on sent_emails; enrich names/recipients in-memory (Pomelo: avoid correlated
        // subqueries / projections on optional FKs).
        var emails = await _db.SentEmails
            .Where(e =>
                (e.RelatedType == EmailActionTargetTypes.VisitParticipant
                    && e.RelatedId != null && participantIds.Contains(e.RelatedId.Value))
                || (e.RelatedType == EmailActionTargetTypes.LogisticsItem
                    && e.RelatedId != null && logisticsIds.Contains(e.RelatedId.Value)))
            .OrderByDescending(e => e.SentEmailId)
            .Select(e => new
            {
                e.SentEmailId,
                e.EmailTemplateId,
                e.RelatedType,
                e.RelatedId,
                e.Subject,
                e.BodySnapshot,
                e.Status,
                e.SentBy,
                e.SentAt,
                e.DeliveredAt,
                e.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        if (emails.Count == 0)
            return new GetVisitInstanceSentEmailsResponse();

        var emailIds = emails.Select(e => e.SentEmailId).ToList();

        var recipientRows = await _db.SentEmailRecipients
            .Where(r => emailIds.Contains(r.SentEmailId))
            .Select(r => new
            {
                r.SentEmailId,
                r.RecipientName,
                r.RecipientEmail,
                r.RecipientType,
                r.DeliveryStatus,
                r.SentAt,
                r.DeliveredAt,
                r.ErrorMessage,
            })
            .ToListAsync(cancellationToken);
        var recipientsByEmail = recipientRows.GroupBy(r => r.SentEmailId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var templateIds = emails.Where(e => e.EmailTemplateId.HasValue)
            .Select(e => e.EmailTemplateId!.Value).Distinct().ToList();
        var templates = templateIds.Count == 0
            ? new Dictionary<ulong, (string Code, string Name)>()
            : await _db.EmailTemplates.Where(t => templateIds.Contains(t.EmailTemplateId))
                .Select(t => new { t.EmailTemplateId, t.TemplateCode, t.Name })
                .ToDictionaryAsync(t => t.EmailTemplateId, t => (Code: t.TemplateCode, Name: t.Name), cancellationToken);

        var senderIds = emails.Where(e => e.SentBy.HasValue).Select(e => e.SentBy!.Value).Distinct().ToList();
        var senderNames = senderIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.Where(u => senderIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        var wantRecipient = string.IsNullOrWhiteSpace(request.RecipientEmail)
            ? null : request.RecipientEmail.Trim();

        var items = new List<SentEmailHistoryDto>(emails.Count);
        foreach (var e in emails)
        {
            var recipients = recipientsByEmail.TryGetValue(e.SentEmailId, out var rs) ? rs : new();
            if (wantRecipient is not null
                && !recipients.Any(r => string.Equals(r.RecipientEmail, wantRecipient, System.StringComparison.OrdinalIgnoreCase)))
                continue;

            string? code = null, name = null;
            if (e.EmailTemplateId.HasValue && templates.TryGetValue(e.EmailTemplateId.Value, out var tpl))
            {
                code = tpl.Code;
                name = tpl.Name;
            }

            items.Add(new SentEmailHistoryDto
            {
                SentEmailId = e.SentEmailId,
                TemplateCode = code,
                TemplateName = name,
                Subject = e.Subject,
                BodySnapshot = e.BodySnapshot,
                EmailStatus = e.Status,
                SentByName = e.SentBy.HasValue && senderNames.TryGetValue(e.SentBy.Value, out var sn) ? sn : null,
                SentAt = e.SentAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
                DeliveredAt = e.DeliveredAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
                CreatedAt = e.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                RelatedType = e.RelatedType,
                RelatedId = e.RelatedId,
                Recipients = recipients.Select(r => new SentEmailRecipientDto
                {
                    RecipientName = r.RecipientName,
                    RecipientEmail = r.RecipientEmail,
                    RecipientType = r.RecipientType,
                    DeliveryStatus = r.DeliveryStatus,
                    SentAt = r.SentAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    DeliveredAt = r.DeliveredAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ErrorMessage = r.ErrorMessage,
                }).ToList(),
            });
        }

        return new GetVisitInstanceSentEmailsResponse { Items = items };
    }
}
