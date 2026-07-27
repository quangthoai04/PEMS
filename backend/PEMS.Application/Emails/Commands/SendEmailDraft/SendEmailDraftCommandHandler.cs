using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

using PEMS.Application.Common;
namespace PEMS.Application.Emails.Commands.SendEmailDraft;

/// <summary>
/// Sends a saved draft: the author's own TO/CC/BCC, their attachments, one message.
///
/// <para>
/// Two things changed here beyond routing the send through the shared pipeline. The handler used to loop
/// the recipients and call SMTP once per address, so a draft addressed to three people produced three
/// separate emails, each showing its reader as the only recipient. And nothing stopped two clicks on
/// "gửi" from both passing the status check and both sending — so the draft is now claimed with a single
/// conditional UPDATE, and only the request that wins the row goes on to send.
/// </para>
/// </summary>
public sealed class SendEmailDraftCommandHandler
    : IRequestHandler<SendEmailDraftCommand, SendEmailDraftResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IFileStorageService _storage;
    private readonly IManualEmailSender _sender;
    private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;
    private readonly EmailRecipientOptions _recipientOptions;

    public SendEmailDraftCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IHtmlSanitizerService sanitizer,
        IFileStorageService storage,
        IManualEmailSender sender,
        PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer,
        IOptions<EmailRecipientOptions> recipientOptions)
    {
        _db = db;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
        _storage = storage;
        _sender = sender;
        _normalizer = normalizer;
        _recipientOptions = recipientOptions?.Value ?? new EmailRecipientOptions();
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

        // ── Everything that can say "no" runs before the draft is claimed ──────
        var content = ManualEmailContent.Validate(
            draft.Subject, draft.BodyContent, draft.BodyFormat, _sanitizer);

        var recipientRows = await _db.EmailDraftRecipients
            .Where(r => r.EmailDraftId == draft.EmailDraftId)
            .OrderBy(r => r.DisplayOrder).ThenBy(r => r.EmailDraftRecipientId)
            .ToListAsync(cancellationToken);

        var envelope = EmailDraftWriter.ValidateRecipients(
            recipientRows.Select(r => new EmailDraftRecipientInput
            {
                Email = r.RecipientEmail,
                Name = r.RecipientName,
                RecipientType = r.RecipientType,
                DisplayOrder = (int)r.DisplayOrder,
            }).ToList(),
            _recipientOptions.MaxRecipients,
            requireTo: true);

        var attachmentRows = await _db.EmailDraftAttachments
            .Where(a => a.EmailDraftId == draft.EmailDraftId)
            .OrderBy(a => a.DisplayOrder).ThenBy(a => a.EmailDraftAttachmentId)
            .ToListAsync(cancellationToken);

        // Re-check scope/size/mime at send time: the files may have changed since the last autosave.
        await EmailDraftWriter.ValidateAndLoadFilesAsync(
            _db, userId,
            attachmentRows.Select(a => new EmailDraftAttachmentInput
            {
                FileId = a.FileId,
                AttachmentType = a.AttachmentType.ToString(),
                ContentId = a.ContentId,
                DisplayName = a.DisplayName,
                DisplayOrder = (int)a.DisplayOrder,
            }).ToList(),
            cancellationToken);

        var outbound = await EmailAttachmentLoader.LoadAsync(
            _db, _storage,
            attachmentRows.Select(a => (a.FileId, a.AttachmentType, a.ContentId, a.DisplayName)).ToList(),
            cancellationToken);

        var body = content.IsHtml
            ? await _normalizer.NormalizeHtmlAsync(content.Body, cancellationToken)
            : content.Body;

        // ── Claim the draft: exactly one request may proceed to send ───────────
        var sentAt = VietnamTime.Now();
        if (!await TryClaimAsync(draft.EmailDraftId, userId, sentAt, cancellationToken))
            throw new ConflictException("Email nháp đã được gửi hoặc huỷ.");

        var result = await _sender.SendAsync(new ManualEmailMessage(
            SenderUserId: userId,
            Subject: content.Subject,
            Body: body,
            BodyFormat: draft.BodyFormat,
            Envelope: envelope,
            Attachments: Pair(attachmentRows, outbound),
            RelatedType: draft.RelatedType,
            RelatedId: draft.RelatedId), cancellationToken);

        // Link the draft to the message it produced. The claim above already moved it to SENT, so this
        // records WHICH email it became — including for a send the provider rejected, whose FAILED row is
        // the evidence the attempt happened.
        var claimed = await _db.EmailDrafts
            .FirstOrDefaultAsync(d => d.EmailDraftId == draft.EmailDraftId, cancellationToken);
        if (claimed is not null)
        {
            claimed.SentEmailId = result.SentEmailId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new SendEmailDraftResponse
        {
            EmailDraftId = draft.EmailDraftId,
            SentEmailId = result.SentEmailId,
            Status = result.Status,
            Success = result.Success,
            DraftStatus = EmailDraftStatus.SENT.ToString(),
            Message = result.Message,
        };
    }

    /// <summary>
    /// Moves the draft DRAFT → SENT in one statement, and reports whether this request is the one that did
    /// it. Two concurrent sends both pass the earlier status check — they read the row before either
    /// writes — so the decision has to be made by the database, where only one UPDATE can match.
    ///
    /// <para>
    /// The window this leaves is honest and narrow: a process that dies between the claim and the insert
    /// leaves a draft marked SENT with no message. That is preferable to the alternative it replaces,
    /// which was sending the same email twice.
    /// </para>
    /// </summary>
    private async Task<bool> TryClaimAsync(
        ulong draftId, ulong userId, DateTime sentAt, CancellationToken cancellationToken)
    {
        var affected = await _db.Database.ExecuteSqlRawAsync(
            @"UPDATE email_drafts
                 SET status = 'SENT', sent_at = {1}, last_edited_by = {2}, updated_at = {1}
               WHERE email_draft_id = {0} AND status = 'DRAFT'",
            new object[] { draftId, sentAt, userId }, cancellationToken);

        return affected == 1;
    }

    /// <summary>Matches each stored attachment row with the bytes loaded for it.</summary>
    private static IReadOnlyList<ManualEmailAttachment> Pair(
        IReadOnlyList<EmailDraftAttachment> rows, IReadOnlyList<OutboundAttachment> loaded)
    {
        var result = new List<ManualEmailAttachment>(rows.Count);
        for (var i = 0; i < rows.Count && i < loaded.Count; i++)
        {
            var r = rows[i];
            result.Add(new ManualEmailAttachment(
                r.FileId, r.AttachmentType, r.ContentId, r.DisplayName, r.DisplayOrder, loaded[i]));
        }
        return result;
    }
}
