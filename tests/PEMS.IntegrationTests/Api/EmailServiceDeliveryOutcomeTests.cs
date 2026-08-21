using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Interfaces;
using PEMS.Infrastructure.Email;
using Xunit;

namespace PEMS.IntegrationTests.Api;

/// <summary>
/// P0 #3b — <see cref="EmailService"/> reports delivery outcomes TRUTHFULLY and environment-aware:
/// disabled/misconfigured SMTP is Skipped in non-production but Failed (fail-closed) in Production; a
/// provider-accepted send is Sent; a provider error is Failed. The actual SMTP dispatch is replaced by a
/// deterministic seam so no network I/O or real email is involved.
/// </summary>
public sealed class EmailServiceDeliveryOutcomeTests
{
    /// <summary>An <see cref="EmailService"/> whose SMTP dispatch is simulated (succeeds or throws).</summary>
    private sealed class TestEmailService : EmailService
    {
        private readonly bool _throwOnDispatch;
        private readonly Func<Exception>? _exceptionFactory;
        public bool Dispatched { get; private set; }
        public int DispatchCount { get; private set; }

        public TestEmailService(IConfiguration config, IHostEnvironment env, bool throwOnDispatch,
            Func<Exception>? exceptionFactory = null)
            : base(config, NullLogger<EmailService>.Instance, env,
                   Options.Create(new PEMS.Application.Emails.Common.EmailRecipientOptions()))
        {
            _throwOnDispatch = throwOnDispatch;
            _exceptionFactory = exceptionFactory;
        }

        protected override Task DispatchAsync(MailMessage message, SmtpConfig config, CancellationToken cancellationToken)
        {
            Dispatched = true;
            DispatchCount++;
            if (_throwOnDispatch) throw _exceptionFactory?.Invoke() ?? new InvalidOperationException("simulated provider failure");
            return Task.CompletedTask;
        }
    }

