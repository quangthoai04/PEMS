using System.Linq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The policy of the setup-progress template, asserted where it is decided rather than where it is used.
///
/// <para>
/// The whole flow rests on this template being the opposite of the invitation it sits next to: no token,
/// so a recipient list the Host controls is safe; caller-controlled, so CC is legal at all. If somebody
/// later "tidies" it into the VISIT_PARTICIPANT family the send would start refusing every CC, and the
/// failure would surface as an unexplained validation error on a screen far from the change.
/// </para>
/// </summary>
public class VisitSetupProgressTemplateTests
{
    private static SystemEmailTemplate Template =>
        SystemEmailTemplates.Find(SystemEmailTemplates.VisitSetupProgressUpdate)!;

    [Fact]
    public void The_template_is_registered()
    {
        Assert.NotNull(SystemEmailTemplates.Find(SystemEmailTemplates.VisitSetupProgressUpdate));
        Assert.Equal("VISIT_SETUP_PROGRESS_UPDATE", SystemEmailTemplates.VisitSetupProgressUpdate);
    }

    [Fact]
    public void It_is_caller_controlled_so_the_host_owns_the_recipient_list()
    {
        Assert.Equal(EmailRecipientPolicy.CallerControlled, Template.RecipientPolicy);
        Assert.True(Template.AllowsCopies);
        Assert.False(Template.RequiresPerRecipientSend);
    }

    [Fact]
    public void It_carries_no_sensitive_action_and_no_secret()
    {
        Assert.False(Template.HasSensitiveAction);
        Assert.False(SensitiveEmailVariables.CarriesSecret(Template));
        Assert.Empty(SensitiveEmailVariables.DeclaredBy(Template));
    }

    [Fact]
    public void It_declares_exactly_the_six_visit_facts_and_nothing_token_shaped()
    {
        Assert.Equal(
            new[] { "campusName", "delegationName", "hostEmail", "hostName", "plannedEnd", "plannedStart" },
            Template.DeclaredVariables.OrderBy(v => v).ToArray());
    }

    [Fact]
    public void Its_purpose_is_report_because_it_distributes_a_document()
    {
        Assert.Equal(EmailTemplatePurposes.Report, Template.Purpose);
    }

    [Fact]
    public void The_contract_lets_the_compose_screen_offer_cc_and_bcc()
    {
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.VisitSetupProgressUpdate)!;

