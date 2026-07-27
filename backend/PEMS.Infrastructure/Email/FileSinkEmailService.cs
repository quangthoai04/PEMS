using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// TESTING-ONLY email sink for real-stack E2E. Instead of sending mail, it appends a redacted JSON record
/// (the full TO/CC/BCC envelope, the template, the subject/body and either the OTP code or the
/// invitation link/token) to a process-shared file inbox, so a Playwright harness can read what was sent
/// without any public endpoint or production backdoor.
///
/// Recording all three recipient groups is what lets an E2E assert the property that matters most here:
/// that a BCC address received the mail while never appearing anywhere a TO/CC recipient could see.
///
/// Registration is DOUBLE-GATED (see Program.cs): only when <c>ASPNETCORE_ENVIRONMENT=Testing</c> AND
/// <c>PEMS_E2E_TEST_SINK_ENABLED=true</c>. It is NEVER registered in Development/Staging/Production. It is
/// FAIL-CLOSED: constructing it without a valid <c>PEMS_E2E_TEST_SINK_PATH</c> throws, so it can never
/// silently swallow mail. The application log never receives the raw OTP/token — only the sink file does,
/// and that file lives under a temp path the test runner owns and deletes.
/// </summary>
public sealed class FileSinkEmailService : IEmailService
{
    public const string EnabledEnvVar = "PEMS_E2E_TEST_SINK_ENABLED";
    public const string PathEnvVar = "PEMS_E2E_TEST_SINK_PATH";

    private static readonly object FileLock = new();
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly string _inboxPath;
    private readonly ILogger<FileSinkEmailService> _logger;

    public FileSinkEmailService(ILogger<FileSinkEmailService> logger)
    {
        _logger = logger;
        var path = Environment.GetEnvironmentVariable(PathEnvVar);
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(
                $"{nameof(FileSinkEmailService)} requires {PathEnvVar}; fail-closed rather than dropping mail silently.");
        _inboxPath = path;
    }

    /// <summary>
    /// True only when ALL THREE hold: the environment is Testing, the explicit gate is set, AND a sink path is
    /// configured. Requiring the path here (not only in the constructor) is fail-closed AND parallel-safe: a
    /// test that flips only the gate on can never make a concurrently-building host register a path-less sink.
    /// </summary>
    public static bool IsEnabledFor(string? environmentName)
        => string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase)
           && string.Equals(Environment.GetEnvironmentVariable(EnabledEnvVar), "true", StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PathEnvVar));

    public Task<EmailDeliveryResult> TrySendAsync(
        OutboundEmail message, CancellationToken cancellationToken = default)
    {
        Append(message);
        return Task.FromResult(EmailDeliveryResult.Sent());
    }

    public Task SendAsync(OutboundEmail message, CancellationToken cancellationToken = default)
    {
        Append(message);
        return Task.CompletedTask;
    }

    private void Append(OutboundEmail message)
    {
        var body = message.Body ?? string.Empty;

        var record = JsonSerializer.Serialize(new
        {
            to = Project(message.To),
            cc = Project(message.Cc),
            bcc = Project(message.Bcc),
            templateCode = message.TemplateCode,
            subject = message.Subject,
            body,
            bodyFormat = message.IsHtml ? "HTML" : "PLAIN_TEXT",
            attachments = message.Attachments.Select(a => new
            {
                fileName = a.FileName,
                contentType = a.ContentType,
                isInline = a.IsInline,
                contentId = a.ContentId,
                sizeBytes = a.Content.Length,
            }).ToArray(),
            headers = message.Headers,

            // Kept for the existing Playwright harness, which reads the OTP / claim link from here.
            // These values live ONLY in this file, never in the application log.
            kind = ClassifyKind(message),
            code = ExtractOtp(body),
            link = ExtractLink(body),

            at = DateTime.UtcNow.ToString("O"),
            status = "SENT",
        }, Json);

        WithRetry(record);
    }

    private static object[] Project(IReadOnlyList<EmailRecipient> recipients)
        => recipients
            .Select(r => (object)new { email = r.Email.Trim().ToLowerInvariant(), displayName = r.DisplayName })
            .ToArray();

    /// <summary>
    /// A coarse label the harness filters on. Derived from the template code where there is one, so it
    /// stays correct as templates are added, instead of a per-method constant that goes stale.
    /// </summary>
    private static string ClassifyKind(OutboundEmail message)
        => message.TemplateCode ?? "GENERIC";

    /// <summary>
    /// Extracts the actionable link for the E2E inbox: a claim/transfer invitation link, or the account
    /// email-confirmation link (<c>/confirm-email?token=…</c>), if present.
    /// </summary>
    private static string? ExtractLink(string body)
    {
        var m = Regex.Match(
            body,
            @"https?://[^\s""'<>]*(?:visit-contact-(?:claim|transfer)/|confirm-email\?token=|public/email-actions/)[^\s""'<>]+");
        return m.Success ? m.Value : null;
    }

    /// <summary>
    /// Recovers the 6-digit OTP from a rendered body. Previously the code was handed in as a method
    /// argument; now that OTP mail is rendered from a template like everything else, the sink reads it
    /// back out of the message the recipient would actually get.
    /// </summary>
    private static string? ExtractOtp(string body)
    {
        var text = Regex.Replace(body, "<[^>]+>", " ");
        var m = Regex.Match(text, @"(?<!\d)(\d{6})(?!\d)");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Cross-process/thread safe append: retry briefly on transient share-violations.</summary>
    private void WithRetry(string record)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                lock (FileLock)
                {
                    File.AppendAllText(_inboxPath, record + Environment.NewLine);
                }
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(20);
            }
        }
    }

    // ── Legacy surface — removed once Giai đoạn 4 finishes migrating callers ──

    [Obsolete("Render from email_templates and use SendAsync(OutboundEmail).")]
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        => SendAsync(Legacy(toEmail, subject, htmlBody), cancellationToken);

    [Obsolete("Render from email_templates and use TrySendAsync(OutboundEmail).")]
    public Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        => TrySendAsync(Legacy(toEmail, subject, htmlBody), cancellationToken);

    // This one still carries its template code even before the content moves into email_templates, so
    // the `kind` the E2E harness filters on stays the same value either side of that migration.


    private static OutboundEmail Legacy(string toEmail, string subject, string htmlBody, string? templateCode = null)
        => new()
        {
            To = new[] { new EmailRecipient(toEmail) },
            Subject = subject,
            Body = htmlBody,
            IsHtml = true,
            TemplateCode = templateCode,
        };
}
