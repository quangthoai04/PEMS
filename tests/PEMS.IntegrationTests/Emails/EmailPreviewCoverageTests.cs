using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Queries.PreviewEmailTemplate;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// G11 / R-106 — every active template can be previewed, in both languages, without a live token
/// anywhere in the output.
///
/// <para>
/// The gap this closes: templates put <c>{{actionBlock}}</c> in their body while the preview supplied no
/// trusted block, so the placeholder survived rendering and the renderer refused the message — an
/// operator editing one got <c>EMAIL_TEMPLATE_UNRESOLVED_PLACEHOLDER</c> instead of a preview.
/// </para>
/// <para>
/// It was first closed by giving unregistered templates a neutral block. Since 2026-08-03 it is closed
/// from the other end instead: every body that writes the placeholder has an entry in
/// <see cref="EmailActionTemplates"/>, so the preview always has real buttons to show and the contract
/// refuses a body that asks for a block nothing will fill.
/// </para>
/// <para>
/// The tests are a theory over the catalog rather than a hand-written list, so a template added later is
/// covered the day it is added rather than the day somebody remembers this file.
/// </para>
/// </summary>
public sealed class EmailPreviewCoverageTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("g11-preview@partner.example.com");

    public void Dispose() => _h.Dispose();

    private sealed class Operator : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId => 1;
        public string? Email => "operator@pems.test";
        public ulong? RoleId => null;
        public string? RoleCode => "HO";
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static PreviewEmailTemplateQueryHandler Preview(ApplicationDbContext db)
        => new(db, new Operator(), new EmailTemplateRenderer(db),
               EmailEvidenceHarness.Senders(db),
               EmailEvidenceHarness.PreviewTokens());

    /// <summary>Every active code in the catalog, once per language: 30 × 2.</summary>
    public static IEnumerable<object[]> EveryTemplateAndLanguage()
        => SystemEmailTemplates.AllCodes
            .OrderBy(c => c, StringComparer.Ordinal)
            .SelectMany(code => new[]
            {
                new object[] { code, EmailLanguages.Vi },
                new object[] { code, EmailLanguages.En },
            });

    /// <summary>
    /// A value for each variable the template declares. Read from <c>variables_text</c> rather than from
    /// a hand-maintained map: the renderer refuses both a missing and an unknown variable, so anything
    /// less than the declared set exactly would fail for a reason that has nothing to do with previewing.
    /// </summary>
    private static async Task<Dictionary<string, string>> ContextForAsync(ApplicationDbContext db, string code)
    {
        var declared = await db.EmailTemplates.AsNoTracking()
            .Where(t => t.TemplateCode == code)
            .Select(t => t.VariablesText)
            .FirstAsync();

        return EmailTemplateVariables.ParseDeclared(declared)
            .ToDictionary(name => name, name => $"[{name}]", StringComparer.Ordinal);
    }

    // ── The closure itself ──────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(EveryTemplateAndLanguage))]
    public async Task Every_active_template_previews_in_both_languages(string code, string language)
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var result = await Preview(db).Handle(
            new PreviewEmailTemplateQuery(code, await ContextForAsync(db, code), language), default);

        Assert.Equal(code, result.TemplateCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Subject), $"{code}/{language}: empty subject");
        Assert.False(string.IsNullOrWhiteSpace(result.BodyHtml), $"{code}/{language}: empty body");

        // The point of the preview is that nothing survives wearing braces — the same rule the send obeys.
        Assert.DoesNotContain("{{", result.BodyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", result.Subject, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every body that writes <c>{{actionBlock}}</c> has a registry entry, and previews as an action
    /// template with a real block.
    ///
    /// <para>
    /// This replaces the earlier "the nine unregistered ones preview as action templates anyway" test.
    /// That neutral-fallback rule is gone (2026-08-03): a template with no entry has no send path that
    /// fills the block, so the placeholder is now refused in the body rather than papered over in the
    /// preview. The two halves are asserted together on purpose — the first is what the contract in
    /// <see cref="EmailTemplateContracts"/> relies on, and asserting it without the second would pass on
    /// a catalog where nobody uses the block at all.
    /// </para>
    /// </summary>
    [Fact]
    public async Task No_template_uses_the_action_block_without_a_registry_entry()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var usingActionBlock = (await db.EmailTemplates.AsNoTracking()
                .Where(t => t.BodyVi!.Contains("{{actionBlock}}") || t.BodyEn!.Contains("{{actionBlock}}"))
                .Select(t => t.TemplateCode)
                .ToListAsync())
            .Where(c => SystemEmailTemplates.Find(c) is not null)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        // The catalog does use it — otherwise the loop below would assert nothing.
        Assert.NotEmpty(usingActionBlock);

        var unregistered = usingActionBlock
            .Where(code => EmailActionTemplates.For(code) is null)
            .ToList();

        Assert.True(unregistered.Count == 0,
            "These bodies carry {{actionBlock}} with no entry in EmailActionTemplates, so no send path "
            + "fills it and the contract refuses to save them: " + string.Join(", ", unregistered));

        foreach (var code in usingActionBlock)
        {
            var result = await Preview(db).Handle(
                new PreviewEmailTemplateQuery(code, await ContextForAsync(db, code), EmailLanguages.Vi), default);

            Assert.True(result.IsActionTemplate, $"{code}: an action area was rendered but not reported");
            Assert.False(string.IsNullOrWhiteSpace(result.LockedActionBlockHtml), $"{code}: no block to show");
            Assert.NotNull(result.SystemActionDescription);

            // The words on the preview's button are the registry's, not a generic stand-in.
            Assert.Equal(
                EmailActionTemplates.DisabledBlockFor(code, EmailLanguages.Vi),
                result.LockedActionBlockHtml);

            // The editable half must not contain the action area: an operator saving it back would
            // otherwise write the preview's own markup into the template.
            Assert.DoesNotContain("PEMS_ACTION_BLOCK", result.BodyHtml!, StringComparison.Ordinal);
        }
    }

    // ── Nothing in a preview is live ────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(EveryTemplateAndLanguage))]
    public async Task No_preview_contains_a_clickable_link_a_token_or_a_script(string code, string language)
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var result = await Preview(db).Handle(
            new PreviewEmailTemplateQuery(code, await ContextForAsync(db, code), language), default);

        var whole = result.Subject + result.BodyHtml + (result.LockedActionBlockHtml ?? string.Empty);

        // A disabled block is made of <span>, never <a>: a preview button must not be pressable at all,
        // and a fake href is a link somebody will eventually click.
        if (result.LockedActionBlockHtml is { } block)
        {
            Assert.DoesNotContain("<a ", block, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("href", block, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("javascript:", whole, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", whole, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror=", whole, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick=", whole, StringComparison.OrdinalIgnoreCase);

        // The two shapes a real one-time credential takes in this system.
        Assert.DoesNotContain("/public/email-actions/", whole, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confirm-email?token=", whole, StringComparison.OrdinalIgnoreCase);

        // Substituted values are the placeholder names in brackets, so any bare six-digit run would be
        // a code that came from the template itself rather than from this test.
        Assert.DoesNotMatch(new Regex(@"(?<!\d)\d{6}(?!\d)"), StripHtml(whole));
    }

    private static string StripHtml(string html) => Regex.Replace(html, "<[^>]+>", " ");

    // ── The send path is unchanged ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The preview's inert block must not have made the SEND tolerant of a missing action. A real send of
    /// an action template with no trusted block still refuses, exactly as before — otherwise a message
    /// would go out with a button that leads nowhere.
    /// </summary>
    [Fact]
    public async Task Send_still_refuses_an_action_template_with_no_action_data()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        var code = EmailActionTemplates.ParticipantInvitation;
        var renderer = new EmailTemplateRenderer(db);
        var context = await ContextForAsync(db, code);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => renderer.RenderAsync(
            new EmailRenderRequest(code, EmailLanguages.Vi, context, TrustedHtmlBlocks: null)));

        Assert.Equal(EmailErrorCodes.TemplateUnresolvedPlaceholder, error.ErrorCode);
    }

    /// <summary>
    /// The same holds for a template the registry does NOT know about — the preview fallback is preview
    /// only, and does not leak into rendering for delivery.
    ///
    /// <para>
    /// The unregistered-but-uses-the-block case no longer exists in the catalog (see
    /// <see cref="No_template_uses_the_action_block_without_a_registry_entry"/>), so the row is CREATED
    /// here for the duration of the test rather than looked up. Picking whichever catalogued template
    /// happened to be in that state — and skipping when none was — is how this test came to assert
    /// nothing at all: it returned green on the very change that emptied the set.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Send_still_refuses_an_unregistered_action_template_with_no_action_data()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        // A plain template with no action spec, and none is added for it.
        const string code = SystemEmailTemplates.AccountRoleChanged;
        Assert.Null(EmailActionTemplates.For(code));

        var renderer = new EmailTemplateRenderer(db);
        var context = await ContextForAsync(db, code);

        await EmailEvidenceHarness.WithTemplateAsync(db, code,
            row => row.BodyVi += "{{actionBlock}}",
            async () =>
            {
                var error = await Assert.ThrowsAsync<BusinessRuleException>(() => renderer.RenderAsync(
                    new EmailRenderRequest(code, EmailLanguages.Vi, context, TrustedHtmlBlocks: null)));

                Assert.Equal(EmailErrorCodes.TemplateUnresolvedPlaceholder, error.ErrorCode);
            });
    }

    /// <summary>
    /// A template edited in the database is previewed from the edit on the very next call — no cache, no
    /// restart. The G11 fallback must not have introduced one.
    /// </summary>
    [Fact]
    public async Task A_hot_edit_shows_up_in_the_next_preview()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();

        const string code = SystemEmailTemplates.AccountRoleChanged;
        var context = await ContextForAsync(db, code);
        var marker = "G11-HOT-" + Guid.NewGuid().ToString("N")[..8];

        await EmailEvidenceHarness.WithTemplateAsync(db, code,
            row => row.SubjectVi = marker + " {{fullName}}",
            async () =>
            {
                var result = await Preview(db).Handle(
                    new PreviewEmailTemplateQuery(code, context, EmailLanguages.Vi), default);
                Assert.StartsWith(marker, result.Subject, StringComparison.Ordinal);
            });

        var restored = await Preview(db).Handle(
            new PreviewEmailTemplateQuery(code, context, EmailLanguages.Vi), default);
        Assert.DoesNotContain(marker, restored.Subject, StringComparison.Ordinal);
    }
}
