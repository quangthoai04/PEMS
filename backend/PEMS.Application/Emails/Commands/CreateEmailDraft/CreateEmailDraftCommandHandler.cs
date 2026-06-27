using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Commands.CreateEmailDraft;

public sealed class CreateEmailDraftCommandHandler : IRequestHandler<CreateEmailDraftCommand, EmailDraftDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;

    public CreateEmailDraftCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IHtmlSanitizerService sanitizer)
    {
        _db = db;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
    }

    public async Task<EmailDraftDto> Handle(CreateEmailDraftCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        var now = DateTime.Now;
        var bodyFormat = EmailDraftWriter.ParseBodyFormat(request.BodyFormat);
        var body = bodyFormat == EmailBodyFormat.HTML
            ? _sanitizer.SanitizeEmailHtml(request.BodyContent)
            : request.BodyContent;

        // Validate attachments BEFORE inserting anything (no orphan draft on a bad file).
        var attachmentInputs = request.Attachments ?? new();
        await EmailDraftWriter.ValidateAndLoadFilesAsync(_db, userId, attachmentInputs, cancellationToken);

        var draft = new EmailDraft
        {
            EmailTemplateId = request.EmailTemplateId,
            RelatedType = string.IsNullOrWhiteSpace(request.RelatedType) ? null : request.RelatedType.Trim(),
            RelatedId = request.RelatedId,
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
            BodyContent = string.IsNullOrEmpty(body) ? null : body,
            BodyFormat = bodyFormat,
            Status = EmailDraftStatus.DRAFT,
            CreatedBy = userId,
            LastEditedBy = userId,
            CreatedAt = now,
        };
        _db.EmailDrafts.Add(draft);
        await _db.SaveChangesAsync(cancellationToken);

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
