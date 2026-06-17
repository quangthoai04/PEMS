using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// SMTP email sender. When <c>Smtp:Enabled</c> is false (default for dev) it logs
/// the message instead of sending, so the auth flow keeps working without a server.
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var smtp = _configuration.GetSection("Smtp");
        var enabled = bool.TryParse(smtp["Enabled"], out var e) && e;

        if (!enabled)
        {
            _logger.LogInformation("[EmailService disabled] To: {To} | Subject: {Subject}\n{Body}", toEmail, subject, htmlBody);
            return;
        }

        var host = smtp["Host"];
        var port = int.TryParse(smtp["Port"], out var p) ? p : 587;
        var user = smtp["User"];
        var password = smtp["Password"];
        var fromEmail = smtp["FromEmail"] ?? user ?? "no-reply@pems.local";
        var fromName = smtp["FromName"] ?? "PEMS";
        var enableSsl = !bool.TryParse(smtp["EnableSsl"], out var ssl) || ssl;

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(host, port) { EnableSsl = enableSsl };
        if (!string.IsNullOrEmpty(user))
            client.Credentials = new NetworkCredential(user, password);

        await client.SendMailAsync(message, cancellationToken);
        _logger.LogInformation("Sent email to {To} (subject: {Subject}).", toEmail, subject);
    }

    public Task SendPasswordResetAsync(string toEmail, string fullName, string code, CancellationToken cancellationToken = default)
    {
        const string subject = "PEMS — Password reset code";
        var body =
            $"<p>Hello {WebUtility.HtmlEncode(fullName)},</p>" +
            $"<p>Your password reset code is: <strong style=\"font-size:18px\">{WebUtility.HtmlEncode(code)}</strong></p>" +
            "<p>This code expires shortly. If you did not request a password reset, you can ignore this email.</p>" +
            "<p>— PEMS</p>";

        return SendAsync(toEmail, subject, body, cancellationToken);
    }
}
