namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Outbound email. When no SMTP server is configured the implementation logs
/// the message instead of sending — so the auth flow never breaks in dev.
/// </summary>
public interface IEmailService
{
    /// <summary>Generic send — caller provides the full HTML body.</summary>
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);

    /// <summary>Sends a password-reset / forgot-password OTP email.</summary>
    Task SendPasswordResetAsync(string toEmail, string fullName, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the 6-digit OTP email for visit-request email verification.
    /// </summary>
    Task SendVisitRequestOtpAsync(
        string toEmail,
        string fullName,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the submission-confirmed email after OTP passes and the request is created.
    /// Includes the request code, pending-approval message, and the provisioned account email.
    /// Sent to the contact person.
    /// </summary>
    Task SendVisitorAccountCreatedOrLinkedEmailAsync(
        string toEmail,
        string contactFullName,
        string delegationName,
        string requestCode,
        string visitScope,
        string plannedTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a short confirmation email to the registrant if they are not the contact person.
    /// </summary>
    Task SendRegistrantConfirmationAsync(
        string toEmail,
        string registrantFullName,
        string contactFullName,
        string contactEmail,
        string delegationName,
        string requestCode,
        CancellationToken cancellationToken = default);
}
