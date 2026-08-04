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
using PEMS.Application.Emails.Commands.CreateEmailDraft;
using PEMS.Application.Emails.Commands.DiscardEmailDraft;
using PEMS.Application.Emails.Commands.ReplytoEmail;
using PEMS.Application.Emails.Commands.SendEmail;
using PEMS.Application.Emails.Commands.SendEmailDraft;
using PEMS.Application.Emails.Commands.UpdateEmailDraft;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Queries.GetEmailDraft;
using PEMS.Infrastructure.FileStorage;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Security;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Giai đoạn 5 — manual mail run for real: real handlers, a real database, and real MIME on disk.
///
/// <para>
/// All three manual paths used to loop the recipient list and call SMTP once per address, so a message
/// "to three people" was three separate messages, each showing its reader as the only recipient. The
/// reply handler went further: it wrote CC and BCC rows into the history and then sent to the TO address
/// alone, so the record claimed delivery to people who received nothing. Both are the kind of defect only
/// a real send can disprove, which is why nothing below is mocked.
/// </para>
/// </summary>
public sealed class ManualEmailPipelineTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("g5-evidence@partner.example.com");
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "pems-g5-files-" + Guid.NewGuid().ToString("N"));

    /// <summary>Suite-private id range, high enough that it cannot collide with any other suite.</summary>
    private const ulong Base = 991_100;
    private const ulong CampusId = Base + 1;
    private const ulong AuthorId = Base + 2;
    private const ulong PartnerId = Base + 3;

    private const string MailPrefix = "g5-";
    private const string MailDomain = "@partner.example.com";
    private static string Mail(ulong userId) => $"{MailPrefix}{userId}{MailDomain}";

    private const string ToA = "g5-to-a@partner.example.com";
    private const string ToB = "g5-to-b@partner.example.com";
    private const string CcA = "g5-cc-a@partner.example.com";
    private const string BccA = "g5-bcc-a@partner.example.com";
    private const string BccB = "g5-bcc-b@partner.example.com";

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

    private CreateEmailDraftCommandHandler CreateDraft(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, Sanitizer, Recipients);

    private UpdateEmailDraftCommandHandler UpdateDraft(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, Sanitizer, Recipients);

    /// <summary>
    /// The validate/claim/send/link pipeline moved into EmailDraftDispatcher when the setup-progress
    /// send needed the same one. The real dispatcher is built here rather than a stub, so these tests
    /// keep measuring the actual send path — only the ownership guard is left in the handler.
    /// </summary>
    private PEMS.Application.Emails.Common.EmailDraftDispatcher Dispatcher(
        ApplicationDbContext db, string? brokenHost = null)
        => new(db, Sanitizer, Storage(), Sender(db, brokenHost), Normalizer(db), Recipients);

    private SendEmailDraftCommandHandler SendDraft(
        ApplicationDbContext db, ICurrentUserService user, string? brokenHost = null)
        => new(db, user, Dispatcher(db, brokenHost));

    private SendEmailCommandHandler Compose(
        ApplicationDbContext db, ICurrentUserService user, string? brokenHost = null)
        => new(user, Sanitizer, Sender(db, brokenHost), Normalizer(db), Recipients);

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
            CampusId, "G5", "PEMS G5 Campus");

        // A STAFF account needs an IC department — a database trigger enforces both that and the campus.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'IC', 'ACTIVE')",
            CampusId, CampusId, "PEMS G5 Văn phòng IC");

        async Task User(ulong id, string name)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, "
                + $"department_id, status) VALUES ({id}, {{0}}, {{1}}, {staffRole}, 'STAFF', {CampusId}, "
                + $"{CampusId}, 'ACTIVE')",
                name, Mail(id));

        await User(AuthorId, "PEMS G5 Người soạn");
        await User(PartnerId, "PEMS G5 Đối tác");
    }

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    private static async Task CleanupRowsAsync(ApplicationDbContext db)
    {
        // sent_email_recipients / _attachments cascade from sent_emails; drafts are removed by owner.
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM sent_emails WHERE sent_by IN ({AuthorId}, {PartnerId})");
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM email_drafts WHERE created_by IN ({AuthorId}, {PartnerId})");
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM files WHERE uploaded_by IN ({AuthorId}, {PartnerId})");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM users WHERE user_id IN ({AuthorId}, {PartnerId})");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM departments WHERE department_id = {CampusId}");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM campuses WHERE campus_id = {CampusId}");
    }

    private static CreateEmailDraftCommand FullEnvelopeDraft() => new()
    {
        Subject = "Mời phối hợp đón đoàn",
        BodyContent = "<p>Kính gửi anh chị,</p><p>Nhờ hỗ trợ đón đoàn ngày 12/08.</p>",
        BodyFormat = "HTML",
        Recipients = new List<EmailComposeRecipientInput>
        {
            new() { Email = ToA, Name = "Người nhận A", RecipientType = "TO", DisplayOrder = 0 },
            new() { Email = ToB, RecipientType = "TO", DisplayOrder = 1 },
            new() { Email = CcA, RecipientType = "CC", DisplayOrder = 0 },
            new() { Email = BccA, RecipientType = "BCC", DisplayOrder = 0 },
            new() { Email = BccB, RecipientType = "BCC", DisplayOrder = 1 },
        },
    };

    // ── A. Draft round-trip ──────────────────────────────────────────────────

    [Fact]
    public async Task A_draft_round_trip_keeps_every_group_exactly_as_entered()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        var created = await CreateDraft(db, Author).Handle(FullEnvelopeDraft(), CancellationToken.None);

        var loaded = await new GetEmailDraftQueryHandler(db, Author)
            .Handle(new GetEmailDraftQuery(created.EmailDraftId), CancellationToken.None);

        Assert.Equal(new[] { ToA, ToB }, loaded.To.Select(r => r.RecipientEmail));
        Assert.Equal(new[] { CcA }, loaded.Cc.Select(r => r.RecipientEmail));
        Assert.Equal(new[] { BccA, BccB }, loaded.Bcc.Select(r => r.RecipientEmail));
        Assert.Equal("Người nhận A", loaded.To[0].RecipientName);

        // The rows themselves carry the group, so nothing downstream has to guess.
        var rows = await db.EmailDraftRecipients.AsNoTracking()
            .Where(r => r.EmailDraftId == created.EmailDraftId).ToListAsync();
        Assert.Equal(2, rows.Count(r => r.RecipientType == "TO"));
        Assert.Equal(1, rows.Count(r => r.RecipientType == "CC"));
        Assert.Equal(2, rows.Count(r => r.RecipientType == "BCC"));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task An_update_replaces_the_envelope_without_leaving_the_old_one_behind()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        var created = await CreateDraft(db, Author).Handle(FullEnvelopeDraft(), CancellationToken.None);

        var updated = await UpdateDraft(db, Author).Handle(new UpdateEmailDraftCommand
        {
            EmailDraftId = created.EmailDraftId,
            Subject = "Mời phối hợp đón đoàn (cập nhật)",
            BodyContent = "<p>Nội dung mới.</p>",
            BodyFormat = "HTML",
            Recipients = new List<EmailComposeRecipientInput>
            {
                new() { Email = ToB, RecipientType = "TO" },
                new() { Email = BccA, RecipientType = "BCC" },
            },
        }, CancellationToken.None);

        Assert.Equal(new[] { ToB }, updated.To.Select(r => r.RecipientEmail));
        Assert.Empty(updated.Cc);
        Assert.Equal(new[] { BccA }, updated.Bcc.Select(r => r.RecipientEmail));

        var remaining = await db.EmailDraftRecipients.AsNoTracking()
            .Where(r => r.EmailDraftId == created.EmailDraftId).Select(r => r.RecipientEmail).ToListAsync();
        Assert.DoesNotContain(ToA, remaining);
        Assert.DoesNotContain(BccB, remaining);

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_rejected_update_leaves_the_saved_envelope_untouched()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        var created = await CreateDraft(db, Author).Handle(FullEnvelopeDraft(), CancellationToken.None);

        // The same mailbox in TO and BCC: accepting it would expose the blind copy the moment the To
        // header is read.
        var bad = new UpdateEmailDraftCommand
        {
            EmailDraftId = created.EmailDraftId,
            Subject = "Hỏng",
            BodyContent = "<p>x</p>",
            Recipients = new List<EmailComposeRecipientInput>
            {
                new() { Email = ToA, RecipientType = "TO" },
                new() { Email = ToA.ToUpperInvariant(), RecipientType = "BCC" },
            },
        };

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => UpdateDraft(db, Author).Handle(bad, CancellationToken.None));
        Assert.Equal(EmailErrorCodes.RecipientCrossGroupDuplicate, error.ErrorCode);

        db.ChangeTracker.Clear();
        var rows = await db.EmailDraftRecipients.AsNoTracking()
            .Where(r => r.EmailDraftId == created.EmailDraftId).ToListAsync();
        Assert.Equal(5, rows.Count);

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_draft_belongs_to_its_author_alone()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);

        var created = await CreateDraft(db, Author).Handle(FullEnvelopeDraft(), CancellationToken.None);
        var id = created.EmailDraftId;

        await Assert.ThrowsAsync<ForbiddenException>(() => new GetEmailDraftQueryHandler(db, Partner)
            .Handle(new GetEmailDraftQuery(id), CancellationToken.None));

        await Assert.ThrowsAsync<ForbiddenException>(() => UpdateDraft(db, Partner).Handle(
            new UpdateEmailDraftCommand { EmailDraftId = id, Subject = "Chiếm quyền", BodyContent = "<p>x</p>" },
            CancellationToken.None));

        await Assert.ThrowsAsync<ForbiddenException>(() => new DiscardEmailDraftCommandHandler(db, Partner)
            .Handle(new DiscardEmailDraftCommand(id), CancellationToken.None));

        await Assert.ThrowsAsync<ForbiddenException>(() => SendDraft(db, Partner)
            .Handle(new SendEmailDraftCommand(id), CancellationToken.None));

        await CleanupRowsAsync(db);
    }

    // ── B. One message for the whole envelope ────────────────────────────────

    [Fact]
    public async Task Sending_a_draft_produces_one_message_and_one_history_row()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var created = await CreateDraft(db, Author).Handle(FullEnvelopeDraft(), CancellationToken.None);
        var result = await SendDraft(db, Author)
            .Handle(new SendEmailDraftCommand(created.EmailDraftId), CancellationToken.None);

        // FIVE addressees, ONE message. The previous handler produced five.
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

        var created = await CreateDraft(db, Author).Handle(FullEnvelopeDraft(), CancellationToken.None);
        await SendDraft(db, Author)
            .Handle(new SendEmailDraftCommand(created.EmailDraftId), CancellationToken.None);

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

        var created = await CreateDraft(db, Author).Handle(FullEnvelopeDraft(), CancellationToken.None);
        var result = await SendDraft(db, Author)
            .Handle(new SendEmailDraftCommand(created.EmailDraftId), CancellationToken.None);

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

        var created = await CreateDraft(db, Author).Handle(FullEnvelopeDraft(), CancellationToken.None);
        var result = await SendDraft(db, Author)
            .Handle(new SendEmailDraftCommand(created.EmailDraftId), CancellationToken.None);

        var sent = await db.SentEmails.AsNoTracking().FirstAsync(e => e.SentEmailId == result.SentEmailId);
        var eml = _h.OnlyMessage();

        Assert.Equal("Mời phối hợp đón đoàn", sent.Subject);
        Assert.Contains(EmlMessage.LiteralPrefix(sent.Subject), eml.DecodedHeader("Subject"));
        Assert.Contains("Nhờ hỗ trợ đón đoàn", sent.BodySnapshot);

        // Manual mail is not template mail: the row must not point at a system template.
        Assert.Null(sent.EmailTemplateId);

        await CleanupRowsAsync(db);
    }

    // ── C. Status truthfulness ───────────────────────────────────────────────

    [Fact]
    public async Task Provider_acceptance_is_recorded_as_SENT_and_never_as_DELIVERED()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var created = await CreateDraft(db, Author).Handle(FullEnvelopeDraft(), CancellationToken.None);
        var result = await SendDraft(db, Author)
            .Handle(new SendEmailDraftCommand(created.EmailDraftId), CancellationToken.None);

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

        var created = await CreateDraft(db, Author).Handle(FullEnvelopeDraft(), CancellationToken.None);

        var result = await SendDraft(db, Author, brokenHost: "127.0.0.1")
            .Handle(new SendEmailDraftCommand(created.EmailDraftId), CancellationToken.None);

        Assert.Equal("FAILED", result.Status);
        Assert.False(result.Success);

        var sent = await db.SentEmails.AsNoTracking().FirstAsync(e => e.SentEmailId == result.SentEmailId);
        var rows = await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.SentEmailId == result.SentEmailId).ToListAsync();

        Assert.Equal("FAILED", sent.Status);
        Assert.Null(sent.SentAt);
        Assert.Null(sent.DeliveredAt);
        Assert.All(rows, r => Assert.Equal("FAILED", r.DeliveryStatus));

        // A safe sentence, not the SMTP exception text.
        Assert.Equal("Email delivery failed.", sent.ErrorMessage);
        Assert.DoesNotContain("SmtpException", sent.ErrorMessage ?? "");
        Assert.DoesNotContain("127.0.0.1", sent.ErrorMessage ?? "");

        // The attempt happened and is on the record; the draft is not silently returned to DRAFT.
        var draft = await db.EmailDrafts.AsNoTracking()
            .FirstAsync(d => d.EmailDraftId == created.EmailDraftId);
        Assert.Equal(PEMS.Domain.Enums.EmailDraftStatus.SENT, draft.Status);
        Assert.Equal(result.SentEmailId, draft.SentEmailId);

        await CleanupRowsAsync(db);
    }

    // ── D. Concurrency ───────────────────────────────────────────────────────

    [Fact]
    public async Task Two_simultaneous_sends_of_one_draft_produce_exactly_one_message()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var created = await CreateDraft(db, Author).Handle(FullEnvelopeDraft(), CancellationToken.None);

        // Separate contexts, as two HTTP requests would have.
        using var dbA = EmailEvidenceHarness.NewContext();
        using var dbB = EmailEvidenceHarness.NewContext();

        var results = await Task.WhenAll(
            Attempt(dbA, created.EmailDraftId),
            Attempt(dbB, created.EmailDraftId));

        Assert.Equal(1, results.Count(r => r.Sent));
        Assert.Equal(1, results.Count(r => !r.Sent));
        Assert.Single(_h.Messages());

        var history = await db.SentEmails.AsNoTracking()
            .Where(e => e.SentBy == AuthorId).CountAsync();
        Assert.Equal(1, history);

        await CleanupRowsAsync(db);

        async Task<(bool Sent, string? Status)> Attempt(ApplicationDbContext context, ulong draftId)
        {
            try
            {
                var r = await SendDraft(context, Author)
                    .Handle(new SendEmailDraftCommand(draftId), CancellationToken.None);
                return (true, r.Status);
            }
            catch (ConflictException)
            {
                return (false, null);
            }
        }
    }

    [Fact]
    public async Task A_sent_draft_cannot_be_sent_again()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var created = await CreateDraft(db, Author).Handle(FullEnvelopeDraft(), CancellationToken.None);
        await SendDraft(db, Author).Handle(new SendEmailDraftCommand(created.EmailDraftId), CancellationToken.None);

        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ConflictException>(() => SendDraft(db, Author)
            .Handle(new SendEmailDraftCommand(created.EmailDraftId), CancellationToken.None));

        Assert.Single(_h.Messages());

        await CleanupRowsAsync(db);
    }

    // ── E. Manual compose ────────────────────────────────────────────────────

    [Fact]
    public async Task Manual_compose_sends_one_message_for_all_three_groups()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedAsync(db);
        _h.ClearMessages();

        var result = await Compose(db, Author).Handle(new SendEmailCommand
        {
            Subject = "Thông báo lịch tiếp đoàn",
            Body = "<p>Kính gửi anh chị,</p>",
            To = new List<EmailRecipientDto> { new() { Email = ToA }, new() { Email = ToB } },
            Cc = new List<EmailRecipientDto> { new() { Email = CcA } },
            Bcc = new List<EmailRecipientDto> { new() { Email = BccA } },
        }, CancellationToken.None);

        Assert.Equal("SENT", result.Status);
        var eml = _h.OnlyMessage();
        Assert.Contains(ToA, eml.Header("To"));
        Assert.Contains(CcA, eml.Header("Cc"));
        Assert.DoesNotContain(BccA, eml.Header("To"));
        Assert.DoesNotContain(BccA, eml.Header("Cc"));

        var rows = await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.SentEmailId == result.SentEmailId).ToListAsync();
        Assert.Equal(2, rows.Count(r => r.RecipientType == "TO"));
        Assert.Equal(1, rows.Count(r => r.RecipientType == "CC"));
        Assert.Equal(1, rows.Count(r => r.RecipientType == "BCC"));

        await CleanupRowsAsync(db);
    }

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

    // ── F. Reply ─────────────────────────────────────────────────────────────

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
}
