using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The two contact-role invitations (Batch 4): <c>VISIT_CONTACT_CLAIM</c> for the person already recorded
/// as the contact, and <c>VISIT_CONTACT_TRANSFER</c> for somebody being asked to take the role over.
///
/// <para>
/// Both carry a one-time link, so both are registered sensitive and neither stores its body. What these
/// tests pin beyond that is the difference between them: the transfer names the CURRENT contact, because
/// an invitation to replace somebody is unverifiable — and indistinguishable from a phishing mail — if it
/// will not say who is handing the role over.
/// </para>
/// </summary>
public sealed class ContactRoleInvitationEndToEndTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("batch4-evidence@partner.example.com");

    public void Dispose() => _h.Dispose();

    private static readonly DateTime Expires = new(2026, 8, 1, 17, 30, 0);
    private const string ClaimUrl = "https://pems.test/visit-contact-claim/RAW-CLAIM-TOKEN";
    private const string TransferUrl = "https://pems.test/visit-contact-transfer/RAW-TRANSFER-TOKEN";
    // An invitation carries one link per answer now (accept / decline), each with its own single-use
    // token, so the page learns which button was pressed from the token rather than from a query
    // parameter a scanner could flip.
    private const string ClaimDeclineUrl = "https://pems.test/visit-contact-claim/RAW-CLAIM-DECLINE-TOKEN";
    private const string TransferDeclineUrl = "https://pems.test/visit-contact-transfer/RAW-TRANSFER-DECLINE-TOKEN";

    /// <summary>
    /// The campus and the window are part of both invitations: the contact role is held per campus, so
    /// one request can send the same person two of these, and the request code and delegation name are
    /// identical across them.
    /// </summary>
    private const string Campus = "FPT University Hà Nội";
    private const string PlannedTime = "09:00 16/08/2026 - 13:00 16/08/2026";

    private SystemEmailRequest Claim() => new(
        SystemEmailTemplates.VisitContactClaim,
        new EmailRecipient(_h.Marker, "Trần Thị Đầu Mối"),
        new Dictionary<string, string>
        {
            ["contactFullName"] = "Trần Thị Đầu Mối",
            ["requestCode"] = "VR-2026-0042",
            ["delegationName"] = "Đoàn Đại học Kyoto",
            ["campusName"] = Campus,
            ["plannedTime"] = PlannedTime,
        },
        TrustedBlocks: new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] =
                EmailComposition.ContactRoleInvitationBlock(ClaimUrl, ClaimDeclineUrl, Expires),
        },
        RelatedType: "VisitRequestIdentityChange",
        RelatedId: 4242);

    private SystemEmailRequest Transfer() => new(
        SystemEmailTemplates.VisitContactTransfer,
        new EmailRecipient(_h.Marker),
        new Dictionary<string, string>
        {
            ["contactFullName"] = _h.Marker,
            ["requestCode"] = "VR-2026-0042",
            ["delegationName"] = "Đoàn Đại học Kyoto",
            ["campusName"] = Campus,
            ["plannedTime"] = PlannedTime,
            ["currentContactName"] = "Trần Thị Đầu Mối",
        },
        TrustedBlocks: new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] =
                EmailComposition.ContactRoleInvitationBlock(TransferUrl, TransferDeclineUrl, Expires),
        },
        RelatedType: "VisitRequestIdentityChange",
        RelatedId: 4243);

    // ── The invitation reaches one person, with the link ─────────────────────

    [Fact]
    public async Task The_claim_invitation_goes_to_one_person_and_carries_the_link()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var row = await db.EmailTemplates.AsNoTracking()
                .SingleAsync(t => t.TemplateCode == SystemEmailTemplates.VisitContactClaim);

            var result = await _h.Dispatcher(db).SendAsync(Claim());
            Assert.Equal(EmailDeliveryStatus.Sent, result.Delivery.Status);

            var eml = _h.OnlyMessage();
            Assert.Equal(1, eml.AddressCount("To"));
            Assert.Equal(string.Empty, eml.Header("Cc"));
            Assert.Equal(string.Empty, eml.Header("Bcc"));
            Assert.Contains(EmlMessage.LiteralPrefix(row.SubjectVi), eml.DecodedHeader("Subject"));
            Assert.Contains("VR-2026-0042", eml.DecodedHeader("Subject"));

            var body = eml.Body;
            Assert.Contains(ClaimUrl, body);
            Assert.Contains("Kyoto", body);
            Assert.DoesNotContain("{{", body);
            // The deadline the token really has, rendered by the action block rather than guessed.
            Assert.Contains("17:30 01/08/2026", body);
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task The_transfer_invitation_names_the_person_handing_the_role_over()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            await _h.Dispatcher(db).SendAsync(Transfer());

            var body = _h.OnlyMessage().Body;
            Assert.Contains(TransferUrl, body);
            // Without this name the mail is "somebody wants to give you a role" — which is exactly what
            // a phishing message says.
            Assert.Contains(System.Net.WebUtility.HtmlEncode("Trần Thị Đầu Mối"), body);
            Assert.DoesNotContain("{{", body);
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── The link is not kept anywhere it could be reused ─────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Neither_invitation_stores_its_one_time_link(bool transfer)
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var result = await _h.Dispatcher(db).SendAsync(transfer ? Transfer() : Claim());

            using var verify = EmailEvidenceHarness.NewContext();
            var sent = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);

            // The record is kept — somebody may need to see what the invitee was told — but the action
            // block is removed, so the accept link is not sitting in a table every internal role can
            // read. A stored link is a stored credential: it would let a colleague take the role in the
            // invitee's place.
            Assert.NotNull(sent.BodySnapshot);
            Assert.DoesNotContain("RAW-", sent.BodySnapshot!);
            Assert.DoesNotContain("/visit-contact-", sent.BodySnapshot!);
            Assert.DoesNotContain("Mở trang xác nhận", sent.BodySnapshot!);
            // …while the part that explains the invitation survives.
            Assert.Contains("VR-2026-0042", sent.BodySnapshot!);
            Assert.DoesNotContain("RAW-", sent.Subject);
            Assert.Equal("SENT", sent.Status);
            Assert.Null(sent.DeliveredAt);
            Assert.Equal("VisitRequestIdentityChange", sent.RelatedType);

            var recipient = Assert.Single(await verify.SentEmailRecipients.AsNoTracking()
                .Where(r => r.SentEmailId == result.SentEmailId).ToListAsync());
            Assert.Equal(_h.Marker, recipient.RecipientEmail);
            Assert.Equal(EmailRecipientTypes.To, recipient.RecipientType);
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task A_subject_edited_to_include_the_action_block_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            await EmailEvidenceHarness.WithTemplateAsync(
                db, SystemEmailTemplates.VisitContactClaim,
                t => t.SubjectVi = "[PEMS] Xác nhận: {{actionBlock}}",
                async () =>
                {
                    var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                        () => _h.Dispatcher(db).SendAsync(Claim()));

                    Assert.Equal(EmailErrorCodes.TemplateSensitiveInSubject, ex.ErrorCode);
                    Assert.DoesNotContain(ClaimUrl, ex.Message);
                    Assert.Empty(_h.Messages());

                    using var verify = EmailEvidenceHarness.NewContext();
                    Assert.False(await verify.SentEmailRecipients.AsNoTracking()
                        .AnyAsync(r => r.RecipientEmail == _h.Marker));
                });
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task Editing_the_template_changes_the_very_next_message_without_a_restart()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            await _h.Dispatcher(db).SendAsync(Claim());
            var before = _h.OnlyMessage().DecodedHeader("Subject");
            _h.ClearMessages();

            await EmailEvidenceHarness.WithTemplateAsync(
                db, SystemEmailTemplates.VisitContactClaim,
                t => t.SubjectVi = "[PEMS] Mời xác nhận đầu mối — {{requestCode}} (bản mới)",
                async () =>
                {
                    await _h.Dispatcher(db).SendAsync(Claim());
                    var after = _h.OnlyMessage().DecodedHeader("Subject");

                    Assert.NotEqual(before, after);
                    Assert.Contains("bản mới", after);
                    Assert.Contains("VR-2026-0042", after);
                });
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task Both_templates_carry_their_secret_in_the_action_block_not_in_the_text()
    {
        // Which is why their history keeps a stripped body rather than nothing: the classification, not
        // the dispatcher, is what decides that.
        Assert.Equal(
            HistoryBodyPolicy.ActionBlockStripped,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.VisitContactClaim));
        Assert.Equal(
            HistoryBodyPolicy.ActionBlockStripped,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.VisitContactTransfer));
        await Task.CompletedTask;
    }
}
