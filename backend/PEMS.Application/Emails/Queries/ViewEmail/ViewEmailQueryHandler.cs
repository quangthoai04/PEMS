using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Emails.Queries.ViewEmail;

/// <summary>
/// One sent message in full — for the people entitled to it.
///
/// <para>
/// This query previously had no authorisation at all. The removed check was explained in a comment as a
/// product decision ("HO, StaffLeader… xem chi tiết quản lý email đã gửi"), but what it actually granted
/// was: any holder of any of the five internal roles could read ANY message by guessing an id, and see
/// its body, its attachments, its failure text and its complete BCC list. The list query beside it was
/// scoped all along, so the two disagreed about who may read what.
/// </para>
/// <para>
/// Access is now a fact about the message, not about the reader's role: sender, addressee, or a viewer
/// with canonical access to the business object the message is attached to. The recipient list is then
/// filtered for that viewer, so a TO or CC recipient sees the message exactly as their own copy showed
/// it — with no sign that anyone was blind-copied.
/// </para>
/// </summary>
public class ViewEmailQueryHandler : IRequestHandler<ViewEmailQuery, ViewEmailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISentEmailObjectScope _objectScope;

    public ViewEmailQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISentEmailObjectScope objectScope)
    {
        _context = context;
        _currentUserService = currentUserService;
        _objectScope = objectScope;
    }

    public async Task<ViewEmailDto> Handle(ViewEmailQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is not { } currentUserId)
            throw new ForbiddenException();

        var email = await _context.SentEmails
            .Include(e => e.EmailTemplate)
            .Include(e => e.Recipients)
            .Include(e => e.Attachments).ThenInclude(a => a.File)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.SentEmailId == request.Id, cancellationToken)
            ?? throw new NotFoundException("SentEmail", request.Id);

        var currentUserEmail = _currentUserService.Email;
        if (string.IsNullOrEmpty(currentUserEmail))
        {
            currentUserEmail = await _context.Users
                .Where(u => u.UserId == currentUserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(cancellationToken) ?? "";
        }

        var recipients = email.Recipients.ToList();

        // The linked-object branch is only consulted when the envelope does not already grant access, so a
        // sender or addressee never pays for the extra lookup.
        // Kept separately from `relation` below: the reply affordance must be decided on the envelope
        // alone, because that is what the reply command re-checks.
        var envelopeRelation = SentEmailAccess.Resolve(
            currentUserId, currentUserEmail, email.SentBy, recipients,
            r => r.RecipientEmail, r => r.RecipientType);

        var relation = envelopeRelation;

        if (relation == SentEmailAccess.Relation.None
            && await _objectScope.CanViewLinkedObjectAsync(email.RelatedType, email.RelatedId, cancellationToken))
            relation = SentEmailAccess.Relation.LinkedObject;

        if (!SentEmailAccess.CanView(relation))
            throw new ForbiddenException("Bạn không có quyền xem email này.");

        var visibleRecipients = SentEmailAccess.FilterRecipients(
            recipients, relation, currentUserEmail, r => r.RecipientEmail, r => r.RecipientType);

        SentEmailSenderDto? senderDto = null;
        if (email.SentBy.HasValue)
        {
            var sender = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == email.SentBy.Value, cancellationToken);
            if (sender != null)
            {
                senderDto = new SentEmailSenderDto
                {
                    UserId = sender.UserId,
                    FullName = sender.FullName,
                    Email = sender.Email
                };
            }
        }

        return new ViewEmailDto
        {
            SentEmailId = email.SentEmailId,
            EmailTemplateId = email.EmailTemplateId,
            TemplateName = email.EmailTemplate?.Name,
            TemplateCode = email.EmailTemplate?.TemplateCode,
            RelatedType = email.RelatedType,
            RelatedId = email.RelatedId,
            Subject = email.Subject,
            BodySnapshot = email.BodySnapshot ?? "",
            BodyFormat = email.BodyFormat.ToString(),
            Status = email.Status,
            ErrorMessage = email.ErrorMessage,
            RetryCount = email.RetryCount,
            LastAttemptAt = email.LastAttemptAt,
            DeliveredAt = email.DeliveredAt,
            SentAt = email.SentAt,
            CreatedAt = email.CreatedAt,
            Sender = senderDto,
            CanReply = SentEmailAccess.CanOfferReply(envelopeRelation, email.SentBy),
            CanMarkComplete = SentEmailAccess.CanMarkComplete(envelopeRelation, email.DeliveredAt),
            // Deliberately NOT accompanied by a total, a count or a "hidden recipients" flag: any of those
            // would tell a TO or CC reader that a blind copy exists, which is the one thing BCC promises
            // it will not do.
            Recipients = visibleRecipients.Select(r => new SentEmailRecipientDto
            {
                RecipientEmail = r.RecipientEmail,
                RecipientName = r.RecipientName,
                RecipientType = r.RecipientType,
                DeliveryStatus = r.DeliveryStatus,
                ProviderMessageId = r.ProviderMessageId,
                ErrorMessage = r.ErrorMessage,
                SentAt = r.SentAt,
                DeliveredAt = r.DeliveredAt
            }).ToList(),
            Attachments = email.Attachments.Select(a => new SentEmailAttachmentDto
            {
                FileId = a.FileId,
                FileName = a.File?.OriginalFilename ?? a.DisplayName ?? "unknown_file",
                MimeType = a.File?.MimeType,
                SizeBytes = a.File?.FileSize,
                IsInline = a.AttachmentType == PEMS.Domain.Enums.EmailAttachmentType.INLINE_IMAGE,
                ContentId = a.ContentId,
                // Our own authenticated routes, never the provider's. A Drive webContentLink here would
                // name the provider's file id in the payload AND invite the client to fetch the bytes
                // down a path where FileAccessAuthorizationService was never consulted.
                PreviewUrl = InternalFileUrls.Content(a.FileId),
                DownloadUrl = InternalFileUrls.Download(a.FileId)
            }).ToList()
        };
    }
}
