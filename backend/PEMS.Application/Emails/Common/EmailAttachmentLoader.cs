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
///
/// <para>
/// A caller that needs to know WHICH ones were skipped — because it must refuse, or because it pairs the
/// results back against its own rows — uses <see cref="LoadAlignedAsync"/> instead. See the remarks
/// there: reading the skips off the length of this list is what caused a real defect.
/// </para>
/// </summary>
public static class EmailAttachmentLoader
{
    public static async Task<List<OutboundAttachment>> LoadAsync(
        IApplicationDbContext db,
        IFileStorageService storage,
        IReadOnlyList<(ulong FileId, EmailAttachmentType Type, string? ContentId, string? DisplayName)> attachments,
        CancellationToken ct)
    {
        var aligned = await LoadAlignedAsync(db, storage, attachments, ct);
        return aligned.Where(a => a is not null).Select(a => a!).ToList();
    }

    /// <summary>
    /// Same load, but the result has EXACTLY one slot per input, in input order, holding null where the
    /// bytes could not be read.
    ///
    /// <para>
    /// This exists because the compacted list is genuinely dangerous to a caller that zips it back
    /// against its own attachment rows by position: drop the first file and every subsequent row is
    /// paired with the previous file's bytes, so a message goes out with one attachment's NAME on
    /// another attachment's CONTENT. On the setup-progress email — where slot 0 is the mandatory
    /// Schedule Report and later slots are whatever the Host added — that meant a guest could receive an
    /// internal photo named <c>PEMS_Schedule_Report_….pdf</c>. Alignment makes the mismatch
    /// unrepresentable rather than merely unlikely.
    /// </para>
    /// </summary>
    public static async Task<List<OutboundAttachment?>> LoadAlignedAsync(
        IApplicationDbContext db,
        IFileStorageService storage,
        IReadOnlyList<(ulong FileId, EmailAttachmentType Type, string? ContentId, string? DisplayName)> attachments,
        CancellationToken ct)
    {
        var result = new List<OutboundAttachment?>(attachments.Count);
        if (attachments.Count == 0) return result;

        var fileIds = attachments.Select(a => a.FileId).Distinct().ToList();
        var files = await db.Files.Where(f => fileIds.Contains(f.FileId)).ToDictionaryAsync(f => f.FileId, ct);

        foreach (var a in attachments)
        {
            if (!files.TryGetValue(a.FileId, out var file))
            {
                result.Add(null);
                continue;
            }

            await using var stream = await storage.OpenReadAsync(file, ct);
            if (stream is null)
            {
                result.Add(null);
                continue;
            }

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
