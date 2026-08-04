using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Contact;
using PEMS.Infrastructure.Email;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The last line: nothing goes out carrying <c>{{contactInformationBlock}}</c> under a policy that says
/// the block is hidden (visibility prompt §11).
///
/// <para>
/// <b>The defect.</b> <c>SystemEmailDispatcher</c> merged the contact block into the trusted-block map
/// unconditionally — as the EMPTY STRING when the resolved policy rendered nothing. A body under a NONE
/// policy therefore had its placeholder silently replaced with nothing and the mail went out looking
/// correct. That is the worse of the two available failures: substituting empty makes a configuration
/// mistake invisible, so the operator who thought they had switched the block off has no way to learn
/// that a body somewhere still asks for one, and no log, history row or screen says otherwise.
/// </para>
/// <para>
/// <b>What is deliberately NOT guarded.</b> An OPTIONAL policy that resolves no contact also renders
/// nothing, and that must keep working: the words never promised a contact, so a mail without one is
/// still true. Only NONE — an explicit decision that this template shows no block — makes the
/// placeholder's presence a contradiction. The two are asserted side by side below, because a guard that
/// could not tell them apart would break every optional template on a visit with no Host assigned.
/// </para>
/// </summary>
public sealed class EmailContactBlockRuntimeGuardTests
{
    /// <summary>A template that MAY carry the block, so the level is the only thing under test.</summary>
    private const string Code = SystemEmailTemplates.AccountRoleChanged;

    private static bool CanUseDatabase()
    {
        try { EmailEvidenceHarness.RequireDb(); return true; }
        catch { return false; }
    }

    private static readonly string Marker = EmailContactBlockText.Marker;

    /// <summary>
    /// A render request against the stored row, with the variables the template declares.
    ///
    /// <para>
    /// The contact block is NOT supplied, which is the point: under a hidden policy the dispatcher no
    /// longer supplies it, and this reproduces that state at the renderer's own boundary. The other two
    /// blocks are supplied so a template that writes them does not fail for an unrelated reason.
    /// </para>
    /// </summary>
    private static async Task<EmailRenderRequest> RequestAsync(
        PEMS.Infrastructure.Persistence.ApplicationDbContext db,
        bool contactBlockForbidden)
    {
        var row = await db.EmailTemplates.AsNoTracking().FirstAsync(t => t.TemplateCode == Code);

        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in EmailTemplateVariables.ParseDeclared(row.VariablesText))
            variables[name] = EmailVariableCatalog.Sample(name, EmailLanguages.Vi);

