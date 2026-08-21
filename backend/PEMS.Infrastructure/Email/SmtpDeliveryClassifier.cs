using System;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Authentication;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// The distinguishable reasons an SMTP send can fail, mirroring the granularity
/// <see cref="PEMS.Application.ApiIntegrations.Common.ResendDeliveryClassifier"/> already gives Resend —
/// collapsing every one of these into <see cref="EmailDeliveryCodes.SmtpSendFailed"/> is exactly the gap
/// this classifier exists to close (email fidelity plan, Phase D).
/// </summary>
public enum SmtpFailureCategory
{
    /// <summary>Explicit authentication-specific evidence. Not retryable without a config fix.</summary>
    AuthFailed,

    /// <summary>The server named a specific rejected recipient. Ambiguous at the WHOLE-MESSAGE level:
    /// PEMS sends one envelope covering TO+CC+BCC, and other recipients in that same transaction may
    /// already have been accepted before this one was rejected.</summary>
    RecipientRejected,

    /// <summary>Explicit provider throttling/rate-limit evidence.</summary>
    RateLimited,

    /// <summary>Explicit sending-quota/daily-limit evidence.</summary>
    QuotaExceeded,

    /// <summary>A generic temporary/4xx-shaped rejection with no explicit rate or quota evidence.</summary>
    TemporaryRejected,

    /// <summary>TLS/STARTTLS negotiation failed.</summary>
    TlsFailed,

    /// <summary>The socket/connection itself failed.</summary>
    ConnectionFailed,

    /// <summary>A genuine (non-caller-cancelled) timeout.</summary>
    Timeout,

    /// <summary>A well-formed SMTP rejection fitting no more specific category. Definitive.</summary>
    ProviderRejected,

    /// <summary>An unrecognized exception shape. The request may or may not have reached the server, so
    /// this is never proof of anything.</summary>
    NetworkUnknown,
}

/// <summary>
/// One classified SMTP outcome — carries only what a log line and a stored row need. Never carries the
/// raw provider response text or exception message verbatim (see <see cref="SafeMessage"/>'s contract):
/// SMTP rejection text routinely echoes the rejected recipient's own address
/// (<see cref="SmtpFailedRecipientException.FailedRecipient"/>, or literal text like
/// "550 mailbox unavailable: user@example.com"), which is exactly the kind of detail that must not reach
/// a log or a stored row untouched.
/// </summary>
public sealed record SmtpClassifiedError(
    SmtpFailureCategory Category,
    string Code,
    string SafeMessage,
    bool IsRetryable,
    bool IsDefinitiveFailure,
    bool IsAmbiguous);

/// <summary>
/// Turns an exception thrown by <see cref="System.Net.Mail.SmtpClient"/> into a
/// <see cref="SmtpClassifiedError"/> — the one place this decision is made, so <c>EmailService</c> and
/// any future SMTP test-connection diagnostic can never disagree about what a failure means.
///
/// <para>
/// <b>Caller cancellation is the caller's problem, not this classifier's.</b> This method receives only
/// the exception, so it cannot tell "the caller's own cancellation token fired" apart from a genuine
/// server-side timeout — <c>EmailService.SendCoreAsync</c> checks
/// <c>OperationCanceledException</c> against its own <c>CancellationToken</c> and rethrows BEFORE ever
/// calling this classifier, so a caller-requested cancellation is never misreported as
/// <see cref="EmailDeliveryCodes.SmtpTimeout"/>.
/// </para>
/// <para>
/// <b>Evidence-gated, not status-code-guessed.</b> <see cref="SmtpStatusCode.ClientNotPermitted"/> (454)
/// is a real .NET enum value but is NOT proof of an authentication failure — real servers use it for
/// assorted temporary policy rejections, and treating every 454 as "wrong credentials" would misreport a
/// generic throttle as a configuration problem. <see cref="SmtpFailureCategory.AuthFailed"/> is produced
/// only when the exception (or an inner exception) carries typed authentication evidence, or the message
/// text explicitly names authentication/credentials — never from a bare status code alone.
/// </para>
/// <para>
/// <b>No retry is decided here.</b> Unlike the Resend HTTP API, plain SMTP gives PEMS no provider-level
/// idempotency key — <see cref="SmtpClassifiedError.IsRetryable"/> is metadata only, and nothing in
/// <c>EmailService</c> reads it to loop. See the plan's "No unsafe SMTP retry" section.
/// </para>
/// </summary>
public static class SmtpDeliveryClassifier
{
    public static SmtpClassifiedError Classify(Exception exception)
    {
        switch (exception)
        {
            // SmtpFailedRecipientsException extends SmtpFailedRecipientException, so this arm must be
            // checked first or the pattern below would catch it instead and lose the multi-recipient
            // shape (not that the classification differs — both are RecipientRejected — but matching the
            // more specific type first is the correct C# pattern regardless).
            case SmtpFailedRecipientsException:
            case SmtpFailedRecipientException:
                return RecipientRejected();

            case SmtpException smtpEx:
                return ClassifySmtpException(smtpEx);

            case SocketException:
                return ConnectionFailed();

            case TimeoutException:
                return Timeout();

            case AuthenticationException:
                return TlsFailed();

            default:
                return NetworkUnknown();
        }
    }

