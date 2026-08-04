using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Utils;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;
using PEMS.UnitTests.Delegations.ExportScheduleReport;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// What a send does when an attachment's bytes are gone.
///
/// <para>
/// The behaviour these tests pin down replaces a silent one. <c>EmailAttachmentLoader</c> answers null
/// for a file it cannot read, and the dispatcher's <c>Pair</c> step dropped those rows and carried on:
/// the draft was claimed SENT, the provider was handed a message one part short, and the caller got
/// <c>Success = true</c> with "Đã gửi email tới N người nhận." Nobody on the sending side could learn
/// that the document had not gone — not from an error, not from a warning, not from a row. The only
/// party positioned to notice was the recipient, and only if they already knew a file was meant to be
/// attached.
/// </para>
/// <para>
/// The refusal deliberately happens BEFORE the draft is claimed, so the author still has their draft.
/// That ordering is asserted here, not just the throw: a fail-closed send that burned the draft on the
/// way out would trade a missing attachment for a lost message.
/// </para>
/// </summary>
public class EmailDraftDispatcherAttachmentGuardTests
{
    private const ulong DraftId = 9100;
    private const ulong ActorUserId = 100;
    private const ulong ReadableFileId = 7001;
    private const ulong PurgedFileId = 7002;

    [Fact]
    public async Task Send_is_refused_when_an_attachment_has_no_readable_bytes()
    {
        using var db = CreateDb();
        var storage = new StubFileStorage();
        storage.Unreadable.Add(PurgedFileId);          // the file was deleted from the store
        var sender = new RecordingManualEmailSender();
        var dispatcher = CreateDispatcher(db, storage, sender);
        var draft = db.EmailDrafts.Single();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => dispatcher.DispatchAsync(draft, ActorUserId, CancellationToken.None));

