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
/// The password-reset mail (Batch 2), end to end against a real database, the real renderer and a real
/// <c>.eml</c> file.
///
/// <para>
/// The distinguishing test here is <see cref="The_history_row_keeps_the_metadata_but_not_the_code"/>.
/// The old code path wrote no history at all; routing this mail through the dispatcher creates one, and
/// a rendered body would put a LIVE reset code into <c>sent_emails.body_snapshot</c> — a column the
/// email-history API serves to every internal role with no recipient check. So for templates the
/// registry marks sensitive, the body is not stored. These tests hold that line from both sides: the
/// code reaches the recipient, and it reaches nothing else.
/// </para>
/// </summary>
public sealed class AuthEmailEndToEndTests : IDisposable
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

    private const string Code = SystemEmailTemplates.AuthPasswordResetOtp;
    private const string Marker = "batch2-evidence@fpt.edu.vn";
    private const string OtpCode = "418293";

    private readonly string _pickupDir =
        Path.Combine(Path.GetTempPath(), "pems-auth-mail-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_pickupDir)) Directory.Delete(_pickupDir, recursive: true); }
        catch (IOException) { /* a temp dir left behind must never fail a test run */ }
    }

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

    private static SystemEmailRequest Reset(string language = EmailLanguages.Vi) => new(
        Code,
        new EmailRecipient(Marker, "Nguyễn Văn Ánh"),
        new Dictionary<string, string>
        {
            ["fullName"] = "Nguyễn Văn Ánh",
            ["otpCode"] = OtpCode,
            ["expireMinutes"] = "15",
        },
        language,
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

    // ── The code reaches the recipient ───────────────────────────────────────

    [Fact]
    public async Task The_reset_mail_carries_the_code_to_one_person_with_no_copies()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var result = await Dispatcher(db).SendAsync(Reset());
            Assert.Equal(EmailDeliveryStatus.Sent, result.Delivery.Status);

            var eml = ReadOnlyMessage();

            Assert.Contains(Marker, HeaderValue(eml, "To"));
            Assert.Equal(1, HeaderValue(eml, "To").Count(c => c == '@'));
            Assert.Equal(string.Empty, HeaderValue(eml, "Cc"));
            Assert.Equal(string.Empty, HeaderValue(eml, "Bcc"));

            // The whole point of the message.
            Assert.Contains(OtpCode, DecodedBody(eml));
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

            await Dispatcher(db).SendAsync(Reset());
            var eml = ReadOnlyMessage();

            Assert.Contains(LiteralPrefix(row.SubjectVi), DecodedHeader(eml, "Subject"));

            var body = DecodedBody(eml);
            Assert.Contains(System.Net.WebUtility.HtmlEncode("Nguyễn Văn Ánh"), body);
            Assert.Contains("15", body);                   // the lifetime the caller passed
            Assert.DoesNotContain("{{", body);
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

            await Dispatcher(db).SendAsync(Reset(EmailLanguages.En));

            var eml = ReadOnlyMessage();
            Assert.Contains(LiteralPrefix(row.SubjectEn), DecodedHeader(eml, "Subject"));
            Assert.Contains(OtpCode, DecodedBody(eml));
        }
        finally { await CleanupAsync(); }
    }

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
                await Dispatcher(db).SendAsync(Reset());
                var before = DecodedHeader(ReadOnlyMessage(), "Subject");

                original.SubjectVi = "[PEMS] Tiêu đề mới cho mã đặt lại";
                original.BodyVi = "<p>{{fullName}} — mã {{otpCode}}, {{expireMinutes}} phút.</p>";
                await db.SaveChangesAsync();

                foreach (var file in Messages()) File.Delete(file);

                await Dispatcher(db).SendAsync(Reset());
                var eml = ReadOnlyMessage();

                Assert.NotEqual(before, DecodedHeader(eml, "Subject"));
                Assert.Contains("Tiêu đề mới cho mã đặt lại", DecodedHeader(eml, "Subject"));
                Assert.Contains(OtpCode, DecodedBody(eml));
                Assert.DoesNotContain("{{", DecodedBody(eml));
            }
            finally
            {
                original.SubjectVi = originalSubject;
                original.BodyVi = originalBody;
                await db.SaveChangesAsync();
            }
        }
        finally { await CleanupAsync(); }
    }

    // ── …and reaches nothing else ────────────────────────────────────────────

    [Fact]
    public async Task The_history_row_keeps_the_metadata_but_not_the_code()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var template = await db.EmailTemplates.AsNoTracking().SingleAsync(t => t.TemplateCode == Code);

            var result = await Dispatcher(db).SendAsync(Reset());

            using var verify = NewContext();
            var sent = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);
            var recipients = await verify.SentEmailRecipients.AsNoTracking()
                .Where(r => r.SentEmailId == result.SentEmailId)
                .ToListAsync();

            // Everything an operator needs to answer "was it sent, to whom, did it work" is there…
            Assert.Equal(template.EmailTemplateId, sent.EmailTemplateId);
            Assert.False(string.IsNullOrWhiteSpace(sent.Subject));
            Assert.Equal("SENT", sent.Status);
            Assert.NotNull(sent.SentAt);
            Assert.Null(sent.DeliveredAt);
            var recipient = Assert.Single(recipients);
            Assert.Equal(Marker, recipient.RecipientEmail);
            Assert.Equal(EmailRecipientTypes.To, recipient.RecipientType);

            // …and the live credential is not.
            Assert.Null(sent.BodySnapshot);
            Assert.DoesNotContain(OtpCode, sent.Subject);
            Assert.DoesNotContain(OtpCode, sent.ErrorMessage ?? string.Empty);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task A_failed_send_records_the_failure_without_recording_the_code()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            // No pickup directory and SMTP "enabled" → the send fails at the provider.
            var brokenConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Smtp:Enabled"] = "true",
                    ["Smtp:Host"] = "no-such-host.invalid",
                    ["Smtp:Port"] = "2525",
                    ["Smtp:FromEmail"] = "no-reply@pems.test",
                })
                .Build();

            var broken = new EmailService(
                brokenConfig, NullLogger<EmailService>.Instance,
                new FakeHostEnvironment("Production"), Options.Create(new EmailRecipientOptions()));

            var dispatcher = new SystemEmailDispatcher(db, new EmailTemplateRenderer(db), broken);
            var result = await dispatcher.SendAsync(Reset());

            Assert.Equal(EmailDeliveryStatus.Failed, result.Delivery.Status);

            using var verify = NewContext();
            var sent = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);

            Assert.Equal("FAILED", sent.Status);
            Assert.Null(sent.BodySnapshot);
            // The error text is the safe one — a provider message must not carry the code either.
            Assert.DoesNotContain(OtpCode, sent.ErrorMessage ?? string.Empty);
        }
        finally { await CleanupAsync(); }
    }

    // ── A hot-edit cannot move the secret into the subject ───────────────────

    /// <summary>
    /// Runs <paramref name="body"/> with the template's subject replaced, and always puts the seeded
    /// subject back. The edit is a plain UPDATE — the same one the template screen performs.
    /// </summary>
    private static async Task WithSubjectAsync(
        ApplicationDbContext db, string? subjectVi, string? subjectEn, Func<Task> body)
    {
        var row = await db.EmailTemplates.SingleAsync(t => t.TemplateCode == Code);
        var originalVi = row.SubjectVi;
        var originalEn = row.SubjectEn;

        try
        {
            if (subjectVi is not null) row.SubjectVi = subjectVi;
            if (subjectEn is not null) row.SubjectEn = subjectEn;
            await db.SaveChangesAsync();

            await body();
        }
        finally
        {
            row.SubjectVi = originalVi;
            row.SubjectEn = originalEn;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task A_subject_edited_to_include_the_code_is_refused_before_anything_is_sent()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            await WithSubjectAsync(db, "[PEMS] Mã của bạn là {{otpCode}}", null, async () =>
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                    () => Dispatcher(db).SendAsync(Reset()));

                Assert.Equal(EmailErrorCodes.TemplateSensitiveInSubject, ex.ErrorCode);
                // The refusal names the placeholder, never its value.
                Assert.Contains("otpCode", ex.Message);
                Assert.DoesNotContain(OtpCode, ex.Message);

                Assert.Empty(Messages());                       // nothing was sent
                using var verify = NewContext();
                Assert.False(await verify.SentEmails.AsNoTracking()
                    .AnyAsync(e => e.Subject.Contains(OtpCode)));
                Assert.False(await verify.SentEmailRecipients.AsNoTracking()
                    .AnyAsync(r => r.RecipientEmail == Marker));
            });
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task A_poisoned_English_subject_is_refused_even_when_sending_Vietnamese()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            await WithSubjectAsync(db, null, "[PEMS] Your code is {{otpCode}}", async () =>
            {
                // Sending VI: the EN subject is the one that was tampered with, and it is still caught.
                // A guard that only looked at the language being sent would let the next English-speaking
                // recipient trip over it in production instead.
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                    () => Dispatcher(db).SendAsync(Reset()));

                Assert.Equal(EmailErrorCodes.TemplateSensitiveInSubject, ex.ErrorCode);
                Assert.DoesNotContain(OtpCode, ex.Message);
                Assert.Empty(Messages());

                using var verify = NewContext();
                Assert.False(await verify.SentEmailRecipients.AsNoTracking()
                    .AnyAsync(r => r.RecipientEmail == Marker));
            });
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task A_non_secret_variable_is_still_welcome_in_the_subject()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            await WithSubjectAsync(db, "[PEMS] Mã đặt lại cho {{fullName}}", null, async () =>
            {
                var result = await Dispatcher(db).SendAsync(Reset());

                Assert.Equal(EmailDeliveryStatus.Sent, result.Delivery.Status);
                var subject = DecodedHeader(ReadOnlyMessage(), "Subject");
                Assert.Contains("Nguyễn Văn Ánh", subject);
                Assert.DoesNotContain(OtpCode, subject);

                // …and the stored subject is the personalised one, still without the code.
                using var verify = NewContext();
                var sent = await verify.SentEmails.AsNoTracking()
                    .SingleAsync(e => e.SentEmailId == result.SentEmailId);
                Assert.Contains("Nguyễn Văn Ánh", sent.Subject);
                Assert.DoesNotContain(OtpCode, sent.Subject);
                Assert.Null(sent.BodySnapshot);
            });
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task An_inactive_template_stops_the_send_and_writes_no_history()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var row = await db.EmailTemplates.SingleAsync(t => t.TemplateCode == Code);
            var originalStatus = row.Status;

            try
            {
                row.Status = "INACTIVE";
                await db.SaveChangesAsync();

                var ex = await Assert.ThrowsAsync<ConflictException>(
                    () => Dispatcher(db).SendAsync(Reset()));

                Assert.Equal(EmailErrorCodes.TemplateInactive, ex.ErrorCode);
                Assert.Empty(Messages());                        // nothing left the building

                using var verify = NewContext();
                Assert.False(await verify.SentEmailRecipients.AsNoTracking()
                    .AnyAsync(r => r.RecipientEmail == Marker));  // and nothing was recorded
            }
            finally
            {
                row.Status = originalStatus;
                await db.SaveChangesAsync();
            }
        }
        finally { await CleanupAsync(); }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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

            var parts = rest[(start + 2)..end].Split('?');
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

    private static string LiteralPrefix(string? stored)
    {
        Assert.False(string.IsNullOrWhiteSpace(stored));
        var at = stored!.IndexOf("{{", StringComparison.Ordinal);
        return (at < 0 ? stored : stored[..at]).Trim();
    }
}