        return new EmailRenderRequest(Code, EmailLanguages.Vi, variables, new Dictionary<string, string>
        {
            [EmailTrustedBlocks.SetupSummaryBlock] =
                EmailComposition.DisabledSetupSummaryBlock(EmailLanguages.Vi),
        })
        {
            ContactBlockForbidden = contactBlockForbidden,
        };
    }

    /// <summary>
    /// A body that still asks for the block, under a policy that says there is none, is refused —
    /// with the code that names the contradiction rather than a generic unresolved-placeholder.
    /// </summary>
    [Fact]
    public async Task A_hidden_policy_refuses_a_body_that_still_carries_the_block()
    {
        if (!CanUseDatabase()) return;
        await using var db = EmailEvidenceHarness.NewContext();

        await EmailEvidenceHarness.WithTemplateAsync(db, Code,
            row => row.BodyVi = "<p>Xin chào.</p>" + Marker,
            async () =>
            {
                var request = await RequestAsync(db, contactBlockForbidden: true);

                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    new EmailTemplateRenderer(db).RenderAsync(request));

                Assert.Equal(EmailErrorCodes.ContactBlockNotAllowedWhenHidden, ex.ErrorCode);
                // The message names both repairs, because either one fixes it and only the operator can
                // decide which.
                Assert.Contains("Không hiển thị", ex.Message, StringComparison.Ordinal);
                Assert.Contains("xóa khối", ex.Message, StringComparison.OrdinalIgnoreCase);
            });
    }

    /// <summary>
    /// The same refusal must NOT be turned into a silent empty substitution, which is what the dispatcher
    /// used to do. Asserted as "the render throws" rather than "the body is clean", because a clean body
    /// is exactly what the defect produced.
    /// </summary>
    [Fact]
    public async Task A_hidden_policy_does_not_quietly_blank_the_placeholder()
    {
        if (!CanUseDatabase()) return;
        await using var db = EmailEvidenceHarness.NewContext();

        await EmailEvidenceHarness.WithTemplateAsync(db, Code,
            row => row.BodyVi = "<p>Xin chào.</p>" + Marker,
            async () =>
            {
                var request = await RequestAsync(db, contactBlockForbidden: true);

                // Supplying the empty block as well — the exact shape the dispatcher used to build — must
                // still not get the message through: the guard reads the STORED body, before substitution.
                var withEmptyBlock = new Dictionary<string, string>(
                    request.TrustedHtmlBlocks!, StringComparer.Ordinal)
                {
                    [EmailTrustedBlocks.ContactInformationBlock] = string.Empty,
                };

                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    new EmailTemplateRenderer(db).RenderAsync(
                        new EmailRenderRequest(Code, EmailLanguages.Vi, request.Variables, withEmptyBlock)
                        {
                            ContactBlockForbidden = true,
                        }));
            });
    }

    /// <summary>
    /// A hidden policy over a body that does NOT ask for the block is an ordinary send. The guard fires on
    /// the contradiction, not on the level.
    /// </summary>
    [Fact]
    public async Task A_hidden_policy_sends_normally_when_the_body_does_not_ask_for_the_block()
    {
        if (!CanUseDatabase()) return;
        await using var db = EmailEvidenceHarness.NewContext();

        await EmailEvidenceHarness.WithTemplateAsync(db, Code,
            row => row.BodyVi = "<p>Xin chào {{fullName}}.</p>",
            async () =>
            {
                var result = await new EmailTemplateRenderer(db).RenderAsync(
                    await RequestAsync(db, contactBlockForbidden: true));

                Assert.DoesNotContain(Marker, result.Body, StringComparison.Ordinal);
                Assert.False(string.IsNullOrWhiteSpace(result.Body));
            });
    }

    /// <summary>
    /// The case that must keep working: OPTIONAL with nothing resolved. The block IS supplied — as the
    /// empty string — and substituting it away is the intended outcome, because the words never promised
    /// a contact.
    /// </summary>
    [Fact]
    public async Task An_optional_policy_that_resolved_no_contact_still_sends()
    {
        if (!CanUseDatabase()) return;
        await using var db = EmailEvidenceHarness.NewContext();

        await EmailEvidenceHarness.WithTemplateAsync(db, Code,
            row => row.BodyVi = "<p>Xin chào {{fullName}}.</p>" + Marker,
            async () =>
            {
                var request = await RequestAsync(db, contactBlockForbidden: false);

                var blocks = new Dictionary<string, string>(
                    request.TrustedHtmlBlocks!, StringComparer.Ordinal)
                {
                    [EmailTrustedBlocks.ContactInformationBlock] = string.Empty,
                };

                var result = await new EmailTemplateRenderer(db).RenderAsync(
                    new EmailRenderRequest(Code, EmailLanguages.Vi, request.Variables, blocks));

                // It went out, and no placeholder reached the recipient.
                Assert.DoesNotContain(Marker, result.Body, StringComparison.Ordinal);
                Assert.Contains("Xin chào", result.Body, StringComparison.Ordinal);
            });
    }

    /// <summary>
    /// Withholding the block on a body that asks for it, WITHOUT the forbidden flag, still fails closed —
    /// on the unresolved-placeholder guard. Asserted so the two guards are known to overlap rather than to
    /// depend on one another: no arrangement of flags lets literal braces reach a recipient.
    /// </summary>
    [Fact]
    public async Task A_body_asking_for_a_block_nobody_supplied_never_reaches_a_recipient()
    {
        if (!CanUseDatabase()) return;
        await using var db = EmailEvidenceHarness.NewContext();

        await EmailEvidenceHarness.WithTemplateAsync(db, Code,
            row => row.BodyVi = "<p>Xin chào.</p>" + Marker,
            async () =>
            {
                var request = await RequestAsync(db, contactBlockForbidden: false);

                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    new EmailTemplateRenderer(db).RenderAsync(request));

                Assert.Equal(EmailErrorCodes.TemplateUnresolvedPlaceholder, ex.ErrorCode);
            });
    }
}
