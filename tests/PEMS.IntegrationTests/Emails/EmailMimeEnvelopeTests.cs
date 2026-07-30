using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.IntegrationTests.Api;
using PEMS.Infrastructure.Email;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Proves the TO/CC/BCC envelope on REAL MIME, not on a mock's recollection of it.
///
/// <para>
/// <c>Smtp:PickupDirectory</c> makes .NET serialise the message to an <c>.eml</c> file instead of
/// connecting to a server, so these tests read exactly the bytes an SMTP server would have received.
/// That matters most for BCC: the guarantee is not "we stored it in a separate column" but "the address
/// appears in no header the other recipients can read", and only the produced message can show that.
/// </para>
/// <para>No network, no real mail, no database.</para>
/// </summary>
public sealed class EmailMimeEnvelopeTests : IDisposable
{
    private readonly string _pickupDir =
        Path.Combine(Path.GetTempPath(), "pems-mime-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_pickupDir)) Directory.Delete(_pickupDir, recursive: true); }
        catch (IOException) { /* a temp dir left behind must never fail a test run */ }
    }

    private EmailService Service()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Enabled"] = "true",
                ["Smtp:PickupDirectory"] = _pickupDir,
                ["Smtp:FromEmail"] = "no-reply@pems.test",
                ["Smtp:FromName"] = "PEMS",
            })
            .Build();

        return new EmailService(
            config,
            NullLogger<EmailService>.Instance,
            new FakeHostEnvironment("Development"),
            Options.Create(new EmailRecipientOptions()));
    }

    /// <summary>Reads the single .eml the pickup directory received.</summary>
    private string ReadOnlyMessage()
    {
        var files = Directory.GetFiles(_pickupDir, "*.eml");
        Assert.Single(files);
        return File.ReadAllText(files[0]);
    }

    private int MessageCount() =>
        Directory.Exists(_pickupDir) ? Directory.GetFiles(_pickupDir, "*.eml").Length : 0;

    private static EmailRecipient R(string email, string? name = null) => new(email, name);

    // ── The core envelope guarantees ─────────────────────────────────────────

    [Fact]
    public async Task One_message_carries_every_TO_CC_and_BCC()
    {
        var result = await Service().TrySendAsync(new OutboundEmail
        {
            To = new[] { R("to1@fpt.edu.vn"), R("to2@fpt.edu.vn") },
            Cc = new[] { R("cc1@fpt.edu.vn") },
            Bcc = new[] { R("secret@fpt.edu.vn") },
            Subject = "Envelope test",
            Body = "<p>body</p>",
        });

        Assert.Equal(EmailDeliveryStatus.Sent, result.Status);

        // ONE message for the whole envelope — the previous implementation sent one per recipient, which
        // is what turned every CC into a private TO.
        Assert.Equal(1, MessageCount());
    }

    [Fact]
    public async Task TO_addresses_land_in_the_To_header()
    {
        await Service().SendAsync(new OutboundEmail
        {
            To = new[] { R("to1@fpt.edu.vn"), R("to2@fpt.edu.vn") },
            Subject = "To header",
            Body = "<p>body</p>",
        });

        var eml = ReadOnlyMessage();
        var to = HeaderValue(eml, "To");

        Assert.Contains("to1@fpt.edu.vn", to);
        Assert.Contains("to2@fpt.edu.vn", to);
    }

    [Fact]
    public async Task CC_addresses_land_in_the_Cc_header_where_recipients_can_see_them()
    {
        await Service().SendAsync(new OutboundEmail
        {
            To = new[] { R("to1@fpt.edu.vn") },
            Cc = new[] { R("cc1@fpt.edu.vn"), R("cc2@fpt.edu.vn") },
            Subject = "Cc header",
            Body = "<p>body</p>",
        });

        var eml = ReadOnlyMessage();
        var cc = HeaderValue(eml, "Cc");

        Assert.Contains("cc1@fpt.edu.vn", cc);
        Assert.Contains("cc2@fpt.edu.vn", cc);
    }

    [Fact]
    public async Task BCC_appears_in_no_header_the_other_recipients_can_read()
    {
        await Service().SendAsync(new OutboundEmail
        {
            To = new[] { R("to1@fpt.edu.vn") },
            Cc = new[] { R("cc1@fpt.edu.vn") },
            Bcc = new[] { R("blind1@fpt.edu.vn"), R("blind2@fpt.edu.vn") },
            Subject = "Bcc privacy",
            Body = "<p>body</p>",
        });

        var eml = ReadOnlyMessage();

        // The visible headers must not mention the blind copies at all…
        Assert.DoesNotContain("blind1@fpt.edu.vn", HeaderValue(eml, "To"));
        Assert.DoesNotContain("blind1@fpt.edu.vn", HeaderValue(eml, "Cc"));
        Assert.DoesNotContain("blind2@fpt.edu.vn", HeaderValue(eml, "To"));
        Assert.DoesNotContain("blind2@fpt.edu.vn", HeaderValue(eml, "Cc"));

        // …and the visible recipients are still addressed correctly.
        Assert.Contains("to1@fpt.edu.vn", HeaderValue(eml, "To"));
        Assert.Contains("cc1@fpt.edu.vn", HeaderValue(eml, "Cc"));
    }

    [Fact]
    public async Task A_display_name_with_vietnamese_diacritics_is_encoded_not_emitted_raw()
    {
        await Service().SendAsync(new OutboundEmail
        {
            To = new[] { R("host@fpt.edu.vn", "Nguyễn Văn A") },
            Subject = "Tiêu đề có dấu",
            Body = "<p>body</p>",
        });

        var eml = ReadOnlyMessage();

        // RFC 2047 encoded-word, not raw bytes that a stricter server would reject.
        Assert.Contains("=?utf-8?", eml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("host@fpt.edu.vn", HeaderValue(eml, "To"));
    }

    [Fact]
    public async Task Thread_headers_are_preserved()
    {
        await Service().SendAsync(new OutboundEmail
        {
            To = new[] { R("host@fpt.edu.vn") },
            Subject = "Re: something",
            Body = "<p>body</p>",
            Headers = new Dictionary<string, string>
            {
                ["In-Reply-To"] = "<original@pems.test>",
                ["References"] = "<original@pems.test>",
            },
        });

        var eml = ReadOnlyMessage();
        Assert.Contains("<original@pems.test>", HeaderValue(eml, "In-Reply-To"));
        Assert.Contains("<original@pems.test>", HeaderValue(eml, "References"));
    }

    // ── Validation and policy are enforced at dispatch, not only by callers ──

    [Fact]
    public async Task Refuses_an_envelope_with_no_TO_and_sends_nothing()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Service().TrySendAsync(new OutboundEmail
        {
            Cc = new[] { R("cc1@fpt.edu.vn") },
            Subject = "No TO",
            Body = "<p>body</p>",
        }));

        Assert.Equal(EmailErrorCodes.RecipientRequired, ex.ErrorCode);
        Assert.Equal(0, MessageCount());
    }

    [Fact]
    public async Task Refuses_the_same_address_in_TO_and_BCC_and_sends_nothing()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Service().TrySendAsync(new OutboundEmail
        {
            To = new[] { R("host@fpt.edu.vn") },
            Bcc = new[] { R("HOST@fpt.edu.vn") },
            Subject = "Cross group",
            Body = "<p>body</p>",
        }));

        Assert.Equal(EmailErrorCodes.RecipientCrossGroupDuplicate, ex.ErrorCode);
        Assert.Equal(0, MessageCount());
    }

    [Fact]
    public async Task Refuses_CC_on_a_security_template_and_sends_nothing()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().TrySendAsync(new OutboundEmail
        {
            To = new[] { R("user@fpt.edu.vn") },
            Cc = new[] { R("manager@fpt.edu.vn") },
            Subject = "OTP",
            Body = "<p>123456</p>",
            TemplateCode = SystemEmailTemplates.AuthPasswordResetOtp,
        }));

        Assert.Equal(EmailErrorCodes.RecipientTypeNotAllowed, ex.ErrorCode);
        Assert.Equal(0, MessageCount());
    }

    [Fact]
    public async Task Refuses_BCC_on_a_security_template_and_sends_nothing()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().TrySendAsync(new OutboundEmail
        {
            To = new[] { R("user@fpt.edu.vn") },
            Bcc = new[] { R("audit@fpt.edu.vn") },
            Subject = "OTP",
            Body = "<p>123456</p>",
            TemplateCode = SystemEmailTemplates.VisitRequestOtp,
        }));

        Assert.Equal(EmailErrorCodes.RecipientTypeNotAllowed, ex.ErrorCode);
        Assert.Equal(0, MessageCount());
    }

    [Fact]
    public async Task Refuses_two_recipients_on_an_invitation_that_carries_a_personal_token()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().TrySendAsync(new OutboundEmail
        {
            To = new[] { R("a@fpt.edu.vn"), R("b@fpt.edu.vn") },
            Subject = "Invitation",
            Body = "<p>accept link</p>",
            TemplateCode = SystemEmailTemplates.VisitParticipantInvitation,
        }));

        Assert.Equal(EmailErrorCodes.RecipientTypeNotAllowed, ex.ErrorCode);
        Assert.Equal(0, MessageCount());
    }

    [Fact]
    public async Task Allows_copies_on_a_caller_controlled_report_template()
    {
        var result = await Service().TrySendAsync(new OutboundEmail
        {
            To = new[] { R("leader@fpt.edu.vn") },
            Cc = new[] { R("assistant@fpt.edu.vn") },
            Subject = "Report",
            Body = "<p>report</p>",
            TemplateCode = SystemEmailTemplates.ReportCampusOperation,
        });

        Assert.Equal(EmailDeliveryStatus.Sent, result.Status);
        Assert.Equal(1, MessageCount());
    }

    [Fact]
    public async Task Refuses_a_subject_carrying_a_header_break_and_sends_nothing()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Service().TrySendAsync(new OutboundEmail
        {
            To = new[] { R("host@fpt.edu.vn") },
            Subject = "Hello\r\nBcc: attacker@evil.test",
            Body = "<p>body</p>",
        }));

        Assert.Equal(EmailErrorCodes.HeaderInvalid, ex.ErrorCode);
        Assert.Equal(0, MessageCount());
    }

    // ── The sender identity is the system's, never the caller's ──────────────
    //
    // Report and invoice commands carry no `from`, but "no caller sets it today" is a fact about today's
    // callers. These hold the property at the place that actually builds the message, so a future caller
    // cannot acquire a sender identity by any route.

    [Fact]
    public async Task The_sender_address_always_comes_from_configuration()
    {
        await Service().SendAsync(new OutboundEmail
        {
            // A display name shaped like an address, on the one field a caller does control.
            To = new[] { R("head@fpt.edu.vn", "ban-giam-hieu@fpt.edu.vn") },
            Subject = "Hóa đơn hậu cần",
            Body = "<p>report</p>",
        });

        var eml = ReadOnlyMessage();
        var from = HeaderValue(eml, "From");

        Assert.Contains("no-reply@pems.test", from);
        Assert.DoesNotContain("ban-giam-hieu@fpt.edu.vn", from);

        // Exactly one From header: a second one would let a reader's client choose which to believe.
        var fromHeaders = eml.Split('\n')
            .TakeWhile(l => l.Trim().Length > 0)
            .Count(l => l.StartsWith("From:", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, fromHeaders);
    }

    /// <summary>
    /// Every header that decides who a message is from, or who it reaches, is refused outright when it
    /// arrives through the bag.
    ///
    /// <para>
    /// This used to be a weaker test: it sent the identity headers and then asserted what the framework
    /// happened to do with them — From overwritten by configuration, Sender and Reply-To dropped. That is
    /// a description of .NET's behaviour, not a rule of ours, and it said nothing at all about
    /// <c>Return-Path</c>, which did survive into the file, or about To/Cc/Bcc. The refusal is now the
    /// rule, so none of it depends on the transport being kind.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("From", "hieu-truong@fpt.edu.vn")]
    [InlineData("Sender", "hieu-truong@fpt.edu.vn")]
    [InlineData("Reply-To", "hieu-truong@fpt.edu.vn")]
    [InlineData("Return-Path", "bounces@evil.example.com")]
    [InlineData("To", "someone-else@fpt.edu.vn")]
    [InlineData("Cc", "someone-else@fpt.edu.vn")]
    [InlineData("Bcc", "watcher@evil.example.com")]
    [InlineData("Message-Id", "<forged@evil.example.com>")]
    public async Task A_header_the_pipeline_owns_is_refused_from_the_bag(string header, string value)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Service().SendAsync(new OutboundEmail
        {
            To = new[] { R("head@fpt.edu.vn") },
            Subject = "Hóa đơn hậu cần",
            Body = "<p>report</p>",
            Headers = new Dictionary<string, string> { [header] = value },
        }));

        Assert.Equal(EmailErrorCodes.HeaderInvalid, ex.ErrorCode);

        // Refused while the message was still being built, before the transport was even reached — the
        // pickup directory is created on the way out, so its absence is the strongest form of "nothing
        // was sent". Tolerate either: what matters is that no .eml exists.
        Assert.Empty(Directory.Exists(_pickupDir)
            ? Directory.GetFiles(_pickupDir, "*.eml")
            : Array.Empty<string>());
    }

    [Fact]
    public async Task Header_names_are_matched_regardless_of_case()
    {
        // "bcc:" and "BCC:" name the same header to every mail server; the denylist must agree.
        var ex = await Assert.ThrowsAsync<ValidationException>(() => Service().SendAsync(new OutboundEmail
        {
            To = new[] { R("head@fpt.edu.vn") },
            Subject = "Hóa đơn hậu cần",
            Body = "<p>report</p>",
            Headers = new Dictionary<string, string> { ["bCc"] = "watcher@evil.example.com" },
        }));

        Assert.Equal(EmailErrorCodes.HeaderInvalid, ex.ErrorCode);
    }

    [Fact]
    public async Task The_sender_identity_still_comes_from_configuration()
    {
        // With the bag closed, this is what remains true of every message: one From, and it is ours.
        await Service().SendAsync(new OutboundEmail
        {
            To = new[] { R("head@fpt.edu.vn") },
            Subject = "Hóa đơn hậu cần",
            Body = "<p>report</p>",
        });

        var eml = ReadOnlyMessage();

        Assert.Contains("no-reply@pems.test", HeaderValue(eml, "From"));
        Assert.Empty(HeaderValue(eml, "Sender"));
        Assert.Empty(HeaderValue(eml, "Return-Path"));
    }

    [Fact]
    public async Task The_message_id_comes_from_the_typed_field_and_reaches_the_wire()
    {
        // Message-Id is on the denylist, so it has to travel some other way: the typed field, which is
        // also what gets written to sent_emails.provider_message_id. If this stopped working, replies
        // would thread against an id no delivered message ever carried.
        await Service().SendAsync(new OutboundEmail
        {
            To = new[] { R("head@fpt.edu.vn") },
            Subject = "Hóa đơn hậu cần",
            Body = "<p>report</p>",
            MessageId = "<pems-typed-id@pems.local>",
        });

        Assert.Contains("pems-typed-id@pems.local", HeaderValue(ReadOnlyMessage(), "Message-Id"));
    }

    [Fact]
    public async Task Thread_headers_are_still_allowed_through_the_headers_bag()
    {
        // The denylist must not cost the reply threading the bag exists for.
        await Service().SendAsync(new OutboundEmail
        {
            To = new[] { R("head@fpt.edu.vn") },
            Subject = "Re: Hóa đơn",
            Body = "<p>reply</p>",
            Headers = new Dictionary<string, string> { ["In-Reply-To"] = "<pems-1@pems.test>" },
        });

        Assert.Contains("pems-1@pems.test", HeaderValue(ReadOnlyMessage(), "In-Reply-To"));
    }

    // ── Status truthfulness ──────────────────────────────────────────────────

    [Fact]
    public async Task Provider_acceptance_reports_Sent_and_never_Delivered()
    {
        var result = await Service().TrySendAsync(new OutboundEmail
        {
            To = new[] { R("host@fpt.edu.vn") },
            Subject = "Status",
            Body = "<p>body</p>",
        });

        // There is no delivery webhook in PEMS, so acceptance is the furthest the system can honestly go.
        Assert.Equal(EmailDeliveryStatus.Sent, result.Status);
        Assert.True(result.IsSent);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the value of a header from the .eml, following RFC 5322 folded continuation lines.
    /// Reading only the header block matters: an address that appears in the BODY is not a leak, but one
    /// in a header is.
    /// </summary>
    private static string HeaderValue(string eml, string headerName)
    {
        var lines = eml.Replace("\r\n", "\n").Split('\n');
        var value = new System.Text.StringBuilder();
        var capturing = false;

        foreach (var line in lines)
        {
            if (line.Length == 0) break; // end of the header block

            if (capturing)
            {
                // A folded continuation line starts with whitespace.
                if (line.StartsWith(' ') || line.StartsWith('\t'))
                {
                    value.Append(line.Trim());
                    continue;
                }
                break;
            }

            if (line.StartsWith(headerName + ":", StringComparison.OrdinalIgnoreCase))
            {
                capturing = true;
                value.Append(line[(headerName.Length + 1)..].Trim());
            }
        }

        return value.ToString();
    }
}
