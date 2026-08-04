using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Contact;
using PEMS.Domain.Enums;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The contact policy: which templates promise a contact, what the block is allowed to say, and what
/// happens when the two disagree.
///
/// <para>
/// The defect behind all of this was not a rendering bug. Fifteen templates told the recipient to
/// "vui lòng liên hệ …" and exactly one of them printed anything they could act on; the other fourteen
/// asked people to make contact and gave them no address. So the assertions here are mostly about
/// promises being kept: a template whose words instruct the reader to get in touch must be REQUIRED, must
/// carry the placeholder, and must refuse to send rather than ship the instruction with nothing behind it.
/// </para>
/// </summary>
public class EmailContactPolicyTests
{
    // ── 1. The policy matches what the templates actually say ───────────────

    /// <summary>
    /// Every registered template has a policy. A template added later with no entry would silently take
    /// the baseline, which is OPTIONAL — the wrong default to arrive at by omission for a message whose
    /// text may well tell somebody to make contact.
    /// </summary>
    [Fact]
    public void Every_registered_template_has_an_explicit_policy()
    {
        var configured = EmailContactPolicyDefaults.ConfiguredTemplateCodes.ToHashSet(StringComparer.Ordinal);

        var missing = SystemEmailTemplates.AllCodes
            .Where(code => !configured.Contains(code))
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// The audited set, pinned. This is the list the seed and the patch are generated from, so a change
    /// of mind about one template has to be made here — where the reason can be written down — and not by
    /// editing SQL until a test goes green.
    /// </summary>
    [Fact]
    public void The_templates_that_promise_a_contact_are_the_ones_marked_required()
    {
        var required = SystemEmailTemplates.AllCodes
            .Where(EmailContactPolicyDefaults.RequiresContactBlock)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[]
        {
            "ACCOUNT_ACTIVATED",
            "ACCOUNT_EMAIL_CHANGED_NEW_NOTICE",
            "ACCOUNT_EMAIL_CHANGED_OLD_NOTICE",
            "ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE",
            "ACCOUNT_STAFF_LEADER_REPLACED",
            "DEPT_PERSONNEL_ACCOUNT_DISABLED",
            "LOGISTICS_CHANGE_PROPOSAL_TO_HOST",
            "LOGISTICS_REQUEST_TO_DEPARTMENT",
            "VISIT_DEPARTMENT_LEADER_INVITATION",
            "VISIT_DEPARTMENT_STAFF_ASSIGNMENT",
            "VISIT_PARTICIPANT_INVITATION",
            "VISIT_REMINDER_PARTICIPANTS",
            "VISIT_SETUP_PROGRESS_UPDATE",
            "VISIT_STUDENT_INVITATION",
        }, required);
    }

    /// <summary>
    /// A message that carries a one-time code shows no contact block.
    ///
    /// <para>
    /// Not an oversight and not a nicety left undone: none of these texts asks the reader to contact
    /// anybody, and a block would only widen what a forwarded or intercepted copy discloses about the
    /// account it belongs to.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(SystemEmailTemplates.AccountEmailConfirmation)]
    [InlineData(SystemEmailTemplates.AuthPasswordResetOtp)]
    [InlineData(SystemEmailTemplates.VisitRequestOtp)]
    public void Credential_bearing_mail_carries_no_contact_block(string code)
    {
        var policy = EmailContactPolicyDefaults.For(code);

        Assert.Equal(EmailContactRequirement.NONE, policy.Requirement);
        Assert.False(policy.RendersBlock);
    }

