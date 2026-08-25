using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PEMS.ArchitectureTests;

/// <summary>
/// Detects a REAL C# reference to an identifier — a genuine <see cref="IdentifierNameSyntax"/> node
/// reachable from executable code, expressions or member-access chains — as opposed to a plain
/// substring occurrence anywhere in the file.
///
/// <para>
/// A source-text architecture guard built on <c>string.Contains</c> cannot tell "the constant is used
/// here" from "the constant is merely mentioned here" — a `// still uses MinScheduleLeadHours?` comment
/// or a `/// &lt;see cref="VisitMutationPolicy.MinScheduleLeadHours"/&gt;` doc reference trips the exact
/// same alarm as <c>var hours = VisitMutationPolicy.MinScheduleLeadHours;</c>, even though neither
/// carries any executable meaning (<see cref="VisitLeadTimeScopeTests"/> hit this: an XML doc
/// <c>&lt;see cref&gt;</c> in <c>IVisitRequestV2CreateService.cs</c> read as a violation).
/// </para>
///
/// <para>
/// This scanner asks Roslyn instead. <see cref="Microsoft.CodeAnalysis.SyntaxNode.DescendantNodes"/>
/// does NOT descend into trivia — comments and XML doc comments (including their parsed
/// <c>cref</c> expressions) — unless explicitly told to with <c>descendIntoTrivia: true</c>, which this
/// deliberately never passes. That single default is exactly the distinction the guard needs: it is
/// not a special case bolted on top, it is what "only look at real code" already means to Roslyn.
/// </para>
/// </summary>
public static class CodeReferenceScanner
{
    /// <summary>
    /// True when <paramref name="identifierName"/> appears as a real identifier node in
    /// <paramref name="sourceText"/> — never inside a <c>//</c> comment, a <c>/* */</c> block comment,
    /// or an XML doc comment (including a <c>&lt;see cref="..."/&gt;</c> reference).
    /// </summary>
    public static bool ReferencesIdentifier(string sourceText, string identifierName)
    {
        // Cheap pre-filter: a file that does not even contain the substring cannot contain the
        // identifier as a syntax node either, and this skips parsing the great majority of a large
        // source tree — only files that mention the name in ANY form reach the parser at all.
        if (!sourceText.Contains(identifierName, StringComparison.Ordinal)) return false;

        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        return root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.Text == identifierName);
    }
}
