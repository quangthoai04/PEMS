using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
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
        public bool Dispatched { get; private set; }

        public TestEmailService(IConfiguration config, IHostEnvironment env, bool throwOnDispatch)
            : base(config, NullLogger<EmailService>.Instance, env) => _throwOnDispatch = throwOnDispatch;

        protected override Task DispatchAsync(MailMessage message, SmtpConfig config, CancellationToken cancellationToken)
        {
            Dispatched = true;
            if (_throwOnDispatch) throw new InvalidOperationException("simulated provider failure");
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
}