    /// <summary>
    /// The two notices sent to an address that was just unlinked may only ever show the SYSTEM support
    /// contact.
    ///
    /// <para>
    /// They can land on a stranger's mailbox — that is what a mistyped address does — which is why the
    /// registry gives them no variables at all rather than naming the account holder. A campus or
    /// department contact would undo that by disclosing which campus, or which department, the account
    /// they know nothing about belongs to.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(SystemEmailTemplates.AccountPendingEmailChangedOldNotice)]
    [InlineData(SystemEmailTemplates.AccountEmailChangedOldNotice)]
    public void Notices_to_an_unlinked_address_may_only_show_system_support(string code)
    {
        var policy = EmailContactPolicyDefaults.For(code);

        Assert.Equal(EmailContactSource.SUPPORT_CONTACT, policy.ContactSource);
        Assert.False(policy.ShowCampus);
        Assert.False(policy.ShowDepartment);
        Assert.False(policy.ShowSender);
    }

    /// <summary>
    /// Everything about one visit resolves its contact from the Host of that campus instance. Asserted as
    /// a group because "do not show a guest another campus's Host" is a property of the whole flow, not of
    /// whichever template somebody remembered.
    /// </summary>
    [Theory]
    [InlineData(SystemEmailTemplates.VisitParticipantInvitation)]
    [InlineData(SystemEmailTemplates.VisitStudentInvitation)]
    [InlineData(SystemEmailTemplates.VisitDepartmentLeaderInvitation)]
    [InlineData(SystemEmailTemplates.VisitDepartmentStaffAssignment)]
    [InlineData(SystemEmailTemplates.VisitReminderParticipants)]
    [InlineData(SystemEmailTemplates.VisitSetupProgressUpdate)]
    public void Visit_mail_resolves_the_host_and_replies_go_to_them(string code)
    {
        var policy = EmailContactPolicyDefaults.For(code);

        Assert.Equal(EmailContactRequirement.REQUIRED, policy.Requirement);
        Assert.Equal(EmailContactSource.HOST, policy.ContactSource);
        Assert.Equal(EmailReplyToSource.CONTACT, policy.ReplyToSource);
    }

    /// <summary>The Host's own reminder does not name the Host to the Host.</summary>
    [Fact]
    public void The_hosts_own_reminder_shows_no_contact()
        => Assert.Equal(
            EmailContactRequirement.NONE,
            EmailContactPolicyDefaults.For(SystemEmailTemplates.VisitReminderHost).Requirement);

    // ── 2. The template contract ────────────────────────────────────────────

    /// <summary>
    /// A REQUIRED template's contract demands the placeholder, so an operator who deletes it while
    /// rewording is refused at SAVE time — where they can still see what they removed — instead of at
    /// send time in front of a recipient.
    /// </summary>
    [Fact]
    public void A_required_template_must_declare_the_contact_block_in_its_contract()
    {
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.VisitParticipantInvitation);

        Assert.NotNull(contract);
        Assert.Contains(EmailTrustedBlocks.ContactInformationBlock, contract!.RequiredSystemBlocks);
        Assert.True(contract.AllowsSystemBlock(EmailTrustedBlocks.ContactInformationBlock));

