using System;
using System.Globalization;
using System.Text.Json;

namespace PEMS.Application.ApiIntegrations.Common;

/// <summary>
/// The distinguishable reasons a Resend HTTP call can fail to deliver. Every cause that a retry policy or
/// an operator needs to treat differently has its own value — collapsing several of these into one is
/// exactly the bug this classifier exists to end (see <see cref="ResendDeliveryClassifier"/>).
/// </summary>
public enum ResendFailureCategory
{
    /// <summary>Short-term rate limit (HTTP 429, or a provider error named for it). Retryable.</summary>
    RateLimited,

    /// <summary>The account's daily send quota is exhausted. Not retryable until tomorrow.</summary>
    DailyQuotaExceeded,

    /// <summary>The account's monthly send quota is exhausted. Not retryable this month.</summary>
    MonthlyQuotaExceeded,

    /// <summary>The API key is missing, invalid or revoked. Not retryable without a config fix.</summary>
    AuthFailed,

    /// <summary>The From address/domain is not verified or is otherwise rejected. Not retryable.</summary>
    SenderRejected,

    /// <summary>HTTP 400/422 — the request payload itself is invalid. Not retryable unchanged.</summary>
    RequestInvalid,

    /// <summary>
    /// Resend gave a definite answer that does not fit any of the categories above. Not retryable by
    /// default — nothing proves a retry would behave differently.
    /// </summary>
    ProviderRejected,

    /// <summary>HTTP 5xx from Resend itself. Transient by nature; retryable.</summary>
    TransientServerError,

    /// <summary>
    /// A timeout, socket failure or other transport exception. The request may or may not have reached
    /// Resend, so the outcome is AMBIGUOUS — never proof that nothing was sent. Retryable only when the
    /// caller can guarantee the retry carries the same provider idempotency key and the same payload.
    /// </summary>
    NetworkUnknown,
}

/// <summary>The stable machine codes a classified Resend outcome is persisted and logged under.</summary>
public static class ResendDeliveryCodes
{
    public const string RateLimited = "RESEND_RATE_LIMITED";
    public const string DailyQuotaExceeded = "RESEND_DAILY_QUOTA_EXCEEDED";
    public const string MonthlyQuotaExceeded = "RESEND_MONTHLY_QUOTA_EXCEEDED";
    public const string AuthFailed = "RESEND_AUTH_FAILED";
    public const string SenderRejected = "RESEND_SENDER_REJECTED";
    public const string RequestInvalid = "RESEND_REQUEST_INVALID";
    public const string ProviderRejected = "RESEND_PROVIDER_REJECTED";
    public const string ServerError = "RESEND_SERVER_ERROR";
    public const string NetworkUnknown = "RESEND_NETWORK_UNKNOWN";
}

/// <summary>
/// One classified Resend outcome — carries only what a retry policy and a safe log line need. Never
/// carries the raw provider response body, which may echo request content back.
/// </summary>
public sealed record ResendClassifiedError(
    ResendFailureCategory Category,
    string Code,
    string SafeMessage,
    string? ProviderErrorName,
    TimeSpan? RetryAfter,
    bool IsRetryable,
    bool IsDefinitiveRejection,
    bool IsQuotaExhausted);

