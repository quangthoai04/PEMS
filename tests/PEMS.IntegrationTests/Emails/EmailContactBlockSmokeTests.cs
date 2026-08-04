using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Contact;
using PEMS.Application.Emails.Queries.PreviewEmailTemplate;
using PEMS.Domain.Enums;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The runtime smoke both prompts ask for, over the four templates they name — with outbound delivery
/// disabled (nothing here reaches a mail server; the shared harness writes to a pickup directory).
///
/// <para>
/// The four are chosen to cover the three capability states and both directions of the visibility rule:
/// <c>ACCOUNT_EMAIL_CONFIRMATION</c> can never carry the block, <c>ACCOUNT_ROLE_CHANGED</c> may and is
/// the operator's choice, <c>ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE</c> and
/// <c>VISIT_PARTICIPANT_INVITATION</c> ship REQUIRED because their wording tells the reader to make
/// contact.
/// </para>
/// <para>
/// What it asserts is the one thing a recipient must never see: a literal placeholder. Both the preview
/// and the render are checked, because they used to disagree — the preview handed every supported
/// template a stand-in card regardless of the level, so an operator could approve a preview showing a
/// contact block on a template whose policy said "Không hiển thị".
/// </para>
/// </summary>
public sealed class EmailContactBlockSmokeTests
{
    public static IEnumerable<object[]> TheFourTemplates() => new[]
    {
        new object[] { SystemEmailTemplates.AccountEmailConfirmation },
        new object[] { SystemEmailTemplates.AccountRoleChanged },
        new object[] { SystemEmailTemplates.AccountPendingEmailChangedOldNotice },
        new object[] { SystemEmailTemplates.VisitParticipantInvitation },
    };

    private sealed class Operator : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId => 1;
        public string? Email => "ho-operator@pems.test";
        public ulong? RoleId => null;
        public string? RoleCode => "HO";
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static PreviewEmailTemplateQueryHandler Preview(ApplicationDbContext db)
        => new(db, new Operator(), new EmailTemplateRenderer(db), new EmailContactPolicyStore(db));

    private static async Task<Dictionary<string, string>> ContextForAsync(ApplicationDbContext db, string code)
    {
        var declared = await db.EmailTemplates.AsNoTracking()
            .Where(t => t.TemplateCode == code)
            .Select(t => t.VariablesText)
            .FirstAsync();

        return EmailTemplateVariables.ParseDeclared(declared)
            .ToDictionary(name => name, name => $"[{name}]", StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(TheFourTemplates))]
    public async Task Neither_language_previews_with_a_literal_contact_placeholder(string code)
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        foreach (var language in new[] { EmailLanguages.Vi, EmailLanguages.En })
        {
            var result = await Preview(db).Handle(
                new PreviewEmailTemplateQuery(code, await ContextForAsync(db, code), language),
                CancellationToken.None);

            Assert.DoesNotContain(EmailContactBlockText.Marker, result.BodyHtml, StringComparison.Ordinal);
            Assert.DoesNotContain(EmailContactBlockText.Marker, result.Subject, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(result.BodyHtml), $"{code}/{language}: empty body");
        }
    }

    /// <summary>
    /// The preview shows the contact card only where the effective policy would render one — the fix for
    /// §10. A stand-in card over a policy of "Không hiển thị" tells an operator their setting did not take
    /// effect; a missing card on a REQUIRED template tells them the block has been lost from the body.
    /// </summary>
    [Theory]
    [MemberData(nameof(TheFourTemplates))]
    public async Task The_preview_shows_the_contact_card_exactly_where_the_policy_renders_one(string code)
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var requirement = await EffectiveContactRequirement
            .ResolveAsync(new EmailContactPolicyStore(db), code, CancellationToken.None);

        var row = await db.EmailTemplates.AsNoTracking().FirstAsync(t => t.TemplateCode == code);
        var bodyAsksForIt = EmailContactBlockText.Contains(row.BodyVi);

        var result = await Preview(db).Handle(
            new PreviewEmailTemplateQuery(code, await ContextForAsync(db, code), EmailLanguages.Vi),
            CancellationToken.None);

        // Matched against the stand-in's OWN wording rather than a heading guessed here: a preview has no
        // visit, so what it substitutes is `EmailContactHtmlRenderer.DisabledBlock` — a dashed box saying
        // the system will fill the contact in. Hard-coding a different phrase would make this test pass or
        // fail on the copywriting rather than on the rule.
        var standIn = EmailContactHtmlRenderer.DisabledBlock(EmailLanguages.Vi);
        var showsCard = result.BodyHtml.Contains(standIn, StringComparison.Ordinal);
        var shouldShow = requirement != EmailContactRequirement.NONE
                         && EmailContactCapabilities.Supports(code)
                         && bodyAsksForIt;

        Assert.True(showsCard == shouldShow,
            $"{code}: level={requirement}, body asks for the block={bodyAsksForIt}, "
            + $"but the preview {(showsCard ? "shows" : "does not show")} a contact card.");
    }

    /// <summary>
    /// The seeded catalog is internally consistent: no template ships a body carrying the block under a
    /// policy that hides it.
    ///
    /// <para>
    /// Asserted over the WHOLE catalog rather than the four, because this is the invariant the new
    /// send-time guard rests on. If a shipped template ever violated it, the guard would refuse a send
    /// that has always worked — and the fault would be in the seed, which is what this names.
    /// </para>
    /// </summary>
    [Fact]
    public async Task No_shipped_template_hides_the_block_while_its_body_still_asks_for_one()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var store = new EmailContactPolicyStore(db);
        var offenders = new List<string>();

        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var requirement = await EffectiveContactRequirement.ResolveAsync(store, code, CancellationToken.None);
            if (requirement != EmailContactRequirement.NONE) continue;

            var row = await db.EmailTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TemplateCode == code);

            if (row is null) continue;

            if (EmailContactBlockText.Contains(row.BodyVi) || EmailContactBlockText.Contains(row.BodyEn))
                offenders.Add(code);
        }

        Assert.True(offenders.Count == 0,
            "These templates hide the contact block but their stored body still contains it, so every "
            + "send of them is now refused: " + string.Join(", ", offenders));
    }
}
