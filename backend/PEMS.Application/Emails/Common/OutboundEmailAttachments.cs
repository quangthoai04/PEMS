using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Emails;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Shared attachment handling for the non-draft rich-email senders (participant invite + logistics
/// request). Reuses the exact draft rules: validates each file reference (ownership / size / extension
/// / mime / inline-needs-cid via <see cref="EmailComposeWriter"/>), records sent_email_attachments rows,
/// and streams the bytes into ready-to-send <see cref="OutboundAttachment"/>s via
/// <see cref="EmailAttachmentLoader"/> (file_id → bytes, INLINE_IMAGE → cid linked resource).
/// </summary>
public static class OutboundEmailAttachments
{
    /// <summary>The attachments carried by an edited-content override (empty unless UseEditedContent).</summary>
    public static IReadOnlyList<EmailComposeAttachmentInput> From(EmailOverride? ov)
        => ov is { UseEditedContent: true, Attachments: { Count: > 0 } a } ? a : Array.Empty<EmailComposeAttachmentInput>();

    /// <summary>Validates the attachment inputs against the DB (throws on an unscoped/oversized/bad file).</summary>
    public static Task ValidateAsync(
        IApplicationDbContext db, ulong userId, IReadOnlyList<EmailComposeAttachmentInput> inputs, CancellationToken ct)
        => EmailComposeWriter.ValidateAndLoadFilesAsync(db, userId, inputs, ct);

    /// <summary>Adds sent_email_attachments rows onto a (not-yet-saved) SentEmail so they cascade-insert.</summary>
    public static void Attach(SentEmail sentEmail, IReadOnlyList<EmailComposeAttachmentInput> inputs, DateTime now)
    {
        var order = 0;
        foreach (var a in inputs)
        {
            sentEmail.Attachments.Add(new SentEmailAttachment
            {
                FileId = a.FileId,
                AttachmentType = EmailComposeWriter.ParseAttachmentType(a.AttachmentType),
                ContentId = string.IsNullOrWhiteSpace(a.ContentId) ? null : a.ContentId!.Trim(),
                DisplayName = a.DisplayName,
                DisplayOrder = a.DisplayOrder > 0 ? (uint)a.DisplayOrder : (uint)order,
                CreatedAt = now,
            });
            order++;
        }
    }

    /// <summary>Streams the attachment bytes into ready-to-send MIME parts (files as attachments,
    /// INLINE_IMAGE as cid linked resources).</summary>
    public static Task<List<OutboundAttachment>> LoadAsync(
        IApplicationDbContext db, IFileStorageService storage,
        IReadOnlyList<EmailComposeAttachmentInput> inputs, CancellationToken ct)
        => EmailAttachmentLoader.LoadAsync(
            db, storage,
            inputs.Select(a => (
                a.FileId,
                EmailComposeWriter.ParseAttachmentType(a.AttachmentType),
                string.IsNullOrWhiteSpace(a.ContentId) ? null : a.ContentId!.Trim(),
                (string?)a.DisplayName)).ToList(),
            ct);
}
