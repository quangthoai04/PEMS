using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Commands.ReplytoEmail;
using PEMS.Application.Emails.Commands.SendEmail;
using PEMS.Application.Emails.Common;
using PEMS.Infrastructure.FileStorage;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Security;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Manual mail run for real: real handlers, a real database, and real MIME on disk.
///
/// <para>
/// All three manual paths used to loop the recipient list and call SMTP once per address, so a message
/// "to three people" was three separate messages, each showing its reader as the only recipient. The
/// reply handler went further: it wrote CC and BCC rows into the history and then sent to the TO address
/// alone, so the record claimed delivery to people who received nothing. Both are the kind of defect only
/// a real send can disprove, which is why nothing below is mocked.
/// </para>
/// <para>
/// This is <c>ManualEmailPipelineTests</c> after the draft row was removed. Every send here goes through
/// <see cref="SendEmailCommandHandler"/>, which is now the only way a manually composed message leaves
/// PEMS. Four groups of the original coverage were about the ROW rather than the send — the draft
/// round-trip, the update-replaces-envelope rule, draft ownership, and the DRAFT → SENT claim that made a
/// double click one message. The first three have no subject any more. The fourth moved wholesale to
/// <c>Idempotency-Key</c> and is covered against this same database by
/// <see cref="EmailSendIdempotencyTests"/>, including the two-concurrent-requests case; repeating it here
/// would assert the same reservation table twice.
/// </para>
/// <para>
/// One thing got BETTER and is covered below for the first time: an attachment used to reach a message
/// only by being written onto a draft row, so a message composed through this handler silently went out
/// without the file the author had attached. Attachments are part of the command now.
/// </para>
/// </summary>
public sealed class ManualEmailDirectSendPipelineTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("g5d-evidence@partner.example.com");
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "pems-g5d-files-" + Guid.NewGuid().ToString("N"));

    /// <summary>Suite-private id range, high enough that it cannot collide with any other suite.</summary>
    private const ulong Base = 991_400;
    private const ulong CampusId = Base + 1;
    private const ulong AuthorId = Base + 2;
    private const ulong PartnerId = Base + 3;
    private const ulong AttachmentFileId = Base + 4;

    private const string MailPrefix = "g5d-";
    private const string MailDomain = "@partner.example.com";
    private static string Mail(ulong userId) => $"{MailPrefix}{userId}{MailDomain}";

    private const string ToA = "g5d-to-a@partner.example.com";
    private const string ToB = "g5d-to-b@partner.example.com";
    private const string CcA = "g5d-cc-a@partner.example.com";
    private const string BccA = "g5d-bcc-a@partner.example.com";
    private const string BccB = "g5d-bcc-b@partner.example.com";

    public void Dispose()
    {
        _h.Dispose();
        try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true); }
        catch (IOException) { /* a temp dir left behind must never fail a test run */ }
    }

    // ── Rig ─────────────────────────────────────────────────────────────────

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId { get; init; }
        public string? Email { get; init; }
        public ulong? RoleId => null;
        public string? RoleCode { get; init; }
        public string? SubRole => null;
        public ulong? PrimaryCampusId { get; init; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class NoHttpClients : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static readonly HtmlSanitizerService Sanitizer = new();
    private static readonly IOptions<EmailRecipientOptions> Recipients =
        Options.Create(new EmailRecipientOptions());

    private static ICurrentUserService Author => new FakeCurrentUser
    {
        UserId = AuthorId, Email = Mail(AuthorId), RoleCode = "STAFF", PrimaryCampusId = CampusId,
    };

    private static ICurrentUserService Partner => new FakeCurrentUser
    {
        UserId = PartnerId, Email = Mail(PartnerId), RoleCode = "STAFF", PrimaryCampusId = CampusId,
    };

    private LocalFileStorageService Storage() => new(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:LocalRoot"] = _storageRoot })
            .Build(),
        new NoHttpClients(),
        new NoServices(),
        NullLogger<LocalFileStorageService>.Instance);

    private PEMS.Application.Emails.Utils.EmailImageLayoutNormalizer Normalizer(ApplicationDbContext db)
        => new(db, Storage());

    private ManualEmailSender Sender(ApplicationDbContext db, string? brokenHost = null)
        => new(db, _h.Sender(brokenHost));

    /// <summary>
    /// The real <see cref="DirectEmailSender"/> rather than a stub: it is the pipeline these tests are
    /// about — content validation, the envelope rules, the send-time attachment re-check, and one MIME
    /// message per send.
    /// </summary>
    private DirectEmailSender Direct(ApplicationDbContext db, string? brokenHost = null)
        => new(db, Sanitizer, Storage(), Sender(db, brokenHost), Normalizer(db), Recipients);

    private SendEmailCommandHandler Compose(
        ApplicationDbContext db, ICurrentUserService user, string? brokenHost = null)
        => new(user, Direct(db, brokenHost));

    private ReplytoEmailCommandHandler Reply(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, Sanitizer, Sender(db), Recipients);

    // ── Seed ────────────────────────────────────────────────────────────────

    private static async Task SeedAsync(ApplicationDbContext db)
    {
        await CleanupRowsAsync(db);

        var roles = await db.Database.SqlQueryRaw<RoleRow>(
            "SELECT role_id AS RoleId, role_code AS RoleCode FROM roles").ToListAsync();
        var staffRole = roles.First(r => r.RoleCode == "STAFF").RoleId;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, status) VALUES ({0}, {1}, {2}, 'ACTIVE')",
            CampusId, "G5D", "PEMS G5D Campus");

        // A STAFF account needs an IC department — a database trigger enforces both that and the campus.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'IC', 'ACTIVE')",
            CampusId, CampusId, "PEMS G5D Văn phòng IC");

        async Task User(ulong id, string name)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, "
                + $"department_id, status) VALUES ({id}, {{0}}, {{1}}, {staffRole}, 'STAFF', {CampusId}, "
                + $"{CampusId}, 'ACTIVE')",
                name, Mail(id));

        await User(AuthorId, "PEMS G5D Người soạn");
        await User(PartnerId, "PEMS G5D Đối tác");
    }

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    private static async Task CleanupRowsAsync(ApplicationDbContext db)
    {
        // sent_email_recipients / _attachments cascade from sent_emails.
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM sent_emails WHERE sent_by IN ({AuthorId}, {PartnerId})");
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM files WHERE uploaded_by IN ({AuthorId}, {PartnerId})");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM users WHERE user_id IN ({AuthorId}, {PartnerId})");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM departments WHERE department_id = {CampusId}");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM campuses WHERE campus_id = {CampusId}");
    }

    /// <summary>The whole envelope, in one composed message: two TO, one CC, two BCC.</summary>
    private static SendEmailCommand FullEnvelope() => new()
    {
        Subject = "Mời phối hợp đón đoàn",
        BodyFormat = "HTML",
        Body = "<p>Kính gửi anh chị,</p><p>Nhờ hỗ trợ đón đoàn ngày 12/08.</p>",
        To = new List<EmailRecipientDto>
        {
            new() { Email = ToA, Name = "Người nhận A" },
            new() { Email = ToB },
        },
        Cc = new List<EmailRecipientDto> { new() { Email = CcA } },
        Bcc = new List<EmailRecipientDto> { new() { Email = BccA }, new() { Email = BccB } },
    };

    // ── One message for the whole envelope ───────────────────────────────────

    [Fact]
    public async Task A_composed_message_reaches_every_group_as_one_message_and_one_history_row()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var result = await Compose(db, Author).Handle(FullEnvelope(), CancellationToken.None);

        // FIVE addressees, ONE message. The handler this replaced produced five.
        var eml = _h.OnlyMessage();

        Assert.Equal("SENT", result.Status);
        Assert.True(result.Success);

        // Visible headers carry exactly the visible recipients.
        var to = eml.Header("To");
        var cc = eml.Header("Cc");
        Assert.Contains(ToA, to);
        Assert.Contains(ToB, to);
        Assert.Contains(CcA, cc);
        Assert.DoesNotContain(BccA, to);
        Assert.DoesNotContain(BccB, to);
        Assert.DoesNotContain(BccA, cc);
        Assert.DoesNotContain(BccB, cc);

        // The database says the same thing the message did.
        var rows = await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.SentEmailId == result.SentEmailId).ToListAsync();
        Assert.Equal(5, rows.Count);
        Assert.Equal(new[] { ToA, ToB }, rows.Where(r => r.RecipientType == "TO").Select(r => r.RecipientEmail));
        Assert.Equal(new[] { CcA }, rows.Where(r => r.RecipientType == "CC").Select(r => r.RecipientEmail));
        Assert.Equal(new[] { BccA, BccB }, rows.Where(r => r.RecipientType == "BCC").Select(r => r.RecipientEmail));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task What_the_visible_recipients_can_read_never_names_a_blind_copy()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        await Compose(db, Author).Handle(FullEnvelope(), CancellationToken.None);

        var eml = _h.OnlyMessage();

        // The message that is transmitted to each recipient carries To, Cc and the body — never Bcc.
        Assert.DoesNotContain(BccA, eml.Header("To"));
        Assert.DoesNotContain(BccA, eml.Header("Cc"));
        Assert.DoesNotContain(BccB, eml.Header("To"));
        Assert.DoesNotContain(BccB, eml.Header("Cc"));
        Assert.DoesNotContain(BccA, eml.Body);
        Assert.DoesNotContain(BccB, eml.Body);

        // Stronger than "not in To/Cc": the serialised message carries no Bcc header at all.
        Assert.Empty(eml.Header("Bcc"));

        // Where the blind addresses DO appear in this file is worth being exact about, because it is the
        // difference between a leak and a correct send. A pickup-directory drop prefixes the message with
        // .NET's X-Sender/X-Receiver envelope lines — one per addressee — which is how a pickup service
        // learns whom to deliver to. They are the file's stand-in for the RCPT TO commands of a live SMTP
        // conversation, and the service strips them before transmission; they are not message headers and
        // no recipient ever sees them. Everything below the envelope block — the message proper — names
        // only the visible recipients.
        var message = eml.Raw[eml.Raw.IndexOf("MIME-Version", StringComparison.Ordinal)..];
        Assert.DoesNotContain(BccA, message);
        Assert.DoesNotContain(BccB, message);
        Assert.Contains(ToA, message);
        Assert.Contains(CcA, message);

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task Every_addressee_shares_the_one_provider_identity()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var result = await Compose(db, Author).Handle(FullEnvelope(), CancellationToken.None);

        var sent = await db.SentEmails.AsNoTracking()
            .FirstAsync(e => e.SentEmailId == result.SentEmailId);
        var rows = await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.SentEmailId == result.SentEmailId).ToListAsync();

        Assert.False(string.IsNullOrWhiteSpace(sent.ProviderMessageId));
        Assert.All(rows, r => Assert.Equal(sent.ProviderMessageId, r.ProviderMessageId));

        // The id in the history is the id the message actually carries.
        Assert.Contains(sent.ProviderMessageId!, _h.OnlyMessage().Header("Message-Id"));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task The_stored_snapshot_is_the_content_that_was_sent()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var result = await Compose(db, Author).Handle(FullEnvelope(), CancellationToken.None);

        var sent = await db.SentEmails.AsNoTracking().FirstAsync(e => e.SentEmailId == result.SentEmailId);
        var eml = _h.OnlyMessage();

        Assert.Equal("Mời phối hợp đón đoàn", sent.Subject);
        Assert.Contains(EmlMessage.LiteralPrefix(sent.Subject), eml.DecodedHeader("Subject"));
        Assert.Contains("Nhờ hỗ trợ đón đoàn", sent.BodySnapshot);

        // Manual mail is not template mail: the row must not point at a system template.
        Assert.Null(sent.EmailTemplateId);

        await CleanupRowsAsync(db);
    }

    /// <summary>
    /// Phase E of the email fidelity plan: system-template mail's Final Preview must show the same
    /// branded shell (header/footer) the real send wraps it in — but manual compose is a deliberately
    /// separate pipeline that was never in scope for that, and must not silently start being wrapped in
    /// it as a side effect of making system-template previews accurate. A real send inspected as real
    /// MIME (not a mock) is the only way to disprove a wrapper this handler never asked for.
    /// </summary>
    [Fact]
    public async Task Manual_compose_is_never_wrapped_in_the_system_branded_shell()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        await Compose(db, Author).Handle(FullEnvelope(), CancellationToken.None);

        var eml = _h.OnlyMessage();
        Assert.DoesNotContain("PEMS — Campus Visit", eml.Body);
        Assert.DoesNotContain("linear-gradient(135deg,#004c91", eml.Body);
        Assert.DoesNotContain("Không trả lời email này", eml.Body);

        await CleanupRowsAsync(db);
    }

    // ── Attachments now travel with the command ──────────────────────────────

    /// <summary>
    /// A file attached in the composer arrives as a real MIME part.
    ///
    /// <para>
    /// This is new coverage rather than a port. Attachments used to reach a message only through a draft
    /// row, and this command hard-coded an empty list — so a person could attach a document, press send,
    /// and have the message go out without it, with nothing reporting the omission.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_file_attached_in_the_composer_arrives_as_a_real_MIME_part()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        await StoreAttachmentAsync(db, "ke-hoach-don-doan.pdf");

        var command = FullEnvelope();
        command.Attachments = new List<EmailComposeAttachmentInput>
        {
            new() { FileId = AttachmentFileId, DisplayName = "ke-hoach-don-doan.pdf", DisplayOrder = 0 },
        };

        var result = await Compose(db, Author).Handle(command, CancellationToken.None);

        Assert.Equal("SENT", result.Status);
        var eml = _h.OnlyMessage();
        Assert.Contains("ke-hoach-don-doan.pdf", eml.Raw);

        var attachments = await db.SentEmailAttachments.AsNoTracking()
            .Where(a => a.SentEmailId == result.SentEmailId).ToListAsync();
        Assert.Equal(AttachmentFileId, Assert.Single(attachments).FileId);

        await CleanupRowsAsync(db);
    }

    /// <summary>
    /// One unreadable file refuses the whole send, and nothing is recorded. The alternative that was in
    /// place — dropping the part and reporting success — told the author their document had gone.
    /// </summary>
    [Fact]
    public async Task An_attachment_whose_bytes_are_gone_refuses_the_send_and_records_nothing()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        // A files row with no object behind it: the shape a purged upload leaves.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO files (file_id, storage_provider, object_key, original_filename, mime_type, "
            + "file_size, uploaded_by, uploaded_at) VALUES ({0}, 'LOCAL', {1}, {2}, 'application/pdf', "
            + "1024, {3}, NOW())",
            AttachmentFileId, $"objects/{AttachmentFileId}-missing", "da-bi-xoa.pdf", AuthorId);

        var command = FullEnvelope();
        command.Attachments = new List<EmailComposeAttachmentInput>
        {
            new() { FileId = AttachmentFileId, DisplayName = "da-bi-xoa.pdf", DisplayOrder = 0 },
        };

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Compose(db, Author).Handle(command, CancellationToken.None));

        Assert.Equal(EmailErrorCodes.AttachmentUnreadable, error.ErrorCode);
        Assert.Contains("da-bi-xoa.pdf", error.Message);
        Assert.Empty(_h.Messages());
        Assert.Equal(0, await db.SentEmails.AsNoTracking().Where(e => e.SentBy == AuthorId).CountAsync());

        await CleanupRowsAsync(db);
    }

    // ── Status truthfulness ──────────────────────────────────────────────────

    [Fact]
    public async Task Provider_acceptance_is_recorded_as_SENT_and_never_as_DELIVERED()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var result = await Compose(db, Author).Handle(FullEnvelope(), CancellationToken.None);

        var sent = await db.SentEmails.AsNoTracking().FirstAsync(e => e.SentEmailId == result.SentEmailId);
        var rows = await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.SentEmailId == result.SentEmailId).ToListAsync();

        Assert.Equal("SENT", sent.Status);
        Assert.NotNull(sent.SentAt);
        // PEMS has no delivery webhook, so nothing may claim the message reached a mailbox.
        Assert.Null(sent.DeliveredAt);
        Assert.Equal(0u, sent.RetryCount);
        Assert.All(rows, r => Assert.Equal("SENT", r.DeliveryStatus));
        Assert.All(rows, r => Assert.Null(r.DeliveredAt));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_provider_failure_is_recorded_as_FAILED_with_a_safe_message()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        var result = await Compose(db, Author, brokenHost: "127.0.0.1")
            .Handle(FullEnvelope(), CancellationToken.None);

        Assert.Equal("FAILED", result.Status);
        Assert.False(result.Success);

        var sent = await db.SentEmails.AsNoTracking().FirstAsync(e => e.SentEmailId == result.SentEmailId);
        var rows = await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.SentEmailId == result.SentEmailId).ToListAsync();

        Assert.Equal("FAILED", sent.Status);
        Assert.Null(sent.SentAt);
        Assert.Null(sent.DeliveredAt);
        Assert.All(rows, r => Assert.Equal("FAILED", r.DeliveryStatus));

        // A safe sentence carrying the machine code (EmailAttemptRecord.Format) — not the SMTP exception
        // text. The prefix is what lets a recovery sweep tell a proven-not-dispatched failure apart from
        // an ambiguous one once this row is the only thing left (see EmailDeliveryCodes.ProvesNothingWasSent).
        //
        // A connection attempt to 127.0.0.1 with nothing listening throws a SocketException, which
        // SmtpDeliveryClassifier now classifies as the granular SMTP_CONNECTION_FAILED rather than the
        // pre-Phase-D catch-all SMTP_SEND_FAILED (email fidelity plan, Phase D) — this test's own
        // expectation moved with it, deliberately, not a regression.
        Assert.Equal("[SMTP_CONNECTION_FAILED] Không thể kết nối tới máy chủ SMTP.", sent.ErrorMessage);
        Assert.DoesNotContain("SmtpException", sent.ErrorMessage ?? "");
        Assert.DoesNotContain("127.0.0.1", sent.ErrorMessage ?? "");

        // The attempt happened and is on the record. With no draft to revert, the failed attempt IS the
        // record — and the composer still holds the message, so the author retries rather than rewrites.
        await CleanupRowsAsync(db);
    }

    // ── Manual compose ───────────────────────────────────────────────────────

    [Fact]
    public async Task Manual_compose_refuses_an_envelope_with_no_TO_and_records_nothing()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var error = await Assert.ThrowsAsync<ValidationException>(() => Compose(db, Author).Handle(
            new SendEmailCommand
            {
                Subject = "Không có người nhận chính",
                Body = "<p>x</p>",
                Cc = new List<EmailRecipientDto> { new() { Email = CcA } },
            }, CancellationToken.None));

        Assert.Equal(EmailErrorCodes.RecipientRequired, error.ErrorCode);
        Assert.Empty(_h.Messages());
        Assert.Equal(0, await db.SentEmails.AsNoTracking().Where(e => e.SentBy == AuthorId).CountAsync());

        await CleanupRowsAsync(db);
    }

    /// <summary>
    /// The same mailbox in TO and BCC leaks the blind copy the moment the TO header is read. It is
    /// refused at the send, which is now the only place it can be refused — there is no earlier save to
    /// catch it on.
    /// </summary>
    [Fact]
    public async Task Manual_compose_refuses_the_same_address_in_TO_and_BCC()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var error = await Assert.ThrowsAsync<ValidationException>(() => Compose(db, Author).Handle(
            new SendEmailCommand
            {
                Subject = "Trùng nhóm",
                Body = "<p>x</p>",
                To = new List<EmailRecipientDto> { new() { Email = ToA } },
                Bcc = new List<EmailRecipientDto> { new() { Email = ToA.ToUpperInvariant() } },
            }, CancellationToken.None));

        Assert.Equal(EmailErrorCodes.RecipientCrossGroupDuplicate, error.ErrorCode);
        Assert.Empty(_h.Messages());
        Assert.Equal(0, await db.SentEmails.AsNoTracking().Where(e => e.SentBy == AuthorId).CountAsync());

        await CleanupRowsAsync(db);
    }

    // ── Reply ────────────────────────────────────────────────────────────────

    /// <summary>Sends one message from the Partner to the Author, to be replied to.</summary>
    private async Task<ulong> SeedIncomingAsync(ApplicationDbContext db)
    {
        var result = await Compose(db, Partner).Handle(new SendEmailCommand
        {
            Subject = "Đề nghị phối hợp",
            Body = "<p>Chào anh,</p>",
            To = new List<EmailRecipientDto> { new() { Email = Mail(AuthorId) } },
            Bcc = new List<EmailRecipientDto> { new() { Email = BccA } },
        }, CancellationToken.None);

        _h.ClearMessages();
        return result.SentEmailId!.Value;
    }

    [Fact]
    public async Task A_reply_carries_its_own_copies_and_never_the_originals_blind_ones()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var originalId = await SeedIncomingAsync(db);

        await Reply(db, Author).Handle(new ReplytoEmailCommand
        {
            OriginalEmailId = originalId,
            Body = "<p>Vâng, tôi đồng ý.</p>",
            Cc = new List<EmailRecipientInput> { new() { Email = CcA } },
            Bcc = new List<EmailRecipientInput> { new() { Email = BccB } },
        }, CancellationToken.None);

        var eml = _h.OnlyMessage();

        // The reply goes to the original sender, with the copies THIS author chose…
        Assert.Contains(Mail(PartnerId), eml.Header("To"));
        Assert.Contains(CcA, eml.Header("Cc"));

        // …and the original's blind copy is nowhere near it.
        Assert.DoesNotContain(BccA, eml.Raw);

        var reply = await db.SentEmails.AsNoTracking()
            .Where(e => e.RelatedType == "REPLY" && e.RelatedId == originalId)
            .OrderByDescending(e => e.SentEmailId).FirstAsync();
        var rows = await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.SentEmailId == reply.SentEmailId).ToListAsync();

        Assert.Equal(new[] { Mail(PartnerId) }, rows.Where(r => r.RecipientType == "TO").Select(r => r.RecipientEmail));
        Assert.Equal(new[] { CcA }, rows.Where(r => r.RecipientType == "CC").Select(r => r.RecipientEmail));
        Assert.Equal(new[] { BccB }, rows.Where(r => r.RecipientType == "BCC").Select(r => r.RecipientEmail));
        Assert.DoesNotContain(rows, r => r.RecipientEmail == BccA);

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_reply_points_at_its_parent_and_leaves_it_untouched()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var originalId = await SeedIncomingAsync(db);
        var before = await db.SentEmails.AsNoTracking().FirstAsync(e => e.SentEmailId == originalId);

        await Reply(db, Author).Handle(new ReplytoEmailCommand
        {
            OriginalEmailId = originalId,
            Body = "<p>Đã nhận.</p>",
        }, CancellationToken.None);

        var eml = _h.OnlyMessage();
        var reply = await db.SentEmails.AsNoTracking()
            .Where(e => e.RelatedType == "REPLY" && e.RelatedId == originalId)
            .OrderByDescending(e => e.SentEmailId).FirstAsync();

        // Threading is real: the header points at the parent's actual identifier.
        Assert.Contains(before.ProviderMessageId!, eml.Header("In-Reply-To"));
        Assert.Contains(before.ProviderMessageId!, eml.Header("References"));
        Assert.Equal(before.ProviderThreadId, reply.ProviderThreadId);
        Assert.StartsWith("Re: ", reply.Subject);

        // The message being answered is not modified — replying is not a delivery confirmation.
        db.ChangeTracker.Clear();
        var after = await db.SentEmails.AsNoTracking().FirstAsync(e => e.SentEmailId == originalId);
        Assert.Equal(before.DeliveredAt, after.DeliveredAt);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.SentAt, after.SentAt);

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task Someone_who_was_never_on_the_message_cannot_reply_to_it()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        // A message between two other parties entirely.
        var result = await Compose(db, Partner).Handle(new SendEmailCommand
        {
            Subject = "Trao đổi riêng",
            Body = "<p>Nội dung riêng.</p>",
            To = new List<EmailRecipientDto> { new() { Email = ToA } },
        }, CancellationToken.None);
        _h.ClearMessages();

        await Assert.ThrowsAsync<ForbiddenException>(() => Reply(db, Author).Handle(
            new ReplytoEmailCommand { OriginalEmailId = result.SentEmailId!.Value, Body = "<p>Xen vào.</p>" },
            CancellationToken.None));

        Assert.Empty(_h.Messages());

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_reply_is_refused_before_anything_is_recorded_when_the_content_is_invalid()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var originalId = await SeedIncomingAsync(db);
        var beforeCount = await db.SentEmails.AsNoTracking().CountAsync();

        // Forging the action-block markers would move the boundary the history strip depends on.
        var error = await Assert.ThrowsAsync<ValidationException>(() => Reply(db, Author).Handle(
            new ReplytoEmailCommand
            {
                OriginalEmailId = originalId,
                Body = EmailComposition.ActionBlockStart + "<a href='#'>Đồng ý</a>" + EmailComposition.ActionBlockEnd,
            }, CancellationToken.None));

        Assert.Equal(EmailErrorCodes.AuthoredActionBlockForbidden, error.ErrorCode);
        Assert.Empty(_h.Messages());
        Assert.Equal(beforeCount, await db.SentEmails.AsNoTracking().CountAsync());

        await CleanupRowsAsync(db);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Puts real bytes in the local store and the matching <c>files</c> row behind them.</summary>
    private async Task StoreAttachmentAsync(ApplicationDbContext db, string fileName)
    {
        var stored = await Storage().SaveAsync(
            new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }),   // "%PDF-"
            fileName, "application/pdf", "emails", CancellationToken.None);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO files (file_id, storage_provider, object_key, original_filename, mime_type, "
            + "file_size, uploaded_by, uploaded_at) VALUES ({0}, 'LOCAL', {1}, {2}, 'application/pdf', "
            + "{3}, {4}, NOW())",
            AttachmentFileId, stored.ObjectKey, fileName, 5, AuthorId);
    }
}