    private static IConfiguration Config(bool enabled, bool withHost = true)
    {
        var dict = new Dictionary<string, string?> { ["Smtp:Enabled"] = enabled ? "true" : "false" };
        if (withHost) dict["Smtp:Host"] = "smtp.example.test";
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static TestEmailService Service(string env, bool enabled, bool withHost = true, bool throwOnDispatch = false)
        => new(Config(enabled, withHost), new FakeHostEnvironment(env), throwOnDispatch);

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    [InlineData("Staging")]
    public async Task Disabled_smtp_in_non_production_is_Skipped_and_never_dispatched(string env)
    {
        var svc = Service(env, enabled: false);

        var result = await svc.TrySendAsync("user@example.com", "Subject", "<b>body</b>");

        Assert.Equal(EmailDeliveryStatus.Skipped, result.Status);
        Assert.False(svc.Dispatched);
    }

    [Fact]
    public async Task Disabled_smtp_in_production_is_Failed_fail_closed()
    {
        var svc = Service("Production", enabled: false);

        var result = await svc.TrySendAsync("user@example.com", "Subject", "<b>body</b>");

        Assert.Equal(EmailDeliveryStatus.Failed, result.Status);
        Assert.False(svc.Dispatched);   // fail-closed — never attempted
    }

    [Fact]
    public async Task Enabled_but_misconfigured_in_production_is_Failed_fail_closed()
    {
        var svc = Service("Production", enabled: true, withHost: false);

        var result = await svc.TrySendAsync("user@example.com", "Subject", "<b>body</b>");

        Assert.Equal(EmailDeliveryStatus.Failed, result.Status);
        Assert.False(svc.Dispatched);
    }

    [Fact]
    public async Task Provider_accepted_is_Sent()
    {
        var svc = Service("Development", enabled: true, throwOnDispatch: false);

        var result = await svc.TrySendAsync("user@example.com", "Subject", "<b>body</b>");

        Assert.Equal(EmailDeliveryStatus.Sent, result.Status);
        Assert.True(svc.Dispatched);
    }

    [Fact]
    public async Task Provider_exception_is_Failed()
    {
        var svc = Service("Development", enabled: true, throwOnDispatch: true);

        var result = await svc.TrySendAsync("user@example.com", "Subject", "<b>body</b>");

        Assert.Equal(EmailDeliveryStatus.Failed, result.Status);
        Assert.True(svc.Dispatched);    // attempted, then failed
    }

    [Fact]
    public async Task Non_sent_result_carries_no_secret()
    {
        var svc = Service("Development", enabled: false);

        var result = await svc.TrySendAsync("user@example.com", "OTP 654321",
            "<a href=\"https://x/confirm?token=RAW-TOKEN-DEADBEEF\">go</a> code 654321");

        var meta = $"{result.Code} {result.SafeMessage}";
        Assert.DoesNotContain("654321", meta);
        Assert.DoesNotContain("RAW-TOKEN-DEADBEEF", meta);
        Assert.DoesNotContain("<a href", meta);
    }

    [Fact]
    public async Task Void_send_throws_fail_closed_in_production_but_not_when_skipped_in_dev()
    {
        // Production disabled → the void contract throws so a fire-and-forget caller still observes it.
        var prod = Service("Production", enabled: false);
        await Assert.ThrowsAsync<EmailDeliveryException>(() => prod.SendAsync("user@example.com", "s", "<b>b</b>"));

        // Development disabled → skipped, so the void contract returns normally (no false failure).
        var dev = Service("Development", enabled: false);
        await dev.SendAsync("user@example.com", "s", "<b>b</b>");   // must not throw
    }

    // ── Phase D: SMTP diagnostics — SendCoreAsync classifies, retries never, propagates cancellation ──

    [Fact]
    public async Task A_recipient_rejection_is_classified_with_the_granular_code_not_the_generic_fallback()
    {
        var svc = new TestEmailService(
            Config(enabled: true), new FakeHostEnvironment("Development"), throwOnDispatch: true,
            () => new System.Net.Mail.SmtpFailedRecipientException(
                System.Net.Mail.SmtpStatusCode.MailboxUnavailable, "user@example.com", "550 mailbox unavailable"));

        var result = await svc.TrySendAsync("user@example.com", "Subject", "<b>body</b>");

        Assert.Equal(EmailDeliveryStatus.Failed, result.Status);
        Assert.Equal(EmailDeliveryCodes.SmtpRecipientRejected, result.Code);
        Assert.NotEqual(EmailDeliveryCodes.SmtpSendFailed, result.Code);
    }

    [Fact]
    public async Task An_unrecognized_exception_falls_back_to_network_unknown_not_the_legacy_catch_all()
    {
        var svc = Service("Development", enabled: true, throwOnDispatch: true);

        var result = await svc.TrySendAsync("user@example.com", "Subject", "<b>body</b>");

        // InvalidOperationException carries no typed or textual SMTP evidence at all — the safe ambiguous
        // default, not the pre-granular-classification catch-all every SMTP exception used to share.
        Assert.Equal(EmailDeliveryCodes.SmtpNetworkUnknown, result.Code);
    }

    [Fact]
    public async Task A_failed_send_dispatches_exactly_once_no_automatic_retry()
    {
        var svc = new TestEmailService(
            Config(enabled: true), new FakeHostEnvironment("Development"), throwOnDispatch: true,
            () => new System.Net.Sockets.SocketException());

        await svc.TrySendAsync("user@example.com", "Subject", "<b>body</b>");

        Assert.Equal(1, svc.DispatchCount);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_and_is_never_reported_as_a_timeout()
    {
        var svc = new TestEmailService(
            Config(enabled: true), new FakeHostEnvironment("Development"), throwOnDispatch: true,
            () => new OperationCanceledException());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The caller's own token is already cancelled when DispatchAsync throws — EmailService must
        // rethrow rather than hand back a classified (and therefore misleading) Failed result.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => svc.TrySendAsync("user@example.com", "Subject", "<b>body</b>", cts.Token));
    }
}
