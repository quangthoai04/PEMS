using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>One variable as the template-management screen needs to show it.</summary>
public sealed record EmailContractVariable(
    string Name,
    string Label,
    string Sample,
    bool Required,
    bool Sensitive,
    bool ForbiddenInSubject);

/// <summary>
/// Everything the editor, the validator and the preview need to know about ONE template, resolved from
/// the backend registry rather than assembled per screen.
///
/// <para>
/// <b>Variables and system blocks are two different things and are carried separately.</b> A variable is
/// a value an operator may write and the caller supplies at send time; a system block is markup the
/// BACKEND builds and injects, which an operator may position but never author or fill in. They were
/// previously merged into one <c>AllowedVariables</c> list so that the editor would not reject a body
/// containing <c>{{actionBlock}}</c> — which worked, but made every question about a block get answered
/// by variable machinery. The visible cost was that a legal block in a legal place could still be
/// reported as <c>EMAIL_TEMPLATE_VARIABLE_UNKNOWN</c> ("biến không tồn tại") whenever the two lists
/// disagreed, which names the wrong thing, points at the wrong fix, and is not even true: a block is not
/// a variable and its absence from a variable list says nothing about whether it is allowed.
/// </para>
/// </summary>
/// <param name="AllowedVariables">
/// Data variables only — never a trusted block. This is the list a placeholder is checked against before
/// being called unknown.
/// </param>
/// <param name="RequiredSystemBlocks">
/// Blocks whose removal from the body makes the message not worth sending. Reported under the code that
/// names the block's own repair, never under a missing-variable code.
/// </param>
/// <param name="OptionalSystemBlocks">
/// Blocks this template may legally carry. A block outside both lists is refused where it was written —
/// it can never resolve on this template, so admitting it would only defer the discovery to send time.
/// </param>
public sealed record EmailTemplateContract(
    string TemplateCode,
    string Module,
    IReadOnlyList<EmailContractVariable> Variables,
    IReadOnlyList<string> AllowedVariables,
    IReadOnlyList<string> RequiredVariables,
    IReadOnlyList<string> OptionalVariables,
    IReadOnlyList<string> RequiredSystemBlocks,
    IReadOnlyList<string> OptionalSystemBlocks,
    IReadOnlyList<string> SensitiveVariables,
    IReadOnlyList<string> ForbiddenInSubject,
    bool ActionSupported,
    bool ActionRequired,
    string? SystemActionDescription,
    bool CarriesSecret,
    bool AllowCc,
    bool AllowBcc,
    string SecurityClassification,
    IReadOnlyList<string> EditableFields)
{
    /// <summary>Every system block this template may carry, required or not.</summary>
    public IReadOnlyList<string> AllowedSystemBlocks =>
        RequiredSystemBlocks.Concat(OptionalSystemBlocks).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>True when the name is a registered trusted block — on ANY template, not just this one.</summary>
    public static bool IsSystemBlock(string placeholderName)
        => EmailTrustedBlocks.All.Contains(placeholderName);

    /// <summary>True when this template may carry the given block.</summary>
    public bool AllowsSystemBlock(string placeholderName)
    {
        if (placeholderName == EmailTrustedBlocks.ActionBlock) return ActionSupported;
        return RequiredSystemBlocks.Contains(placeholderName, StringComparer.Ordinal)
               || OptionalSystemBlocks.Contains(placeholderName, StringComparer.Ordinal);
    }
}

/// <summary>
/// Resolves the per-template contract that <see cref="SystemEmailTemplates"/>,
/// <see cref="EmailActionTemplates"/> and <see cref="SensitiveEmailVariables"/> jointly describe (G11-J).
///
/// <para>
/// Before this existed, four places each held their own idea of what a template's variables were: the
/// registry, the database's <c>variables_text</c> column, the editor screen's hard-coded list, and the
/// preview's sample dictionary. They disagreed, and the disagreement was visible to operators as a
/// warning on a template nobody had touched. There is now one answer, and everything that needs one
/// asks here.
/// </para>
///
/// <para>
/// <b>What "required" means, precisely.</b> A required variable is one whose REMOVAL FROM THE CONTENT
/// breaks the message beyond repair — a password-reset email with no code in it, an invitation with no
/// buttons. That is a deliberately narrow definition. The alternative, treating every declared variable
/// as required, would forbid an operator from rewording a sentence that happens to drop a name, which is
/// ordinary editing and not a defect. Everything else is optional to WRITE; nothing is optional to
/// RESOLVE, because a placeholder that is present and has no runtime value still fails the send closed.
/// </para>
/// </summary>
public static class EmailTemplateContracts
{
    /// <summary>The only fields an operator may change on a system template (G11-I).</summary>
    public static readonly IReadOnlyList<string> EditableFieldNames = new[]
    {
        "name", "description", "subjectVi", "subjectEn", "bodyVi", "bodyEn",
    };

