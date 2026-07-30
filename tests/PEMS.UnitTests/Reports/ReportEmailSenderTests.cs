using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Reports.Common;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Enums;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Reports;

/// <summary>
/// The five steps every report send shares: refuse an unusable document, store it, record the message
/// and its attachment linkage, deliver the exact bytes, and fail the command when delivery did not
/// happen.
///
/// <para>
/// Each of these is invisible from outside: an attachment that never reached
/// <c>sent_email_attachments</c> still arrives in the inbox, and a Skipped delivery still looks like
/// success to a caller that only watches for exceptions. That is why they are asserted here rather than
/// left to the six callers.
/// </para>
/// </summary>
public class ReportEmailSenderTests
{
    /// <summary>Minimal well-formed PDF bytes — enough to satisfy the signature check.</summary>
    private static byte[] Pdf(string tail = "report") =>
        System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n" + tail + "\n%%EOF");

    private sealed class RecordingStorage : IFileStorageService
    {
        public List<(string FileName, string? ContentType, string? Purpose, byte[] Content)> Saved { get; } = new();
        public Exception? ThrowOnSave { get; set; }

        public async Task<StoredFileInfo> SaveAsync(
            Stream content, string originalFilename, string? contentType, string? purpose,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnSave is not null) throw ThrowOnSave;

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();
            Saved.Add((originalFilename, contentType, purpose, bytes));

            return new StoredFileInfo(
                "LOCAL", $"reports/{Guid.NewGuid():N}.pdf", bytes.Length,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }

        public Task<Stream?> OpenReadAsync(UploadedFile file, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(new MemoryStream(Saved[0].Content));
    }

    private sealed class Harness
    {
        public DelegationsTestDbContext Db { get; } = DelegationsTestDbContext.Create();
        public RecordingStorage Storage { get; } = new();
        public FakeSystemEmailDispatcher Dispatcher { get; } = new();
        public ReportEmailSender Sender { get; }

        public Harness() => Sender = new ReportEmailSender(Db, Storage, Dispatcher);

        public Task<ulong> Send(byte[]? pdf = null, string fileName = "PEMS_BaoCao_20260727_1405.pdf")
            => Sender.SendAsync(new ReportEmailMessage(
                SystemEmailTemplates.ReportCampusOperation,
                new EmailRecipient("leader@fpt.edu.vn", "Trần Thị B"),
                new Dictionary<string, string>
                {
                    ["recipientName"] = "Trần Thị B",
                    ["campusName"] = "FPTU Hà Nội",
                    ["periodFrom"] = "01/07/2026",
                    ["periodTo"] = "31/07/2026",
                },
                fileName,
                pdf ?? Pdf(),
                SentBy: 9,
                ReportEmailRelatedTypes.Campus,
                RelatedId: 3), CancellationToken.None);
    }