    private static SmtpClassifiedError ClassifySmtpException(SmtpException ex)
    {
        // TLS: the server told us to negotiate TLS first, or the underlying handshake itself failed
        // (SmtpClient wraps SslStream failures as an inner AuthenticationException/IOException — measured
        // shape, not assumed; see the class doc's caller-cancellation note for the same discipline).
        if (ex.StatusCode == SmtpStatusCode.MustIssueStartTlsFirst || ex.InnerException is AuthenticationException)
            return TlsFailed();

        if (ex.InnerException is SocketException)
            return ConnectionFailed();

        if (HasAuthEvidence(ex.Message))
            return AuthFailed();

        if (HasQuotaEvidence(ex.Message))
            return QuotaExceeded();

        if (HasRateEvidence(ex.Message))
            return RateLimited();

        // 4xx-shaped temporary conditions with no explicit rate/quota evidence — including
        // ClientNotPermitted (454), which real servers use for assorted temporary policy rejections and
        // is NOT proof of an auth failure (see class doc).
        if (ex.StatusCode is SmtpStatusCode.MailboxBusy or SmtpStatusCode.LocalErrorInProcessing
            or SmtpStatusCode.InsufficientStorage or SmtpStatusCode.ClientNotPermitted
            or SmtpStatusCode.ServiceNotAvailable)
            return TemporaryRejected();

        // A well-formed SmtpException with a real status code the server actually returned (not
        // GeneralFailure, which means .NET could not determine one) is a definitive, if generic,
        // rejection.
        if (ex.StatusCode != SmtpStatusCode.GeneralFailure)
            return ProviderRejected();

        // GeneralFailure with no typed or textual evidence at all — the client-side exception that
        // carries the least information a real SmtpException can carry.
        return NetworkUnknown();
    }

    private static bool HasAuthEvidence(string? message)
        => message is not null
           && (message.Contains("authenticat", StringComparison.OrdinalIgnoreCase)
               || message.Contains("credential", StringComparison.OrdinalIgnoreCase));

    private static bool HasQuotaEvidence(string? message)
        => message is not null
           && (message.Contains("quota", StringComparison.OrdinalIgnoreCase)
               || message.Contains("daily sending limit", StringComparison.OrdinalIgnoreCase)
               || message.Contains("daily user sending limit", StringComparison.OrdinalIgnoreCase));

    private static bool HasRateEvidence(string? message)
        => message is not null
           && (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
               || message.Contains("too many", StringComparison.OrdinalIgnoreCase)
               || message.Contains("throttl", StringComparison.OrdinalIgnoreCase));

    // ── Category factories ──────────────────────────────────────────────────

    private static SmtpClassifiedError AuthFailed() => new(
        SmtpFailureCategory.AuthFailed, EmailDeliveryCodes.SmtpAuthFailed,
        "Máy chủ SMTP từ chối xác thực.",
        IsRetryable: false, IsDefinitiveFailure: true, IsAmbiguous: false);

    private static SmtpClassifiedError RecipientRejected() => new(
        SmtpFailureCategory.RecipientRejected, EmailDeliveryCodes.SmtpRecipientRejected,
        "Máy chủ SMTP từ chối một người nhận trong email này.",
        // Message-level ambiguous: other recipients in the same TO+CC+BCC envelope may already have
        // been accepted before this one was rejected — see the class/type doc.
        IsRetryable: false, IsDefinitiveFailure: false, IsAmbiguous: true);

    private static SmtpClassifiedError RateLimited() => new(
        SmtpFailureCategory.RateLimited, EmailDeliveryCodes.SmtpRateLimited,
        "Máy chủ SMTP đang giới hạn tốc độ gửi tạm thời.",
        IsRetryable: false, IsDefinitiveFailure: false, IsAmbiguous: true);

    private static SmtpClassifiedError QuotaExceeded() => new(
        SmtpFailureCategory.QuotaExceeded, EmailDeliveryCodes.SmtpQuotaExceeded,
        "Đã vượt hạn mức gửi email của máy chủ SMTP.",
        IsRetryable: false, IsDefinitiveFailure: false, IsAmbiguous: true);

    private static SmtpClassifiedError TemporaryRejected() => new(
        SmtpFailureCategory.TemporaryRejected, EmailDeliveryCodes.SmtpTemporaryRejected,
        "Máy chủ SMTP từ chối tạm thời — có thể do hộp thư bận hoặc điều kiện tạm thời khác.",
        IsRetryable: false, IsDefinitiveFailure: false, IsAmbiguous: true);

    private static SmtpClassifiedError TlsFailed() => new(
        SmtpFailureCategory.TlsFailed, EmailDeliveryCodes.SmtpTlsFailed,
        "Thiết lập kết nối bảo mật (TLS) với máy chủ SMTP thất bại.",
        IsRetryable: false, IsDefinitiveFailure: true, IsAmbiguous: false);

    private static SmtpClassifiedError ConnectionFailed() => new(
        SmtpFailureCategory.ConnectionFailed, EmailDeliveryCodes.SmtpConnectionFailed,
        "Không thể kết nối tới máy chủ SMTP.",
        IsRetryable: false, IsDefinitiveFailure: false, IsAmbiguous: true);

    private static SmtpClassifiedError Timeout() => new(
        SmtpFailureCategory.Timeout, EmailDeliveryCodes.SmtpTimeout,
        "Kết nối tới máy chủ SMTP hết thời gian chờ.",
        IsRetryable: false, IsDefinitiveFailure: false, IsAmbiguous: true);

    private static SmtpClassifiedError ProviderRejected() => new(
        SmtpFailureCategory.ProviderRejected, EmailDeliveryCodes.SmtpProviderRejected,
        "Máy chủ SMTP từ chối gửi email.",
        IsRetryable: false, IsDefinitiveFailure: true, IsAmbiguous: false);

    private static SmtpClassifiedError NetworkUnknown() => new(
        SmtpFailureCategory.NetworkUnknown, EmailDeliveryCodes.SmtpNetworkUnknown,
        "Không xác định được máy chủ SMTP đã nhận email hay chưa (lỗi kết nối/không rõ nguyên nhân).",
        IsRetryable: false, IsDefinitiveFailure: false, IsAmbiguous: true);
}
