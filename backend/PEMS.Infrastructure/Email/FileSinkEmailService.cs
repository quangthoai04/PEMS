using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// TESTING-ONLY email sink for real-stack E2E. Instead of sending mail, it appends a redacted JSON record
/// (recipient + kind + the OTP code or the invitation link/token + a timestamp) to a process-shared file
/// inbox so a Playwright harness can read the OTP / claim / transfer link without any public endpoint or
/// production backdoor.
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

    private void Append(string to, string kind, string? code, string? link, string? subject)
    {
        var record = JsonSerializer.Serialize(new
        {
            to = to.Trim().ToLowerInvariant(),
            kind,
            code,
            link,
            subject,
            at = DateTime.UtcNow.ToString("O"),
        }, Json);

        // Cross-process/thread safe append: retry briefly on transient share-violations.
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

    /// <summary>
    /// Extracts the actionable link from an email body for the E2E inbox: a claim/transfer invitation link,
    /// or the account email-confirmation link (<c>/confirm-email?token=…</c>), if present.
    /// </summary>
    private static string? ExtractLink(string body)
    {
        var m = Regex.Match(body, @"https?://[^\s""'<>]*(?:visit-contact-(?:claim|transfer)/|confirm-email\?token=)[^\s""'<>]+");
        return m.Success ? m.Value : null;
    }

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        Append(toEmail, "GENERIC", code: null, link: ExtractLink(htmlBody), subject);
        return Task.CompletedTask;
    }

    /// <summary>Truthful contract for the file sink: the record is always captured, so the outcome is Sent.</summary>
    public Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        Append(toEmail, "GENERIC", code: null, link: ExtractLink(htmlBody), subject);
        return Task.FromResult(EmailDeliveryResult.Sent());
    }

    public Task SendAsync(OutboundEmail message, CancellationToken cancellationToken = default)
    {
        Append(message.ToEmail, "GENERIC_MIME", code: null, link: ExtractLink(message.Body ?? string.Empty), message.Subject);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string fullName, string code, CancellationToken cancellationToken = default)
    {
        Append(toEmail, "PASSWORD_RESET", code, link: null, subject: null);
        return Task.CompletedTask;
    }

    public Task SendVisitRequestOtpAsync(string toEmail, string fullName, string code, CancellationToken cancellationToken = default)
    {
        Append(toEmail, "VISIT_REQUEST_OTP", code, link: null, subject: null);
        return Task.CompletedTask;
    }

    public Task SendVisitorAccountCreatedOrLinkedEmailAsync(
        string toEmail, string contactFullName, string delegationName, string requestCode, string visitScope,
        string plannedTime, CancellationToken cancellationToken = default)
    {
        Append(toEmail, "VISITOR_ACCOUNT", code: null, link: null, subject: requestCode);
        return Task.CompletedTask;
    }

    public Task SendRegistrantConfirmationAsync(
        string toEmail, string registrantFullName, string contactFullName, string contactEmail,
        string delegationName, string requestCode, CancellationToken cancellationToken = default)
    {
        Append(toEmail, "REGISTRANT_CONFIRMATION", code: null, link: null, subject: requestCode);
        return Task.CompletedTask;
    }
}
