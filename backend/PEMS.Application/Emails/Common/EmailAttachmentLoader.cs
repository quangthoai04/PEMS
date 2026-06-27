using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Resolves email attachment metadata rows (file_id + attachment_type + content_id) into ready-to-send
/// <see cref="OutboundAttachment"/>s by streaming each file's bytes from <see cref="IFileStorageService"/>.
/// Files that can't be located are skipped (logged upstream), so a missing blob never blocks the send.
/// </summary>
public static class EmailAttachmentLoader
{
    public static async Task<List<OutboundAttachment>> LoadAsync(
        IApplicationDbContext db,
        IFileStorageService storage,
        IReadOnlyList<(ulong FileId, EmailAttachmentType Type, string? ContentId, string? DisplayName)> attachments,
        CancellationToken ct)
    {
        var result = new List<OutboundAttachment>();
        if (attachments.Count == 0) return result;

        var fileIds = attachments.Select(a => a.FileId).Distinct().ToList();
        var files = await db.Files.Where(f => fileIds.Contains(f.FileId)).ToDictionaryAsync(f => f.FileId, ct);

        foreach (var a in attachments)
        {
            if (!files.TryGetValue(a.FileId, out var file)) continue;

            await using var stream = await storage.OpenReadAsync(file, ct);
            if (stream is null) continue;

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);

            result.Add(new OutboundAttachment
            {
                Content = ms.ToArray(),
                FileName = !string.IsNullOrWhiteSpace(a.DisplayName) ? a.DisplayName! : (file.OriginalFilename ?? "attachment"),
                ContentType = file.MimeType,
                IsInline = a.Type == EmailAttachmentType.INLINE_IMAGE && !string.IsNullOrWhiteSpace(a.ContentId),
                ContentId = a.ContentId,
            });
        }

        return result;
    }
}
