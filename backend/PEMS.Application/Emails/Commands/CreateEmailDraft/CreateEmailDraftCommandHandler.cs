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

using PEMS.Application.Common;
namespace PEMS.Application.Emails.Commands.CreateEmailDraft;

public sealed class CreateEmailDraftCommandHandler : IRequestHandler<CreateEmailDraftCommand, EmailDraftDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly EmailRecipientOptions _recipientOptions;

    public CreateEmailDraftCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IHtmlSanitizerService sanitizer,
        Microsoft.Extensions.Options.IOptions<EmailRecipientOptions> recipientOptions)
    {
        _db = db;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
        _recipientOptions = recipientOptions?.Value ?? new EmailRecipientOptions();
    }

    public async Task<EmailDraftDto> Handle(CreateEmailDraftCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        var now = VietnamTime.Now();
        var bodyFormat = EmailDraftWriter.ParseBodyFormat(request.BodyFormat);
        var body = bodyFormat == EmailBodyFormat.HTML
            ? _sanitizer.SanitizeEmailHtml(request.BodyContent)
            : request.BodyContent;

        // Validate recipients and attachments BEFORE inserting anything (no orphan draft on a bad file or
        // an envelope that could never be sent). A draft may legitimately have no TO yet — it is being
        // written — but a duplicate or a cross-group address is a mistake to report now, not at send.
        var envelope = EmailDraftWriter.ValidateRecipients(
            request.Recipients, _recipientOptions.MaxRecipients, requireTo: false);
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

        foreach (var row in EmailDraftWriter.ToDraftRows(draft.EmailDraftId, envelope, now))
            _db.EmailDraftRecipients.Add(row);

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
