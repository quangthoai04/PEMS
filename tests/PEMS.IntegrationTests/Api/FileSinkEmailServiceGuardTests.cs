using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Infrastructure.Email;
using Xunit;

namespace PEMS.IntegrationTests.Api;

/// <summary>
/// Guards for the TESTING-ONLY real-stack E2E email/OTP sink (H-4): it must be double-gated (Testing env +
/// explicit flag), fail-closed without a sink path, capture the OTP/link to the file inbox, and never be
/// enabled outside Testing. Runs serially (mutates process env vars, restored in finally). No DB.
/// </summary>
[Collection("FileSinkEnvSerial")]
public sealed class FileSinkEmailServiceGuardTests
{
    private static (string? enabled, string? path) Snapshot() =>
        (Environment.GetEnvironmentVariable(FileSinkEmailService.EnabledEnvVar),
         Environment.GetEnvironmentVariable(FileSinkEmailService.PathEnvVar));

    private static void Restore((string? enabled, string? path) s)
    {
        Environment.SetEnvironmentVariable(FileSinkEmailService.EnabledEnvVar, s.enabled);
        Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, s.path);
    }

    [Fact]
    public void IsEnabledFor_requires_Testing_env_AND_gate_AND_a_sink_path()
    {
        var saved = Snapshot();
        try
        {
            // All three conditions present → enabled (path required so a gate-only flip never registers a
            // path-less sink into a concurrently-building host).
            Environment.SetEnvironmentVariable(FileSinkEmailService.EnabledEnvVar, "true");
            Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, Path.Combine(Path.GetTempPath(), "sink.jsonl"));
            Assert.True(FileSinkEmailService.IsEnabledFor("Testing"));
            // Any non-Testing environment is off, even fully gated — never in prod/dev/staging.
            Assert.False(FileSinkEmailService.IsEnabledFor("Production"));
            Assert.False(FileSinkEmailService.IsEnabledFor("Development"));
            Assert.False(FileSinkEmailService.IsEnabledFor("Staging"));

            // Gate on but NO path → off (fail-closed; this is the flake-safe case).
            Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, null);
            Assert.False(FileSinkEmailService.IsEnabledFor("Testing"));

            // Path present but gate off → off.
            Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, Path.Combine(Path.GetTempPath(), "sink.jsonl"));
            Environment.SetEnvironmentVariable(FileSinkEmailService.EnabledEnvVar, null);
            Assert.False(FileSinkEmailService.IsEnabledFor("Testing"));

            Environment.SetEnvironmentVariable(FileSinkEmailService.EnabledEnvVar, "false");
            Assert.False(FileSinkEmailService.IsEnabledFor("Testing"));
        }
        finally { Restore(saved); }
    }

    [Fact]
    public void Constructor_is_fail_closed_without_a_sink_path()
    {
        var saved = Snapshot();
        try
        {
            Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, null);
            Assert.Throws<InvalidOperationException>(() => new FileSinkEmailService(NullLogger<FileSinkEmailService>.Instance));
        }
        finally { Restore(saved); }
    }

    [Fact]
    public async Task Captures_the_OTP_code_and_the_invitation_link_to_the_file_inbox()
    {
        var saved = Snapshot();
        var inbox = Path.Combine(Path.GetTempPath(), $"pems_sink_{Guid.NewGuid():N}.jsonl");
        try
        {
            Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, inbox);
            var sink = new FileSinkEmailService(NullLogger<FileSinkEmailService>.Instance);

            await sink.SendVisitRequestOtpAsync("Reg@Example.com", "Reg", "654321");
            await sink.SendAsync("contact@example.com", "Lời mời",
                "<a href=\"https://app.local/visit-contact-claim/RAW-TOKEN-123\">Xác nhận</a>");

            var lines = (await File.ReadAllLinesAsync(inbox)).Where(l => l.Length > 0).ToList();
            Assert.Equal(2, lines.Count);

            var otp = JsonSerializer.Deserialize<JsonElement>(lines[0]);
            Assert.Equal("VISIT_REQUEST_OTP", otp.GetProperty("kind").GetString());
            Assert.Equal("654321", otp.GetProperty("code").GetString());
            Assert.Equal("reg@example.com", otp.GetProperty("to").GetString()); // normalized

            var invite = JsonSerializer.Deserialize<JsonElement>(lines[1]);
            Assert.Equal("GENERIC", invite.GetProperty("kind").GetString());
            Assert.Contains("visit-contact-claim/RAW-TOKEN-123", invite.GetProperty("link").GetString());
        }
        finally
        {
            Restore(saved);
            if (File.Exists(inbox)) File.Delete(inbox);
        }
    }
}
