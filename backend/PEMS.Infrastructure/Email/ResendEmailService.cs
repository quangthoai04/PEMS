using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// Resend HTTPS email transport — dispatches email via Resend API when Email:Provider = Resend.
/// Credentials and options are configured in <c>api_configurations</c> by ADMIN.
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly IApplicationDbContext _db;
    private readonly ISecretProtector _secretProtector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly EmailRecipientOptions _recipientOptions;
    private readonly EmailService _smtpEmailService;

    public ResendEmailService(
        IApplicationDbContext db,
        ISecretProtector secretProtector,
        IHttpClientFactory httpClientFactory,
        ILogger<ResendEmailService> logger,
        IHostEnvironment environment,
        IConfiguration configuration,
        IOptions<EmailRecipientOptions> recipientOptions,
        EmailService smtpEmailService)
    {
        _db = db;
        _secretProtector = secretProtector;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _environment = environment;
        _configuration = configuration;
        _recipientOptions = recipientOptions?.Value ?? new EmailRecipientOptions();
        _smtpEmailService = smtpEmailService;
    }

    public async Task<EmailDeliveryResult> TrySendAsync(
        OutboundEmail email, CancellationToken cancellationToken = default)
    {
        var envelope = EmailRecipientValidator.Validate(email, _recipientOptions.MaxRecipients);
        EmailRecipientPolicyEnforcer.Assert(email.TemplateCode, envelope);
        EmailRecipientValidator.AssertNoHeaderBreak(email.Subject ?? string.Empty, "tiêu đề email");

        var config = await _db.ApiConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ApiCode == ResendEmailConstants.ApiCode && c.DeletedAt == null, cancellationToken);

        if (config == null || config.Status != "ACTIVE" || string.IsNullOrWhiteSpace(config.BearerTokenEncrypted))
        {
            _logger.LogWarning(
                "[ResendEmailService] Resend config disabled or missing in DB. To:{To} Cc:{Cc} Bcc:{Bcc} Subject:{Subject} Env:{Env}",
                MaskAll(envelope.To), MaskAll(envelope.Cc), MaskAll(envelope.Bcc),
                email.Subject, _environment.EnvironmentName);

            // If SMTP is enabled in configuration, fallback to SMTP
            var smtpEnabled = _configuration.GetValue<bool?>("Smtp:Enabled") ?? true;
            if (smtpEnabled)
            {
                _logger.LogInformation("[ResendEmailService] Resend is inactive in DB. Falling back to SMTP service.");
                return await _smtpEmailService.TrySendAsync(email, cancellationToken);
            }

            return EmailDeliveryResult.Failed("RESEND_MISCONFIGURED", "Dịch vụ gửi email Resend chưa được cấu hình hoặc chưa bật.");
        }

        string apiKey;
        try
        {
            apiKey = _secretProtector.Unprotect(config.BearerTokenEncrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ResendEmailService] Failed to decrypt Resend API Key.");
            return EmailDeliveryResult.Failed("RESEND_CREDENTIAL_ERROR", "Không thể giải mã Resend API key.");
        }

        var settings = ResendProviderSettings.Parse(config.SettingsJson);
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? ResendEmailConstants.BaseUrl : config.BaseUrl.TrimEnd('/');

        var payload = BuildResendPayload(email, envelope, settings);

        try
        {
            using var client = _httpClientFactory.CreateClient("ResendEmailService");
            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 30);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseText);
                string? messageId = null;
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    messageId = idProp.GetString();
                }

                _logger.LogInformation(
                    "[ResendEmailService] Email sent successfully. ResendId:{ResendId} To:{To} Cc:{Cc} Bcc:{Bcc} Subject:{Subject}",
                    messageId, MaskAll(envelope.To), MaskAll(envelope.Cc), MaskAll(envelope.Bcc), email.Subject);

                return EmailDeliveryResult.Sent(messageId);
            }
            else
            {
                _logger.LogError(
                    "[ResendEmailService] Resend API error HttpStatusCode:{StatusCode}. To:{To} Subject:{Subject}",
                    (int)response.StatusCode, MaskAll(envelope.To), email.Subject);

                return EmailDeliveryResult.Failed("RESEND_SEND_FAILED", "Resend API từ chối yêu cầu gửi email.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ResendEmailService] Resend send FAILED due to exception. To:{To} Subject:{Subject}",
                MaskAll(envelope.To), email.Subject);

            return EmailDeliveryResult.Failed("RESEND_SEND_FAILED", "Lỗi kết nối tới Resend API.");
        }
    }

    public async Task SendAsync(OutboundEmail email, CancellationToken cancellationToken = default)
        => ThrowIfFailed(await TrySendAsync(email, cancellationToken));

    /// <summary>
    /// The exact JSON body sent to Resend.
    ///
    /// <para>
    /// <c>internal</c> so the transport-parity tests can compare it against the <c>.eml</c> the SMTP
    /// path writes. Two transports that quietly disagree about Reply-To, CC or attachments is the
    /// failure this whole comparison exists to catch, and there is no way to see it from outside
    /// without actually sending mail.
    /// </para>
    /// </summary>
    internal static ResendPayload BuildResendPayload(
        OutboundEmail email, ValidatedEnvelope envelope, ResendProviderSettings settings)
    {
        var fromEmail = !string.IsNullOrWhiteSpace(settings.FromEmail) ? settings.FromEmail : "no-reply@mail.pems-fpt.site";
        var fromName = !string.IsNullOrWhiteSpace(settings.FromName) ? settings.FromName : "PEMS";
        var from = !string.IsNullOrWhiteSpace(fromName) ? $"{fromName} <{fromEmail}>" : fromEmail;

        var payload = new ResendPayload
        {
            From = from,
            To = envelope.To.Select(r => FormatRecipient(r)).ToList(),
            Subject = email.Subject ?? string.Empty,
        };

        if (envelope.Cc.Count > 0)
        {
            payload.Cc = envelope.Cc.Select(r => FormatRecipient(r)).ToList();
        }

        if (envelope.Bcc.Count > 0)
        {
            payload.Bcc = envelope.Bcc.Select(r => FormatRecipient(r)).ToList();
        }

        // Reply-To is taken from ONE source, whole. The two halves used to fall back independently
        // (`email.ReplyTo?.Email ?? settings.ReplyToEmail` beside
        // `email.ReplyTo?.DisplayName ?? settings.ReplyToName`), so a message carrying an address with
        // no display name — which is what the dispatcher produces whenever the sending account has no
        // resolved name — was labelled with the SYSTEM's name: "PEMS <ha.nguyen@fpt.edu.vn>". The
        // recipient then reads a reply addressed to PEMS that in fact goes to a person.
        //
        // SMTP never had the problem: it adds the message's own Reply-To recipient wholesale and
        // consults configuration only when the list came out empty. This now matches it.
        var (replyToEmail, replyToName) =
            email.ReplyTo is { } messageReplyTo && !string.IsNullOrWhiteSpace(messageReplyTo.Email)
                ? (messageReplyTo.Email, messageReplyTo.DisplayName)
                : (settings.ReplyToEmail, settings.ReplyToName);

        if (!string.IsNullOrWhiteSpace(replyToEmail))
        {
            var replyToFormatted = !string.IsNullOrWhiteSpace(replyToName) ? $"{replyToName} <{replyToEmail}>" : replyToEmail;
            payload.ReplyTo = new List<string> { replyToFormatted };
        }

        if (email.IsHtml)
        {
            payload.Html = email.Body ?? string.Empty;
        }
        else
        {
            payload.Text = email.Body ?? string.Empty;
        }

        if (email.Attachments.Count > 0)
        {
            payload.Attachments = email.Attachments.Select(a => new ResendAttachmentPayload
            {
                Filename = a.FileName,
                Content = Convert.ToBase64String(a.Content),
                ContentType = a.ContentType
            }).ToList();
        }

        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(email.MessageId))
        {
            headers["Message-ID"] = email.MessageId!;
        }
        if (email.Headers is { Count: > 0 })
        {
            foreach (var (k, v) in email.Headers)
            {
                if (!string.IsNullOrWhiteSpace(k) && v != null)
                {
                    headers[k] = v;
                }
            }
        }

        if (headers.Count > 0)
        {
            payload.Headers = headers;
        }

        return payload;
    }

    private static string FormatRecipient(EmailRecipient recipient)
        => string.IsNullOrWhiteSpace(recipient.DisplayName)
            ? recipient.Email
            : $"{recipient.DisplayName} <{recipient.Email}>";

    private static void ThrowIfFailed(EmailDeliveryResult result)
    {
        if (result.Status == EmailDeliveryStatus.Failed)
            throw new EmailDeliveryException(
                result.Code ?? "EMAIL_SEND_FAILED", result.SafeMessage ?? "Email delivery failed.");
    }

    [Obsolete("Render from email_templates and use SendAsync(OutboundEmail).")]
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        => SendAsync(LegacySingle(toEmail, subject, htmlBody), cancellationToken);

    [Obsolete("Render from email_templates and use TrySendAsync(OutboundEmail).")]
    public Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        => TrySendAsync(LegacySingle(toEmail, subject, htmlBody), cancellationToken);

    private static OutboundEmail LegacySingle(string toEmail, string subject, string htmlBody)
        => new()
        {
            To = new[] { new EmailRecipient(toEmail) },
            Subject = subject,
            Body = htmlBody,
            IsHtml = true,
        };

    private static string MaskEmail(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "(none)";
        var at = address.LastIndexOf('@');
        return at > 0 ? "***@" + address[(at + 1)..] : "***";
    }

    private static string MaskAll(IReadOnlyList<EmailRecipient> recipients)
        => recipients.Count == 0 ? "(none)" : string.Join(",", recipients.Select(r => MaskEmail(r.Email)));
}

internal sealed class ResendPayload
{
    [JsonPropertyName("from")]
    public string From { get; set; } = null!;

    [JsonPropertyName("to")]
    public List<string> To { get; set; } = new();

    [JsonPropertyName("cc")]
    public List<string>? Cc { get; set; }

    [JsonPropertyName("bcc")]
    public List<string>? Bcc { get; set; }

    [JsonPropertyName("reply_to")]
    public List<string>? ReplyTo { get; set; }

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("html")]
    public string? Html { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("attachments")]
    public List<ResendAttachmentPayload>? Attachments { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }
}

internal sealed class ResendAttachmentPayload
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = null!;

    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }
}
