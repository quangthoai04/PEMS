using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Common;

/// <summary>The outcome of dispatching one draft — the shape both send endpoints return.</summary>
public sealed record EmailDraftDispatchResult(
    ulong EmailDraftId,
    ulong SentEmailId,
    string Status,
    bool Success,
    string DraftStatus,
    string Message);

/// <summary>
/// Turns a saved draft into exactly one sent message: re-validate, claim, send, link.
///
/// <para>
/// It exists because there are now two ways to send a draft — the generic compose screen, and the
/// setup-progress flow whose guards are about a visit rather than about a mailbox — and the parts that
/// must not differ between them are all here: content validation, the envelope rules, the send-time
/// re-check of attachment scope, the atomic DRAFT → SENT claim that stops a double click becoming two
/// messages, and the link back from the draft to the message it became. Copying the handler would have
/// meant two of each, and the one that drifts is the one nobody is looking at.
/// </para>
/// <para>
/// What is NOT here is authorisation. Each caller decides who may send: the generic path checks draft
/// ownership, the setup-progress path re-checks the host and the visit's stage. This service assumes
/// that decision has already been made and refuses only on things that are true of the draft itself.
/// </para>
/// </summary>
public interface IEmailDraftDispatcher
{
    /// <summary>
    /// Dispatches <paramref name="draft"/> on behalf of <paramref name="actorUserId"/>.
    /// Throws <see cref="ConflictException"/> when the draft is not (or is no longer) in DRAFT status.
    /// </summary>
    Task<EmailDraftDispatchResult> DispatchAsync(
        EmailDraft draft, ulong actorUserId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IEmailDraftDispatcher"/>
public sealed class EmailDraftDispatcher : IEmailDraftDispatcher
{
    private readonly IApplicationDbContext _db;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IFileStorageService _storage;
    private readonly IManualEmailSender _sender;
    private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;
    private readonly EmailRecipientOptions _recipientOptions;

    public EmailDraftDispatcher(
        IApplicationDbContext db,
        IHtmlSanitizerService sanitizer,
        IFileStorageService storage,
        IManualEmailSender sender,
        PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer,
        IOptions<EmailRecipientOptions> recipientOptions)
    {
        _db = db;
        _sanitizer = sanitizer;
        _storage = storage;
        _sender = sender;
        _normalizer = normalizer;
        _recipientOptions = recipientOptions?.Value ?? new EmailRecipientOptions();
    }

    public async Task<EmailDraftDispatchResult> DispatchAsync(
        EmailDraft draft, ulong actorUserId, CancellationToken cancellationToken)
    {
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
            _db, actorUserId,
            attachmentRows.Select(a => new EmailDraftAttachmentInput
            {
                FileId = a.FileId,
                AttachmentType = a.AttachmentType.ToString(),
                ContentId = a.ContentId,
                DisplayName = a.DisplayName,
                DisplayOrder = (int)a.DisplayOrder,
            }).ToList(),
            cancellationToken);

        // Aligned, not compacted: Pair below matches rows to bytes by position, and a compacted list
        // shifts every attachment after a skipped one onto the wrong row's name.
        var outbound = await EmailAttachmentLoader.LoadAlignedAsync(
            _db, _storage,
            attachmentRows.Select(a => (a.FileId, a.AttachmentType, a.ContentId, a.DisplayName)).ToList(),
            cancellationToken);

        var body = content.IsHtml
            ? await _normalizer.NormalizeHtmlAsync(content.Body, cancellationToken)
            : content.Body;

        // ── Claim the draft: exactly one request may proceed to send ───────────
        var sentAt = VietnamTime.Now();
        if (!await TryClaimAsync(draft.EmailDraftId, actorUserId, sentAt, cancellationToken))
            throw new ConflictException("Email nháp đã được gửi hoặc huỷ.");

        var result = await _sender.SendAsync(new ManualEmailMessage(
            SenderUserId: actorUserId,
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

        return new EmailDraftDispatchResult(
            draft.EmailDraftId, result.SentEmailId, result.Status, result.Success,
            EmailDraftStatus.SENT.ToString(), result.Message);
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

    /// <summary>
    /// Matches each stored attachment row with the bytes loaded for it. <paramref name="loaded"/> is
    /// index-aligned with <paramref name="rows"/> and holds null for a file whose bytes could not be
    /// read, so a skipped file drops out here instead of sliding the following rows onto the wrong
    /// content. Sending a row with no bytes is not an option — the recipient would get an empty part
    /// carrying a real document's filename.
    /// </summary>
    private static IReadOnlyList<ManualEmailAttachment> Pair(
        IReadOnlyList<EmailDraftAttachment> rows, IReadOnlyList<OutboundAttachment?> loaded)
    {
        var result = new List<ManualEmailAttachment>(rows.Count);
        for (var i = 0; i < rows.Count && i < loaded.Count; i++)
        {
            if (loaded[i] is not { } bytes) continue;
            var r = rows[i];
            result.Add(new ManualEmailAttachment(
                r.FileId, r.AttachmentType, r.ContentId, r.DisplayName, r.DisplayOrder, bytes));
        }
        return result;
    }
}
