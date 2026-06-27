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

namespace PEMS.Application.Emails.Commands.UpdateEmailDraft;

public sealed class UpdateEmailDraftCommandHandler : IRequestHandler<UpdateEmailDraftCommand, EmailDraftDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;

    public UpdateEmailDraftCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IHtmlSanitizerService sanitizer)
    {
        _db = db;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
    }

    public async Task<EmailDraftDto> Handle(UpdateEmailDraftCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        var draft = await _db.EmailDrafts
            .FirstOrDefaultAsync(d => d.EmailDraftId == request.EmailDraftId, cancellationToken)
            ?? throw new NotFoundException("EmailDraft", request.EmailDraftId);

        if (draft.CreatedBy != userId)
            throw new ForbiddenException("Bạn chỉ được sửa email nháp do chính mình tạo.");
        if (draft.Status != EmailDraftStatus.DRAFT)
            throw new ConflictException("Email nháp đã được gửi hoặc huỷ, không thể chỉnh sửa.");

        var now = DateTime.Now;
        var bodyFormat = EmailDraftWriter.ParseBodyFormat(request.BodyFormat);
        var body = bodyFormat == EmailBodyFormat.HTML
            ? _sanitizer.SanitizeEmailHtml(request.BodyContent)
            : request.BodyContent;

        var attachmentInputs = request.Attachments ?? new();
        await EmailDraftWriter.ValidateAndLoadFilesAsync(_db, userId, attachmentInputs, cancellationToken);

        draft.EmailTemplateId = request.EmailTemplateId;
        draft.RelatedType = string.IsNullOrWhiteSpace(request.RelatedType) ? null : request.RelatedType.Trim();
        draft.RelatedId = request.RelatedId;
        draft.Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim();
        draft.BodyContent = string.IsNullOrEmpty(body) ? null : body;
        draft.BodyFormat = bodyFormat;
        draft.LastEditedBy = userId;
        draft.UpdatedAt = now;

        // Replace recipients + attachments wholesale (simplest correct autosave semantics).
        var oldRecipients = await _db.EmailDraftRecipients
            .Where(r => r.EmailDraftId == draft.EmailDraftId).ToListAsync(cancellationToken);
        _db.EmailDraftRecipients.RemoveRange(oldRecipients);
        var oldAttachments = await _db.EmailDraftAttachments
            .Where(a => a.EmailDraftId == draft.EmailDraftId).ToListAsync(cancellationToken);
        _db.EmailDraftAttachments.RemoveRange(oldAttachments);

        foreach (var r in request.Recipients ?? new())
        {
            var email = r.Email?.Trim();
            if (string.IsNullOrWhiteSpace(email)) continue;
            _db.EmailDraftRecipients.Add(new EmailDraftRecipient
            {
                EmailDraftId = draft.EmailDraftId,
                RecipientEmail = email,
                RecipientName = string.IsNullOrWhiteSpace(r.Name) ? null : r.Name.Trim(),
                RecipientType = EmailDraftWriter.NormalizeRecipientType(r.RecipientType),
                DisplayOrder = (uint)Math.Max(0, r.DisplayOrder),
                CreatedAt = now,
            });
        }

        foreach (var a in attachmentInputs)
        {
            _db.EmailDraftAttachments.Add(new EmailDraftAttachment
            {
                EmailDraftId = draft.EmailDraftId,
                FileId = a.FileId,
                AttachmentType = EmailDraftWriter.ParseAttachmentType(a.AttachmentType),
                ContentId = string.IsNullOrWhiteSpace(a.ContentId) ? null : a.ContentId.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(a.DisplayName) ? null : a.DisplayName.Trim(),
                DisplayOrder = (uint)Math.Max(0, a.DisplayOrder),
                CreatedAt = now,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return (await EmailDraftMapper.LoadDtoAsync(_db, draft.EmailDraftId, cancellationToken))!;
    }
}
