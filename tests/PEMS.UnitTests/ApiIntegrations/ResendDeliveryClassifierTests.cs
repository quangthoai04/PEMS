using System;
using System.Linq;
using System.Net.Http;
using PEMS.Application.ApiIntegrations.Common;
using Xunit;

namespace PEMS.UnitTests.ApiIntegrations;

/// <summary>
/// How a Resend HTTP failure is classified — pure, no network, no database. This is the shared decision
/// both <c>ResendEmailService</c> (a real send) and <c>TestApiIntegrationCommandHandler</c> (the admin
/// "Test Connection" button) defer to, so the two can never disagree about what a failure means.
///
/// <para>
/// Before this classifier existed every one of the cases below collapsed into one code —
/// <c>RESEND_SEND_FAILED</c> — whether Resend rejected an invalid sender, refused an expired API key, or
/// the socket simply died. An operator reading <c>sent_emails</c> could not tell a configuration problem
/// from a quota problem from "try again in a minute", and a network-ambiguous outcome was indistinguishable
/// from a definitive rejection — which is the property that made an automatic retry unsafe.
/// </para>
/// </summary>
public sealed class ResendDeliveryClassifierTests
{
    // ── Provider error name takes priority over the raw HTTP status ───────────

    [Fact]
    public void A_429_rate_limit_is_retryable()
    {
        var result = ResendDeliveryClassifier.ClassifyResponse(
            429, """{"name":"rate_limit_exceeded","message":"Too many requests"}""", "30");

        Assert.Equal(ResendFailureCategory.RateLimited, result.Category);
        Assert.Equal(ResendDeliveryCodes.RateLimited, result.Code);
        Assert.True(result.IsRetryable);
        Assert.False(result.IsDefinitiveRejection);
        Assert.False(result.IsQuotaExhausted);
        Assert.Equal(TimeSpan.FromSeconds(30), result.RetryAfter);
    }

    [Fact]
    public void A_daily_quota_error_is_not_retryable()
    {
        var result = ResendDeliveryClassifier.ClassifyResponse(
            429, """{"name":"daily_quota_exceeded","message":"Daily quota reached"}""", null);

        Assert.Equal(ResendFailureCategory.DailyQuotaExceeded, result.Category);
        Assert.Equal(ResendDeliveryCodes.DailyQuotaExceeded, result.Code);
        Assert.False(result.IsRetryable);
        Assert.True(result.IsDefinitiveRejection);
        Assert.True(result.IsQuotaExhausted);
    }

    [Fact]
    public void A_monthly_quota_error_is_told_apart_from_a_daily_one()
    {
        var result = ResendDeliveryClassifier.ClassifyResponse(
            429, """{"name":"monthly_quota_exceeded","message":"Monthly quota reached"}""", null);

        Assert.Equal(ResendFailureCategory.MonthlyQuotaExceeded, result.Category);
        Assert.Equal(ResendDeliveryCodes.MonthlyQuotaExceeded, result.Code);
        Assert.False(result.IsRetryable);
        Assert.True(result.IsQuotaExhausted);
    }

    [Fact]
    public void An_invalid_api_key_is_an_auth_failure_and_is_not_retryable()
    {
        var result = ResendDeliveryClassifier.ClassifyResponse(
            401, """{"name":"invalid_api_key","message":"API key is invalid"}""", null);

        Assert.Equal(ResendFailureCategory.AuthFailed, result.Category);
        Assert.Equal(ResendDeliveryCodes.AuthFailed, result.Code);
        Assert.False(result.IsRetryable);
        Assert.True(result.IsDefinitiveRejection);
    }

