using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Interfaces;
using PEMS.Infrastructure.Email;
using Xunit;

namespace PEMS.UnitTests.Email;

/// <summary>
/// Pins <c>FileSinkEmailService.ExtractLink</c>'s current set of recognized link shapes — the E2E real-
/// stack harness reads this field to retrieve an invitation token, and a link the regex fails to
/// recognize is a link the harness silently cannot act on (measured against the Operational Contact
/// Transfer flow, FLOW 05: the sink recorded the email but `link` came back null because the regex only
/// knew the OLD `visit-contact-transfer/` path, not the CURRENT
/// `operational-contact-confirmation/{token}` path <c>OperationalContactInvitationService</c> actually
/// builds).
/// </summary>
public class FileSinkEmailServiceTests
{
    private static (FileSinkEmailService Service, string Path) NewSink()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pems-sink-test-{Guid.NewGuid():N}.jsonl");
        Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, path);
        return (new FileSinkEmailService(NullLogger<FileSinkEmailService>.Instance), path);
    }

    private static string? LinkFieldOf(string sinkFilePath)
    {
        var line = File.ReadAllText(sinkFilePath).TrimEnd('\n', '\r');
        using var doc = JsonDocument.Parse(line);
        return doc.RootElement.TryGetProperty("link", out var el) && el.ValueKind != JsonValueKind.Null
            ? el.GetString()
            : null;
    }

    [Fact]
    public async Task Extracts_the_current_operational_contact_confirmation_link()
    {
        var (sink, path) = NewSink();
        try
        {
            var token = Guid.NewGuid().ToString("N");
            var body = $"Xin vui long bam vao lien ket sau: https://app.example.com/operational-contact-confirmation/{token} de xac nhan.";
            await sink.SendAsync(new OutboundEmail
            {
                To = new[] { new EmailRecipient("kim@example.com") },
                Subject = "Loi moi tiep nhan vai tro dau moi",
                Body = body,
                IsHtml = false,
                TemplateCode = null,
            });

            var link = LinkFieldOf(path);
            Assert.NotNull(link);
            Assert.Contains($"operational-contact-confirmation/{token}", link);
        }
        finally
        {
            File.Delete(path);
            Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, null);
        }
    }

    [Fact]
    public async Task Still_extracts_the_legacy_visit_contact_transfer_link()
    {
        // A link already captured/inboxed before the URL-building code moved on must still resolve —
        // same reasoning as the two legacy route aliases kept in App.tsx.
        var (sink, path) = NewSink();
        try
        {
            var token = Guid.NewGuid().ToString("N");
            var body = $"https://app.example.com/visit-contact-transfer/{token}";
            await sink.SendAsync(new OutboundEmail
            {
                To = new[] { new EmailRecipient("kim@example.com") },
                Subject = "Legacy link shape",
                Body = body,
                IsHtml = false,
                TemplateCode = null,
            });

            var link = LinkFieldOf(path);
            Assert.NotNull(link);
            Assert.Contains($"visit-contact-transfer/{token}", link);
        }
        finally
        {
            File.Delete(path);
            Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, null);
        }
    }

    [Fact]
    public async Task Still_extracts_the_account_email_confirmation_link()
    {
        var (sink, path) = NewSink();
        try
        {
            var token = Guid.NewGuid().ToString("N");
            var body = $"https://app.example.com/confirm-email?token={token}";
            await sink.SendAsync(new OutboundEmail
            {
                To = new[] { new EmailRecipient("staff@example.com") },
                Subject = "Xac nhan email",
                Body = body,
                IsHtml = false,
                TemplateCode = null,
            });

            var link = LinkFieldOf(path);
            Assert.NotNull(link);
            Assert.Contains($"confirm-email?token={token}", link);
        }
        finally
        {
            File.Delete(path);
            Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, null);
        }
    }
}
