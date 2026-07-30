using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.IntegrationTests.Api;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The Batch-1 account mail, end to end and with nothing faked in between: the row in
/// <c>email_templates</c> on a real MySQL database, the real renderer, the real dispatcher, the real
/// <see cref="EmailService"/>, and the <c>.eml</c> file an SMTP server would have received.
///
/// <para>
/// Two claims need this whole chain to be believable. First, that the content is the DATABASE's — proven
/// by editing the row mid-process and watching the very next message change, with no restart and no cache
/// to clear. Second, that an account notice is addressed to one person and copied to nobody — proven on
/// the produced headers, since "we passed a single recipient" is a statement about intent and the
/// message is the statement about fact.
/// </para>
/// </summary>
public sealed class AccountEmailEndToEndTests : IDisposable
{
    private static string ConnString =>
        PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private static bool? _dbUp;
    private static string? _dbFailure;

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch (Exception ex) { _dbUp = false; _dbFailure = ex.ToString(); }
        }

        Assert.True(_dbUp, "Disposable MySQL database is not reachable. " + _dbFailure);
    }

    private const string Code = SystemEmailTemplates.AccountEmailConfirmation;
    private const string Marker = "batch1-evidence@fpt.edu.vn";
    private const string ConfirmUrl = "https://pems.test/confirm-email?token=raw-evidence-token";

    private readonly string _pickupDir =
        Path.Combine(Path.GetTempPath(), "pems-account-mail-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_pickupDir)) Directory.Delete(_pickupDir, recursive: true); }
        catch (IOException) { /* a temp dir left behind must never fail a test run */ }
    }

    /// <summary>The real sender, writing .eml files instead of opening a connection.</summary>
    private EmailService Sender()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Enabled"] = "true",
                ["Smtp:PickupDirectory"] = _pickupDir,
                ["Smtp:FromEmail"] = "no-reply@pems.test",
                ["Smtp:FromName"] = "PEMS",
            })
            .Build();

        return new EmailService(
            config,
            NullLogger<EmailService>.Instance,
            new FakeHostEnvironment("Development"),
            Options.Create(new EmailRecipientOptions()));
    }

    private SystemEmailDispatcher Dispatcher(ApplicationDbContext db)
        => new(db, new EmailTemplateRenderer(db), Sender());

    private static SystemEmailRequest Confirmation(string language = EmailLanguages.Vi) => new(
        Code,
        new EmailRecipient(Marker, "Nguyễn Văn Ánh"),
        new Dictionary<string, string>
        {
            ["fullName"] = "Nguyễn Văn Ánh",
            ["roleName"] = "Staff — Chuyên viên IC",
            ["campusName"] = "FPT University HCM",
            ["expiresInHours"] = "24",
        },
        language,
        TrustedBlocks: new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] = EmailComposition.ConfirmEmailBlock(ConfirmUrl),
        },
        RelatedType: "User",
        RelatedId: 3);

    private string[] Messages() =>
        Directory.Exists(_pickupDir) ? Directory.GetFiles(_pickupDir, "*.eml") : Array.Empty<string>();

    private string ReadOnlyMessage()
    {
        var files = Messages();
        Assert.Single(files);
        return File.ReadAllText(files[0]);
    }

    /// <summary>Removes only the history rows this class creates, identified by its marker address.</summary>
    private static async Task CleanupAsync()
    {
        using var db = NewContext();
        var ids = await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.RecipientEmail == Marker)
            .Select(r => r.SentEmailId)
            .Distinct()
            .ToListAsync();

        if (ids.Count == 0) return;

        await db.SentEmailRecipients.Where(r => ids.Contains(r.SentEmailId)).ExecuteDeleteAsync();
        await db.SentEmails.Where(e => ids.Contains(e.SentEmailId)).ExecuteDeleteAsync();
    }

    // ── The envelope, on real MIME ───────────────────────────────────────────

    [Fact]
    public async Task The_confirmation_mail_goes_to_one_person_with_no_copies()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var result = await Dispatcher(db).SendAsync(Confirmation());
            Assert.Equal(EmailDeliveryStatus.Sent, result.Delivery.Status);

            var eml = ReadOnlyMessage();

            // Exactly one addressee, and no Cc/Bcc header of any kind. An account confirmation carries a
            // one-time link: a second recipient on the same message would be handed somebody else's.
            Assert.Contains(Marker, HeaderValue(eml, "To"));
            Assert.Equal(1, CountAddresses(HeaderValue(eml, "To")));
            Assert.Equal(string.Empty, HeaderValue(eml, "Cc"));
            Assert.Equal(string.Empty, HeaderValue(eml, "Bcc"));
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task The_subject_and_body_are_the_ones_stored_in_the_database()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var row = await db.EmailTemplates.AsNoTracking().SingleAsync(t => t.TemplateCode == Code);

            await Dispatcher(db).SendAsync(Confirmation());
            var eml = ReadOnlyMessage();

            // The literal part of the stored subject is enough to tie the produced message to that row.
            Assert.Contains(LiteralPrefix(row.SubjectVi), DecodedHeader(eml, "Subject"));

            var body = DecodedBody(eml);
            // Variable values arrive HTML-encoded (WebUtility.HtmlEncode also turns Vietnamese letters
            // into numeric character references — harmless in a mail client, but it means the assertion
            // has to be made on the encoded form).
            Assert.Contains(System.Net.WebUtility.HtmlEncode("Nguyễn Văn Ánh"), body);
            Assert.Contains("FPT University HCM", body);          // ASCII value, unchanged by encoding
            Assert.Contains(ConfirmUrl, body);                    // the trusted block, not a variable
            Assert.DoesNotContain("{{", body);                    // nothing left unresolved
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task English_is_taken_from_the_same_row_not_from_a_second_template()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var row = await db.EmailTemplates.AsNoTracking().SingleAsync(t => t.TemplateCode == Code);

            await Dispatcher(db).SendAsync(Confirmation(EmailLanguages.En));

            Assert.Contains(LiteralPrefix(row.SubjectEn), DecodedHeader(ReadOnlyMessage(), "Subject"));
        }
        finally { await CleanupAsync(); }
    }

    // ── The content really does come from the database ───────────────────────

    [Fact]
    public async Task Editing_the_template_changes_the_very_next_message_without_a_restart()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var original = await db.EmailTemplates.SingleAsync(t => t.TemplateCode == Code);
            var originalSubject = original.SubjectVi;
            var originalBody = original.BodyVi;

            try
            {
                // First send: whatever the row currently says.
                await Dispatcher(db).SendAsync(Confirmation());
                var before = DecodedHeader(ReadOnlyMessage(), "Subject");

                // An operator edits the template through the configuration screen — here, the same UPDATE
                // that screen performs. No process restart, no cache invalidation call.
                original.SubjectVi = "[PEMS] Ban quản trị đã đổi tiêu đề {{campusName}}";
                original.BodyVi = "<p>Nội dung mới cho {{fullName}} ({{roleName}}, {{expiresInHours}}h).</p>{{actionBlock}}";
                await db.SaveChangesAsync();

                foreach (var file in Messages()) File.Delete(file);

                // Second send, same process, same DbContext, same dispatcher construction.
                await Dispatcher(db).SendAsync(Confirmation());
                var eml = ReadOnlyMessage();
                var after = DecodedHeader(eml, "Subject");
                var body = DecodedBody(eml);

                Assert.NotEqual(before, after);
                Assert.Contains("Ban quản trị đã đổi tiêu đề", after);
                Assert.Contains("FPT University HCM", after);       // the edited subject still renders
                // The literal text of the edited body arrives as typed; the variable value beside it is
                // encoded, as every variable value is.
                Assert.Contains("Nội dung mới cho", body);
                Assert.Contains(System.Net.WebUtility.HtmlEncode("Nguyễn Văn Ánh"), body);
                Assert.Contains("24h", body);
                Assert.Contains(ConfirmUrl, body);                  // the action block still lands
                Assert.DoesNotContain("{{", body);
            }
            finally
            {
                // Put the seeded content back: this class shares the disposable database with the
                // registry/seed contract tests, which assert on the real content of this row.
                original.SubjectVi = originalSubject;
                original.BodyVi = originalBody;
                await db.SaveChangesAsync();
            }
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task A_subject_edited_to_include_the_action_block_is_refused()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var row = await db.EmailTemplates.SingleAsync(t => t.TemplateCode == Code);
            var originalSubject = row.SubjectVi;

            try
            {
                // The confirmation link only ever enters a message through the action block, so putting
                // that block in the subject is how a one-time URL would end up in sent_emails.subject.
                row.SubjectVi = "[PEMS] Xác nhận: {{actionBlock}}";
                await db.SaveChangesAsync();

                var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                    () => Dispatcher(db).SendAsync(Confirmation()));

                Assert.Equal(EmailErrorCodes.TemplateSensitiveInSubject, ex.ErrorCode);
                Assert.DoesNotContain(ConfirmUrl, ex.Message);
                Assert.Empty(Messages());

                using var verify = NewContext();
                Assert.False(await verify.SentEmailRecipients.AsNoTracking()
                    .AnyAsync(r => r.RecipientEmail == Marker));
            }
            finally
            {
                row.SubjectVi = originalSubject;
                await db.SaveChangesAsync();
            }
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task The_history_row_keeps_the_metadata_but_not_the_one_time_link()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var template = await db.EmailTemplates.AsNoTracking().SingleAsync(t => t.TemplateCode == Code);

            var result = await Dispatcher(db).SendAsync(Confirmation());

            // Read back through a separate context — the point is what the database holds, not what the
            // dispatcher's change tracker remembers.
            using var verify = NewContext();
            var sent = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);
            var recipients = await verify.SentEmailRecipients.AsNoTracking()
                .Where(r => r.SentEmailId == result.SentEmailId)
                .ToListAsync();

            Assert.Equal(template.EmailTemplateId, sent.EmailTemplateId);
            Assert.DoesNotContain("{{", sent.Subject);              // the RENDERED subject, not the pattern

            // The confirmation URL is a one-time token: stored in body_snapshot it would be readable
            // through the email-history API by any internal role, who could then activate somebody
            // else's account. It lives only in the action block, so the history keeps the message with
            // that block removed — the record stays useful and the credential does not survive.
            Assert.Equal(HistoryBodyPolicy.ActionBlockStripped, SensitiveEmailHistory.PolicyFor(Code));
            Assert.NotNull(sent.BodySnapshot);
            Assert.DoesNotContain(ConfirmUrl, sent.BodySnapshot!);
            Assert.DoesNotContain("raw-evidence-token", sent.BodySnapshot!);
            Assert.Contains("FPT University HCM", sent.BodySnapshot!);   // the explanatory text remains
            Assert.DoesNotContain(ConfirmUrl, sent.Subject);

            var recipient = Assert.Single(recipients);
            Assert.Equal(Marker, recipient.RecipientEmail);
            Assert.Equal(EmailRecipientTypes.To, recipient.RecipientType);

            // Acceptance is as far as PEMS can honestly go: there is no delivery webhook.
            Assert.Equal("SENT", sent.Status);
            Assert.Null(sent.DeliveredAt);
        }
        finally { await CleanupAsync(); }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a header's value from the .eml, following RFC 5322 folded continuation lines and stopping
    /// at the end of the header block — an address in the BODY is not a leak; one in a header is.
    /// </summary>
    private static string HeaderValue(string eml, string headerName)
    {
        var lines = eml.Replace("\r\n", "\n").Split('\n');
        var value = new StringBuilder();
        var capturing = false;

        foreach (var line in lines)
        {
            if (line.Length == 0) break;

            if (capturing)
            {
                if (line[0] is ' ' or '\t') { value.Append(line.Trim()); continue; }
                break;
            }

            if (line.StartsWith(headerName + ":", StringComparison.OrdinalIgnoreCase))
            {
                capturing = true;
                value.Append(line[(headerName.Length + 1)..].Trim());
            }
        }

        return value.ToString();
    }

    /// <summary>A header value with any RFC 2047 encoded-words decoded back to text.</summary>
    private static string DecodedHeader(string eml, string headerName)
        => DecodeEncodedWords(HeaderValue(eml, headerName));

    private static string DecodeEncodedWords(string value)
    {
        var result = new StringBuilder();
        var rest = value;

        while (true)
        {
            var start = rest.IndexOf("=?", StringComparison.Ordinal);
            if (start < 0) { result.Append(rest); break; }

            result.Append(rest[..start]);
            var end = rest.IndexOf("?=", start + 2, StringComparison.Ordinal);
            if (end < 0) { result.Append(rest[start..]); break; }

            // =?charset?B|Q?text?=
            var word = rest[(start + 2)..end];
            var parts = word.Split('?');
            if (parts.Length == 3)
            {
                var charset = Encoding.GetEncoding(parts[0]);
                result.Append(parts[1].ToUpperInvariant() == "B"
                    ? charset.GetString(Convert.FromBase64String(parts[2]))
                    : DecodeQuotedPrintable(parts[2], charset));
            }

            rest = rest[(end + 2)..];
        }

        return result.ToString();
    }

    private static string DecodeQuotedPrintable(string text, Encoding charset)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '=' && i + 2 < text.Length)
            {
                bytes.Add(Convert.ToByte(text.Substring(i + 1, 2), 16));
                i += 2;
            }
            else bytes.Add((byte)(text[i] == '_' ? ' ' : text[i]));
        }

        return charset.GetString(bytes.ToArray());
    }

    /// <summary>The message body, decoded from whatever transfer encoding .NET chose for it.</summary>
    private static string DecodedBody(string eml)
    {
        var normalized = eml.Replace("\r\n", "\n");
        var split = normalized.IndexOf("\n\n", StringComparison.Ordinal);
        var headers = split < 0 ? normalized : normalized[..split];
        var body = split < 0 ? string.Empty : normalized[(split + 2)..];

        if (headers.Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            var cleaned = new string(body.Where(c => !char.IsWhiteSpace(c)).ToArray());
            return Encoding.UTF8.GetString(Convert.FromBase64String(cleaned));
        }

        if (headers.Contains("quoted-printable", StringComparison.OrdinalIgnoreCase))
            return DecodeQuotedPrintable(body.Replace("=\n", string.Empty), Encoding.UTF8);

        return body;
    }

    /// <summary>How many addresses an address header lists.</summary>
    private static int CountAddresses(string headerValue)
        => headerValue.Count(c => c == '@');

    /// <summary>
    /// The fixed text at the start of a stored subject, up to its first placeholder. Comparing on that
    /// keeps the assertion true whether or not the row's subject interpolates anything.
    /// </summary>
    private static string LiteralPrefix(string? stored)
    {
        Assert.False(string.IsNullOrWhiteSpace(stored));
        var at = stored!.IndexOf("{{", StringComparison.Ordinal);
        return (at < 0 ? stored : stored[..at]).Trim();
    }
}
