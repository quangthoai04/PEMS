namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Issues one-time email-ownership confirmations for <c>PENDING_EMAIL_CONFIRMATION</c> accounts (P0 #1).
/// The raw token is returned to the caller ONLY so it can be embedded in the emailed confirmation link;
/// the database stores just its hash. Issuing supersedes any previous live token for the account, so a
/// resend or email edit invalidates the old link.
/// </summary>
public interface IAccountEmailConfirmationService
{
    /// <summary>
    /// Supersedes any existing PENDING confirmation for the user, adds a fresh PENDING row (the hash of a
    /// new random token) to the tracked context — NOT saved; the caller persists it inside its own
    /// transaction — and returns the raw token. <paramref name="isResend"/> advances resend_count.
    /// </summary>
    Task<string> IssuePendingAsync(ulong userId, string normalizedTargetEmail, bool isResend, CancellationToken cancellationToken);

    /// <summary>The frontend confirmation-page URL carrying the raw token (built from App:FrontendBaseUrl).</summary>
    string BuildConfirmUrl(string rawToken);

    /// <summary>
    /// How many hours a confirmation link stays valid. Exposed because the email says so in words: the
    /// template renders it as a variable rather than hard-coding "24", so the sentence can never drift
    /// away from the expiry the service actually applies.
    /// </summary>
    int ExpiryHours { get; }

    /// <summary>The Internal Portal sign-in URL (built from App:FrontendBaseUrl).</summary>
    string BuildLoginUrl();
}
