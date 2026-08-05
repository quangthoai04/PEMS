using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// SMTP email sender — the one place a PEMS message becomes MIME and leaves the process.
///
/// <para>
/// <b>The envelope is honoured exactly.</b> TO goes in the To header, CC in the Cc header, and BCC only
/// into the SMTP envelope — <see cref="MailMessage.Bcc"/> is expanded into RCPT TO commands but is never
/// written into the message, so no TO/CC recipient can see who was blind-copied. One message is sent for
/// the whole envelope; the previous code looped per recipient, which turned every CC into a TO and made
/// a "carbon copy" indistinguishable from a direct one.
/// </para>
/// <para>
/// Delivery status is TRUTHFUL and environment-aware:
/// <list type="bullet">
/// <item><c>Smtp:Enabled=false</c> in a NON-production environment → <see cref="EmailDeliveryStatus.Skipped"/>
/// (nothing sent; metadata-only log). It is never reported as "sent".</item>
/// <item><c>Smtp:Enabled=false</c> or misconfigured in Production → <see cref="EmailDeliveryStatus.Failed"/>
/// (fail-closed: email is a required feature and must not be silently dropped).</item>
/// <item>provider accepted → <see cref="EmailDeliveryStatus.Sent"/>; provider error → Failed.</item>
/// </list>
/// Provider acceptance is NOT delivery: nothing here ever reports <c>DELIVERED</c>, because PEMS has no
/// delivery webhook to learn that from.
/// </para>
/// <para>
/// It never logs the OTP, action token, confirmation URL or the message body; recipients are reduced to
/// their domain in metadata logs.
/// </para>
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IHostEnvironment _environment;
    private readonly EmailRecipientOptions _recipientOptions;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IHostEnvironment environment,
        IOptions<EmailRecipientOptions> recipientOptions)
    {
        _configuration    = configuration;
        _logger           = logger;
        _environment      = environment;
        _recipientOptions = recipientOptions?.Value ?? new EmailRecipientOptions();
    }

    // ── Envelope-aware send ───────────────────────────────────────────────────

    public async Task<EmailDeliveryResult> TrySendAsync(
        OutboundEmail email, CancellationToken cancellationToken = default)
    {
        // Validation and policy live HERE, at the last gate before dispatch, rather than only in the
        // handlers. A handler can be added or edited without remembering the rules; this cannot be
        // bypassed by any caller.
        var envelope = EmailRecipientValidator.Validate(email, _recipientOptions.MaxRecipients);
        EmailRecipientPolicyEnforcer.Assert(email.TemplateCode, envelope);
        EmailRecipientValidator.AssertNoHeaderBreak(email.Subject ?? string.Empty, "tiêu đề email");

        using var message = BuildMessage(email, envelope);
        return await SendCoreAsync(message, envelope, cancellationToken);
    }

    public async Task SendAsync(OutboundEmail email, CancellationToken cancellationToken = default)
        => ThrowIfFailed(await TrySendAsync(email, cancellationToken));

    /// <summary>
    /// Builds the MIME message. Inline images become linked resources on the HTML alternate view so
    /// <c>&lt;img src="cid:…"&gt;</c> resolves in Gmail/Outlook; everything else becomes an attachment.
    /// </summary>
    private static MailMessage BuildMessage(OutboundEmail email, ValidatedEnvelope envelope)
    {
        var message = new MailMessage { Subject = email.Subject ?? string.Empty };

        foreach (var r in envelope.To) message.To.Add(ToMailAddress(r));
        foreach (var r in envelope.Cc) message.CC.Add(ToMailAddress(r));
        foreach (var r in envelope.Bcc) message.Bcc.Add(ToMailAddress(r));

        // The message's OWN reply address, when the pipeline resolved one.
        //
        // This was missing, and its absence was invisible from every side: the dispatcher computed a
        // Reply-To from the contact policy, put it on the outbound message, and this method dropped it —
        // after which the configured <c>Smtp:ReplyToEmail</c> was added below because the list was empty.
        // So an invitation whose policy says "replies go to the Host" went out with replies pointed at
        // the system mailbox, and nothing anywhere reported a mismatch. The Resend transport honoured the
        // field the whole time, so the two transports also disagreed about the same message.
        //
        // Added BEFORE the configuration fallback deliberately: that fallback is conditioned on the list
        // being empty, which is exactly the "nobody had an opinion about this message" case it is for.
        if (email.ReplyTo is { } replyTo) message.ReplyToList.Add(ToMailAddress(replyTo));

        var inline = email.Attachments
            .Where(a => a.IsInline && !string.IsNullOrWhiteSpace(a.ContentId)).ToList();
        var files = email.Attachments
            .Where(a => !a.IsInline || string.IsNullOrWhiteSpace(a.ContentId)).ToList();

        if (email.IsHtml)
        {
            var htmlView = AlternateView.CreateAlternateViewFromString(
                email.Body ?? string.Empty, null, MediaTypeNames.Text.Html);

            foreach (var img in inline)
            {
                var lr = new LinkedResource(
                    new MemoryStream(img.Content), img.ContentType ?? "application/octet-stream")
                {
                    ContentId = img.ContentId,
                    TransferEncoding = TransferEncoding.Base64,
                };
                lr.ContentType.Name = img.FileName;
                htmlView.LinkedResources.Add(lr);
            }

            message.AlternateViews.Add(htmlView);
        }
        else
        {
            message.Body = email.Body;
            message.IsBodyHtml = false;
        }

        foreach (var f in files)
        {
            message.Attachments.Add(new Attachment(
                new MemoryStream(f.Content), f.FileName, f.ContentType ?? "application/octet-stream"));
        }

        // Identity comes from the pipeline, never from the bag. Message-Id is set from the typed field so
        // the delivered message carries the same identifier that was written to sent_emails.
        if (!string.IsNullOrWhiteSpace(email.MessageId))
        {
            EmailRecipientValidator.AssertNoHeaderBreak(email.MessageId!, "Message-Id");
            message.Headers.Add("Message-Id", email.MessageId!);
        }

        if (email.Headers is { Count: > 0 })
        {
            foreach (var (name, value) in email.Headers)
            {
                if (string.IsNullOrWhiteSpace(name) || value is null) continue;
                // A header value carrying a newline would append headers of its own.
                EmailRecipientValidator.AssertNoHeaderBreak(name, "tên header");
                EmailRecipientValidator.AssertNoHeaderBreak(value, $"header '{name}'");
                // …and a caller may not smuggle in a From, a Bcc or a Return-Path this way.
                EmailRecipientValidator.AssertHeaderNameAllowed(name);
                message.Headers.Add(name, value);
            }
        }

        return message;
    }

    private static MailAddress ToMailAddress(EmailRecipient recipient)
        => string.IsNullOrWhiteSpace(recipient.DisplayName)
            ? new MailAddress(recipient.Email)
            // The encoding argument makes .NET RFC 2047-encode a non-ASCII display name (Vietnamese
            // diacritics) instead of emitting raw bytes into the header.
            : new MailAddress(recipient.Email, recipient.DisplayName, System.Text.Encoding.UTF8);

    /// <summary>
    /// Resolves SMTP config and dispatches the message, returning the TRUTHFUL outcome. Disabled/misconfigured
    /// is Skipped in non-production but Failed (fail-closed) in Production; provider errors are Failed.
    /// </summary>
    private async Task<EmailDeliveryResult> SendCoreAsync(
        MailMessage message, ValidatedEnvelope envelope, CancellationToken cancellationToken)
    {
        var smtp    = _configuration.GetSection("Smtp");
        var enabled = bool.TryParse(smtp["Enabled"], out var e) && e;

        var fromEmail = smtp["FromEmail"] ?? smtp["User"] ?? "no-reply@pems.local";
        var fromName  = smtp["FromName"] ?? "PEMS";
        message.From = new MailAddress(fromEmail, fromName, System.Text.Encoding.UTF8);

        if (message.ReplyToList.Count == 0)
        {
            var replyToEmail = smtp["ReplyToEmail"];
            var replyToName  = smtp["ReplyToName"] ?? "PEMS";
            if (!string.IsNullOrWhiteSpace(replyToEmail))
                message.ReplyToList.Add(new MailAddress(replyToEmail, replyToName, System.Text.Encoding.UTF8));
        }

        var host        = smtp["Host"];
        var pickupDir   = smtp["PickupDirectory"];
        var usePickup   = !string.IsNullOrWhiteSpace(pickupDir);
        var inlineCount = message.AlternateViews.Count > 0 ? message.AlternateViews[0].LinkedResources.Count : 0;

        // ── SMTP not usable (disabled OR no host, and no pickup directory) ──────
        if (!enabled || (string.IsNullOrWhiteSpace(host) && !usePickup))
        {
            var reason = !enabled ? EmailDeliveryCodes.SmtpDisabled : EmailDeliveryCodes.SmtpMisconfigured;

            // Metadata ONLY — the body may carry OTP codes, action tokens or confirmation URLs, so it must
            // never reach the logs; recipients are reduced to their domain (no address local-part persisted).
            _logger.LogInformation(
                "[EmailService] {Reason} — email NOT sent. To:{To} Cc:{Cc} Bcc:{Bcc} Subject:{Subject} " +
                "Attachments:{Att} Inline:{Inline} Env:{Env}",
                reason, MaskAll(envelope.To), MaskAll(envelope.Cc), MaskAll(envelope.Bcc),
                message.Subject, message.Attachments.Count, inlineCount, _environment.EnvironmentName);

            if (_environment.IsProduction())
            {
                // Fail-closed: in Production, email is a required feature and must not be silently dropped.
                _logger.LogError(
                    "[EmailService] {Reason} in Production — failing closed (email is required). Subject:{Subject}",
                    reason, message.Subject);
                return EmailDeliveryResult.Failed(reason, "Email service is not configured.");
            }

            // Non-production: an intentional skip, never "sent".
            return EmailDeliveryResult.Skipped(reason, "SMTP is not enabled in this environment.");
        }

        var port      = int.TryParse(smtp["Port"], out var p) ? p : 587;
        var user      = smtp["User"];
        var password  = smtp["Password"];
        var enableSsl = !bool.TryParse(smtp["EnableSsl"], out var ssl) || ssl;
        var config    = new SmtpConfig(host, port, user, password, enableSsl, pickupDir);

        try
        {
            await DispatchAsync(message, config, cancellationToken);
            _logger.LogInformation(
                "Sent email. To:{To} Cc:{Cc} Bcc:{Bcc} Subject:{Subject} Attachments:{Att}",
                MaskAll(envelope.To), MaskAll(envelope.Cc), MaskAll(envelope.Bcc),
                message.Subject, message.Attachments.Count);
            return EmailDeliveryResult.Sent();
        }
        catch (Exception ex)
        {
            // Log the failure WITHOUT the body/recipient local-part/secret. The SmtpClient exception text is
            // operational (host/status), never a user secret, but the safe message returned stays generic.
            _logger.LogError(ex,
                "[EmailService] SMTP send FAILED. To:{To} Subject:{Subject}",
                MaskAll(envelope.To), message.Subject);
            return EmailDeliveryResult.Failed(EmailDeliveryCodes.SmtpSendFailed, "Email delivery failed.");
        }
    }

    /// <summary>
    /// Dispatches the built message. Virtual so tests can simulate provider success/failure
    /// deterministically without any network I/O.
    ///
    /// When <c>Smtp:PickupDirectory</c> is configured, .NET serialises the message to a real
    /// <c>.eml</c> file instead of connecting anywhere. That is how the integration suite inspects
    /// genuine MIME — including proving that BCC addresses appear in no header — rather than trusting a
    /// hand-rolled mock to have modelled the format correctly.
    /// </summary>
    protected virtual async Task DispatchAsync(
        MailMessage message, SmtpConfig config, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(config.PickupDirectory))
        {
            Directory.CreateDirectory(config.PickupDirectory!);
            using var pickupClient = new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = config.PickupDirectory,
            };
            await pickupClient.SendMailAsync(message, cancellationToken);
            return;
        }

        using var client = new SmtpClient(config.Host, config.Port) { EnableSsl = config.EnableSsl };
        client.UseDefaultCredentials = false;
        if (!string.IsNullOrEmpty(config.User))
            client.Credentials = new NetworkCredential(config.User, config.Password);

        await client.SendMailAsync(message, cancellationToken);
    }

    /// <summary>Void send contract: a hard Failed outcome throws so fire-and-forget callers still observe it.</summary>
    private static void ThrowIfFailed(EmailDeliveryResult result)
    {
        if (result.Status == EmailDeliveryStatus.Failed)
            throw new EmailDeliveryException(
                result.Code ?? "EMAIL_SEND_FAILED", result.SafeMessage ?? "Email delivery failed.");
    }

    // ── Legacy surface — removed once Giai đoạn 4 finishes migrating callers ──

    [System.Obsolete("Render from email_templates and use SendAsync(OutboundEmail).")]
    public Task SendAsync(string toEmail, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
        => SendAsync(LegacySingle(toEmail, subject, htmlBody), cancellationToken);

    [System.Obsolete("Render from email_templates and use TrySendAsync(OutboundEmail).")]
    public Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
        => TrySendAsync(LegacySingle(toEmail, subject, htmlBody), cancellationToken);

    private static OutboundEmail LegacySingle(
        string toEmail, string subject, string htmlBody, string? templateCode = null)
        => new()
        {
            To = new[] { new EmailRecipient(toEmail) },
            Subject = subject,
            Body = htmlBody,
            IsHtml = true,
            TemplateCode = templateCode,
        };

    // The hard-coded password-reset email used to live here. Batch 2 moved it into
    // AUTH_PASSWORD_RESET_OTP; the copy is deleted rather than kept "just in case", because a second
    // source of the same message is exactly how the account mails drifted apart before.


    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>HTML-encode a value for safe inclusion in email body.</summary>
    private static string HE(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>
    /// Reduce an address to a non-PII <c>***@domain</c> form for safe metadata logging, so the local-part
    /// (which may identify a person) is never persisted to logs.
    /// </summary>
    private static string MaskEmail(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "(none)";
        var at = address.LastIndexOf('@');
        return at > 0 ? "***@" + address[(at + 1)..] : "***";
    }

    /// <summary>Masked, comma-joined form of a recipient group for metadata logging.</summary>
    private static string MaskAll(IReadOnlyList<EmailRecipient> recipients)
        => recipients.Count == 0 ? "(none)" : string.Join(",", recipients.Select(r => MaskEmail(r.Email)));
}

/// <summary>
/// Resolved SMTP connection settings passed to <see cref="EmailService.DispatchAsync"/>.
/// <paramref name="PickupDirectory"/>, when set, replaces the network connection with a real
/// <c>.eml</c> file drop (used by tests to inspect genuine MIME).
/// </summary>
public readonly record struct SmtpConfig(
    string? Host, int Port, string? User, string? Password, bool EnableSsl, string? PickupDirectory = null);

/// <summary>
/// Thrown by the void send contract when delivery hard-fails (provider error, or fail-closed in
/// Production). Carries only a machine code + a human-safe message — never a secret.
/// </summary>
public sealed class EmailDeliveryException : Exception
{
    public string Code { get; }

    public EmailDeliveryException(string code, string safeMessage) : base(safeMessage) => Code = code;
}
