using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Shared validation + materialisation logic for editable email drafts (Create / Update / Send).
/// Keeps the recipient-type / attachment-type / inline-image / file-scope rules in one place so a
/// draft can never be persisted or sent with an unscoped file or a broken inline-image reference.
/// </summary>
public static class EmailDraftWriter
{
    /// <summary>Max attachment size we accept (25 MB). Mirrors a sensible mail-attachment ceiling.</summary>
    public const long MaxAttachmentBytes = 25L * 1024 * 1024;

    private static readonly HashSet<string> AllowedRecipientTypes = new(StringComparer.OrdinalIgnoreCase)
        { "TO", "CC", "BCC" };

    // Block executable / script content regardless of how it is labelled.
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".bat", ".cmd", ".com", ".msi", ".scr", ".js", ".vbs", ".ps1", ".sh", ".jar", ".dll", ".cpl", ".hta" };

    public static string NormalizeRecipientType(string? value)
    {
        var v = string.IsNullOrWhiteSpace(value) ? "TO" : value.Trim().ToUpperInvariant();
        if (!AllowedRecipientTypes.Contains(v))
            throw new ValidationException($"Loại người nhận không hợp lệ: '{value}'. Chỉ chấp nhận TO, CC, BCC.");
        return v;
    }

    /// <summary>
    /// Splits the flat recipient input into TO/CC/BCC and runs the shared envelope rules over it.
    ///
    /// <para>
    /// The draft screen used to accept anything: two identical addresses, the same mailbox in TO and BCC
    /// (which leaks the blind copy the moment the TO header is read), a display name with a newline in it.
    /// None of that failed until dispatch, by which point the sender had left the compose screen. Checking
    /// here means the draft cannot hold an envelope that could not be sent.
    /// </para>
    /// <para>
    /// <paramref name="requireTo"/> is false while a draft is being edited — an autosave of a message
    /// whose TO has not been typed yet is a normal state, not an error — and true at send.
    /// </para>
    /// </summary>
    public static ValidatedEnvelope ValidateRecipients(
        IReadOnlyList<EmailDraftRecipientInput>? inputs, int maxRecipients, bool requireTo)
    {
        var to = new List<EmailRecipient>();
        var cc = new List<EmailRecipient>();
        var bcc = new List<EmailRecipient>();

        foreach (var r in inputs ?? new List<EmailDraftRecipientInput>())
        {
            if (r is null) continue;
            var recipient = new EmailRecipient(r.Email ?? string.Empty, r.Name);
            switch (NormalizeRecipientType(r.RecipientType))
            {
                case EmailRecipientTypes.Cc: cc.Add(recipient); break;
                case EmailRecipientTypes.Bcc: bcc.Add(recipient); break;
                default: to.Add(recipient); break;
            }
        }

        return EmailRecipientValidator.Validate(to, cc, bcc, maxRecipients, requireTo);
    }

    /// <summary>
    /// Turns a checked envelope into draft rows, keeping each group's order and its type. Display order is
    /// assigned per group so the three lists come back out in the order they went in.
    /// </summary>
    public static IEnumerable<EmailDraftRecipient> ToDraftRows(
        ulong draftId, ValidatedEnvelope envelope, DateTime now)
    {
        foreach (var row in Rows(envelope.To, EmailRecipientTypes.To)) yield return row;
        foreach (var row in Rows(envelope.Cc, EmailRecipientTypes.Cc)) yield return row;
        foreach (var row in Rows(envelope.Bcc, EmailRecipientTypes.Bcc)) yield return row;

        IEnumerable<EmailDraftRecipient> Rows(IReadOnlyList<EmailRecipient> group, string type)
            => group.Select((r, i) => new EmailDraftRecipient
            {
                EmailDraftId = draftId,
                RecipientEmail = r.Email,
                RecipientName = r.DisplayName,
                RecipientType = type,
                DisplayOrder = (uint)i,
                CreatedAt = now,
            });
    }

    public static EmailAttachmentType ParseAttachmentType(string? value)
    {
        var v = string.IsNullOrWhiteSpace(value) ? "ATTACHMENT" : value.Trim().ToUpperInvariant();
        return v switch
        {
            "ATTACHMENT" => EmailAttachmentType.ATTACHMENT,
            "INLINE_IMAGE" => EmailAttachmentType.INLINE_IMAGE,
            _ => throw new ValidationException($"Loại đính kèm không hợp lệ: '{value}'. Chỉ chấp nhận ATTACHMENT, INLINE_IMAGE."),
        };
    }

    public static EmailBodyFormat ParseBodyFormat(string? value)
    {
        var v = string.IsNullOrWhiteSpace(value) ? "HTML" : value.Trim().ToUpperInvariant();
        return v switch
        {
            "HTML" => EmailBodyFormat.HTML,
            "PLAIN_TEXT" => EmailBodyFormat.PLAIN_TEXT,
            _ => throw new ValidationException($"Định dạng nội dung không hợp lệ: '{value}'. Chỉ chấp nhận HTML, PLAIN_TEXT."),
        };
    }

    /// <summary>
    /// Validates the attachment inputs against the DB and returns the loaded files keyed by id.
    /// Rules: every file must exist and belong to the current user; size/mime/extension are checked;
    /// INLINE_IMAGE requires a content_id (unique within the set) and an image mime type.
    /// </summary>
    public static async Task<IReadOnlyDictionary<ulong, UploadedFile>> ValidateAndLoadFilesAsync(
        IApplicationDbContext db,
        ulong currentUserId,
        IReadOnlyList<EmailDraftAttachmentInput> attachments,
        CancellationToken ct)
    {
        if (attachments.Count == 0)
            return new Dictionary<ulong, UploadedFile>();

        // content_id uniqueness within the request (only inline images carry one).
        var contentIds = attachments
            .Where(a => !string.IsNullOrWhiteSpace(a.ContentId))
            .Select(a => a.ContentId!.Trim())
            .ToList();
        if (contentIds.Count != contentIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new ValidationException("Content-ID của ảnh inline bị trùng trong cùng một email/draft.");

        var fileIds = attachments.Select(a => a.FileId).Distinct().ToList();
        var files = await db.Files
            .Where(f => fileIds.Contains(f.FileId))
            .ToDictionaryAsync(f => f.FileId, ct);

        foreach (var a in attachments)
        {
            if (!files.TryGetValue(a.FileId, out var file))
                throw new NotFoundException("File", a.FileId);

            // Scope: only files the current user owns may be attached.
            if (file.UploadedBy != currentUserId)
                throw new ForbiddenException("Bạn chỉ được đính kèm file do chính mình tải lên.");

            if (file.FileSize is > MaxAttachmentBytes)
                throw new ValidationException($"File '{file.OriginalFilename}' vượt quá dung lượng cho phép (25 MB).");

            var ext = System.IO.Path.GetExtension(file.OriginalFilename ?? string.Empty);
            if (!string.IsNullOrEmpty(ext) && BlockedExtensions.Contains(ext))
                throw new ValidationException($"Không cho phép đính kèm file thực thi: '{file.OriginalFilename}'.");

            var type = ParseAttachmentType(a.AttachmentType);
            if (type == EmailAttachmentType.INLINE_IMAGE)
            {
                if (string.IsNullOrWhiteSpace(a.ContentId))
                    throw new ValidationException("Ảnh inline (INLINE_IMAGE) bắt buộc phải có content_id.");
                if (file.MimeType is { } mime && !mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    throw new ValidationException($"File '{file.OriginalFilename}' không phải ảnh nên không thể dùng làm ảnh inline.");
            }
        }

        return files;
    }
}