    // ── The document must be real before anything is written ────────────────

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3 })]
    public async Task An_empty_or_non_pdf_document_stops_the_send_before_any_write(byte[] content)
    {
        var h = new Harness();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Send(content));

        Assert.Equal(EmailErrorCodes.ReportAttachmentInvalid, ex.ErrorCode);
        Assert.Empty(h.Storage.Saved);
        Assert.Empty(h.Dispatcher.Sent);
        Assert.Empty(await h.Db.Files.ToListAsync());
    }

    [Fact]
    public async Task An_unsafe_file_name_stops_the_send_before_any_write()
    {
        var h = new Harness();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Send(fileName: "report\r\nBcc: attacker@evil.test.pdf"));

        Assert.Equal(EmailErrorCodes.ReportAttachmentNameInvalid, ex.ErrorCode);
        Assert.Empty(h.Storage.Saved);
        Assert.Empty(h.Dispatcher.Sent);
    }

    // ── Storage + history linkage ───────────────────────────────────────────

    [Fact]
    public async Task The_document_is_stored_as_a_pdf_under_the_report_purpose()
    {
        var h = new Harness();

        await h.Send();

        var saved = Assert.Single(h.Storage.Saved);
        Assert.Equal("PEMS_BaoCao_20260727_1405.pdf", saved.FileName);
        Assert.Equal("application/pdf", saved.ContentType);
        Assert.Equal(FilePurposeDbValues.ReportAttachment, saved.Purpose);
    }

    [Fact]
    public async Task A_files_row_records_the_document_that_was_stored()
    {
        var h = new Harness();

        await h.Send();

        var file = Assert.Single(await h.Db.Files.ToListAsync());
        Assert.Equal("application/pdf", file.MimeType);
        Assert.Equal("PEMS_BaoCao_20260727_1405.pdf", file.OriginalFilename);
        Assert.Equal(FilePurposeDbValues.ReportAttachment, file.FilePurpose);
        Assert.Equal(Pdf().Length, file.FileSize);
        Assert.Equal((ulong)9, file.UploadedBy);
        Assert.False(string.IsNullOrWhiteSpace(file.ObjectKey));
        Assert.False(string.IsNullOrWhiteSpace(file.ChecksumSha256));
    }

    [Fact]
    public async Task The_message_is_linked_to_the_document_as_a_plain_attachment()
    {
        var h = new Harness();

        var sentEmailId = await h.Send();

        var file = await h.Db.Files.SingleAsync();
        var link = Assert.Single(await h.Db.Attachments.ToListAsync());
        Assert.Equal(sentEmailId, link.SentEmailId);
        Assert.Equal(file.FileId, link.FileId);
        Assert.Equal(EmailAttachmentType.ATTACHMENT, link.AttachmentType);
        Assert.Null(link.ContentId);            // not an inline image — nothing references it by cid
        Assert.Equal("PEMS_BaoCao_20260727_1405.pdf", link.DisplayName);
        Assert.Equal(0u, link.DisplayOrder);
    }

    // ── What actually goes out ──────────────────────────────────────────────

    [Fact]
    public async Task The_bytes_delivered_are_the_bytes_generated()
    {
        var h = new Harness();
        var document = Pdf("exact-bytes-matter");

        await h.Send(document);

        var attachment = Assert.Single(h.Dispatcher.Delivered.Single().Attachments);
        Assert.Equal(document, attachment.Content);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.False(attachment.IsInline);
        Assert.Null(attachment.ContentId);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(document)),
            Convert.ToHexString(SHA256.HashData(attachment.Content)));
    }

    [Fact]
    public async Task The_message_names_the_template_and_carries_only_its_variables()
    {
        var h = new Harness();

        await h.Send();

        var request = h.Dispatcher.Single(SystemEmailTemplates.ReportCampusOperation);
        Assert.Equal("leader@fpt.edu.vn", request.To.Email);
        Assert.Equal(
            new[] { "campusName", "periodFrom", "periodTo", "recipientName" },
            request.Variables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.Equal(ReportEmailRelatedTypes.Campus, request.RelatedType);
        Assert.Equal((ulong)3, request.RelatedId);
    }

    /// <summary>The content mode is the template's — a report has no author-edited variant.</summary>
    [Fact]
    public async Task The_content_comes_from_the_template_and_carries_no_action_block()
    {
        var h = new Harness();

        await h.Send();

        var request = h.Dispatcher.Single(SystemEmailTemplates.ReportCampusOperation);
        Assert.IsType<SystemEmailContent.FromTemplate>(request.Content);
        Assert.Null(request.TrustedBlocks);
    }

    [Fact]
    public void A_report_template_keeps_its_full_body_in_the_history()
    {
        foreach (var code in new[]
                 {
                     SystemEmailTemplates.ReportCampusOperation,
                     SystemEmailTemplates.ReportDepartmentCollaboration,
                     SystemEmailTemplates.ReportDepartmentInvoice,
                     SystemEmailTemplates.ReportPersonnelPerformance,
                 })
        {
            Assert.Equal(HistoryBodyPolicy.Full, SensitiveEmailHistory.PolicyFor(code));
        }
    }

    // ── Mandatory delivery ──────────────────────────────────────────────────

    [Fact]
    public async Task A_failed_delivery_fails_the_command()
    {
        var h = new Harness();
        h.Dispatcher.Outcome = EmailDeliveryResult.Failed("SMTP_UNAVAILABLE", "Không gửi được email.");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Send());

        Assert.Equal(EmailErrorCodes.ReportDeliveryFailed, ex.ErrorCode);
    }

    /// <summary>
    /// Skipped means SMTP is switched off — the message reached no provider at all. For a user who
    /// pressed "gửi báo cáo" that is not success, however convenient it would be to treat it as one.
    /// </summary>
    [Fact]
    public async Task A_skipped_delivery_fails_the_command()
    {
        var h = new Harness();
        h.Dispatcher.Outcome = EmailDeliveryResult.Skipped("SMTP tắt.");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Send());

        Assert.Equal(EmailErrorCodes.ReportDeliveryFailed, ex.ErrorCode);
    }

    [Fact]
    public async Task A_failed_delivery_keeps_the_evidence_of_what_was_attempted()
    {
        var h = new Harness();
        h.Dispatcher.Outcome = EmailDeliveryResult.Failed("SMTP_UNAVAILABLE", "Không gửi được email.");

        await Assert.ThrowsAsync<BusinessRuleException>(() => h.Send());

        // The failure is raised AFTER the history and its attachment linkage were written, so an operator
        // can still see what was going to be sent and to whom.
        Assert.Single(await h.Db.Files.ToListAsync());
        Assert.Single(await h.Db.Attachments.ToListAsync());
        Assert.Single(h.Dispatcher.Delivered);
    }

    [Fact]
    public async Task An_accepted_delivery_returns_the_recorded_message_id()
    {
        var h = new Harness();

        var sentEmailId = await h.Send();

        Assert.NotEqual(0ul, sentEmailId);
        Assert.Single(h.Dispatcher.Delivered);
    }

    // ── Failures that must not leave debris ─────────────────────────────────

    [Fact]
    public async Task A_storage_failure_fails_the_command_without_a_message_or_a_files_row()
    {
        var h = new Harness();
        h.Storage.ThrowOnSave = new IOException("D:\\App_Data\\uploads is full");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Send());

        Assert.Equal(EmailErrorCodes.ReportAttachmentStorageFailed, ex.ErrorCode);
        // The disk path in the provider exception must not travel with the error.
        Assert.DoesNotContain("App_Data", ex.Message);
        Assert.Empty(await h.Db.Files.ToListAsync());
        Assert.Empty(h.Dispatcher.Sent);
    }

    /// <summary>
    /// A broken template throws inside PrepareAsync. Nothing was sent and nothing points at the stored
    /// file, so its row is removed rather than left behind for good.
    /// </summary>
    [Fact]
    public async Task A_broken_template_fails_the_command_and_leaves_no_orphan_file_row()
    {
        var h = new Harness();
        h.Dispatcher.ThrowOnSend = new BusinessRuleException(
            "Không tìm thấy mẫu email.", EmailErrorCodes.TemplateNotFound);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Send());

        Assert.Equal(EmailErrorCodes.TemplateNotFound, ex.ErrorCode);
        Assert.Empty(await h.Db.Files.ToListAsync());
        Assert.Empty(await h.Db.Attachments.ToListAsync());
        Assert.Empty(h.Dispatcher.Delivered);
    }

    // ── Several recipients in one command ───────────────────────────────────

    /// <summary>
    /// Two sends must not share a stream position: the second recipient receiving a truncated copy of
    /// the first document is exactly the failure a reused stream produces.
    /// </summary>
    [Fact]
    public async Task Each_send_stores_and_delivers_its_own_complete_copy()
    {
        var h = new Harness();
        var document = Pdf("shared-document");

        await h.Send(document);
        await h.Send(document);

        Assert.Equal(2, h.Storage.Saved.Count);
        Assert.All(h.Storage.Saved, s => Assert.Equal(document, s.Content));
        Assert.Equal(2, (await h.Db.Files.ToListAsync()).Count);
        Assert.Equal(2, (await h.Db.Attachments.ToListAsync()).Count);
        Assert.All(h.Dispatcher.Delivered, d => Assert.Equal(document, d.Attachments.Single().Content));

        // Two documents, two distinct storage keys — neither send overwrote the other.
        var files = await h.Db.Files.ToListAsync();
        Assert.Equal(2, files.Select(f => f.ObjectKey).Distinct().Count());
    }

    [Fact]
    public async Task Each_recipient_is_linked_to_its_own_stored_file()
    {
        var h = new Harness();

        await h.Send();
        await h.Send();

        var links = await h.Db.Attachments.ToListAsync();
        Assert.Equal(2, links.Select(l => l.SentEmailId).Distinct().Count());
        Assert.Equal(2, links.Select(l => l.FileId).Distinct().Count());
    }
}
