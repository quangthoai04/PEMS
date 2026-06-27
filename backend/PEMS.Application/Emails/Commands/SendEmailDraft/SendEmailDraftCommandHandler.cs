using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Commands.SendEmailDraft;

public sealed class SendEmailDraftCommandHandler
    : IRequestHandler<SendEmailDraftCommand, SendEmailDraftResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _email;
    private readonly IHtmlSanitizerService _sanitizer;

    public SendEmailDraftCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IEmailService email,
        IHtmlSanitizerService sanitizer)
    {
        _db = db;
        _currentUser = currentUser;
        _email = email;
        _sanitizer = sanitizer;
    }

    public async Task<SendEmailDraftResponse> Handle(
        SendEmailDraftCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        var draft = await _db.EmailDrafts
            .FirstOrDefaultAsync(d => d.EmailDraftId == request.EmailDraftId, cancellationToken)
            ?? throw new NotFoundException("EmailDraft", request.EmailDraftId);

        if (draft.CreatedBy != userId)
            throw new ForbiddenException("Bạn chỉ được gửi email nháp do chính mình tạo.");
        if (draft.Status != EmailDraftStatus.DRAFT)
            throw new ConflictException("Email nháp đã được gửi hoặc huỷ.");
        if (string.IsNullOrWhiteSpace(draft.Subject))
            throw new ValidationException("Email nháp chưa có tiêu đề, không thể gửi.");

        var recipients = await _db.EmailDraftRecipients
            .Where(r => r.EmailDraftId == draft.EmailDraftId)
            .OrderBy(r => r.DisplayOrder).ThenBy(r => r.EmailDraftRecipientId)
            .ToListAsync(cancellationToken);
        if (recipients.Count == 0)
            throw new ValidationException("Email nháp chưa có người nhận, không thể gửi.");

        var attachmentRows = await _db.EmailDraftAttachments
            .Where(a => a.EmailDraftId == draft.EmailDraftId)
            .OrderBy(a => a.DisplayOrder).ThenBy(a => a.EmailDraftAttachmentId)
            .ToListAsync(cancellationToken);

        // Re-validate attachment scope/size/mime at send time (files may have changed since autosave).
        var attachmentInputs = attachmentRows.Select(a => new EmailDraftAttachmentInput
        {
            FileId = a.FileId,
            AttachmentType = a.AttachmentType.ToString(),
            ContentId = a.ContentId,
            DisplayName = a.DisplayName,
            DisplayOrder = (int)a.DisplayOrder,
        }).ToList();
        await EmailDraftWriter.ValidateAndLoadFilesAsync(_db, userId, attachmentInputs, cancellationToken);

        var now = DateTime.Now;
        var subject = draft.Subject!.Trim();
        var body = draft.BodyFormat == EmailBodyFormat.HTML
            ? _sanitizer.Sanitize(draft.BodyContent)
            : (draft.BodyContent ?? string.Empty);

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        var sentEmail = new SentEmail
        {
            EmailTemplateId = draft.EmailTemplateId,
            RelatedType = draft.RelatedType,
            RelatedId = draft.RelatedId,
            Subject = subject,
            BodySnapshot = body,
            BodyFormat = draft.BodyFormat,
            Status = "QUEUED",
            SentBy = userId,
            CreatedAt = now,
            LastAttemptAt = now,
        };
        foreach (var r in recipients)
        {
            sentEmail.Recipients.Add(new SentEmailRecipient
            {
                RecipientEmail = r.RecipientEmail,
                RecipientName = r.RecipientName,
                RecipientType = r.RecipientType,
                DeliveryStatus = "QUEUED",
                CreatedAt = now,
            });
        }
        foreach (var a in attachmentRows)
        {
            sentEmail.Attachments.Add(new SentEmailAttachment
            {
                FileId = a.FileId,
                AttachmentType = a.AttachmentType,
                ContentId = a.ContentId,
                DisplayName = a.DisplayName,
                DisplayOrder = a.DisplayOrder,
                CreatedAt = now,
            });
        }
        _db.SentEmails.Add(sentEmail);
        await _db.SaveChangesAsync(cancellationToken);

        // Dispatch. NOTE: IEmailService has no attachment/inline-image support yet, so files are
        // persisted as metadata but not yet streamed as MIME parts (see known limitations).
        var hasFailure = false;
        foreach (var recipient in sentEmail.Recipients)
        {
            recipient.SentAt = DateTime.Now;
            try
            {
                await _email.SendAsync(recipient.RecipientEmail, subject, body, cancellationToken);
                recipient.DeliveryStatus = "DELIVERED";
                recipient.DeliveredAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                hasFailure = true;
                recipient.DeliveryStatus = "FAILED";
                recipient.ErrorMessage = ex.Message;
            }
        }

        sentEmail.SentAt = DateTime.Now;
        sentEmail.LastAttemptAt = sentEmail.SentAt;
        sentEmail.Status = hasFailure ? "FAILED" : "SENT";
        sentEmail.DeliveredAt = hasFailure ? null : sentEmail.SentAt;
        sentEmail.ErrorMessage = hasFailure ? "Một hoặc nhiều người nhận gửi thất bại." : null;

        draft.Status = EmailDraftStatus.SENT;
        draft.SentEmailId = sentEmail.SentEmailId;
        draft.SentAt = sentEmail.SentAt;
        draft.LastEditedBy = userId;

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new SendEmailDraftResponse
        {
            EmailDraftId = draft.EmailDraftId,
            SentEmailId = sentEmail.SentEmailId,
            Status = sentEmail.Status,
            DraftStatus = draft.Status.ToString(),
            Message = hasFailure
                ? "Gửi email thất bại với một hoặc nhiều người nhận."
                : "Đã gửi email từ nháp thành công.",
        };
    }
}
