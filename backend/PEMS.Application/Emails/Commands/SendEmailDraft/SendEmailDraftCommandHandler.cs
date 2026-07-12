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

using PEMS.Application.Common;
namespace PEMS.Application.Emails.Commands.SendEmailDraft;

public sealed class SendEmailDraftCommandHandler
    : IRequestHandler<SendEmailDraftCommand, SendEmailDraftResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _email;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IFileStorageService _storage;
    private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;

    public SendEmailDraftCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IEmailService email,
        IHtmlSanitizerService sanitizer,
        IFileStorageService storage,
        PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer)
    {
        _db = db;
        _currentUser = currentUser;
        _email = email;
        _sanitizer = sanitizer;
        _storage = storage;
        _normalizer = normalizer;
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

        var now = VietnamTime.Now();
        var subject = draft.Subject!.Trim();
        var rawBody = draft.BodyFormat == EmailBodyFormat.HTML
            ? _sanitizer.SanitizeEmailHtml(draft.BodyContent)
            : (draft.BodyContent ?? string.Empty);
        
        var body = draft.BodyFormat == EmailBodyFormat.HTML
            ? await _normalizer.NormalizeHtmlAsync(rawBody, cancellationToken)
            : rawBody;

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

        // Resolve attachment bytes ONCE (reused per recipient): real MIME parts — files as downloadable
        // attachments, INLINE_IMAGE as cid linked resources matching <img src="cid:..."> in the body.
        var isHtml = draft.BodyFormat == EmailBodyFormat.HTML;
        var outboundAttachments = await EmailAttachmentLoader.LoadAsync(
            _db, _storage,
            attachmentRows.Select(a => (a.FileId, a.AttachmentType, a.ContentId, a.DisplayName)).ToList(),
            cancellationToken);

        var hasFailure = false;
        foreach (var recipient in sentEmail.Recipients)
        {
            recipient.SentAt = VietnamTime.Now();
            try
            {
                await _email.SendAsync(new OutboundEmail
                {
                    ToEmail = recipient.RecipientEmail,
                    Subject = subject,
                    Body = body,
                    IsHtml = isHtml,
                    Attachments = outboundAttachments,
                }, cancellationToken);
                recipient.DeliveryStatus = "DELIVERED";
                recipient.DeliveredAt = VietnamTime.Now();
            }
            catch (Exception ex)
            {
                hasFailure = true;
                recipient.DeliveryStatus = "FAILED";
                recipient.ErrorMessage = ex.Message;
            }
        }

        sentEmail.SentAt = VietnamTime.Now();
        sentEmail.LastAttemptAt = sentEmail.SentAt;

        // Compute aggregated status: ALL ok → SENT; ALL failed → FAILED; mixed → PARTIAL_FAILED.
        var allFailed = sentEmail.Recipients.All(r => r.DeliveryStatus == "FAILED");
        if (!hasFailure)
        {
            sentEmail.Status = "SENT";
            sentEmail.DeliveredAt = sentEmail.SentAt;
            sentEmail.ErrorMessage = null;
        }
        else if (allFailed)
        {
            sentEmail.Status = "FAILED";
            sentEmail.DeliveredAt = null;
            sentEmail.ErrorMessage = "Tất cả người nhận gửi thất bại.";
        }
        else
        {
            sentEmail.Status = "PARTIAL_FAILED";
            sentEmail.DeliveredAt = null;
            sentEmail.ErrorMessage = "Một hoặc nhiều người nhận gửi thất bại.";
        }

        draft.Status = EmailDraftStatus.SENT;
        draft.SentEmailId = sentEmail.SentEmailId;
        draft.SentAt = sentEmail.SentAt;
        draft.LastEditedBy = userId;

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var message = sentEmail.Status switch
        {
            "SENT" => "Đã gửi email từ nháp thành công.",
            "FAILED" => "Gửi email thất bại với tất cả người nhận.",
            _ => "Gửi email thất bại với một hoặc nhiều người nhận.",
        };

        return new SendEmailDraftResponse
        {
            EmailDraftId = draft.EmailDraftId,
            SentEmailId = sentEmail.SentEmailId,
            Status = sentEmail.Status,
            Success = sentEmail.Status == "SENT",
            DraftStatus = draft.Status.ToString(),
            Message = message,
        };
    }
}
