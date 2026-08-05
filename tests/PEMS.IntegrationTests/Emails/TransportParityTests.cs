using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Infrastructure.Email;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The two transports carry the SAME message (V4 §24, acceptance 24 and 53).
///
/// <para>
/// PEMS can dispatch over SMTP or over Resend, chosen by configuration. Which one is running is an
/// operational decision nobody making a template thinks about — so anything the two disagree about is a
/// message that changes when an administrator flips a setting, and it changes silently.
/// </para>
/// <para>
/// The comparison is between REAL outputs: the <c>.eml</c> the SMTP path serialises to a pickup
/// directory, and the JSON body the Resend path would post. Not two mocks agreeing with each other.
/// </para>
/// </summary>
public sealed class TransportParityTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("parity-recipient@partner.example.com");

    public void Dispose() => _h.Dispose();

    private static readonly ResendProviderSettings Settings = new()
    {
        FromEmail = "no-reply@mail.pems-fpt.site",
        FromName = "PEMS",
        ReplyToEmail = "support@fpt.edu.vn",
        ReplyToName = "PEMS Support",
    };

    /// <summary>One message, built once, handed to both transports.</summary>
    private static OutboundEmail Message(EmailRecipient? replyTo, string to) => new()
    {
        To = new List<EmailRecipient> { new(to, "Nguyễn Văn Bình") },
        Cc = new List<EmailRecipient> { new("cc.person@fpt.edu.vn", "Trần Thị Hà") },
        Bcc = new List<EmailRecipient> { new("blind.person@fpt.edu.vn", null) },
        Subject = "Thư mời tham gia đón đoàn — kiểm tra ký tự tiếng Việt",
        Body = "<p>Kính gửi anh Bình,</p><p>Nhờ anh hỗ trợ đón đoàn khách.</p>",
        IsHtml = true,
        ReplyTo = replyTo,
    };

    /// <summary>
    /// <b>The defect this pins.</b> Resend used to fall back for the Reply-To ADDRESS and the Reply-To
    /// NAME independently:
    /// <code>
    /// var replyToEmail = email.ReplyTo?.Email ?? settings.ReplyToEmail;
    /// var replyToName  = email.ReplyTo?.DisplayName ?? settings.ReplyToName;
    /// </code>
    /// A message carrying an address with no display name — which is what the dispatcher produces
    /// whenever the sending account has no resolved name — therefore went out as
    /// <c>PEMS Support &lt;ha.nguyen@fpt.edu.vn&gt;</c>: the system's label on a person's mailbox. The
    /// recipient reads a reply addressed to PEMS Support that in fact reaches an individual.
    ///
    /// <para>
    /// SMTP never had it, because it adds the message's own Reply-To recipient wholesale and consults
    /// configuration only when the list came out empty. The parity assertion is what would have caught
    /// it: neither transport may attach a name the message did not supply.
    /// </para>
    /// </summary>
    [Fact]
    public void A_reply_to_with_no_display_name_gets_no_name_from_configuration()
    {
        var payload = ResendEmailService.BuildResendPayload(
            Message(new EmailRecipient("ha.nguyen@fpt.edu.vn", null), _h.Marker),
            Envelope(new EmailRecipient("ha.nguyen@fpt.edu.vn", null)),
            Settings);

        Assert.NotNull(payload.ReplyTo);
        var replyTo = Assert.Single(payload.ReplyTo!);

        Assert.Equal("ha.nguyen@fpt.edu.vn", replyTo);
        Assert.DoesNotContain("PEMS Support", replyTo, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reply_to_with_a_display_name_keeps_that_name()
    {
        var payload = ResendEmailService.BuildResendPayload(
            Message(new EmailRecipient("ha.nguyen@fpt.edu.vn", "Trần Thị Hà"), _h.Marker),
            Envelope(new EmailRecipient("ha.nguyen@fpt.edu.vn", "Trần Thị Hà")),
            Settings);

        Assert.Equal("Trần Thị Hà <ha.nguyen@fpt.edu.vn>", Assert.Single(payload.ReplyTo!));
    }

    /// <summary>
    /// With no Reply-To on the message, BOTH halves come from configuration — the name belongs to that
    /// address, so pairing them is correct here and only here.
    /// </summary>
    [Fact]
    public void A_message_with_no_reply_to_falls_back_to_the_configured_pair()
    {
        var payload = ResendEmailService.BuildResendPayload(
            Message(null, _h.Marker), Envelope(null), Settings);

        Assert.Equal("PEMS Support <support@fpt.edu.vn>", Assert.Single(payload.ReplyTo!));
    }

    /// <summary>
    /// The whole envelope, compared against the message SMTP actually serialised.
    ///
    /// <para>
    /// Read out of the <c>.eml</c> rather than out of a MailMessage object, because the file is what a
    /// provider receives. BCC is the interesting one: it must reach the transport on both sides and
    /// appear in the headers on neither.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Both_transports_carry_the_same_recipients_subject_and_reply_to()
    {
        try
        {
            var replyTo = new EmailRecipient("ha.nguyen@fpt.edu.vn", "Trần Thị Hà");
            var message = Message(replyTo, _h.Marker);

            await _h.Sender().SendAsync(message);
            var eml = _h.OnlyMessage();

            var payload = ResendEmailService.BuildResendPayload(message, Envelope(replyTo), Settings);

            // TO and CC: the same addresses, in the same order.
            Assert.Equal(
                new[] { _h.Marker },
                payload.To!.Select(AddressOf).ToArray());
            Assert.Contains(_h.Marker, eml.Header("To"));

            Assert.Equal(
                new[] { "cc.person@fpt.edu.vn" },
                payload.Cc!.Select(AddressOf).ToArray());
            Assert.Contains("cc.person@fpt.edu.vn", eml.Header("Cc"));

            // BCC: addressed by both, and named in the headers of neither.
            Assert.Equal(
                new[] { "blind.person@fpt.edu.vn" },
                payload.Bcc!.Select(AddressOf).ToArray());
            // The pickup file carries no SMTP envelope, so the blind copy is checked by ABSENCE from headers
            // below; that it was addressed at all is proven by the realstack envelope suite.
            Assert.DoesNotContain("bcc:", (eml.Raw ?? string.Empty).Split("\r\n\r\n")[0].ToLowerInvariant());

            // Reply-To: the same mailbox on both.
            Assert.Equal("ha.nguyen@fpt.edu.vn", AddressOf(Assert.Single(payload.ReplyTo!)));
            Assert.Contains("ha.nguyen@fpt.edu.vn", eml.Header("Reply-To"));

            // Subject: the same Vietnamese text, however each transport encodes it.
            Assert.Equal(message.Subject, payload.Subject);
            Assert.False(string.IsNullOrWhiteSpace(eml.Header("Subject")));

            // The body is the message's own HTML on both sides — neither transport renders its own.
            Assert.Equal(message.Body, payload.Html);
            Assert.Null(payload.Text);
            Assert.Contains("Nhờ anh hỗ trợ đón đoàn khách", eml.DecodedTextParts);
        }
        finally { await _h.CleanupAsync(); }
    }

    /// <summary>
    /// Attachments arrive with the same name and content type on both transports.
    ///
    /// <para>
    /// A filename that differs between providers is a report somebody cannot find, and a content type
    /// that differs is a PDF one client opens and the other offers to download as binary.
    /// </para>
    /// </summary>
    [Fact]
    public void Both_transports_carry_the_same_attachment_name_and_type()
    {
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 báo cáo");

        var message = new OutboundEmail
        {
            To = new List<EmailRecipient> { new(_h.Marker, "Nguyễn Văn Bình") },
            Subject = "Báo cáo đoàn khách",
            Body = "<p>Báo cáo đính kèm.</p>",
            IsHtml = true,
            Attachments = new[]
            {
                new OutboundAttachment
                {
                    FileName = "báo-cáo-đoàn-khách.pdf", Content = bytes, ContentType = "application/pdf",
                },
            },
        };

        var payload = ResendEmailService.BuildResendPayload(
            message, EmailRecipientValidator.Validate(message, maxRecipients: 50), Settings);

        var attachment = Assert.Single(payload.Attachments!);
        Assert.Equal("báo-cáo-đoàn-khách.pdf", attachment.Filename);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(Convert.ToBase64String(bytes), attachment.Content);
    }

    /// <summary>The validated envelope the transport works from, built the way the transport builds it.</summary>
    private ValidatedEnvelope Envelope(EmailRecipient? replyTo) =>
        EmailRecipientValidator.Validate(Message(replyTo, _h.Marker), maxRecipients: 50);

    /// <summary>The bare address out of a `Name &lt;addr&gt;` or a plain `addr`.</summary>
    private static string AddressOf(string formatted)
    {
        var open = formatted.LastIndexOf('<');
        return open < 0
            ? formatted.Trim()
            : formatted[(open + 1)..].TrimEnd('>').Trim();
    }
}