/// <summary>
/// Turns a Resend HTTP response (or the exception thrown instead of one) into a
/// <see cref="ResendClassifiedError"/> — the one place this decision is made, shared by the runtime sender
/// (<c>ResendEmailService</c>) and the admin "Test Connection" button
/// (<c>TestApiIntegrationCommandHandler</c>) so the two can never disagree about what a failure means.
///
/// <para>
/// Classification prefers the provider's own error <c>name</c>/<c>type</c> when the body carries one, and
/// falls back to the HTTP status only when it does not — a 400 named <c>rate_limit_exceeded</c> is a rate
/// limit, not a generic validation failure, and hard-coding on status alone would get that wrong.
/// </para>
/// </summary>
public static class ResendDeliveryClassifier
{
    /// <summary>
    /// Classifies a completed HTTP exchange with Resend — a non-2xx status is ALWAYS evidence the provider
    /// was reached and answered, so this never returns <see cref="ResendFailureCategory.NetworkUnknown"/>.
    /// </summary>
    /// <param name="retryAfterHeaderValue">
    /// The raw <c>Retry-After</c> header value, if Resend sent one — delta-seconds or an HTTP-date, per
    /// RFC 9110. Passed as a string (not a parsed <c>HttpResponseHeaders</c>) so this stays testable with
    /// nothing but a status code and a body.
    /// </param>
    public static ResendClassifiedError ClassifyResponse(
        int httpStatusCode, string? responseBody, string? retryAfterHeaderValue)
    {
        var retryAfter = ParseRetryAfter(retryAfterHeaderValue);
        var providerName = TryParseProviderErrorName(responseBody);
        var normalized = providerName?.Trim().ToLowerInvariant();

        if (normalized is { Length: > 0 })
        {
            if (normalized.Contains("rate_limit") || normalized.Contains("too_many"))
                return RateLimited(retryAfter, providerName);

            if (normalized.Contains("quota"))
                return normalized.Contains("month")
                    ? MonthlyQuotaExceeded(providerName)
                    : DailyQuotaExceeded(providerName);

            if (normalized is "invalid_api_key" or "missing_api_key" or "restricted_api_key"
                or "unauthorized" or "authentication_error")
                return AuthFailed(providerName);

            if (normalized.Contains("domain") || normalized.Contains("from_address")
                || normalized.Contains("sender") || normalized is "invalid_to_address")
                return SenderRejected(providerName);

            if (normalized is "validation_error" or "missing_required_field"
                or "invalid_idempotency_key" or "invalid_attachment" or "invalid_parameter"
                || normalized.StartsWith("invalid_", StringComparison.Ordinal))
                return RequestInvalid(providerName);

            if (normalized is "internal_server_error" or "application_error")
                return TransientServerError(retryAfter, providerName);
        }

        // No recognizable provider error name — fall back to the HTTP status, which is still real evidence
        // of what Resend decided, just less specific than a named error.
        return httpStatusCode switch
        {
            429 => RateLimited(retryAfter, providerName),
            401 or 403 => AuthFailed(providerName),
            400 or 422 => RequestInvalid(providerName),
            >= 500 and <= 599 => TransientServerError(retryAfter, providerName),
            _ => ProviderRejected(providerName),
        };
    }

    /// <summary>
    /// Classifies an exception thrown instead of a response — timeout, DNS/socket failure, or a
    /// non-caller-requested cancellation. Always <see cref="ResendFailureCategory.NetworkUnknown"/>: none
    /// of these prove the request never reached Resend.
    /// </summary>
    public static ResendClassifiedError ClassifyException(Exception exception) => NetworkUnknown();

    // ── Category factories ──────────────────────────────────────────────────

    private static ResendClassifiedError RateLimited(TimeSpan? retryAfter, string? providerName) => new(
        ResendFailureCategory.RateLimited, ResendDeliveryCodes.RateLimited,
        "Resend đang giới hạn tốc độ gửi tạm thời. Hệ thống sẽ tự thử lại.",
        providerName, retryAfter, IsRetryable: true, IsDefinitiveRejection: false, IsQuotaExhausted: false);

    private static ResendClassifiedError DailyQuotaExceeded(string? providerName) => new(
        ResendFailureCategory.DailyQuotaExceeded, ResendDeliveryCodes.DailyQuotaExceeded,
        "Đã vượt hạn mức gửi email trong ngày của Resend.",
        providerName, null, IsRetryable: false, IsDefinitiveRejection: true, IsQuotaExhausted: true);

    private static ResendClassifiedError MonthlyQuotaExceeded(string? providerName) => new(
        ResendFailureCategory.MonthlyQuotaExceeded, ResendDeliveryCodes.MonthlyQuotaExceeded,
        "Đã vượt hạn mức gửi email trong tháng của Resend.",
        providerName, null, IsRetryable: false, IsDefinitiveRejection: true, IsQuotaExhausted: true);

    private static ResendClassifiedError AuthFailed(string? providerName) => new(
        ResendFailureCategory.AuthFailed, ResendDeliveryCodes.AuthFailed,
        "Resend từ chối xác thực — API key không hợp lệ hoặc đã bị thu hồi.",
        providerName, null, IsRetryable: false, IsDefinitiveRejection: true, IsQuotaExhausted: false);

    private static ResendClassifiedError SenderRejected(string? providerName) => new(
        ResendFailureCategory.SenderRejected, ResendDeliveryCodes.SenderRejected,
        "Địa chỉ hoặc miền gửi (From) chưa được Resend xác minh, hoặc không hợp lệ.",
        providerName, null, IsRetryable: false, IsDefinitiveRejection: true, IsQuotaExhausted: false);

