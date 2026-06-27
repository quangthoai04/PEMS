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
