using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.Infrastructure.Email;
using PEMS.IntegrationTests.Api;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// <see cref="ResendEmailService"/> driven entirely through a stub HTTP handler — no network, no real
/// Resend, no SMTP. Covers what the flat <c>RESEND_SEND_FAILED</c> code used to hide: which failures are
/// safe to retry, that a retry of the SAME logical email always carries the SAME
/// <c>Idempotency-Key</c>/payload, that a caller-requested cancellation stops the loop rather than being
/// swallowed into a delivery result, and that the pre-contact SMTP fallback (missing/inactive config) still
/// works unchanged.
/// </summary>
public sealed class ResendEmailServiceTests
{
    private const string ToAddress = "recipient@fpt.edu.vn";

    // ── Success ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_successful_send_reports_Sent_with_the_provider_message_id()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """{"id":"resend-msg-1"}"""));
        var harness = new Harness(handler);

        var result = await harness.SendAsync();

        Assert.Equal(EmailDeliveryStatus.Sent, result.Status);
        Assert.Equal("resend-msg-1", result.ProviderMessageId);
        Assert.Equal(1, handler.CallCount);
    }

    // ── Rate limit: retryable ───────────────────────────────────────────────

    [Fact]
    public async Task A_429_retries_and_succeeds_on_the_second_attempt()
    {
        var handler = new RecordingHandler(attempt => attempt == 1
            ? RateLimited(retryAfterSeconds: 0)
            : Json(HttpStatusCode.OK, """{"id":"resend-msg-2"}"""));
        var harness = new Harness(handler);

        var result = await harness.SendAsync();

        Assert.Equal(EmailDeliveryStatus.Sent, result.Status);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task A_429_that_never_recovers_is_reported_as_rate_limited_once_MaxRetries_is_exhausted()
    {
        var handler = new RecordingHandler(_ => RateLimited(retryAfterSeconds: 0));
        var harness = new Harness(handler, maxRetries: 2);

        var result = await harness.SendAsync();

        Assert.Equal(EmailDeliveryStatus.Failed, result.Status);
        Assert.Equal(ResendDeliveryCodes.RateLimited, result.Code);
        Assert.Equal(3, handler.CallCount); // 1 initial + 2 retries
    }

    // ── Definitive rejections: never retried ───────────────────────────────

    [Fact]
    public async Task A_daily_quota_error_is_reported_immediately_with_no_retry()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.TooManyRequests,
            """{"name":"daily_quota_exceeded"}"""));
        var harness = new Harness(handler);

        var result = await harness.SendAsync();

        Assert.Equal(EmailDeliveryStatus.Failed, result.Status);
        Assert.Equal(ResendDeliveryCodes.DailyQuotaExceeded, result.Code);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task An_auth_failure_is_reported_immediately_with_no_retry()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Unauthorized,
            """{"name":"invalid_api_key"}"""));
        var harness = new Harness(handler);

        var result = await harness.SendAsync();

        Assert.Equal(EmailDeliveryStatus.Failed, result.Status);
        Assert.Equal(ResendDeliveryCodes.AuthFailed, result.Code);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task A_sender_domain_rejection_is_reported_immediately_with_no_retry()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Forbidden,
            """{"name":"invalid_from_address"}"""));
        var harness = new Harness(handler);

        var result = await harness.SendAsync();

        Assert.Equal(EmailDeliveryStatus.Failed, result.Status);
        Assert.Equal(ResendDeliveryCodes.SenderRejected, result.Code);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task A_422_validation_error_is_reported_immediately_with_no_retry()
    {
        var handler = new RecordingHandler(_ => Json((HttpStatusCode)422, """{"name":"validation_error"}"""));
        var harness = new Harness(handler);

        var result = await harness.SendAsync();

        Assert.Equal(EmailDeliveryStatus.Failed, result.Status);
        Assert.Equal(ResendDeliveryCodes.RequestInvalid, result.Code);
        Assert.Equal(1, handler.CallCount);
    }

    // ── Transient 5xx: retryable ────────────────────────────────────────────

    [Fact]
    public async Task A_transient_5xx_retries_and_succeeds()
    {
        var handler = new RecordingHandler(attempt => attempt == 1
            ? Json(HttpStatusCode.ServiceUnavailable, """{"name":"internal_server_error"}""", retryAfterSeconds: 0)
            : Json(HttpStatusCode.OK, """{"id":"resend-msg-3"}"""));
        var harness = new Harness(handler);

        var result = await harness.SendAsync();

        Assert.Equal(EmailDeliveryStatus.Sent, result.Status);
        Assert.Equal(2, handler.CallCount);
    }

    // ── Network exception: ambiguous, retried only under the idempotency key ──

    [Fact]
    public async Task A_network_timeout_retries_with_the_same_Idempotency_Key_every_attempt()
    {
        var handler = new RecordingHandler(attempt => attempt == 1
            ? throw new HttpRequestException("connection reset")
            : Json(HttpStatusCode.OK, """{"id":"resend-msg-4"}"""));
        var harness = new Harness(handler);

        var result = await harness.SendAsync();

        Assert.Equal(EmailDeliveryStatus.Sent, result.Status);
        Assert.Equal(2, handler.IdempotencyKeys.Count);
        Assert.All(handler.IdempotencyKeys, key => Assert.Equal(handler.IdempotencyKeys[0], key));
        Assert.False(string.IsNullOrWhiteSpace(handler.IdempotencyKeys[0]));
    }

    [Fact]
    public async Task A_network_timeout_never_changes_the_payload_between_retries()
    {
        var handler = new RecordingHandler(attempt => attempt == 1
            ? throw new HttpRequestException("connection reset")
            : Json(HttpStatusCode.OK, """{"id":"resend-msg-5"}"""));
        var harness = new Harness(handler);

        await harness.SendAsync();

        Assert.Equal(2, handler.Bodies.Count);
        Assert.Equal(handler.Bodies[0], handler.Bodies[1]);
    }

    [Fact]
    public async Task Two_different_logical_emails_get_two_different_Idempotency_Keys()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """{"id":"ok"}"""));
        var harness = new Harness(handler);

        await harness.SendAsync(sentEmailId: 501);
        await harness.SendAsync(sentEmailId: 502);

        Assert.Equal(2, handler.IdempotencyKeys.Count);
        Assert.NotEqual(handler.IdempotencyKeys[0], handler.IdempotencyKeys[1]);
    }

    [Fact]
    public async Task With_no_idempotency_key_a_network_timeout_is_never_retried()
    {
        // Simulates the one remaining legacy caller (SaveMinutesCommandHandler's obsolete
        // IEmailService.SendAsync(string,...) overload) that has no persisted SentEmail row to derive a
        // key from — an ambiguous network outcome there must not be retried, because nothing durable
        // proves a retry would be the same message.
        var handler = new RecordingHandler(_ => throw new HttpRequestException("connection reset"));
        var harness = new Harness(handler);

        var result = await harness.SendAsync(withIdempotencyKey: false);

        Assert.Equal(EmailDeliveryStatus.Failed, result.Status);
        Assert.Equal(ResendDeliveryCodes.NetworkUnknown, result.Code);
        Assert.Equal(1, handler.CallCount);
    }

    // ── Cancellation stops the loop — never becomes a delivery result ─────────

    [Fact]
    public async Task A_caller_cancellation_during_backoff_propagates_instead_of_being_swallowed()
    {
        using var cts = new CancellationTokenSource();
        var handler = new RecordingHandler(attempt =>
        {
            if (attempt == 1)
            {
                cts.Cancel();
                return RateLimited(retryAfterSeconds: 5); // would otherwise wait — cancellation must pre-empt it
            }
            throw new InvalidOperationException("must not be called after cancellation");
        });
        var harness = new Harness(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.SendAsync(cancellationToken: cts.Token));

        Assert.Equal(1, handler.CallCount);
    }

    // ── No blind SMTP fallback once Resend has been contacted ─────────────────

    /// <summary>
    /// SMTP fallback only exists BEFORE Resend is contacted (missing/inactive config — see the tests
    /// below). Once the retry loop has made an HTTP call, <c>Status</c> can only be <c>Sent</c> or
    /// <c>Failed</c> with a Resend-classified code — a <c>Skipped</c> status (what <see cref="EmailService"/>
    /// reports when SMTP is disabled) would be the tell that a fallback happened where none is allowed.
    /// </summary>
    [Fact]
    public async Task A_definitive_Resend_rejection_never_falls_back_to_SMTP()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Unauthorized, """{"name":"invalid_api_key"}"""));
        var harness = new Harness(handler);

        var result = await harness.SendAsync();

        Assert.Equal(EmailDeliveryStatus.Failed, result.Status);
        Assert.Equal(ResendDeliveryCodes.AuthFailed, result.Code);
        Assert.True(handler.CallCount > 0);
    }

    [Fact]
    public async Task An_ambiguous_network_outcome_never_falls_back_to_SMTP_either()
    {
        var handler = new RecordingHandler(_ => throw new HttpRequestException("connection reset"));
        var harness = new Harness(handler);

        var result = await harness.SendAsync(withIdempotencyKey: false);

        Assert.Equal(EmailDeliveryStatus.Failed, result.Status);
        Assert.Equal(ResendDeliveryCodes.NetworkUnknown, result.Code);
        Assert.True(handler.CallCount > 0);
    }

    // ── The pre-contact fallback (config missing/inactive) is unchanged ──────

    [Fact]
    public async Task Missing_Resend_config_still_falls_back_to_SMTP_before_any_HTTP_call()
    {
        var handler = new RecordingHandler(
            _ => throw new InvalidOperationException("Resend must never be contacted with no config."));
        var harness = new Harness(handler, seedConfig: false);

        var result = await harness.SendAsync();

        // EmailService (SMTP) with Smtp:Enabled=false in a non-production environment reports Skipped —
        // a Sent/Failed here would mean Resend's HTTP path ran instead.
        Assert.Equal(EmailDeliveryStatus.Skipped, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task An_inactive_Resend_config_also_falls_back_to_SMTP()
    {
        var handler = new RecordingHandler(
            _ => throw new InvalidOperationException("Resend must never be contacted while inactive."));
        var harness = new Harness(handler, configStatus: "INACTIVE");

        var result = await harness.SendAsync();

        Assert.Equal(EmailDeliveryStatus.Skipped, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    private static HttpResponseMessage Json(HttpStatusCode status, string body, int? retryAfterSeconds = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (retryAfterSeconds is { } seconds)
            response.Headers.TryAddWithoutValidation("Retry-After", seconds.ToString());
        return response;
    }

    private static HttpResponseMessage RateLimited(int retryAfterSeconds)
        => Json(HttpStatusCode.TooManyRequests, """{"name":"rate_limit_exceeded"}""", retryAfterSeconds);

    /// <summary>Records every call the transport makes: the Idempotency-Key header and the raw body sent.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpResponseMessage> _respond;
        public List<string?> IdempotencyKeys { get; } = new();
        public List<string> Bodies { get; } = new();
        public int CallCount { get; private set; }

        public RecordingHandler(Func<int, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            IdempotencyKeys.Add(
                request.Headers.TryGetValues(ResendEmailConstants.IdempotencyHeaderName, out var values)
                    ? values.FirstOrDefault()
                    : null);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));

            return _respond(CallCount);
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class PassthroughSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => ciphertext;
    }

    private sealed class Harness
    {
        private readonly ApiIntegrationsTestDbContext _db = ApiIntegrationsTestDbContext.Create();
        private readonly ResendEmailService _service;

        public Harness(
            HttpMessageHandler handler,
            bool seedConfig = true,
            string configStatus = "ACTIVE",
            uint maxRetries = 2)
        {
            if (seedConfig)
            {
                _db.ApiConfigurations.Add(new ApiConfiguration
                {
                    ApiCode = ResendEmailConstants.ApiCode,
                    Name = "Resend - test",
                    ProviderName = ResendEmailConstants.ProviderName,
                    Purpose = ResendEmailConstants.Purpose,
                    BaseUrl = "https://api.resend.test",
                    AuthType = ResendEmailConstants.AuthType,
                    BearerTokenEncrypted = "fake-api-key",
                    Status = configStatus,
                    RetryEnabled = true,
                    MaxRetries = maxRetries,
                    TimeoutSeconds = 30,
                    CreatedAt = DateTime.UtcNow,
                });
                _db.SaveChanges();
            }

            // Smtp:Enabled=true (so ResendEmailService's OWN "should I even try SMTP" check says yes) but
            // no Smtp:Host (so EmailService.SendCoreAsync itself resolves the fallback as unconfigured) —
            // in a non-production environment that combination reports Skipped without touching the
            // network. That is the deterministic signal the "pre-contact fallback" tests below read: a
            // Sent/Failed instead would mean Resend's HTTP path ran when it should never have been
            // reachable at all.
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Smtp:Enabled"] = "true" })
                .Build();
            var env = new FakeHostEnvironment("Development");

            var smtp = new EmailService(
                config, NullLogger<EmailService>.Instance, env, Options.Create(new EmailRecipientOptions()));

            _service = new ResendEmailService(
                _db,
                new PassthroughSecretProtector(),
                new StubHttpClientFactory(handler),
                NullLogger<ResendEmailService>.Instance,
                env,
                config,
                Options.Create(new EmailRecipientOptions()),
                smtp);
        }

        public Task<EmailDeliveryResult> SendAsync(
            ulong sentEmailId = 42, bool withIdempotencyKey = true, CancellationToken cancellationToken = default)
            => _service.TrySendAsync(new OutboundEmail
            {
                To = new[] { new EmailRecipient(ToAddress, "Người nhận") },
                Subject = "Chủ đề kiểm tra",
                Body = "<p>Nội dung kiểm tra.</p>",
                IsHtml = true,
                DeliveryIdempotencyKey = withIdempotencyKey ? $"pems-system-{sentEmailId}" : null,
            }, cancellationToken);
    }
}
