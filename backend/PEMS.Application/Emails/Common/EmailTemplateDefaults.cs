using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// The shipped default content of one system email template — what "restore to default" restores.
///
/// <para>
/// Exactly the six operator-editable fields, no more: those are the only fields UC-44 can change, so
/// they are the only fields that can have drifted away from the shipped wording. <c>BodyFormat</c> rides
/// along for the parity check only (it is not operator-editable, so restore never writes it); nothing
/// here can touch <c>template_code</c>, <c>purpose</c>, <c>campus_id</c> or <c>status</c>.
/// </para>
/// </summary>
public sealed record EmailTemplateDefault(
    string TemplateCode,
    string Name,
    string? Description,
    string SubjectVi,
    string BodyVi,
    string SubjectEn,
    string BodyEn,
    string BodyFormat);

/// <summary>
/// The authoritative backend source of default template content, loaded once from an embedded resource.
///
/// <para>
/// <b>Why a generated resource and not C# literals.</b> The wording of these thirty emails is authored in
/// the canonical SQL seed and nowhere else (decision D-01, which is why
/// <see cref="SystemEmailTemplates"/> deliberately carries no sentences). Hand-copying 30 × 4 content
/// fields into C# would create a second author for the same text, and the two would drift the first time
/// someone fixed a typo in one of them. So the content is EXTRACTED from a fresh canonical import — MySQL
/// evaluates the seed's <c>CONCAT(...)</c> expressions and unescapes its own string literals, which no
/// hand-rolled SQL parser does reliably — serialised to
/// <c>Emails/Common/Assets/email-template-defaults.json</c>, and embedded in this assembly.
/// </para>
/// <para>
/// <b>Why that does not become a second source of truth.</b> <c>EmailTemplateDefaultsParityTests</c>
/// re-derives the file from a fresh canonical import on every integration run and compares field by
/// field. Editing the SQL without regenerating the resource turns into a failing test, not a silent
/// divergence. A third assertion compares the resource against the ACTIVE catalog of that same fresh
/// import, which is what proves the extraction faithful rather than merely self-consistent.
/// </para>
/// <para>
/// <b>What is explicitly not a default source.</b> Not the live <c>email_templates</c> rows — those are
/// what an operator may have broken, so reading them back would restore the damage. Not
/// <c>sent_emails.body_snapshot</c>, not a draft, not an audit row: all three are records of one message
/// at one moment, already merged with recipient data. And not "ask HO to re-run a SQL script": a restore
/// the operator cannot perform from the screen is not a feature.
/// </para>
/// </summary>
public static class EmailTemplateDefaults
{
    private const string ResourceName =
        "PEMS.Application.Emails.Common.Assets.email-template-defaults.json";

    private static readonly Lazy<IReadOnlyDictionary<string, EmailTemplateDefault>> _byCode =
        new(Load, isThreadSafe: true);

    /// <summary>Every shipped default, keyed by template code (ordinal, case-sensitive as codes are).</summary>
    public static IReadOnlyDictionary<string, EmailTemplateDefault> ByCode => _byCode.Value;

    /// <summary>The shipped default for one code, or null when the code has none.</summary>
    public static EmailTemplateDefault? For(string? templateCode)
        => string.IsNullOrWhiteSpace(templateCode)
            ? null
            : ByCode.TryGetValue(templateCode!, out var d) ? d : null;

    /// <summary>Every code that has a shipped default, sorted — used by the parity tests.</summary>
    public static IReadOnlyList<string> AllCodes => ByCode.Keys.OrderBy(c => c, StringComparer.Ordinal).ToList();

    /// <summary>The embedded JSON exactly as shipped, for the byte-level parity assertion.</summary>
    public static string ReadRawResource()
    {
        using var stream = typeof(EmailTemplateDefaults).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' is missing. Restore-to-default cannot work without " +
                "it; check the <EmbeddedResource> entry in PEMS.Application.csproj.");

        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static IReadOnlyDictionary<string, EmailTemplateDefault> Load()
    {
        var json = ReadRawResource();

        var parsed = JsonSerializer.Deserialize<List<EmailTemplateDefault>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException(
                "Embedded template defaults deserialised to null — the resource is corrupt.");

        // Fail loudly at first use rather than restoring a truncated body. A duplicate code would make
        // "the default for X" ambiguous, and an empty subject or body would restore a broken template.
        var duplicates = parsed.GroupBy(p => p.TemplateCode, StringComparer.Ordinal)
                               .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                "Embedded template defaults contain duplicate codes: " + string.Join(", ", duplicates));

        var incomplete = parsed.Where(p =>
            string.IsNullOrWhiteSpace(p.TemplateCode) ||
            string.IsNullOrWhiteSpace(p.Name) ||
            string.IsNullOrWhiteSpace(p.SubjectVi) || string.IsNullOrWhiteSpace(p.BodyVi) ||
            string.IsNullOrWhiteSpace(p.SubjectEn) || string.IsNullOrWhiteSpace(p.BodyEn))
            .Select(p => p.TemplateCode).ToList();
        if (incomplete.Count > 0)
            throw new InvalidOperationException(
                "Embedded template defaults are missing VI or EN content for: " + string.Join(", ", incomplete));

        return parsed.ToDictionary(p => p.TemplateCode, p => p, StringComparer.Ordinal);
    }
}
