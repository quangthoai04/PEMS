using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Contact;
using PEMS.Domain.Enums;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// What the template editor's preview pane substitutes for a system block.
///
/// <para>
/// The requirement these pin: an operator editing a template sees <c>{{actionBlock}}</c> in the content
/// (so they can position it) and the RENDERED buttons in the preview (so they can see what the recipient
/// gets). The sample markup is built by the same <c>EmailComposition</c> / <c>EmailContactHtmlRenderer</c>
/// helpers the send uses — a second implementation in the frontend would start correct and drift, and the
/// operator would have no way to tell which of the two a recipient receives.
/// </para>
/// <para>
/// Nothing here may produce a live link. A preview mints no tokens, so the disabled blocks are
/// <c>&lt;span&gt;</c> elements with no <c>href</c> at all — a click cannot navigate because there is
/// nothing to navigate to.
/// </para>
/// </summary>
public class EmailSystemBlockPreviewTests
{
    // ── Action block ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(EmailLanguages.Vi)]
    [InlineData(EmailLanguages.En)]
    public void No_disabled_action_block_carries_a_link_a_token_or_a_url(string language)
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var html = DisabledActionBlockFor(code, language);

            Assert.DoesNotContain("<a ", html, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("href", html, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("http://", html, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("https://", html, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", html, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// An accept/decline template previews the buttons it actually sends, by name.
    /// </summary>
    [Fact]
    public void An_invitation_previews_its_accept_and_decline_buttons()
    {
        var html = DisabledActionBlockFor(SystemEmailTemplates.VisitParticipantInvitation, EmailLanguages.Vi);

        Assert.Contains("Chấp nhận", html);
        Assert.Contains("Từ chối", html);
    }

    /// <summary>
    /// A detail-link template previews ITS label, not the Department flow's. Before the label metadata
    /// existed every stand-in read "Mở yêu cầu để xử lý", so an operator editing the visit reminder saw
    /// a button promising an action that template does not offer.
    /// </summary>
    /// <summary>
    /// <c>LOGISTICS_REQUEST_TO_DEPARTMENT</c> is a logistics-action template, not a detail-link one, so
    /// its own block is Đồng ý / Từ chối / Hành động khác — it is listed here to prove the selection
    /// picks the right BLOCK, not just the right label.
    /// </summary>
    [Theory]
    [InlineData(SystemEmailTemplates.LogisticsExpenseReportReminder, "kê khai chi phí")]
    [InlineData(SystemEmailTemplates.VisitReminderHost, "Xem chi tiết chuyến tiếp khách")]
    [InlineData(SystemEmailTemplates.LogisticsRequestToDepartment, "Hành động khác")]
    public void A_detail_link_template_previews_its_own_label(string code, string expected)
    {
        var html = Decoded(DisabledActionBlockFor(code, EmailLanguages.Vi));

        Assert.Contains(expected, html, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A template with no registered action spec gets a neutral block that names NO business outcome.
    ///
    /// <para>
    /// This is deliberate and worth stating: what buttons those emails should carry is a product decision
    /// that has not been made (R-106), and inventing a label here would put an answer to it in front of
    /// operators as though it had been. The block shows where the action area sits and stops there.
    /// </para>
    /// </summary>
    [Fact]
    public void An_unregistered_template_previews_a_neutral_area_not_an_invented_button()
    {
        // ACCOUNT_ACTIVATED writes {{actionBlock}} and its send injects a sign-in button, but it has no
        // registry entry — one of the nine in that position (R-106).
        Assert.Null(EmailActionTemplates.For(SystemEmailTemplates.AccountActivated));

        var html = Decoded(DisabledActionBlockFor(SystemEmailTemplates.AccountActivated, EmailLanguages.Vi));

        Assert.Contains("Khu vực nút thao tác", html);
        Assert.DoesNotContain("Chấp nhận", html);
        Assert.DoesNotContain("Xác nhận email", html);
    }

    // ── Confirm-email action (registered 2026-08-03) ─────────────────────────

    /// <summary>
    /// <c>ACCOUNT_EMAIL_CONFIRMATION</c> has an unmistakable business action, so it is registered and its
    /// preview shows the real button rather than a neutral placeholder.
    /// </summary>
    [Fact]
    public void The_confirmation_template_previews_its_confirm_button()
    {
        var spec = EmailActionTemplates.For(SystemEmailTemplates.AccountEmailConfirmation);

        Assert.NotNull(spec);
        Assert.True(spec!.HasConfirmAction);

        var html = Decoded(DisabledActionBlockFor(
            SystemEmailTemplates.AccountEmailConfirmation, EmailLanguages.Vi));

        Assert.Contains("Xác nhận email", html);
        Assert.DoesNotContain("Khu vực nút thao tác", html);
    }

    [Theory]
    [InlineData(EmailLanguages.Vi, "Xác nhận email")]
    [InlineData(EmailLanguages.En, "Confirm email")]
    public void The_confirm_button_label_follows_the_language(string language, string expected)
    {
        Assert.Equal(expected, EmailActionTemplates.ConfirmEmailLabel(language));

        var html = Decoded(DisabledActionBlockFor(
            SystemEmailTemplates.AccountEmailConfirmation, language));

        Assert.Contains(expected, html);
    }

    /// <summary>
    /// The send and the preview read the SAME label metadata. This is the property that stops an
    /// operator approving a button whose words no recipient ever sees.
    /// </summary>
    [Theory]
    [InlineData(EmailLanguages.Vi)]
    [InlineData(EmailLanguages.En)]
    public void The_real_send_and_the_preview_carry_the_same_confirm_label(string language)
    {
        var label = EmailActionTemplates.ConfirmEmailLabel(language);

        var real = Decoded(EmailComposition.ConfirmEmailBlock("https://pems.test/confirm?t=abc", label));
        var preview = Decoded(EmailComposition.DisabledConfirmEmailBlock(label));

        Assert.Contains(label, real);
        Assert.Contains(label, preview);
    }

    /// <summary>
    /// Registering the spec must not have touched the token path: the real block still carries the URL
    /// it was given, and the preview still carries no link at all.
    /// </summary>
    [Fact]
    public void Registering_the_spec_left_the_confirm_url_untouched()
    {
        const string url = "https://pems.test/confirm?token=one-time-value";

        var real = EmailComposition.ConfirmEmailBlock(url);
        Assert.Contains(url, real);
        Assert.Contains("<a href=", real, System.StringComparison.OrdinalIgnoreCase);

        var preview = EmailComposition.DisabledConfirmEmailBlock();
        Assert.DoesNotContain("href", preview, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", preview, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Being registered makes the block REQUIRED in the body. The canonical content already carries it in
    /// both languages, so this is enforcement of what is already true — but if a future edit dropped it,
    /// the save must be refused rather than a confirmation mail going out with no way to confirm.
    /// </summary>
    [Fact]
    public void The_confirmation_template_now_requires_its_action_block()
    {
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.AccountEmailConfirmation)!;

        Assert.True(contract.RequiresActionBlock);
        Assert.Contains(EmailTrustedBlocks.ActionBlock, contract.RequiredSystemBlocks);

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Xác nhận email",
            bodyVi: "<p>Chào {{fullName}}.</p>",   // block removed
            subjectEn: null, bodyEn: null);

        Assert.Contains(issues, i => i.Code == EmailErrorCodes.TemplateActionBlockRequired);
    }

    [Fact]
    public void The_neutral_area_follows_the_requested_language()
    {
        var vi = Decoded(EmailComposition.DisabledUnspecifiedActionBlock(EmailLanguages.Vi));
        var en = Decoded(EmailComposition.DisabledUnspecifiedActionBlock(EmailLanguages.En));

        Assert.Contains("Khu vực nút thao tác", vi);
        Assert.Contains("Action area", en);
        Assert.NotEqual(vi, en);
    }

    // ── Contact block ────────────────────────────────────────────────────────

    private static EmailContactPolicyResolution Policy(
        EmailContactRequirement requirement = EmailContactRequirement.REQUIRED,
        bool email = true, bool phone = true,
        bool department = false, bool campus = false, bool sender = false)
        => EmailContactPolicyDefaults.SystemBaseline with
        {
            Requirement = requirement,
            ShowEmail = email,
            ShowPhone = phone,
            ShowDepartment = department,
            ShowCampus = campus,
            ShowSender = sender,
        };

    [Fact]
    public void The_contact_sample_shows_only_the_fields_the_policy_enables()
    {
        var minimal = Decoded(EmailContactHtmlRenderer.SampleBlock(
            Policy(email: true, phone: false), EmailLanguages.Vi));

        Assert.Contains("lien.he.mau@example.invalid", minimal);
        Assert.DoesNotContain("0900 000 000", minimal);
        Assert.DoesNotContain("Phòng Hành chính", minimal);
        Assert.DoesNotContain("FPTU Hà Nội", minimal);
        Assert.DoesNotContain("Được gửi bởi", minimal);

        var full = Decoded(EmailContactHtmlRenderer.SampleBlock(
            Policy(department: true, campus: true, sender: true), EmailLanguages.Vi));

        Assert.Contains("0900 000 000", full);
        Assert.Contains("Phòng Hành chính", full);
        Assert.Contains("FPTU Hà Nội", full);
        Assert.Contains("Được gửi bởi", full);
    }

    /// <summary>Each toggle moves the preview on its own — this is what makes the pane trustworthy.</summary>
    [Fact]
    public void Turning_a_single_toggle_changes_the_sample()
    {
        var before = Decoded(EmailContactHtmlRenderer.SampleBlock(Policy(campus: false), EmailLanguages.Vi));
        var after = Decoded(EmailContactHtmlRenderer.SampleBlock(Policy(campus: true), EmailLanguages.Vi));

        Assert.NotEqual(before, after);
        Assert.DoesNotContain("FPTU Hà Nội", before);
        Assert.Contains("FPTU Hà Nội", after);
    }

    [Fact]
    public void A_no_contact_policy_renders_no_block_at_all()
    {
        var html = EmailContactHtmlRenderer.SampleBlock(
            Policy(EmailContactRequirement.NONE), EmailLanguages.Vi);

        Assert.Equal(string.Empty, html);
    }

    /// <summary>
    /// Both channels hidden renders nothing rather than a heading over an unreachable name — the same
    /// rule the real send applies, reached here because the preview goes through the same renderer.
    /// </summary>
    [Fact]
    public void A_policy_with_no_channel_renders_nothing()
    {
        var html = EmailContactHtmlRenderer.SampleBlock(
            Policy(email: false, phone: false), EmailLanguages.Vi);

        Assert.Equal(string.Empty, html);
    }

    /// <summary>
    /// The sample must not read as a real person. Addresses use the reserved .invalid TLD (RFC 2606),
    /// which can never resolve, and the name says it is sample data — a preview that looks real is one
    /// screenshot away from being taken for a recipient's actual contact details.
    /// </summary>
    [Fact]
    public void The_contact_sample_is_obviously_not_real_data()
    {
        foreach (var lang in new[] { EmailLanguages.Vi, EmailLanguages.En })
        {
            var html = EmailContactHtmlRenderer.SampleBlock(Policy(), lang);

            Assert.Contains(".invalid", html);
            Assert.DoesNotContain("@fpt.edu.vn", html);
            Assert.DoesNotContain("@gmail.com", html);
        }
    }

    [Fact]
    public async Task The_preview_query_renders_from_the_draft_policy_it_is_given()
    {
        var handler = new PreviewEmailContactBlockQueryHandler();

        var withPhone = await handler.Handle(new PreviewEmailContactBlockQuery
        {
            TemplateCode = SystemEmailTemplates.VisitParticipantInvitation,
            Language = EmailLanguages.Vi,
            Requirement = nameof(EmailContactRequirement.REQUIRED),
            ContactSource = nameof(EmailContactSource.HOST),
            ShowEmail = true,
            ShowPhone = true,
        }, CancellationToken.None);

        var withoutPhone = await handler.Handle(new PreviewEmailContactBlockQuery
        {
            TemplateCode = SystemEmailTemplates.VisitParticipantInvitation,
            Language = EmailLanguages.Vi,
            Requirement = nameof(EmailContactRequirement.REQUIRED),
            ContactSource = nameof(EmailContactSource.HOST),
            ShowEmail = true,
            ShowPhone = false,
        }, CancellationToken.None);

        Assert.True(withPhone.RendersBlock);
        Assert.Contains("0900 000 000", withPhone.Html);
        Assert.DoesNotContain("0900 000 000", withoutPhone.Html);
    }

    /// <summary>
    /// A half-finished draft — both channels momentarily unticked — answers with an empty block, NOT a
    /// refusal. Failing here would clear the pane with an error while the operator is still choosing;
    /// the refusal belongs at save, where the update command already applies it.
    /// </summary>
    [Fact]
    public async Task A_contradictory_draft_previews_empty_rather_than_failing()
    {
        var handler = new PreviewEmailContactBlockQueryHandler();

        var result = await handler.Handle(new PreviewEmailContactBlockQuery
        {
            TemplateCode = SystemEmailTemplates.VisitParticipantInvitation,
            Requirement = nameof(EmailContactRequirement.REQUIRED),
            ContactSource = nameof(EmailContactSource.HOST),
            ShowEmail = false,
            ShowPhone = false,
        }, CancellationToken.None);

        Assert.False(result.RendersBlock);
        Assert.Equal(string.Empty, result.Html);
    }

    /// <summary>A heading is text: markup is stripped in the preview exactly as it is on save.</summary>
    [Fact]
    public async Task A_heading_containing_markup_is_stripped_in_the_preview()
    {
        var handler = new PreviewEmailContactBlockQueryHandler();

        var result = await handler.Handle(new PreviewEmailContactBlockQuery
        {
            TemplateCode = SystemEmailTemplates.VisitParticipantInvitation,
            Requirement = nameof(EmailContactRequirement.REQUIRED),
            ContactSource = nameof(EmailContactSource.HOST),
            ShowEmail = true,
            HeadingVi = "<script>alert(1)</script>Liên hệ",
        }, CancellationToken.None);

        Assert.DoesNotContain("<script", result.Html, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Liên hệ", Decoded(result.Html));
    }

    // ── Rig ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// The same selection <c>GetEmailTemplateContractQueryHandler</c> and
    /// <c>PreviewEmailTemplateQueryHandler</c> both make. Kept in one place here so a divergence between
    /// those two shows up as a failure rather than as two previews that disagree.
    /// </summary>
    /// <summary>
    /// Vietnamese text survives HTML encoding only partly: <c>WebUtility.HtmlEncode</c> turns
    /// U+00A0–U+00FF into numeric entities, so <c>ê</c> becomes <c>&amp;#234;</c> while <c>ầ</c>
    /// (U+1EA7) passes through. A raw substring assertion on Vietnamese therefore fails on text that is
    /// present. Decode first.
    /// </summary>
    private static string Decoded(string html) => System.Net.WebUtility.HtmlDecode(html);

    /// <summary>
    /// The PRODUCTION helper, not a copy of it. Both the contract the editor fetches and the preview
    /// modal call this same method, so a test written against a private re-implementation could not tell
    /// them apart — which is how the two came to be able to show different buttons for one template.
    /// </summary>
    private static string DisabledActionBlockFor(string code, string language)
        => EmailActionTemplates.DisabledBlockFor(code, language);
}