        Assert.True(contract.AllowCc);
        Assert.True(contract.AllowBcc);
        Assert.False(contract.CarriesSecret);
        Assert.False(contract.RequiresActionBlock);
        Assert.Equal(EmailTemplateContracts.ClassificationStandard, contract.SecurityClassification);
    }

    /// <summary>
    /// The invitation is the template this flow deliberately did NOT reuse. Asserting its policy here
    /// keeps the contrast visible: it is single-recipient because it carries a one-time accept/decline
    /// link, and putting two people on one such message hands each of them the other's link.
    /// </summary>
    [Fact]
    public void The_invitation_template_it_replaces_remains_single_recipient()
    {
        var invitation = SystemEmailTemplates.Find(SystemEmailTemplates.VisitParticipantInvitation)!;

        Assert.Equal(EmailRecipientPolicy.SingleRecipientNoCopies, invitation.RecipientPolicy);
        Assert.False(invitation.AllowsCopies);
        Assert.True(invitation.HasSensitiveAction);
    }

    [Fact]
    public void The_shipped_default_content_exists_in_both_languages_and_uses_every_declared_variable()
    {
        var shipped = EmailTemplateDefaults.For(SystemEmailTemplates.VisitSetupProgressUpdate);
        Assert.NotNull(shipped);

        Assert.False(string.IsNullOrWhiteSpace(shipped!.SubjectVi));
        Assert.False(string.IsNullOrWhiteSpace(shipped.BodyVi));
        Assert.False(string.IsNullOrWhiteSpace(shipped.SubjectEn));
        Assert.False(string.IsNullOrWhiteSpace(shipped.BodyEn));

        var used = EmailTemplateVariables.ExtractPlaceholders(
            shipped.SubjectVi, shipped.BodyVi, shipped.SubjectEn, shipped.BodyEn);

        Assert.Equal(
            Template.DeclaredVariables.OrderBy(v => v).ToArray(),
            used.Where(v => !EmailTrustedBlocks.All.Contains(v)).OrderBy(v => v).ToArray());
    }

    /// <summary>
    /// The tables are the message. Everything the template itself writes is a covering sentence, so a
    /// body without this placeholder is an email announcing an update that contains no update — and
    /// because a trusted block is not a variable, nothing at send time would notice.
    /// </summary>
    [Fact]
    public void The_shipped_default_carries_the_setup_summary_block_in_both_languages()
    {
        var shipped = EmailTemplateDefaults.For(SystemEmailTemplates.VisitSetupProgressUpdate)!;

        Assert.Contains("{{setupSummaryBlock}}", shipped.BodyVi);
        Assert.Contains("{{setupSummaryBlock}}", shipped.BodyEn);
        // A trusted block in a subject would be a table in a subject line.
        Assert.DoesNotContain("{{setupSummaryBlock}}", shipped.SubjectVi);
        Assert.DoesNotContain("{{setupSummaryBlock}}", shipped.SubjectEn);
    }

    [Fact]
    public void The_contract_requires_the_setup_summary_block_so_an_operator_cannot_delete_the_tables()
    {
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.VisitSetupProgressUpdate)!;

        Assert.Contains(EmailTrustedBlocks.SetupSummaryBlock, contract.AllowedVariables);
        Assert.Contains(EmailTrustedBlocks.SetupSummaryBlock, contract.RequiredVariables);

        // It is the backend's block, so the editor must not offer it as a variable to fill in, and the
        // preview must not pass it as one — SystemEmailContent rejects a caller-supplied trusted block.
        Assert.DoesNotContain(contract.Variables, v => v.Name == EmailTrustedBlocks.SetupSummaryBlock);
        Assert.DoesNotContain(
            EmailTrustedBlocks.SetupSummaryBlock,
            EmailTemplateContracts.PreviewSample(SystemEmailTemplates.VisitSetupProgressUpdate, "vi").Keys);
    }

    /// <summary>
    /// A trusted block is allowed in the body but must never appear in <c>variables_text</c>, which is
    /// the operator-facing list of fields they may fill in. Both template writers derive that column
    /// from the contract's allowed set, so admitting a new block to that set silently added it to the
    /// column — advertising a field nobody may supply, and tripping the E4 check in 03_verify.sql the
    /// next time the catalog was verified.
    /// </summary>
    [Fact]
    public void The_block_is_never_written_into_the_operator_facing_variable_list()
    {
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.VisitSetupProgressUpdate)!;

        // This is the exact expression both the update and restore handlers use to build the column.
        var variablesText = string.Join(",", contract.AllowedVariables
            .Where(v => !EmailTrustedBlocks.All.Contains(v)));

        Assert.DoesNotContain("setupSummaryBlock", variablesText);
        Assert.DoesNotContain("actionBlock", variablesText);
        Assert.Equal("campusName,delegationName,hostEmail,hostName,plannedEnd,plannedStart",
            string.Join(",", variablesText.Split(',').OrderBy(v => v, StringComparer.Ordinal)));
    }

    [Fact]
    public void Removing_the_block_from_the_body_is_reported_as_an_error()
    {
        var shipped = EmailTemplateDefaults.For(SystemEmailTemplates.VisitSetupProgressUpdate)!;
        var stripped = shipped.BodyVi.Replace("{{setupSummaryBlock}}", string.Empty);

        var issues = EmailTemplateContentValidator.Validate(
            EmailTemplateContracts.For(SystemEmailTemplates.VisitSetupProgressUpdate)!,
            shipped.SubjectVi, stripped, shipped.SubjectEn, shipped.BodyEn);

        Assert.Contains(issues, i =>
            i.VariableName == EmailTrustedBlocks.SetupSummaryBlock && i.IsError);
    }

    [Fact]
    public void The_shipped_default_embeds_no_action_or_token_url()
    {
        var shipped = EmailTemplateDefaults.For(SystemEmailTemplates.VisitSetupProgressUpdate)!;
        var content = string.Concat(shipped.BodyVi, shipped.BodyEn, shipped.SubjectVi, shipped.SubjectEn);

        Assert.DoesNotContain("/public/email-actions/", content);
        Assert.DoesNotContain("{{actionBlock}}", content);
        Assert.DoesNotContain("token=", content);
    }
}
