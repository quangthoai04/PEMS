using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEMS.Infrastructure.Email;
using Xunit;

namespace PEMS.IntegrationTests.Api;

/// <summary>Minimal <see cref="IHostEnvironment"/> for constructing an <see cref="EmailService"/> in tests.</summary>
internal sealed class FakeHostEnvironment : IHostEnvironment
{
    public FakeHostEnvironment(string environmentName) => EnvironmentName = environmentName;
    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; } = "PEMS.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

/// <summary>
/// P0 #3a — when SMTP is disabled, <see cref="EmailService"/> must NOT write any sensitive content to the
/// logs: no OTP codes, no action tokens, no confirmation URLs, no HTML body, and not even the recipient's
/// local-part (only the masked <c>***@domain</c>). Only safe metadata (subject, counts, reason skipped)
/// may be logged. Pure in-memory checks — no SMTP, no DB.
/// </summary>
public sealed class EmailServiceSensitiveLoggingTests
{
    /// <summary>An <see cref="ILogger{T}"/> that records the fully-rendered message of every log entry.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public readonly List<string> Messages = new();
        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private static EmailService DisabledService(out CapturingLogger<EmailService> logger)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Smtp:Enabled"] = "false" })
            .Build();
        logger = new CapturingLogger<EmailService>();
        return new EmailService(config, logger, new FakeHostEnvironment("Development"));
    }

    [Fact]
    public async Task Disabled_smtp_does_not_log_the_otp_code_or_the_html_body()
    {
        var svc = DisabledService(out var logger);

        await svc.SendVisitRequestOtpAsync("victim.person@partner.example.com", "Nguyen Van A", "654321");

        var log = string.Join("\n", logger.Messages);
        Assert.DoesNotContain("654321", log);          // the OTP code itself
        Assert.DoesNotContain("<!DOCTYPE", log);        // HTML body opener
        Assert.DoesNotContain("letter-spacing", log);   // an inline-style marker unique to the body
        Assert.DoesNotContain("victim.person", log);    // recipient local-part (PII) must be masked away
    }

    [Fact]
    public async Task Disabled_smtp_does_not_log_the_confirmation_link_or_token()
    {
        var svc = DisabledService(out var logger);

        await svc.SendAsync("owner@example.com", "Xác nhận email",
            "<a href=\"https://pems-fpt.site/confirm-email?token=RAW-TOKEN-DEADBEEF\">Xác nhận</a>");

        var log = string.Join("\n", logger.Messages);
        Assert.DoesNotContain("RAW-TOKEN-DEADBEEF", log);   // the action token
        Assert.DoesNotContain("confirm-email?token=", log); // the full confirmation URL carrying the token
        Assert.DoesNotContain("<a href", log);              // the body
    }

    [Fact]
    public async Task Disabled_smtp_logs_only_safe_metadata_and_says_why_it_was_not_sent()
    {
        var svc = DisabledService(out var logger);

        await svc.SendVisitRequestOtpAsync("victim.person@partner.example.com", "Nguyen Van A", "654321");

        var log = string.Join("\n", logger.Messages);
        Assert.NotEmpty(logger.Messages);
        Assert.Contains("NOT sent", log);             // the reason it was not sent (truthful, non-secret)
        Assert.Contains("partner.example.com", log);  // recipient DOMAIN is allowed metadata
        Assert.Contains("Xác thực", log);             // the fixed subject/template identifier, not a secret
    }
}
