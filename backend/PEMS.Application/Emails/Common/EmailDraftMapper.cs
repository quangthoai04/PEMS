using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;


/// <summary>
/// Re-reads an email draft (header + recipients + attachments + file metadata) and assembles the
/// <see cref="EmailDraftDto"/>. Loads each table separately and joins in memory (Pomelo: avoid
/// correlated subqueries / projections over optional FKs).
/// </summary>
public static class EmailDraftMapper
{
    public static async Task<EmailDraftDto?> LoadDtoAsync(
        IApplicationDbContext db, ulong draftId, CancellationToken ct)
    {
        var draft = await db.EmailDrafts
            .Where(d => d.EmailDraftId == draftId)
            .Select(d => new
            {
                d.EmailDraftId,
                d.EmailTemplateId,
                d.RelatedType,
                d.RelatedId,
                d.Subject,
                d.BodyContent,
                d.BodyFormat,
                d.Status,
                d.SentEmailId,
                d.CreatedAt,
                d.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct);
        if (draft is null) return null;

        var recipients = await db.EmailDraftRecipients
            .Where(r => r.EmailDraftId == draftId)
            .OrderBy(r => r.DisplayOrder).ThenBy(r => r.EmailDraftRecipientId)
            .Select(r => new EmailDraftRecipientDto
            {
                EmailDraftRecipientId = r.EmailDraftRecipientId,
                RecipientEmail = r.RecipientEmail,
                RecipientName = r.RecipientName,
                RecipientType = r.RecipientType,
                DisplayOrder = (int)r.DisplayOrder,
            })
            .ToListAsync(ct);

        var attachmentRows = await db.EmailDraftAttachments
            .Where(a => a.EmailDraftId == draftId)
            .OrderBy(a => a.DisplayOrder).ThenBy(a => a.EmailDraftAttachmentId)
            .Select(a => new
            {
                a.EmailDraftAttachmentId,
                a.FileId,
                a.AttachmentType,
                a.ContentId,
                a.DisplayName,
                a.DisplayOrder,
            })
            .ToListAsync(ct);

        var fileIds = attachmentRows.Select(a => a.FileId).Distinct().ToList();
        var files = fileIds.Count == 0
            ? new Dictionary<ulong, (string? Original, string? Mime, long? Size, string? WebView, string? Download, string? Thumb)>()
            : await db.Files.Where(f => fileIds.Contains(f.FileId))
                .ToDictionaryAsync(
                    f => f.FileId,
                    f => (Original: f.OriginalFilename, Mime: f.MimeType, Size: f.FileSize, WebView: f.WebViewUrl, Download: f.DownloadUrl, Thumb: f.ThumbnailUrl),
                    ct);

        var attachments = attachmentRows.Select(a =>
        {
            files.TryGetValue(a.FileId, out var fm);
            return new EmailDraftAttachmentDto
            {
                EmailDraftAttachmentId = a.EmailDraftAttachmentId,
                FileId = a.FileId,
                AttachmentType = a.AttachmentType.ToString(),
                ContentId = a.ContentId,
                DisplayName = a.DisplayName,
                DisplayOrder = (int)a.DisplayOrder,
                OriginalFilename = fm.Original,
                MimeType = fm.Mime,
                FileSize = fm.Size,
                WebViewUrl = fm.WebView,
                DownloadUrl = fm.Download,
                ThumbnailUrl = fm.Thumb,
            };
        }).ToList();

        return new EmailDraftDto
        {
            EmailDraftId = draft.EmailDraftId,
            EmailTemplateId = draft.EmailTemplateId,
            RelatedType = draft.RelatedType,
            RelatedId = draft.RelatedId,
            Subject = draft.Subject,
            BodyContent = draft.BodyContent,
            BodyFormat = draft.BodyFormat.ToString(),
            Status = draft.Status.ToString(),
            SentEmailId = draft.SentEmailId,
            CreatedAt = draft.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            UpdatedAt = draft.UpdatedAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
            Recipients = recipients,
            To = OfType(recipients, EmailRecipientTypes.To),
            Cc = OfType(recipients, EmailRecipientTypes.Cc),
            Bcc = OfType(recipients, EmailRecipientTypes.Bcc),
            Attachments = attachments,
        };
    }

    /// <summary>
    /// One group of the envelope. An unrecognised or missing type reads as TO, matching the column default
    /// — a recipient with no group is a primary one, never a silently blind one.
    /// </summary>
    private static List<EmailDraftRecipientDto> OfType(List<EmailDraftRecipientDto> rows, string type)
    {
        var wanted = type == EmailRecipientTypes.To;
        return rows.Where(r =>
        {
            var t = string.IsNullOrWhiteSpace(r.RecipientType)
                ? EmailRecipientTypes.To
                : r.RecipientType.Trim().ToUpperInvariant();
            return t == type
                || (wanted && t != EmailRecipientTypes.Cc && t != EmailRecipientTypes.Bcc);
        }).ToList();
    }
}