    [Fact]
    public void An_unverified_sender_domain_is_told_apart_from_auth_failure()
    {
        var result = ResendDeliveryClassifier.ClassifyResponse(
            403, """{"name":"invalid_from_address","message":"Domain is not verified"}""", null);

        Assert.Equal(ResendFailureCategory.SenderRejected, result.Category);
        Assert.Equal(ResendDeliveryCodes.SenderRejected, result.Code);
        Assert.False(result.IsRetryable);
        Assert.NotEqual(ResendDeliveryCodes.AuthFailed, result.Code);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(422)]
    public void A_validation_error_is_request_invalid_and_not_retryable(int status)
    {
        var result = ResendDeliveryClassifier.ClassifyResponse(
            status, """{"name":"validation_error","message":"subject is required"}""", null);

        Assert.Equal(ResendFailureCategory.RequestInvalid, result.Category);
        Assert.Equal(ResendDeliveryCodes.RequestInvalid, result.Code);
        Assert.False(result.IsRetryable);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void A_5xx_is_a_transient_server_error_and_is_retryable(int status)
    {
        var result = ResendDeliveryClassifier.ClassifyResponse(status, """{"name":"internal_server_error"}""", null);

        Assert.Equal(ResendFailureCategory.TransientServerError, result.Category);
        Assert.Equal(ResendDeliveryCodes.ServerError, result.Code);
        Assert.True(result.IsRetryable);
        Assert.False(result.IsDefinitiveRejection);
    }

    // ── No provider name in the body — fall back to the HTTP status ───────────

    [Fact]
    public void A_429_with_no_recognizable_body_still_classifies_as_rate_limited_from_status()
    {
        var result = ResendDeliveryClassifier.ClassifyResponse(429, """{"message":"slow down"}""", "5");

        Assert.Equal(ResendFailureCategory.RateLimited, result.Category);
        Assert.Equal(TimeSpan.FromSeconds(5), result.RetryAfter);
    }

    [Fact]
    public void An_unrecognized_error_payload_falls_back_to_provider_rejected()
    {
        var result = ResendDeliveryClassifier.ClassifyResponse(
            409, """{"name":"some_future_error_name_this_classifier_has_never_seen"}""", null);

        Assert.Equal(ResendFailureCategory.ProviderRejected, result.Category);
        Assert.Equal(ResendDeliveryCodes.ProviderRejected, result.Code);
        Assert.False(result.IsRetryable);
        Assert.True(result.IsDefinitiveRejection);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("""{"unexpected": {"nesting": [1,2,3]}}""")]
    public void A_malformed_or_unrecognized_body_never_throws_and_falls_back_to_status(string? body)
    {
        var result = ResendDeliveryClassifier.ClassifyResponse(500, body, null);

        Assert.Equal(ResendFailureCategory.TransientServerError, result.Category);
    }

    // ── Exceptions: always ambiguous, never proof nothing was sent ────────────

    [Fact]
    public void A_timeout_exception_classifies_as_network_unknown()
    {
        var result = ResendDeliveryClassifier.ClassifyException(new TaskCanceledExceptionStub());

        Assert.Equal(ResendFailureCategory.NetworkUnknown, result.Category);
        Assert.Equal(ResendDeliveryCodes.NetworkUnknown, result.Code);
        Assert.True(result.IsRetryable);
        Assert.False(result.IsDefinitiveRejection);
    }

    [Fact]
    public void A_socket_exception_classifies_as_network_unknown_too()
    {
        var result = ResendDeliveryClassifier.ClassifyException(new HttpRequestException("connection reset"));

        Assert.Equal(ResendFailureCategory.NetworkUnknown, result.Category);
        Assert.True(result.IsRetryable);
    }

    /// <summary>A stand-in so the timeout test does not depend on constructing a real TaskCanceledException.</summary>
    private sealed class TaskCanceledExceptionStub : Exception
    {
    }

    // ── Retry-After parsing (RFC 9110 §10.2.3) ─────────────────────────────────

    [Fact]
    public void Retry_after_parses_delta_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(12), ResendDeliveryClassifier.ParseRetryAfter("12"));
    }

    [Fact]
    public void Retry_after_parses_an_http_date_in_the_future()
    {
        var future = DateTimeOffset.UtcNow.AddSeconds(90).ToString("R");

        var parsed = ResendDeliveryClassifier.ParseRetryAfter(future);

        Assert.NotNull(parsed);
        Assert.InRange(parsed!.Value.TotalSeconds, 80, 100);
    }

    [Fact]
    public void Retry_after_in_the_past_never_produces_a_negative_delay()
    {
        var past = DateTimeOffset.UtcNow.AddSeconds(-90).ToString("R");

        var parsed = ResendDeliveryClassifier.ParseRetryAfter(past);

        Assert.Equal(TimeSpan.Zero, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-value")]
    public void Retry_after_returns_null_for_anything_unparseable(string? value)
    {
        Assert.Null(ResendDeliveryClassifier.ParseRetryAfter(value));
    }

    // ── The property the whole exercise turns on ───────────────────────────────

    [Fact]
    public void Every_distinguishable_cause_has_a_distinguishable_code()
    {
        var codes = new[]
        {
            ResendDeliveryClassifier.ClassifyResponse(429, """{"name":"rate_limit_exceeded"}""", null).Code,
            ResendDeliveryClassifier.ClassifyResponse(429, """{"name":"daily_quota_exceeded"}""", null).Code,
            ResendDeliveryClassifier.ClassifyResponse(429, """{"name":"monthly_quota_exceeded"}""", null).Code,
            ResendDeliveryClassifier.ClassifyResponse(401, """{"name":"invalid_api_key"}""", null).Code,
            ResendDeliveryClassifier.ClassifyResponse(403, """{"name":"invalid_from_address"}""", null).Code,
            ResendDeliveryClassifier.ClassifyResponse(422, """{"name":"validation_error"}""", null).Code,
            ResendDeliveryClassifier.ClassifyResponse(500, """{"name":"internal_server_error"}""", null).Code,
            ResendDeliveryClassifier.ClassifyResponse(409, """{"name":"totally_unknown_thing"}""", null).Code,
            ResendDeliveryClassifier.ClassifyException(new HttpRequestException()).Code,
        };

        Assert.Equal(codes.Length, codes.Distinct().Count());
    }
}