    public const string ClassificationSensitive = "SENSITIVE";
    public const string ClassificationStandard = "STANDARD";

    /// <summary>
    /// Trusted blocks a specific template cannot do without, beyond the action block (whose requirement
    /// is derived from <see cref="EmailActionTemplates"/> instead).
    ///
    /// <para>
    /// Only the setup-progress update is listed. Its entire content is the setup tables the backend
    /// builds; an operator who deletes the placeholder while rewording the covering sentence would ship
    /// a mail that says an update is attached and then shows nothing, and no send-time check would catch
    /// it because a missing trusted block is not a missing variable.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> RequiredTrustedBlockByTemplate =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SystemEmailTemplates.VisitSetupProgressUpdate] = EmailTrustedBlocks.SetupSummaryBlock,
        };

    /// <summary>
    /// The trusted block this template cannot render without, or null when it has none.
    ///
    /// <para>
    /// Deliberately NOT derived from <see cref="EmailTemplateContract.RequiredVariables"/>, which also
    /// carries <see cref="EmailTrustedBlocks.ActionBlock"/> for every template with an action spec. The
    /// action block has its own structural check on the rendered body and a long tail of templates that
    /// write it without being registered; widening a send-time refusal to cover it would fail messages
    /// that have always gone out. This answers the narrower question the renderer needs: which block is
    /// this template's actual content, such that a body without it is not worth sending.
    /// </para>
    /// </summary>
    public static string? RequiredTrustedBlockFor(string? templateCode)
        => templateCode is not null
           && RequiredTrustedBlockByTemplate.TryGetValue(templateCode, out var block)
            ? block
            : null;

    /// <summary>
    /// Every trusted block whose absence from the stored body makes this template unsendable, paired with
    /// the error code that names the right repair.
    ///
    /// <para>
    /// Two blocks can be required at once — the setup-progress update needs its tables AND, because its
    /// text tells the guest to contact the Host, the contact card. They are reported under different codes
    /// on purpose: a missing setup block means the row has drifted from canonical and must be re-synced,
    /// while a missing contact block means the body and the contact policy disagree and one of the two has
    /// to give. Same symptom, different person fixes it.
    /// </para>
    /// </summary>
    public static IReadOnlyList<(string Block, string ErrorCode)> RequiredBlocksFor(string? templateCode)
    {
        if (templateCode is null) return Array.Empty<(string, string)>();

        var required = new List<(string, string)>();

        if (RequiredTrustedBlockByTemplate.TryGetValue(templateCode, out var contentBlock))
            required.Add((contentBlock, EmailErrorCodes.TemplateRequiredBlockNotInBody));

        if (Contact.EmailContactPolicyDefaults.RequiresContactBlock(templateCode))
            required.Add((EmailTrustedBlocks.ContactInformationBlock,
                EmailErrorCodes.TemplateRequiredContactBlockNotInBody));

        return required;
    }

    /// <summary>The contract for a system template, or null when the code is not a system template.</summary>
    public static EmailTemplateContract? For(string? templateCode)
    {
        var template = SystemEmailTemplates.Find(templateCode);
        if (template is null) return null;

        var spec = EmailActionTemplates.For(template.TemplateCode);
        var carriesSecret = SensitiveEmailVariables.CarriesSecret(template);
        var sensitive = SensitiveEmailVariables.DeclaredBy(template);

        // ── Data variables ───────────────────────────────────────────────────
        // Only what an operator may write and a caller supplies. A trusted block declared here by
        // accident would re-merge the two categories this split exists to keep apart, so it is filtered
        // out rather than trusted.
        var allowed = template.DeclaredVariables
            .Where(v => !EmailTrustedBlocks.All.Contains(v))
            .ToList();

        // Narrow by design; see the class remarks.
        var required = new List<string>(sensitive);
        var optional = allowed.Where(v => !required.Contains(v, StringComparer.Ordinal)).ToList();

        // ── System blocks ────────────────────────────────────────────────────
        // The action block is legal ONLY where the registry declares an action spec, and required there.
        // The two flags are separate on the contract so the editor can say "bắt buộc giữ" or "tùy chọn"
        // without re-deriving the rule, and so a future template that MAY carry a block without having to
        // is expressible; today no such template exists and the two are equal by construction.
        //
        // This replaces the earlier "allowed everywhere, required for the registered few" rule (R-106).
        // That rule existed because fourteen bodies wrote {{actionBlock}} while five were registered, and
        // refusing the other nine would have made them unsavable. The registry has since caught up — every
        // body that writes the placeholder now has a spec, asserted by
        // EmailPreviewCoverageTests.No_template_uses_the_action_block_without_a_registry_entry — so the
        // allowance has nothing left to protect and only lets an operator paste a block into a template
        // whose send path will never fill it.
        var actionSupported = spec is not null;
        var actionRequired = spec is not null;

        var requiredBlocks = new List<string>();
        var optionalBlocks = new List<string>();

        if (actionSupported)
        {
            (actionRequired ? requiredBlocks : optionalBlocks).Add(EmailTrustedBlocks.ActionBlock);
        }

        // Any other trusted block is allowed ONLY on the template that needs it. The blanket allowance
        // above is specific to the action block and rests on a fact that holds for nothing else: many
        // templates already write it. A block used by one template has no such history, so admitting it
        // everywhere would only let an operator paste it somewhere it can never resolve and discover
        // that at send time. Refused at save instead, where the mistake was made.
        if (RequiredTrustedBlockByTemplate.TryGetValue(template.TemplateCode, out var mustHaveBlock))
            requiredBlocks.Add(mustHaveBlock);

        // The contact block is allowed wherever the policy renders one and required where the policy is
        // REQUIRED, so the editor refuses a body that drops it at SAVE time — where the operator made the
        // change and can still see what they deleted — rather than at send time in front of a recipient.
        var contactPolicy = Contact.EmailContactPolicyDefaults.For(template.TemplateCode);
        if (contactPolicy.RendersBlock)
        {
            var isRequired = contactPolicy.Requirement == Domain.Enums.EmailContactRequirement.REQUIRED;
            (isRequired ? requiredBlocks : optionalBlocks).Add(EmailTrustedBlocks.ContactInformationBlock);
        }

        // Computed over variables AND blocks: a trusted block is the only route by which markup — and
        // therefore a live one-time URL — enters a rendered message, so a subject that interpolates one
        // is storing a link by construction. Splitting the lists must not lose that rule.
        //
        // The action block is listed unconditionally, NOT only where it is allowed in the body. "May not
        // appear in a subject" is a different question from "may appear in this body", and answering the
        // second one first is how the rule went missing from the seventeen templates without a spec: the
        // one place the editor reads to know a placeholder is banned from the subject stopped naming it
        // there. The renderer still refuses it at send (AssertNoSecretInSubject), but an operator should
        // be told before they save, not after a recipient would have got it.
        var forbiddenInSubject = allowed
            .Concat(requiredBlocks)
            .Concat(optionalBlocks)
            .Append(EmailTrustedBlocks.ActionBlock)
            .Where(SensitiveEmailVariables.ForbiddenInSubject)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new EmailTemplateContract(
            template.TemplateCode,
            template.Purpose,
            Array.Empty<EmailContractVariable>(),   // filled per language by Describe
            allowed,
            required,
            optional,
            requiredBlocks,
            optionalBlocks,
            sensitive,
            forbiddenInSubject,
            actionSupported,
            actionRequired,
            spec?.SystemActionDescription,
            carriesSecret,
            // A message carrying a one-time code or a personal action link may never be copied: CC and
            // BCC would hand one person's credential to another. This is the same rule the recipient
            // policy enforces at send time, surfaced here so the compose screen can disable the fields
            // rather than let the operator fill them in and be refused.
            AllowCc: template.AllowsCopies && !carriesSecret,
            AllowBcc: template.AllowsCopies && !carriesSecret,
            carriesSecret ? ClassificationSensitive : ClassificationStandard,
            EditableFieldNames);
    }

    /// <summary>
    /// The contract with its variables described in one language — labels and preview samples included.
    /// </summary>
    public static EmailTemplateContract? Describe(string? templateCode, string? language)
    {
        var contract = For(templateCode);
        if (contract is null) return null;

        var lang = EmailLanguages.Normalize(language);

        var variables = contract.AllowedVariables
            .Where(name => !EmailTrustedBlocks.All.Contains(name))
            .Select(name => new EmailContractVariable(
                name,
                EmailVariableCatalog.Label(name, lang),
                EmailVariableCatalog.Sample(name, lang),
                contract.RequiredVariables.Contains(name, StringComparer.Ordinal),
                contract.SensitiveVariables.Contains(name, StringComparer.Ordinal),
                SensitiveEmailVariables.ForbiddenInSubject(name)))
            .OrderBy(v => v.Name, StringComparer.Ordinal)
            .ToList();

        return contract with { Variables = variables };
    }

    /// <summary>
    /// Sample values for every variable a template declares, in the requested language — the dictionary
    /// a preview renders with. Trusted blocks are NOT included: they are supplied by the preview handler
    /// as inert markup, and a caller must never be able to pass one as a variable — <see cref="SystemEmailContent"/>
    /// rejects the attempt outright, so including one here would break preview rather than enrich it.
    /// </summary>
    public static IReadOnlyDictionary<string, string> PreviewSample(string? templateCode, string? language)
    {
        var contract = For(templateCode);
        var lang = EmailLanguages.Normalize(language);
        var sample = new Dictionary<string, string>(StringComparer.Ordinal);

        if (contract is null) return sample;

        foreach (var name in contract.AllowedVariables)
        {
            if (EmailTrustedBlocks.All.Contains(name)) continue;
            sample[name] = EmailVariableCatalog.Sample(name, lang);
        }

        return sample;
    }
}
