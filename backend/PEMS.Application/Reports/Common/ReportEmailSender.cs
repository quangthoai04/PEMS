using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Reports.Common;

/// <summary>
/// Default <see cref="IReportEmailSender"/>. See the interface for why the six callers share it.
/// </summary>
public sealed class ReportEmailSender : IReportEmailSender
{
    private const string PdfContentType = "application/pdf";

    /// <summary>Every PDF starts with this; a report that does not is not a document we can promise.</summary>
    private static readonly byte[] PdfSignature = { 0x25, 0x50, 0x44, 0x46, 0x2D }; // %PDF-

    private readonly IApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ISystemEmailDispatcher _dispatcher;

    public ReportEmailSender(
        IApplicationDbContext db,
        IFileStorageService storage,
        ISystemEmailDispatcher dispatcher)
    {
        _db = db;
        _storage = storage;
        _dispatcher = dispatcher;
    }

    public async Task<ulong> SendAsync(ReportEmailMessage message, CancellationToken cancellationToken = default)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));

        // 1) Everything that can refuse the document runs before any write. A caller that generated an
        //    empty or non-PDF byte array is a bug in report generation, not a delivery outcome.
        var fileName = ReportAttachmentName.Validate(message.FileName);
        AssertUsablePdf(message.Pdf);

        // 2) The bytes, then the files row. sent_email_attachments.file_id is NOT NULL with a foreign key,
        //    so the attachment cannot be recorded against anything less than a real stored file.
        var file = await StoreAsync(message, fileName, cancellationToken);

        PreparedSystemEmail prepared;
        try
        {
            // 3) Render, check and record the message. A broken template throws here — before the send —
            //    and never falls back to content written in C#.
            prepared = await _dispatcher.PrepareAsync(
                new SystemEmailRequest(
                    message.TemplateCode,
                    message.To,
                    message.Variables,
                    message.Language,
                    RelatedType: message.RelatedType,
                    RelatedId: message.RelatedId,
                    SentBy: message.SentBy),
                cancellationToken);

            // 4) The history link. Written before delivery so a message that goes out is never recorded
            //    as having had no attachment.
            _db.SentEmailAttachments.Add(new SentEmailAttachment
            {
                SentEmailId = prepared.SentEmailId,
                FileId = file.FileId,
                AttachmentType = EmailAttachmentType.ATTACHMENT,
                ContentId = null,           // Not an inline image — nothing in the body references it by cid.
                DisplayName = fileName,
                DisplayOrder = 0,
                CreatedAt = VietnamTime.Now(),
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Nothing was sent and nothing points at this file, so the row is removed rather than left
            // as a permanent orphan in `files`. The stored blob itself cannot be deleted — the storage
            // contract has no delete — so it is left unreferenced, the same trade-off the shared upload
            // service already accepts when its own metadata insert fails.
            await TryRemoveFileRowAsync(file, cancellationToken);
            throw;
        }

        // 5) Deliver the exact bytes that were stored and recorded — no regeneration between the snapshot
        //    and the send, so the document in the recipient's inbox is the one the history describes.
        var delivery = await _dispatcher.DeliverAsync(
            prepared with
            {
                Attachments = new[]
                {
                    new OutboundAttachment
                    {
                        Content = message.Pdf,
                        FileName = fileName,
                        ContentType = PdfContentType,
                        IsInline = false,
                        ContentId = null,
                    },
                },
            },
            cancellationToken);

        // 6) Mandatory: the user pressed "gửi", so anything short of provider acceptance is a failed
        //    command. The history row keeps the truthful FAILED/QUEUED status written above — this throws
        //    after it, so raising the error never costs the evidence of what happened.
        if (delivery.Status != EmailDeliveryStatus.Sent)
            throw new BusinessRuleException(
                "Không gửi được email báo cáo. Vui lòng thử lại hoặc liên hệ quản trị hệ thống.",
                EmailErrorCodes.ReportDeliveryFailed);

        return prepared.SentEmailId;
    }

    private static void AssertUsablePdf(byte[]? content)
    {
        if (content is null || content.Length == 0)
            throw new BusinessRuleException(
                "Không tạo được tệp báo cáo đính kèm.", EmailErrorCodes.ReportAttachmentInvalid);

        if (content.Length < PdfSignature.Length)
            throw new BusinessRuleException(
                "Tệp báo cáo đính kèm không phải PDF hợp lệ.", EmailErrorCodes.ReportAttachmentInvalid);

        for (var i = 0; i < PdfSignature.Length; i++)
        {
            if (content[i] == PdfSignature[i]) continue;
            throw new BusinessRuleException(
                "Tệp báo cáo đính kèm không phải PDF hợp lệ.", EmailErrorCodes.ReportAttachmentInvalid);
        }
    }

    private async Task<UploadedFile> StoreAsync(
        ReportEmailMessage message, string fileName, CancellationToken cancellationToken)
    {
        StoredFileInfo stored;
        try
        {
            // A fresh stream per message: two personalised reports sent in the same command must never
            // share a position, or the second recipient receives a truncated copy of the first document.
            await using var content = new MemoryStream(message.Pdf, writable: false);
            stored = await _storage.SaveAsync(content, fileName, PdfContentType,
                FilePurposeDbValues.ReportAttachment, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The storage exception can name a disk path; it is logged by the storage layer, not repeated
            // into a message that reaches the user or the email history.
            throw new BusinessRuleException(
                "Không lưu được tệp báo cáo đính kèm.", EmailErrorCodes.ReportAttachmentStorageFailed);
        }

        var file = new UploadedFile
        {
            StorageProvider = stored.StorageProvider,
            BucketName = stored.BucketName,
            ObjectKey = stored.ObjectKey,
            OriginalFilename = fileName,
            MimeType = PdfContentType,
            FileSize = stored.FileSize,
            ChecksumSha256 = stored.ChecksumSha256,
            FilePurpose = FilePurposeDbValues.ReportAttachment,
            UploadedBy = message.SentBy,
            UploadedAt = VietnamTime.Now(),
        };

        _db.Files.Add(file);
        await _db.SaveChangesAsync(cancellationToken);
        return file;
    }

    private async Task TryRemoveFileRowAsync(UploadedFile file, CancellationToken cancellationToken)
    {
        try
        {
            _db.Files.Remove(file);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Never replace the real failure with a cleanup failure — the caller must see why the send
            // stopped, not why the tidy-up did.
        }
    }
}