        // And NOT as a variable. It is required, and it was previously reachable through the variable
        // lists — which is how a mandatory block came to be reported as a variable that "does not exist
        // in the system" whenever the two lists were compared.
        Assert.DoesNotContain(EmailTrustedBlocks.ContactInformationBlock, contract.AllowedVariables);
        Assert.DoesNotContain(EmailTrustedBlocks.ContactInformationBlock, contract.RequiredVariables);
    }

    /// <summary>
    /// A NONE template may not even use the block. Allowing it everywhere would let an operator paste a
    /// placeholder into a message whose policy will never resolve one, and find out at send time.
    /// </summary>
    [Fact]
    public void A_no_contact_template_may_not_use_the_block_at_all()
    {
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.AuthPasswordResetOtp);

        Assert.NotNull(contract);
        Assert.False(contract!.AllowsSystemBlock(EmailTrustedBlocks.ContactInformationBlock));
        Assert.DoesNotContain(EmailTrustedBlocks.ContactInformationBlock, contract.AllowedVariables);
    }

    /// <summary>
    /// The setup-progress update needs BOTH blocks, and each reports under its own code — one says
    /// "re-sync this row from canonical", the other says "the body and the contact policy disagree".
    /// Same symptom, different repair, different person.
    /// </summary>
    [Fact]
    public void The_setup_progress_update_requires_two_blocks_with_distinct_error_codes()
    {
        var required = EmailTemplateContracts
            .RequiredBlocksFor(SystemEmailTemplates.VisitSetupProgressUpdate)
            .ToDictionary(x => x.Block, x => x.ErrorCode, StringComparer.Ordinal);

        Assert.Equal(2, required.Count);
        Assert.Equal(
            EmailErrorCodes.TemplateRequiredBlockNotInBody,
            required[EmailTrustedBlocks.SetupSummaryBlock]);
        Assert.Equal(
            EmailErrorCodes.TemplateRequiredContactBlockNotInBody,
            required[EmailTrustedBlocks.ContactInformationBlock]);
    }

    [Fact]
    public void A_template_with_no_contact_requirement_requires_no_blocks()
        => Assert.Empty(EmailTemplateContracts.RequiredBlocksFor(SystemEmailTemplates.VisitRequestOtp));

    // ── 3. The rendered block ───────────────────────────────────────────────

    private static readonly EmailContactPolicyResolution HostPolicy =
        EmailContactPolicyDefaults.For(SystemEmailTemplates.VisitParticipantInvitation);

    private static EmailContactInformation Host(string? email = "host@fpt.edu.vn", string? phone = "0900000001")
        => new(EmailContactSource.HOST, "Nguyễn Văn A",
            RoleLabel: "Người phụ trách tiếp đón",
            DepartmentName: "Phòng Hợp tác Quốc tế",
            CampusName: "FPT University HCM",
            Email: email, Phone: phone);

    private static XElement Parse(string html) => XElement.Parse("<root>" + html + "</root>");

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void The_block_shows_the_name_and_the_channels_the_policy_allows(string language)
    {
        var html = EmailContactHtmlRenderer.Render(Host(), HostPolicy, language);

        Assert.Contains("Nguyễn Văn A", html);
        Assert.Contains("host@fpt.edu.vn", html);
        Assert.Contains("0900000001", html);
        // The Host policy shows the campus but not the department.
        Assert.Contains("FPT University HCM", html);
        Assert.DoesNotContain("Phòng Hợp tác Quốc tế", html);
    }

    /// <summary>
    /// A field with no value is omitted rather than printed as a placeholder. "N/A" under a telephone
    /// heading tells the recipient nothing and reads like a fault in the mail.
    /// </summary>
    [Fact]
    public void A_missing_phone_number_is_left_out_rather_than_shown_as_not_available()
    {
        var html = EmailContactHtmlRenderer.Render(Host(phone: null), HostPolicy, "vi");

        Assert.Contains("host@fpt.edu.vn", html);
        Assert.DoesNotContain("N/A", html);
        Assert.DoesNotContain("Điện thoại", html);
    }

    /// <summary>
    /// No channel means no block. A heading and a name with no way to reach the person is the original
    /// defect in miniature, so the renderer declines to produce one.
    /// </summary>
    [Fact]
    public void A_contact_with_no_reachable_channel_renders_nothing()
    {
        var html = EmailContactHtmlRenderer.Render(Host(email: null, phone: null), HostPolicy, "vi");

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void A_none_policy_renders_nothing_even_with_a_perfectly_good_contact()
    {
        var policy = EmailContactPolicyDefaults.For(SystemEmailTemplates.AuthPasswordResetOtp);

        Assert.Equal(string.Empty, EmailContactHtmlRenderer.Render(Host(), policy, "vi"));
    }

    /// <summary>
    /// Everything dynamic is encoded. The block is injected verbatim into the body, so a person whose
    /// display name contains markup is a string here — not a way to write HTML into somebody's mail.
    /// </summary>
    [Fact]
    public void Values_are_encoded_and_the_block_stays_well_formed()
    {
        var hostile = new EmailContactInformation(
            EmailContactSource.HOST,
            "<script>alert(1)</script>",
            RoleLabel: "</td></tr><tr><td>injected",
            CampusName: "FPT & Partners",
            Email: "\"><b>x</b>@fpt.edu.vn",
            Phone: "0900000001");

        var html = EmailContactHtmlRenderer.Render(hostile, HostPolicy, "vi");

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);

        // Parsing is the real assertion: had the closing tags in the role label been taken as markup,
        // the document would not balance.
        var cells = Parse(html).Descendants("tbody").Elements("tr").ToList();
        Assert.NotEmpty(cells);
        // Counted by column span, not by cell count: the heading is now one full-width cell in the
        // same row group (it was <thead>/<th>, which the composer's editor silently drops).
        foreach (var row in cells) Assert.Equal(2, row.Elements("td").Sum(ColumnSpan));
    }

    /// <summary>The block is a table with declared widths, for the same reason the setup tables are.</summary>
    [Fact]
    public void The_block_declares_its_column_widths()
    {
        var table = Parse(EmailContactHtmlRenderer.Render(Host(), HostPolicy, "vi"))
            .Descendants("table").Single();

        Assert.Contains("table-layout:fixed", (string?)table.Attribute("style"));
        Assert.Equal(2, table.Elements("colgroup").Elements("col").Count());

        // A cell that spans every column has no column of its own to size, so the heading is exempt.
        foreach (var cell in table.Descendants("td").Where(td => ColumnSpan(td) == 1))
            Assert.False(string.IsNullOrEmpty((string?)cell.Attribute("width")));
    }

    /// <summary>
    /// The heading must not go back to being <c>&lt;thead&gt;</c>/<c>&lt;th&gt;</c>. This block is
    /// injected into email bodies the Host can reopen in the rich-text composer, whose document model
    /// drops both tags while keeping their text — which lifts the heading out of the table and runs it
    /// into the first row. Same rule, and same reason, as VisitSetupEmailHtml.
    /// </summary>
    [Fact]
    public void The_block_uses_no_table_header_markup()
    {
        var html = EmailContactHtmlRenderer.Render(Host(), HostPolicy, "vi");

        Assert.DoesNotContain("<thead", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<th ", html, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Parse(html).Descendants("th"));
    }

    /// <summary>How many columns a cell occupies — 1 unless it declares a colspan.</summary>
    private static int ColumnSpan(XElement cell) =>
        int.TryParse((string?)cell.Attribute("colspan"), out var span) && span > 0 ? span : 1;

    [Fact]
    public void The_sender_line_appears_only_when_the_policy_asks_for_it()
    {
        var withSender = HostPolicy with { ShowSender = true };

        Assert.Contains("Trần Văn B",
            EmailContactHtmlRenderer.Render(Host(), withSender, "vi", senderName: "Trần Văn B"));
        Assert.DoesNotContain("Trần Văn B",
            EmailContactHtmlRenderer.Render(Host(), HostPolicy, "vi", senderName: "Trần Văn B"));
    }

    /// <summary>
    /// Asserted on the DECODED text, not the raw markup. <c>WebUtility.HtmlEncode</c> rewrites
    /// U+00A0..U+00FF as numeric references, so "Thông" leaves the renderer as "Th&amp;#244;ng" while
    /// "liên" passes through untouched — both display identically. Matching the raw string would make
    /// this test pass or fail on which diacritics the heading happens to contain.
    /// </summary>
    [Theory]
    [InlineData("vi", "Thông tin liên hệ")]
    [InlineData("en", "Contact information")]
    public void The_heading_follows_the_language(string language, string expected)
        => Assert.Contains(
            expected,
            System.Net.WebUtility.HtmlDecode(EmailContactHtmlRenderer.Render(Host(), HostPolicy, language)));

    /// <summary>A preview has no visit, so it shows a stand-in and never a fabricated person.</summary>
    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void The_preview_stand_in_invents_no_contact_details(string language)
    {
        var html = EmailContactHtmlRenderer.DisabledBlock(language);

        Assert.DoesNotContain("@", html);
        Assert.NotEqual(string.Empty, html);
    }
}
