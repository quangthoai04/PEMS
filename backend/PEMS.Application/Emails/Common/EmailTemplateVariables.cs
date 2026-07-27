using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// The placeholder contract, in one place: how a <c>{{name}}</c> is recognised, how
/// <c>email_templates.variables_text</c> is parsed, and what a valid variable name looks like.
///
/// <para>
/// The renderer and the seed-contract test both depend on this. Keeping the two on one implementation is
/// the point: a template whose <c>variables_text</c> disagrees with the placeholders actually written in
/// its body is exactly the drift that used to reach recipients as the literal text "Chưa có thông tin",
/// and it can only be detected if both sides count placeholders the same way.
/// </para>
/// </summary>
public static class EmailTemplateVariables
{
    /// <summary>
    /// Matches placeholders loosely on purpose — including the legacy PascalCase and underscore
    /// spellings — so an outdated one is *reported* rather than passed through to a recipient.
    /// </summary>
    public static readonly Regex PlaceholderPattern =
        new(@"\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}", RegexOptions.Compiled);

    /// <summary>The only shape a variable name may take: lower camelCase.</summary>
    public static readonly Regex ValidNamePattern =
        new("^[a-z][A-Za-z0-9]*$", RegexOptions.Compiled);

    private static readonly Regex EncodedOpenBrace = new("%7[Bb]\\s*%7[Bb]", RegexOptions.Compiled);
    private static readonly Regex EncodedCloseBrace = new("%7[Dd]\\s*%7[Dd]", RegexOptions.Compiled);

    /// <summary>
    /// Splits <c>variables_text</c> into a set. Accepts comma, semicolon or newline separators and
    /// tolerates names written with braces, because the existing seed uses several of those shapes.
    /// A blank value means the template declares no variables, which is valid for a static notice.
    /// </summary>
    public static IReadOnlySet<string> ParseDeclared(string? variablesText)
    {
        if (string.IsNullOrWhiteSpace(variablesText))
            return new HashSet<string>(StringComparer.Ordinal);

        return variablesText
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim().Trim('{', '}').Trim())
            .Where(v => v.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Every distinct placeholder name written in the given content.</summary>
    public static IReadOnlySet<string> ExtractPlaceholders(params string?[] content)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (var part in content)
        {
            if (string.IsNullOrEmpty(part)) continue;
            foreach (Match m in PlaceholderPattern.Matches(NormalizeEncodedBraces(part)))
                found.Add(m.Groups[1].Value);
        }

        return found;
    }

    /// <summary>Turns <c>%7B%7Bname%7D%7D</c> — what a rich editor stores inside an href — back into braces.</summary>
    public static string NormalizeEncodedBraces(string value)
        => EncodedCloseBrace.Replace(EncodedOpenBrace.Replace(value, "{{"), "}}");

    public static bool IsValidName(string name) => ValidNamePattern.IsMatch(name);
}
