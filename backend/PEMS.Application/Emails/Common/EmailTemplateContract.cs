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
/// </summary>
public sealed record EmailTemplateContract(
    string TemplateCode,
    string Module,
    IReadOnlyList<EmailContractVariable> Variables,
    IReadOnlyList<string> AllowedVariables,
    IReadOnlyList<string> RequiredVariables,
    IReadOnlyList<string> OptionalVariables,
    IReadOnlyList<string> SensitiveVariables,
    IReadOnlyList<string> ForbiddenInSubject,
    bool RequiresActionBlock,
    bool CarriesSecret,
    bool AllowCc,
    bool AllowBcc,
    string SecurityClassification,
    IReadOnlyList<string> EditableFields);

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

    /// <summary>The contract for a system template, or null when the code is not a system template.</summary>
    public static EmailTemplateContract? For(string? templateCode)
    {
        var template = SystemEmailTemplates.Find(templateCode);
        if (template is null) return null;

        var spec = EmailActionTemplates.For(template.TemplateCode);
        var carriesSecret = SensitiveEmailVariables.CarriesSecret(template);
        var sensitive = SensitiveEmailVariables.DeclaredBy(template);

        // The action block is not a variable an operator supplies — it is a trusted block the backend
        // injects — but the editor has to know the placeholder is legal.
        //
        // ALLOWED on every template, REQUIRED only where the registry declares an action spec. Fourteen
        // of the thirty canonical templates write {{actionBlock}} in their body and only five are
        // registered; allowing it solely for those five would have made the other nine impossible to
        // save at all, which is the same false refusal this whole contract exists to remove — just
        // pointed at a different set of templates. Whether a given template's send can actually resolve
        // the block is a send-time question, and it stays fail-closed there (R-106).
        var requiresActionBlock = spec is not null;

        var allowed = new List<string>(template.DeclaredVariables) { EmailTrustedBlocks.ActionBlock };

        // Narrow by design; see the class remarks.
        var required = new List<string>(sensitive);
        if (requiresActionBlock) required.Add(EmailTrustedBlocks.ActionBlock);

        // Any other trusted block is allowed ONLY on the template that needs it. The blanket allowance
        // above is specific to the action block and rests on a fact that holds for nothing else: many
        // templates already write it. A block used by one template has no such history, so admitting it
        // everywhere would only let an operator paste it somewhere it can never resolve and discover
        // that at send time. Refused at save instead, where the mistake was made.
        if (RequiredTrustedBlockByTemplate.TryGetValue(template.TemplateCode, out var mustHaveBlock))
        {
            allowed.Add(mustHaveBlock);
            required.Add(mustHaveBlock);
        }

        var optional = allowed.Where(v => !required.Contains(v, StringComparer.Ordinal)).ToList();

        var forbiddenInSubject = allowed
            .Where(SensitiveEmailVariables.ForbiddenInSubject)
            .ToList();

        return new EmailTemplateContract(
            template.TemplateCode,
            template.Purpose,
            Array.Empty<EmailContractVariable>(),   // filled per language by Describe
            allowed,
            required,
            optional,
            sensitive,
            forbiddenInSubject,
            requiresActionBlock,
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