    private static ResendClassifiedError RequestInvalid(string? providerName) => new(
        ResendFailureCategory.RequestInvalid, ResendDeliveryCodes.RequestInvalid,
        "Resend từ chối yêu cầu do dữ liệu gửi không hợp lệ.",
        providerName, null, IsRetryable: false, IsDefinitiveRejection: true, IsQuotaExhausted: false);

    private static ResendClassifiedError TransientServerError(TimeSpan? retryAfter, string? providerName) => new(
        ResendFailureCategory.TransientServerError, ResendDeliveryCodes.ServerError,
        "Resend đang gặp sự cố tạm thời phía máy chủ. Hệ thống sẽ tự thử lại.",
        providerName, retryAfter, IsRetryable: true, IsDefinitiveRejection: false, IsQuotaExhausted: false);

    private static ResendClassifiedError ProviderRejected(string? providerName) => new(
        ResendFailureCategory.ProviderRejected, ResendDeliveryCodes.ProviderRejected,
        "Resend từ chối yêu cầu gửi email.",
        providerName, null, IsRetryable: false, IsDefinitiveRejection: true, IsQuotaExhausted: false);

    private static ResendClassifiedError NetworkUnknown() => new(
        ResendFailureCategory.NetworkUnknown, ResendDeliveryCodes.NetworkUnknown,
        "Không xác định được Resend đã nhận yêu cầu hay chưa (lỗi kết nối/timeout).",
        null, null, IsRetryable: true, IsDefinitiveRejection: false, IsQuotaExhausted: false);

    // ── Body parsing — name/type only; the message text never leaves this method ──

    /// <summary>
    /// Reads the provider's own error name off the body, trying the common shapes Resend and similar
    /// JSON APIs use (<c>{"name":...}</c>, <c>{"type":...}</c>, or nested under <c>{"error":{...}}</c>).
    /// Returns null for anything that is not a JSON object with a recognizable field — including a
    /// malformed or non-JSON body — so the caller falls back to the HTTP status instead of throwing.
    /// </summary>
    private static string? TryParseProviderErrorName(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var name = GetString(root, "name") ?? GetString(root, "type") ?? GetString(root, "code");
            if (name is not null) return name;

            if (root.TryGetProperty("error", out var errorEl) && errorEl.ValueKind == JsonValueKind.Object)
                return GetString(errorEl, "name") ?? GetString(errorEl, "type") ?? GetString(errorEl, "code");

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // ── Retry-After (RFC 9110 §10.2.3): delta-seconds or an HTTP-date ─────────

    public static TimeSpan? ParseRetryAfter(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue)) return null;

        if (int.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            return seconds < 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(seconds);

        if (DateTimeOffset.TryParse(
                headerValue, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when))
        {
            var delta = when - DateTimeOffset.UtcNow;
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }

        return null;
    }
}

/// <summary>
/// Bounded backoff for a Resend retry: honours the provider's own <c>Retry-After</c> when it gave one,
/// otherwise a doubling backoff from 1s capped at 20s. Never returns a delay past
/// <see cref="MaxSingleDelay"/> — a caller asked to wait longer than that should treat the attempt as
/// exhausted rather than block on it (plan: "không sleep vô hạn").
/// </summary>
public static class ResendRetryPolicy
{
    /// <summary>The longest delay this policy will ever ask a caller to wait for one retry.</summary>
    public static readonly TimeSpan MaxSingleDelay = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ExponentialCap = TimeSpan.FromSeconds(20);

    /// <param name="attemptNumber">The 1-based attempt that just failed.</param>
    /// <param name="retryAfter">The provider's own Retry-After, when it gave one.</param>
    public static TimeSpan ComputeDelay(int attemptNumber, TimeSpan? retryAfter)
    {
        if (retryAfter is { } explicitDelay)
            return explicitDelay < TimeSpan.Zero ? TimeSpan.Zero : explicitDelay;

        var exponent = Math.Min(Math.Max(0, attemptNumber - 1), 10);
        var multiplier = Math.Pow(2, exponent);
        var delay = TimeSpan.FromMilliseconds(BaseDelay.TotalMilliseconds * multiplier);
        return delay > ExponentialCap ? ExponentialCap : delay;
    }
}
