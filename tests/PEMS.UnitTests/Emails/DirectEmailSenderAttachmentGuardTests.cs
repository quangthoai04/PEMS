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
using PEMS.Domain.Enums;
using PEMS.UnitTests.Delegations.ExportScheduleReport;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// What a send does when an attachment's bytes are gone.
///
/// <para>
/// The behaviour these tests pin down replaces a silent one. <c>EmailAttachmentLoader</c> answers null
/// for a file it cannot read, and the pairing step used to drop those rows and carry on: the provider
/// was handed a message one part short and the caller got <c>Success = true</c> with "Đã gửi email tới
/// N người nhận." Nobody on the sending side could learn that the document had not gone — not from an
/// error, not from a warning, not from a row. The only party positioned to notice was the recipient,
/// and only if they already knew a file was meant to be attached.
/// </para>
/// <para>
/// These were written against <c>EmailDraftDispatcher</c> and now run against <see
/// cref="DirectEmailSender"/>, which is the same rule set without the draft row. Two things changed
/// with the move, and both make the coverage stronger rather than weaker. The old suite asserted that
/// a refusal left the draft in DRAFT — the author's message had to survive the refusal; with nothing
/// persisted, the equivalent guarantee is that the composer still holds every word, which is asserted
/// here as "nothing was sent and nothing was recorded". And the happy path can now assert a COMPLETED
/// send: the old one could only get as far as the raw-SQL DRAFT → SENT claim, which the InMemory
/// provider cannot execute.
/// </para>
/// </summary>
public class DirectEmailSenderAttachmentGuardTests
{
    private const ulong ActorUserId = 100;
    private const ulong ReadableFileId = 7001;
    private const ulong PurgedFileId = 7002;

    [Fact]
    public async Task Send_is_refused_when_an_attachment_has_no_readable_bytes()
    {
        using var db = CreateDb();
        var storage = new StubFileStorage();
        storage.Unreadable.Add(PurgedFileId);          // the file was deleted from the store
        var sender = CreateSender(db, storage, new RecordingManualEmailSender());

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => sender.SendAsync(Request(), ActorUserId, CancellationToken.None));

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
        var provider = new RecordingManualEmailSender();

        await Assert.ThrowsAsync<ValidationException>(
            () => CreateSender(db, storage, provider).SendAsync(Request(), ActorUserId, CancellationToken.None));

        Assert.Empty(provider.Sent);
    }

    /// <summary>
    /// The preview refuses on the same grounds as the send. This is what makes the preview worth
    /// looking at: a message that previews cleanly and then fails on send sends the author back to
    /// guess which of the two answers was true.
    /// </summary>
    [Fact]
    public async Task Preview_is_refused_on_the_same_grounds_as_a_send()
    {
        using var db = CreateDb();
        var storage = new StubFileStorage();
        storage.Unreadable.Add(PurgedFileId);
        var provider = new RecordingManualEmailSender();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateSender(db, storage, provider).PreviewAsync(Request(), ActorUserId, CancellationToken.None));

        Assert.Equal(EmailErrorCodes.AttachmentUnreadable, ex.ErrorCode);
        Assert.Empty(provider.Sent);
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
        var provider = new RecordingManualEmailSender();
        var sender = CreateSender(db, new StubFileStorage(), provider);

        // No matching files row for the second attachment.
        var request = Request(secondFileId: 999_999);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sender.SendAsync(request, ActorUserId, CancellationToken.None));

        Assert.Empty(provider.Sent);
    }

    /// <summary>
    /// The guard must not stand in the way of a message whose files are all fine — a fail-closed check
    /// that also refuses good sends is worse than the defect it replaced.
    /// </summary>
    [Fact]
    public async Task Attachment_guard_does_not_fire_when_every_attachment_is_readable()
    {
        using var db = CreateDb();
        var provider = new RecordingManualEmailSender();

        var result = await CreateSender(db, new StubFileStorage(), provider)
            .SendAsync(Request(), ActorUserId, CancellationToken.None);

        Assert.True(result.Success);
        var sent = Assert.Single(provider.Sent);
        // Both parts went, in the order the author put them in.
        Assert.Equal(
            new[] { "ke-hoach.pdf", "bao-cao-quy-3.pdf" },
            sent.Attachments.Select(a => a.DisplayName).ToArray());
    }

    // ── Fixture ───────────────────────────────────────────────────────────────

    private static DirectEmailSender CreateSender(
        ScheduleReportTestDbContext db, IFileStorageService storage, IManualEmailSender provider)
        => new(db, new PassThroughSanitizer(), storage, provider, new PassThroughNormalizer(),
            Options.Create(new EmailRecipientOptions()));

    /// <summary>
    /// The message the author composed. It carries two attachments so the index-aligned pairing is
    /// exercised: a guard that compacted the list would slide the second file onto the first row's name.
    /// </summary>
    private static DirectEmailRequest Request(ulong secondFileId = PurgedFileId) => new(
        Subject: "Cập nhật chuẩn bị chuyến thăm",
        BodyContent: "<p>Kính gửi quý đoàn, tài liệu đính kèm theo email này.</p>",
        BodyFormat: "HTML",
        Recipients: new List<EmailComposeRecipientInput>
        {
            new() { Email = "khach@doitac.vn", RecipientType = EmailRecipientTypes.To, DisplayOrder = 0 },
        },
        Attachments: new List<EmailComposeAttachmentInput>
        {
            new() { FileId = ReadableFileId, DisplayName = "ke-hoach.pdf", DisplayOrder = 0 },
            new() { FileId = secondFileId, DisplayName = "bao-cao-quy-3.pdf", DisplayOrder = 1 },
        },
        RelatedType: null,
        RelatedId: null);

    private static ScheduleReportTestDbContext CreateDb()
    {
        var db = ScheduleReportTestDbContext.Create();

        db.Files.AddRange(
            NewFile(ReadableFileId, "ke-hoach.pdf"),
            NewFile(PurgedFileId, "bao-cao-quy-3.pdf"));

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
        // The sender re-checks attachment SCOPE before it loads bytes: only files the sender uploaded
        // may be attached. Without this the fixture fails on ownership and never reaches the behaviour
        // under test.
        UploadedBy = ActorUserId,
        UploadedAt = new DateTime(2026, 8, 1),
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
