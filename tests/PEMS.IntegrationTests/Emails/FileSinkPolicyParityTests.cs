using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Infrastructure.Email;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The testing file sink must enforce exactly what the real provider enforces (G11-H).
///
/// <para>
/// It used to write whatever it was handed. That made it a bypass of
/// <see cref="EmailRecipientPolicyEnforcer"/> — but the consequence was worse than a missing check,
/// because the sink is where real-stack evidence comes from. A run could show a one-time-token template
/// going out with a BCC, record it as a pass, and prove nothing at all about production, where the same
/// send is refused. A test double that enforces less than the thing it doubles produces evidence for a
/// system that does not exist.
/// </para>
/// </summary>
public sealed class FileSinkPolicyParityTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pems-sink-parity-" + Guid.NewGuid().ToString("N"));
    private readonly string _inbox;
    private readonly string? _previousPath;

    public FileSinkPolicyParityTests()
    {
        Directory.CreateDirectory(_dir);
        _inbox = Path.Combine(_dir, "inbox.jsonl");
        _previousPath = Environment.GetEnvironmentVariable(FileSinkEmailService.PathEnvVar);
        Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, _inbox);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(FileSinkEmailService.PathEnvVar, _previousPath);
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    private FileSinkEmailService Sink() => new(NullLogger<FileSinkEmailService>.Instance);

    private static EmailRecipient R(string email) => new(email, null);

    private static OutboundEmail Message(
        string? templateCode,
        IReadOnlyList<EmailRecipient> to,
        IReadOnlyList<EmailRecipient>? cc = null,
        IReadOnlyList<EmailRecipient>? bcc = null,
        string subject = "Tiêu đề")
        => new()
        {
            TemplateCode = templateCode,
            To = to,
            Cc = cc ?? Array.Empty<EmailRecipient>(),
            Bcc = bcc ?? Array.Empty<EmailRecipient>(),
            Subject = subject,
            Body = "<p>Nội dung</p>",
            IsHtml = true,
        };

    private int LineCount() => File.Exists(_inbox) ? File.ReadAllLines(_inbox).Length : 0;

    // ── The policy applies ───────────────────────────────────────────────────

    [Fact]
    public async Task A_secret_bearing_template_with_a_cc_is_refused_and_nothing_is_written()
    {
        var sink = Sink();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sink.SendAsync(Message(
                SystemEmailTemplates.AuthPasswordResetOtp,
                new[] { R("a@fpt.edu.vn") },
                cc: new[] { R("b@fpt.edu.vn") })));

        Assert.Equal(0, LineCount());
    }

    [Fact]
    public async Task A_secret_bearing_template_with_a_bcc_is_refused()
    {
        var sink = Sink();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sink.SendAsync(Message(
                SystemEmailTemplates.VisitParticipantInvitation,
                new[] { R("a@fpt.edu.vn") },
                bcc: new[] { R("hidden@fpt.edu.vn") })));

        Assert.Equal(EmailErrorCodes.RecipientTypeNotAllowed, ex.ErrorCode);
        Assert.Equal(0, LineCount());
    }

    /// <summary>
    /// One message per person, not one message to several. Two people on one TO of a token-bearing
    /// template each receive the other's link.
    /// </summary>
    [Fact]
    public async Task A_single_recipient_template_with_two_addressees_is_refused()
    {
        var sink = Sink();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sink.SendAsync(Message(
                SystemEmailTemplates.VisitRequestOtp,
                new[] { R("a@fpt.edu.vn"), R("b@fpt.edu.vn") })));

        Assert.Equal(0, LineCount());
    }

    [Fact]
    public async Task A_report_template_may_carry_copies()
    {
        var sink = Sink();

        await sink.SendAsync(Message(
            SystemEmailTemplates.ReportCampusOperation,
            new[] { R("to@fpt.edu.vn") },
            cc: new[] { R("cc@fpt.edu.vn") },
            bcc: new[] { R("bcc@fpt.edu.vn") }));

        Assert.Equal(1, LineCount());
    }

    // ── Envelope shape ───────────────────────────────────────────────────────

    [Fact]
    public async Task An_envelope_with_no_recipient_is_refused()
    {
        var sink = Sink();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            sink.SendAsync(Message(null, Array.Empty<EmailRecipient>())));

        Assert.Equal(EmailErrorCodes.RecipientRequired, ex.ErrorCode);
        Assert.Equal(0, LineCount());
    }

    [Fact]
    public async Task The_same_address_in_two_groups_is_refused()
    {
        var sink = Sink();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            sink.SendAsync(Message(
                null,
                new[] { R("same@fpt.edu.vn") },
                bcc: new[] { R("SAME@fpt.edu.vn") })));

        Assert.Equal(EmailErrorCodes.RecipientCrossGroupDuplicate, ex.ErrorCode);
        Assert.Equal(0, LineCount());
    }

    /// <summary>A CR/LF in a subject would let a caller append headers of its own.</summary>
    [Fact]
    public async Task A_header_break_in_the_subject_is_refused()
    {
        var sink = Sink();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            sink.SendAsync(Message(
                null, new[] { R("a@fpt.edu.vn") }, subject: "Xin chào\r\nBcc: leak@evil.example")));

        Assert.Equal(EmailErrorCodes.HeaderInvalid, ex.ErrorCode);
        Assert.Equal(0, LineCount());
    }

    // ── What gets recorded ───────────────────────────────────────────────────

    /// <summary>
    /// Evidence must show what a provider would have been handed — the normalised envelope — not the
    /// raw request. A recorded address with the caller's stray whitespace still on it would make an
    /// assertion about "who received this" depend on how the caller happened to type it.
    /// </summary>
    [Fact]
    public async Task The_recorded_envelope_is_the_normalised_one()
    {
        var sink = Sink();

        await sink.SendAsync(Message(
            SystemEmailTemplates.ReportCampusOperation,
            new[] { R("  Spaced@fpt.edu.vn  ") },
            cc: new[] { R("cc@fpt.edu.vn") }));

        var line = File.ReadAllLines(_inbox).Single();

        // Trimmed AND lower-cased: the validator normalises the address, so evidence records the one
        // form a comparison can rely on rather than whatever casing the caller happened to use.
        Assert.Contains("spaced@fpt.edu.vn", line);
        Assert.DoesNotContain("  Spaced", line);
        Assert.DoesNotContain("Spaced@fpt.edu.vn", line);
    }

    /// <summary>
    /// The same address twice in one group is REFUSED, not silently collapsed — which is the stricter
    /// of the two reasonable behaviours, and the one the real provider path applies. The sink now
    /// agrees with it; before, it would have written the message and the duplicate with it.
    /// </summary>
    [Fact]
    public async Task A_duplicate_within_one_group_is_refused()
    {
        var sink = Sink();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            sink.SendAsync(Message(
                SystemEmailTemplates.ReportCampusOperation,
                new[] { R("to@fpt.edu.vn") },
                cc: new[] { R("cc@fpt.edu.vn"), R("CC@fpt.edu.vn") })));

        Assert.Equal(EmailErrorCodes.RecipientDuplicate, ex.ErrorCode);
        Assert.Equal(0, LineCount());
    }

    [Fact]
    public async Task TrySendAsync_applies_the_same_gate_as_SendAsync()
    {
        var sink = Sink();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sink.TrySendAsync(Message(
                SystemEmailTemplates.AuthPasswordResetOtp,
                new[] { R("a@fpt.edu.vn") },
                cc: new[] { R("b@fpt.edu.vn") })));

        Assert.Equal(0, LineCount());
    }
}
