namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Outbound email. When no SMTP server is configured the implementation logs the
/// message instead of sending (so the auth flow never breaks in dev).
/// </summary>
public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);

    /// <summary>Sends a password-reset email containing the one-time code / token.</summary>
    Task SendPasswordResetAsync(string toEmail, string fullName, string code, CancellationToken cancellationToken = default);
}
