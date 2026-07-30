using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
/// The public visit-request verification code (Batch 3), end to end.
///
/// <para>
/// This mail differs from the account ones in who is on the other end: nobody yet. The address is what
/// the code is proving, so the flow tells the visitor nothing about templates, configuration or why a
/// send failed — every failure that stops the code leaving becomes the one stable answer
/// <c>OTP_SEND_FAILED</c>. These tests hold both halves: the code reaches the address, and no detail
/// about the system reaches the caller.
/// </para>
/// </summary>
public sealed class VisitRequestOtpEndToEndTests : IDisposable
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

    private const string Code = SystemEmailTemplates.VisitRequestOtp;
    private const string Marker = "batch3-evidence@example.com";
    private const string OtpCode = "735219";

    private readonly string _pickupDir =
        Path.Combine(Path.GetTempPath(), "pems-visit-otp-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_pickupDir)) Directory.Delete(_pickupDir, recursive: true); }
        catch (IOException) { /* a temp dir left behind must never fail a test run */ }
    }

    /// <summary>An OTP service that only has to answer "how long is a visit code valid".</summary>
    private sealed class FixedOtpSettings : IOtpService
    {
        public int CodeMinutes => 15;
        public int VisitRequestCodeMinutes => 5;

        public Task<string> CreateAsync(PEMS.Domain.Entities.Users.User user, string purpose, string? ip, string? ua, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> CreateForEmailAsync(string email, string purpose, string? ip, string? ua, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OtpVerificationResult> VerifyAsync(string email, string purpose, string rawCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OtpChallengeIssue> CreateChallengeAsync(string email, string purpose, string submissionId, string issueReason, string? ip, string? ua, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OtpChallengeVerification> VerifyChallengeAsync(string sessionToken, string email, string purpose, string submissionId, string rawCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OtpChallengeIssue> ResendChallengeAsync(string sessionToken, string email, string purpose, string? ip, string? ua, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OtpChallengeIssue> RecoverChallengeAsync(string email, string purpose, string submissionId, string? ip, string? ua, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static readonly IOtpService Settings = new FixedOtpSettings();

    private EmailService Sender(string? host = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Smtp:Enabled"] = "true",
            ["Smtp:FromEmail"] = "no-reply@pems.test",
            ["Smtp:FromName"] = "PEMS",
        };

        if (host is null) values["Smtp:PickupDirectory"] = _pickupDir;
        else { values["Smtp:Host"] = host; values["Smtp:Port"] = "2525"; }

        return new EmailService(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            NullLogger<EmailService>.Instance,
            new FakeHostEnvironment(host is null ? "Development" : "Production"),
            Options.Create(new EmailRecipientOptions()));
    }

    private SystemEmailDispatcher Dispatcher(ApplicationDbContext db, string? brokenHost = null)
        => new(db, new EmailTemplateRenderer(db), Sender(brokenHost));

    private Task SendAsync(ApplicationDbContext db, string? brokenHost = null)
        => VisitRequestOtpMail.SendAsync(
            Dispatcher(db, brokenHost), Settings, Marker, "Nguyễn Văn Ánh", OtpCode, CancellationToken.None);

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
            .Select(r => r.SentEmailId).Distinct().ToListAsync();

        if (ids.Count == 0) return;

        await db.SentEmailRecipients.Where(r => ids.Contains(r.SentEmailId)).ExecuteDeleteAsync();
        await db.SentEmails.Where(e => ids.Contains(e.SentEmailId)).ExecuteDeleteAsync();
    }

    // ── The code reaches the address being verified ──────────────────────────

    [Fact]
    public async Task The_code_goes_to_the_address_under_verification_and_nowhere_else()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            await SendAsync(db);

            var eml = ReadOnlyMessage();
            Assert.Contains(Marker, HeaderValue(eml, "To"));
            Assert.Equal(1, HeaderValue(eml, "To").Count(c => c == '@'));
            Assert.Equal(string.Empty, HeaderValue(eml, "Cc"));
            Assert.Equal(string.Empty, HeaderValue(eml, "Bcc"));

            // No display name: at this point the form's name is a claim, not a verified fact, so the
            // envelope does not assert it belongs to whoever opens the mailbox.
            Assert.DoesNotContain("Ánh", HeaderValue(eml, "To"));
            Assert.DoesNotContain("=?", HeaderValue(eml, "To"));

            Assert.Contains(OtpCode, DecodedBody(eml));
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task The_content_and_the_stated_lifetime_come_from_the_database_and_the_settings()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var row = await db.EmailTemplates.AsNoTracking().SingleAsync(t => t.TemplateCode == Code);

            await SendAsync(db);
            var eml = ReadOnlyMessage();

            Assert.Contains(LiteralPrefix(row.SubjectVi), DecodedHeader(eml, "Subject"));

            var body = DecodedBody(eml);
            // 5 minutes — the visit-request setting, NOT the 15-minute password-reset one. The old
            // hard-coded body said "5 phút" in prose; now the number comes from the same place the
            // token's expiry does.
            Assert.Contains("5", body);
            Assert.DoesNotContain("{{", body);
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
            var row = await db.EmailTemplates.SingleAsync(t => t.TemplateCode == Code);
            var originalSubject = row.SubjectVi;

            try
            {
                await SendAsync(db);
                var before = DecodedHeader(ReadOnlyMessage(), "Subject");

                row.SubjectVi = "[PEMS] Mã xác thực tham quan (bản mới)";
                await db.SaveChangesAsync();
                foreach (var f in Messages()) File.Delete(f);

                await SendAsync(db);
                var after = DecodedHeader(ReadOnlyMessage(), "Subject");

                Assert.NotEqual(before, after);
                Assert.Contains("bản mới", after);
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
    public async Task The_history_row_keeps_the_metadata_but_not_the_code()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var template = await db.EmailTemplates.AsNoTracking().SingleAsync(t => t.TemplateCode == Code);

            await SendAsync(db);

            using var verify = NewContext();
            var sent = await verify.SentEmails.AsNoTracking()
                .OrderByDescending(e => e.SentEmailId)
                .FirstAsync(e => e.EmailTemplateId == template.EmailTemplateId);

            Assert.Equal("SENT", sent.Status);
            Assert.Null(sent.DeliveredAt);
            Assert.Null(sent.BodySnapshot);                       // the code is the body
            Assert.DoesNotContain(OtpCode, sent.Subject);
            Assert.Equal("VisitRequestOtp", sent.RelatedType);
        }
        finally { await CleanupAsync(); }
    }

    // ── Nothing about the system reaches the visitor ─────────────────────────

    [Fact]
    public async Task A_delivery_failure_becomes_the_stable_public_code()
    {
        RequireDb();
        try
        {
            using var db = NewContext();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => SendAsync(db, brokenHost: "no-such-host.invalid"));

            Assert.Equal(VisitRequestOtpMail.SendFailedCode, ex.ErrorCode);
            Assert.DoesNotContain(OtpCode, ex.Message);

            // The attempt is still recorded — an operator can see the code could not be delivered.
            using var verify = NewContext();
            var row = await verify.SentEmails.AsNoTracking()
                .OrderByDescending(e => e.SentEmailId)
                .FirstOrDefaultAsync(e => e.RelatedType == "VisitRequestOtp");
            Assert.Equal("FAILED", row!.Status);
            Assert.Null(row.BodySnapshot);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task A_broken_template_is_not_explained_to_the_visitor()
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

                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => SendAsync(db));

                // A configuration fault, but the public caller gets the same answer as a delivery
                // failure — telling a stranger "this template is inactive" describes the system to
                // somebody who has not even proved they own the address.
                Assert.Equal(VisitRequestOtpMail.SendFailedCode, ex.ErrorCode);
                Assert.DoesNotContain("INACTIVE", ex.Message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(Code, ex.Message);
                Assert.Empty(Messages());
            }
            finally
            {
                row.Status = originalStatus;
                await db.SaveChangesAsync();
            }
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task A_subject_edited_to_include_the_code_is_refused_and_still_says_only_send_failed()
    {
        RequireDb();
        try
        {
            using var db = NewContext();
            var row = await db.EmailTemplates.SingleAsync(t => t.TemplateCode == Code);
            var originalSubject = row.SubjectVi;

            try
            {
                row.SubjectVi = "[PEMS] Mã của bạn: {{otpCode}}";
                await db.SaveChangesAsync();

                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => SendAsync(db));

                Assert.Equal(VisitRequestOtpMail.SendFailedCode, ex.ErrorCode);
                Assert.DoesNotContain(OtpCode, ex.Message);
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

    // ── Helpers (same shape as the other e-mail evidence suites) ─────────────

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
            return Encoding.UTF8.GetString(Convert.FromBase64String(
                new string(body.Where(c => !char.IsWhiteSpace(c)).ToArray())));

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