        Assert.Equal(EmailErrorCodes.AttachmentUnreadable, ex.ErrorCode);
        // Names the file, because "một tệp đính kèm" leaves the author guessing which of theirs to fix.
        Assert.Contains("bao-cao-quy-3.pdf", ex.Message);
    }

    [Fact]
    public async Task Refused_send_never_reaches_the_provider()
    {
        using var db = CreateDb();
        var storage = new StubFileStorage();
        storage.Unreadable.Add(PurgedFileId);
        var sender = new RecordingManualEmailSender();
        var dispatcher = CreateDispatcher(db, storage, sender);

        await Assert.ThrowsAsync<ValidationException>(
            () => dispatcher.DispatchAsync(db.EmailDrafts.Single(), ActorUserId, CancellationToken.None));

        Assert.Empty(sender.Sent);
    }

    /// <summary>
    /// The draft survives a refusal. This is the half that makes fail-closed acceptable: the author
    /// re-uploads the file and presses send again, rather than rewriting the email.
    /// </summary>
    [Fact]
    public async Task Refused_send_leaves_the_draft_in_DRAFT()
    {
        using var db = CreateDb();
        var storage = new StubFileStorage();
        storage.Unreadable.Add(PurgedFileId);
        var dispatcher = CreateDispatcher(db, storage, new RecordingManualEmailSender());

        await Assert.ThrowsAsync<ValidationException>(
            () => dispatcher.DispatchAsync(db.EmailDrafts.Single(), ActorUserId, CancellationToken.None));

        Assert.Equal(EmailDraftStatus.DRAFT, db.EmailDrafts.Single().Status);
    }

    /// <summary>
    /// A row pointing at a <c>files</c> id that does not exist was ALREADY fail-closed, one step
    /// earlier, in the send-time scope re-check. Pinned here so the two halves stay distinguishable:
    /// this test failing as a <see cref="ValidationException"/> would mean the scope check had stopped
    /// running and the new guard was silently covering for it.
    /// </summary>
    [Fact]
    public async Task Send_is_refused_when_an_attachment_row_points_at_no_file_at_all()
    {
        using var db = CreateDb();
        var orphan = db.EmailDraftAttachments.Single(a => a.FileId == PurgedFileId);
        orphan.FileId = 999_999;                        // no matching files row
        db.SaveChanges();

        var sender = new RecordingManualEmailSender();
        var dispatcher = CreateDispatcher(db, new StubFileStorage(), sender);

        await Assert.ThrowsAsync<NotFoundException>(
            () => dispatcher.DispatchAsync(db.EmailDrafts.Single(), ActorUserId, CancellationToken.None));

        Assert.Empty(sender.Sent);
        Assert.Equal(EmailDraftStatus.DRAFT, db.EmailDrafts.Single().Status);
    }

    /// <summary>
    /// The guard must not stand in the way of a draft whose files are all fine — a fail-closed check
    /// that also refuses good sends is worse than the defect it replaced.
    ///
    /// <para>
    /// This stops short of asserting a completed send. The dispatcher claims the draft with raw SQL
    /// (<c>UPDATE … WHERE status = 'DRAFT'</c>, the statement that makes a double click one message
    /// rather than two), which the InMemory provider cannot execute — so the run gets as far as the
    /// claim and no further. That is exactly far enough for this test's question: the claim is AFTER
    /// the attachment guard, so reaching it proves the guard passed. Delivery itself is covered where
    /// a real database exists, in the integration suite.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Attachment_guard_does_not_fire_when_every_attachment_is_readable()
    {
        using var db = CreateDb();
        var dispatcher = CreateDispatcher(db, new StubFileStorage(), new RecordingManualEmailSender());

        var ex = await Record.ExceptionAsync(
            () => dispatcher.DispatchAsync(db.EmailDrafts.Single(), ActorUserId, CancellationToken.None));

        // Whatever stopped it, it was not the attachment guard.
        Assert.IsNotType<ValidationException>(ex);
        Assert.Equal(EmailDraftStatus.DRAFT, db.EmailDrafts.Single().Status);
    }

    // ── Fixture ───────────────────────────────────────────────────────────────

    private static EmailDraftDispatcher CreateDispatcher(
        ScheduleReportTestDbContext db, IFileStorageService storage, IManualEmailSender sender)
        => new(db, new PassThroughSanitizer(), storage, sender, new PassThroughNormalizer(),
            Options.Create(new EmailRecipientOptions()));

    private static ScheduleReportTestDbContext CreateDb()
    {
        var db = ScheduleReportTestDbContext.Create();

        db.Files.AddRange(
            NewFile(ReadableFileId, "ke-hoach.pdf"),
            NewFile(PurgedFileId, "bao-cao-quy-3.pdf"));

        db.EmailDrafts.Add(new EmailDraft
        {
            EmailDraftId = DraftId,
            Subject = "Cập nhật chuẩn bị chuyến thăm",
            BodyContent = "<p>Kính gửi quý đoàn, tài liệu đính kèm theo email này.</p>",
            BodyFormat = EmailBodyFormat.HTML,
            Status = EmailDraftStatus.DRAFT,
            CreatedBy = ActorUserId,
            CreatedAt = new DateTime(2026, 8, 4, 9, 0, 0),
        });

        db.EmailDraftRecipients.Add(new EmailDraftRecipient
        {
            EmailDraftRecipientId = 1,
            EmailDraftId = DraftId,
            RecipientEmail = "khach@doitac.vn",
            RecipientType = EmailRecipientTypes.To,
            DisplayOrder = 0,
            CreatedAt = new DateTime(2026, 8, 4, 9, 0, 0),
        });

        db.EmailDraftAttachments.AddRange(
            NewAttachment(1, ReadableFileId, "ke-hoach.pdf", 0),
            NewAttachment(2, PurgedFileId, "bao-cao-quy-3.pdf", 1));

        db.SaveChanges();
        return db;
    }

    private static UploadedFile NewFile(ulong fileId, string name) => new()
    {
        FileId = fileId,
        StorageProvider = "LOCAL",
        ObjectKey = $"objects/{fileId}",
        OriginalFilename = name,
        MimeType = "application/pdf",
        // The dispatcher re-checks attachment SCOPE before it loads bytes: only files the sender
        // uploaded may be attached. Without this the fixture fails on ownership and never reaches the
        // behaviour under test.
        UploadedBy = ActorUserId,
        UploadedAt = new DateTime(2026, 8, 1),
    };

    private static EmailDraftAttachment NewAttachment(
        ulong id, ulong fileId, string displayName, uint order) => new()
    {
        EmailDraftAttachmentId = id,
        EmailDraftId = DraftId,
        FileId = fileId,
        AttachmentType = EmailAttachmentType.ATTACHMENT,
        DisplayName = displayName,
        DisplayOrder = order,
        CreatedAt = new DateTime(2026, 8, 4, 9, 0, 0),
    };

    /// <summary>Records what would have been sent; asserting on emptiness is the point.</summary>
    private sealed class RecordingManualEmailSender : IManualEmailSender
    {
        public List<ManualEmailMessage> Sent { get; } = new();

        public Task<ManualEmailResult> SendAsync(
            ManualEmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.FromResult(new ManualEmailResult(
                SentEmailId: 4242, Status: "SENT", Success: true, Message: "Đã gửi."));
        }
    }

    /// <summary>
    /// Sanitisation is not what these tests are about, and a real sanitiser here would make them fail
    /// for reasons unrelated to attachments. The bodies used are plain, safe HTML.
    /// </summary>
    private sealed class PassThroughSanitizer : IHtmlSanitizerService
    {
        public string Sanitize(string? html) => html ?? string.Empty;

        public string SanitizeEmailHtml(string? html) => html ?? string.Empty;
    }

    private sealed class PassThroughNormalizer : IEmailImageLayoutNormalizer
    {
        public Task<string> NormalizeHtmlAsync(string html, CancellationToken cancellationToken = default)
            => Task.FromResult(html);
    }
}
