using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Infrastructure.Security;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// B5-D1 — the two content modes.
///
/// <para>
/// The Host may rewrite an invitation before sending it. That is a real feature, and the question this
/// suite answers is what it does and does not change. It changes the sentences. It does not change which
/// template the message is, who receives it, whether copies are allowed, which buttons appear, what the
/// tokens are, how much of the message the history keeps, or whether a secret may appear in the subject —
/// all of which stay where they were: in the template registry and the dispatcher.
/// </para>
/// <para>
/// Everything below runs the real renderer, the real sanitiser and a real SMTP pickup directory against a
/// disposable database, because the interesting failures are exactly the ones a fake cannot have: markup
/// that survives sanitising, a link that survives the history strip, a subject that reaches
/// <c>sent_emails</c> with a token in it.
/// </para>
/// </summary>
public sealed class AuthoredEmailContentTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("b5d1-evidence@partner.example.com");
    private static readonly HtmlSanitizerService Sanitizer = new();

    public void Dispose() => _h.Dispose();

    private const string AcceptUrl = "https://pems.test/api/public/email-actions/RAW-ACCEPT-0001";
    private const string DeclineUrl = "https://pems.test/api/public/email-actions/RAW-DECLINE-0001";

    private static Dictionary<string, string> InvitationVariables() => new()
    {
        ["recipientName"] = "Nguyễn Văn Bình",
        ["delegationName"] = "Đoàn Đại học Kyoto",
        ["campusName"] = "FPT Đà Nẵng",
        ["plannedTime"] = "09:00 12/08/2026 - 11:30 12/08/2026",
        ["hostName"] = "Trần Thị Hà",
        ["roleLabel"] = "Staff hỗ trợ IC",
        ["hostMessage"] = "Nhờ anh hỗ trợ phần đón tiếp.",
    };

    private static Dictionary<string, string> ActionBlock() => new()
    {
        [EmailTrustedBlocks.ActionBlock] = EmailComposition.AcceptDeclineBlock(AcceptUrl, DeclineUrl),
    };

    private SystemEmailRequest Invitation(SystemEmailContent? content = null) => new(
        SystemEmailTemplates.VisitParticipantInvitation,
        new EmailRecipient(_h.Marker, "Nguyễn Văn Bình"),
        InvitationVariables(),
        TrustedBlocks: ActionBlock(),
        RelatedType: "VisitParticipant",
        RelatedId: 7701)
    {
        Content = content ?? SystemEmailContent.FromTemplate.Instance,
    };

    private static SystemEmailContent.AuthoredByUser Authored(
        string subject = "Nhờ anh hỗ trợ đoàn Kyoto ngày 12/08",
        string body = "<p>Chào anh Bình,</p><p>Đoàn Kyoto tới lúc 9h sáng thứ Tư. Nhờ anh hỗ trợ phần đón tiếp.</p>")
        => SystemEmailContent.AuthoredByUser.Create(subject, body, Sanitizer);

    // ── 1. The template mode is unchanged ────────────────────────────────────

    [Fact]
    public async Task Without_an_edit_the_template_is_still_the_only_source_of_content()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var row = await db.EmailTemplates.AsNoTracking()
                .SingleAsync(t => t.TemplateCode == SystemEmailTemplates.VisitParticipantInvitation);

            var result = await _h.Dispatcher(db).SendAsync(Invitation());
            Assert.Equal(EmailDeliveryStatus.Sent, result.Delivery.Status);

            var eml = _h.OnlyMessage();
            Assert.Contains(EmlMessage.LiteralPrefix(row.SubjectVi), eml.DecodedHeader("Subject"));
            // Wording only the seeded template has — proof the words came from the database row.
            Assert.Contains("Vui l", eml.Body);
            Assert.Contains(AcceptUrl, eml.Body);
            Assert.DoesNotContain("{{", eml.Body);
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── 2. Authored content is delivered, with the backend's buttons ─────────

    [Fact]
    public async Task An_edited_message_is_delivered_with_the_authors_words_and_the_systems_buttons()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var row = await db.EmailTemplates.AsNoTracking()
                .SingleAsync(t => t.TemplateCode == SystemEmailTemplates.VisitParticipantInvitation);

            var result = await _h.Dispatcher(db).SendAsync(Invitation(Authored()));
            Assert.Equal(EmailDeliveryStatus.Sent, result.Delivery.Status);

            var eml = _h.OnlyMessage();
            Assert.Equal("Nhờ anh hỗ trợ đoàn Kyoto ngày 12/08", eml.DecodedHeader("Subject"));
            Assert.NotEqual(EmlMessage.LiteralPrefix(row.SubjectVi), eml.DecodedHeader("Subject"));

            var body = eml.Body;
            Assert.Contains("9h s", body);                       // the author's sentence
            Assert.DoesNotContain("Vui l", body);                // …and not the template's
            Assert.Contains(AcceptUrl, body);                    // the real accept link is still there
            Assert.Contains(DeclineUrl, body);
            Assert.Contains("PEMS_ACTION_BLOCK_START", body);    // …inside exactly one canonical block
            Assert.Equal(1, CountOccurrences(body, "PEMS_ACTION_BLOCK_START"));
            Assert.Equal(1, CountOccurrences(body, "PEMS_ACTION_BLOCK_END"));
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task The_recorded_template_is_the_same_one_either_way()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var fromTemplate = await _h.Dispatcher(db).SendAsync(Invitation());
            _h.ClearMessages();
            var fromAuthor = await _h.Dispatcher(db).SendAsync(Invitation(Authored()));

            // Editing the words does not re-point the message at a different template, and does not
            // detach it from one — the history stays traceable to the template it belongs to.
            Assert.Equal(fromTemplate.EmailTemplateId, fromAuthor.EmailTemplateId);
            Assert.NotEqual(0ul, fromAuthor.EmailTemplateId);
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── 3–4. Sanitising still applies to what the author wrote ───────────────

    [Theory]
    [InlineData("<p>Xin chào</p><script>alert('x')</script>", "alert")]
    [InlineData("<p onclick=\"steal()\">Xin chào</p>", "onclick")]
    [InlineData("<p>Xin chào <a href=\"javascript:steal()\">bấm</a></p>", "javascript:")]
    [InlineData("<iframe src=\"https://evil.example.com\"></iframe><p>Xin chào</p>", "iframe")]
    public async Task Dangerous_markup_never_survives_into_the_message(string authoredBody, string forbidden)
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var result = await _h.Dispatcher(db).SendAsync(Invitation(Authored(body: authoredBody)));

            var body = _h.OnlyMessage().Body;
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Xin ch", body);   // …while the message itself survives

            using var verify = EmailEvidenceHarness.NewContext();
            var snapshot = await verify.SentEmails.AsNoTracking()
                .Where(e => e.SentEmailId == result.SentEmailId)
                .Select(e => e.BodySnapshot).SingleAsync();
            Assert.DoesNotContain(forbidden, snapshot ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── 5–6. What authored content may NOT change ────────────────────────────

    [Fact]
    public void Authored_content_carries_no_recipient_no_template_and_no_policy()
    {
        // Stated as a type-level fact rather than a runtime one: AuthoredByUser has exactly two members,
        // both content. There is no field on it through which an author could reach TO/CC/BCC, the
        // template code, the recipient policy or the retention policy — so no test can be written that
        // makes it happen, which is the strongest form this guarantee can take.
        var members = typeof(SystemEmailContent.AuthoredByUser)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(n => n != "EqualityContract")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[] { "BodyHtml", "Subject" }, members);

        // …and neither can be swapped afterwards: get-only members make `with` unable to set them.
        Assert.All(
            typeof(SystemEmailContent.AuthoredByUser).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance),
            p => Assert.Null(p.SetMethod));
    }

    [Fact]
    public async Task An_edited_invitation_still_goes_to_one_person_with_no_copies()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            await _h.Dispatcher(db).SendAsync(Invitation(Authored()));

            var eml = _h.OnlyMessage();
            Assert.Equal(1, eml.AddressCount("To"));
            Assert.Equal(string.Empty, eml.Header("Cc"));
            Assert.Equal(string.Empty, eml.Header("Bcc"));

            using var verify = EmailEvidenceHarness.NewContext();
            var recipients = await verify.SentEmailRecipients.AsNoTracking()
                .Where(r => r.RecipientEmail == _h.Marker).ToListAsync();
            Assert.All(recipients, r => Assert.Equal(EmailRecipientTypes.To, r.RecipientType));
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task An_edited_invitation_keeps_the_retention_policy_of_its_template()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var result = await _h.Dispatcher(db).SendAsync(Invitation(Authored()));

            // The policy follows the template's classification, not the fact that a person wrote the text.
            Assert.Equal(
                HistoryBodyPolicy.ActionBlockStripped,
                SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.VisitParticipantInvitation));

            using var verify = EmailEvidenceHarness.NewContext();
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);
            Assert.NotNull(stored.BodySnapshot);
            Assert.DoesNotContain("RAW-ACCEPT", stored.BodySnapshot!);
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── 7–9. The subject guard applies to what an author typed ───────────────

    [Fact]
    public void An_authored_subject_may_not_interpolate_a_credential()
    {
        EmailEvidenceHarness.RequireDb();
        var ex = Assert.Throws<BusinessRuleException>(() => RenderAuthoredSubject("Mã của bạn: {{otpCode}}"));
        Assert.Equal(EmailErrorCodes.TemplateSensitiveInSubject, ex.ErrorCode);
    }

    [Fact]
    public void An_authored_subject_may_not_interpolate_the_action_block()
    {
        // Refused where the author writes it, not later: {{actionBlock}} anywhere in authored content is
        // an attempt to place the system's buttons by hand.
        var ex = Assert.Throws<ValidationException>(
            () => Authored(subject: "Lời mời {{actionBlock}}"));
        Assert.Equal(EmailErrorCodes.AuthoredActionBlockForbidden, ex.ErrorCode);
    }

    [Fact]
    public async Task An_authored_subject_may_not_carry_the_one_time_link_itself()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => _h.Dispatcher(db).SendAsync(
                    Invitation(Authored(subject: "Bấm vào đây: " + AcceptUrl))));

            Assert.Equal(EmailErrorCodes.SubjectSecretLeak, ex.ErrorCode);
            // The error names the kind of thing, never the thing: this message is logged and shown.
            Assert.DoesNotContain(AcceptUrl, ex.Message);
            Assert.DoesNotContain("RAW-ACCEPT", ex.Message);

            Assert.Empty(_h.Messages());
            using var verify = EmailEvidenceHarness.NewContext();
            Assert.False(await verify.SentEmailRecipients.AsNoTracking()
                .AnyAsync(r => r.RecipientEmail == _h.Marker));
        }
        finally { await _h.CleanupAsync(); }
    }

    [Fact]
    public async Task An_authored_subject_may_not_carry_the_bare_token_either()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => _h.Dispatcher(db).SendAsync(
                    Invitation(Authored(subject: "Mã tham dự RAW-ACCEPT-0001"))));

            Assert.Equal(EmailErrorCodes.SubjectSecretLeak, ex.ErrorCode);
            Assert.DoesNotContain("RAW-ACCEPT-0001", ex.Message);
            Assert.Empty(_h.Messages());
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── 10–11. Forged and malformed action-block markers ─────────────────────

    [Theory]
    [InlineData("<p>Xin chào</p><!-- PEMS_ACTION_BLOCK_START --><p>giả mạo</p><!-- PEMS_ACTION_BLOCK_END -->")]
    [InlineData("<p>Xin chào</p><!-- PEMS_ACTION_BLOCK_START -->")]
    [InlineData("<!-- PEMS_ACTION_BLOCK_END --><p>Xin chào</p>")]
    [InlineData("PEMS_ACTION_BLOCK_START <p>Xin chào</p> PEMS_ACTION_BLOCK_END")]
    public void An_author_may_not_write_the_action_block_markers(string authoredBody)
    {
        var ex = Assert.Throws<ValidationException>(() => Authored(body: authoredBody));
        Assert.Equal(EmailErrorCodes.AuthoredActionBlockForbidden, ex.ErrorCode);
    }

    [Theory]
    // missing end · missing start · reversed · nested · two blocks
    [InlineData("<!-- PEMS_ACTION_BLOCK_START --><p>a</p>")]
    [InlineData("<p>a</p><!-- PEMS_ACTION_BLOCK_END -->")]
    [InlineData("<!-- PEMS_ACTION_BLOCK_END --><p>a</p><!-- PEMS_ACTION_BLOCK_START -->")]
    [InlineData("<!-- PEMS_ACTION_BLOCK_START --><!-- PEMS_ACTION_BLOCK_START --><p>a</p><!-- PEMS_ACTION_BLOCK_END --><!-- PEMS_ACTION_BLOCK_END -->")]
    [InlineData("<!-- PEMS_ACTION_BLOCK_START --><p>a</p><!-- PEMS_ACTION_BLOCK_END --><!-- PEMS_ACTION_BLOCK_START --><p>b</p><!-- PEMS_ACTION_BLOCK_END -->")]
    public void Malformed_markers_are_refused_rather_than_stripped_on_a_best_effort_basis(string html)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => EmailComposition.StripActionArtifacts(html));
        Assert.Equal(EmailErrorCodes.ActionBlockMalformed, ex.ErrorCode);
    }

    [Fact]
    public void Content_with_no_markers_at_all_is_left_alone()
    {
        // Zero markers is the normal case for template bodies and fresh authored content — the structural
        // rule must not turn that into an error.
        const string html = "<p>Xin chào</p><p>Hẹn gặp lại.</p>";
        Assert.Equal(html, EmailComposition.StripActionArtifacts(html));
        EmailComposition.AssertActionBlockStructure(html);
    }

    // ── 12–14. What the recipient gets vs what the history keeps ─────────────

    [Fact]
    public async Task The_delivered_message_carries_a_working_link_and_the_stored_one_does_not()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var result = await _h.Dispatcher(db).SendAsync(Invitation(Authored()));

            // What was actually sent: the buttons, with both live URLs, inside one canonical block.
            var delivered = _h.OnlyMessage().Body;
            Assert.Contains(AcceptUrl, delivered);
            Assert.Contains(DeclineUrl, delivered);
            Assert.Equal(1, CountOccurrences(delivered, "PEMS_ACTION_BLOCK_START"));

            // What was stored: the author's message, with every trace of the link gone.
            using var verify = EmailEvidenceHarness.NewContext();
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);

            Assert.NotNull(stored.BodySnapshot);
            var snapshot = stored.BodySnapshot!;
            Assert.DoesNotContain(AcceptUrl, snapshot);
            Assert.DoesNotContain(DeclineUrl, snapshot);
            Assert.DoesNotContain("RAW-", snapshot);
            Assert.DoesNotContain("email-actions", snapshot);
            Assert.DoesNotContain("PEMS_ACTION_BLOCK", snapshot);
            Assert.DoesNotContain("Chấp nhận", System.Net.WebUtility.HtmlDecode(snapshot));

            // …and the author's own words survive, which is the point of keeping a record at all.
            Assert.Contains("9h s", snapshot);
            Assert.DoesNotContain("RAW-", stored.Subject);
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── 15–16. The other two retention policies are untouched ────────────────

    [Fact]
    public void The_history_policy_still_follows_the_classification_and_nothing_else()
    {
        Assert.Equal(HistoryBodyPolicy.None,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.AuthPasswordResetOtp));
        Assert.Equal(HistoryBodyPolicy.None,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.VisitRequestOtp));
        Assert.Equal(HistoryBodyPolicy.Full,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.AccountActivated));
        Assert.Equal(HistoryBodyPolicy.Full,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.VisitReminderHost));
    }

    // ── 17. A refused message leaves nothing behind ──────────────────────────

    [Fact]
    public async Task A_refused_edit_writes_no_history_no_recipient_and_sends_nothing()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            // Rejected at construction: an empty body is not a message.
            Assert.Throws<ValidationException>(() => Authored(body: "   "));

            // Rejected inside the pipeline: a subject carrying the link.
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => _h.Dispatcher(db).SendAsync(Invitation(Authored(subject: AcceptUrl))));

            Assert.Empty(_h.Messages());

            using var verify = EmailEvidenceHarness.NewContext();
            Assert.False(await verify.SentEmailRecipients.AsNoTracking()
                .AnyAsync(r => r.RecipientEmail == _h.Marker));
        }
        finally { await _h.CleanupAsync(); }
    }

    [Theory]
    [InlineData("", "<p>Nội dung</p>", EmailErrorCodes.AuthoredSubjectRequired)]
    [InlineData("Tiêu đề\r\nBcc: attacker@evil.test", "<p>Nội dung</p>", EmailErrorCodes.HeaderInvalid)]
    public void An_unusable_authored_subject_is_refused_with_a_stable_code(
        string subject, string body, string expectedCode)
    {
        var ex = Assert.Throws<ValidationException>(() => Authored(subject, body));
        Assert.Equal(expectedCode, ex.ErrorCode);
    }

    [Fact]
    public void An_oversized_edit_is_refused_before_anything_else_happens()
    {
        var longSubject = new string('x', EmailOverrideLimits.SubjectMax + 1);
        Assert.Equal(
            EmailErrorCodes.AuthoredSubjectTooLong,
            Assert.Throws<ValidationException>(() => Authored(subject: longSubject)).ErrorCode);

        var longBody = "<p>" + new string('y', EmailOverrideLimits.BodyMax) + "</p>";
        Assert.Equal(
            EmailErrorCodes.AuthoredBodyTooLong,
            Assert.Throws<ValidationException>(() => Authored(body: longBody)).ErrorCode);
    }

    // ── 18. Delivery failure behaves exactly as before ───────────────────────

    [Fact]
    public async Task A_provider_failure_still_records_the_message_and_does_not_throw()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            using var db = EmailEvidenceHarness.NewContext();
            var result = await _h.Dispatcher(db, brokenHost: "127.0.0.1")
                .SendAsync(Invitation(Authored()));

            Assert.Equal(EmailDeliveryStatus.Failed, result.Delivery.Status);
            Assert.NotEqual(0ul, result.SentEmailId);

            using var verify = EmailEvidenceHarness.NewContext();
            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.SentEmailId == result.SentEmailId);
            Assert.Equal("FAILED", stored.Status);
            Assert.Null(stored.SentAt);
            // A failed send still must not leave the link in the record.
            Assert.DoesNotContain("RAW-ACCEPT", stored.BodySnapshot ?? string.Empty);
        }
        finally { await _h.CleanupAsync(); }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Renders an authored subject through the real pipeline, in memory, to assert on the guard.</summary>
    /// <summary>
    /// Calls the renderer DIRECTLY, so the variables have to be complete here.
    ///
    /// <para>
    /// Everywhere else in these tests the dispatcher assembles the variables, and it is the dispatcher
    /// that layers the six sender values in. This helper skips it to get at the renderer's own subject
    /// rules — which means that once the invitation template began DECLARING the sender names, the
    /// renderer's completeness check fired first and every test using this helper reported
    /// EMAIL_TEMPLATE_VARIABLE_MISSING instead of the rule it was written to prove.
    /// </para>
    /// <para>
    /// Both are refusals and neither leaks, so nothing was ever unsafe here — but a test that stops
    /// exercising its own subject has stopped guarding it. Supplying the sender values restores that.
    /// </para>
    /// </summary>
    private void RenderAuthoredSubject(string subject)
    {
        using var db = EmailEvidenceHarness.NewContext();
        var renderer = new PEMS.Infrastructure.Email.EmailTemplateRenderer(db);

        var variables = InvitationVariables();
        foreach (var name in PEMS.Application.Emails.Sender.EmailSenderVariableNames.All)
            variables[name] = "";

        renderer.RenderAsync(new EmailRenderRequest(
                SystemEmailTemplates.VisitParticipantInvitation,
                EmailLanguages.Vi,
                variables,
                ActionBlock())
        {
            Content = Authored(subject: subject),
        }).GetAwaiter().GetResult();
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var at = 0;
        while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0) { count++; at += needle.Length; }
        return count;
    }
}
