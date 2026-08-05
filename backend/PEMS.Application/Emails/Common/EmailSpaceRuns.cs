using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Runs of spaces are not a layout tool (V4 §7.1, §7.4).
///
/// <para>
/// Someone lining a column up with the space bar has a reasonable expectation and HTML disagrees with it:
/// consecutive spaces collapse, so the text arrives un-aligned. The editor answers that by emitting
/// <c>&amp;nbsp;</c>, which is the WORSE outcome rather than the fix — a run of non-breaking spaces holds
/// its width in the composer, so the column looks right to the person building it, and then refuses to
/// wrap on a phone. The reader gets a line they have to scroll sideways to finish.
/// </para>
/// <para>
/// <b>Why this is not a rewrite.</b> Collapsing the run server-side would deliver a message different
/// from the one the sender approved, and on the template screen it would silently undo an edit somebody
/// deliberately made. So the content is REFUSED, with a sentence naming the tools that do work. The
/// author decides what the layout becomes.
/// </para>
/// <para>
/// <b>Why a DOM walk and not a regular expression.</b> A pattern over raw HTML matches things that are
/// not text at all: the indentation between two block elements, a <c>style</c> value, the padding in a
/// URL. Measured against the 31 canonical templates, a naive scan reports 62 offending fields and every
/// one of them is the newline-and-indent between <c>&lt;/p&gt;</c> and <c>&lt;table&gt;</c>. Walking text
/// nodes individually reports none, because those are separate nodes and were never one run.
/// </para>
/// </summary>
public static class EmailSpaceRuns
{
    /// <summary>
    /// Three, not two. Two spaces are ordinary typing after a full stop, and a rule that refuses a save
    /// over those is one people route around — by pasting the same text somewhere else and sending that.
    /// </summary>
    public const int MaxConsecutiveSpaces = 2;

    private const char NoBreakSpace = ' ';

    /// <summary>A run of spaces, after every non-breaking form has been folded into the plain one.</summary>
    private static readonly Regex RunPattern = new(" {3,}", RegexOptions.Compiled);

    /// <summary>
    /// Elements whose text belongs to the SYSTEM, not to the author: the action area the dispatcher
    /// builds, and anything else marked as a system block. Their spacing is not an editorial decision and
    /// refusing a save over it would leave an author with an error they have no way to repair.
    /// </summary>
    private static bool IsSystemOwned(HtmlNode node)
    {
        for (var current = node; current is not null; current = current.ParentNode)
        {
            if (current.NodeType != HtmlNodeType.Element) continue;

            var name = current.Name.ToLowerInvariant();
            if (name is "script" or "style" or "head") return true;
            if (current.Attributes.Contains("data-system-block")) return true;

            var css = current.GetAttributeValue("class", string.Empty);
            if (css.Contains("pems-var-chip", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// The visible text of one node, with entities decoded so <c>&amp;nbsp;</c>, <c>&amp;#160;</c> and
    /// <c>&amp;#xA0;</c> are all seen as the character they are.
    ///
    /// <para>
    /// ASCII whitespace is trimmed from the ENDS and non-breaking space is not. Leading newline-and-indent
    /// is how the markup was formatted; the browser collapses it and the reader never sees it. A leading
    /// run of non-breaking spaces is the opposite — nothing produces it by accident, it survives to the
    /// inbox, and indenting a paragraph that way is precisely the habit this rule exists to catch.
    /// </para>
    /// </summary>
    private static string VisibleText(HtmlNode text)
    {
        var decoded = HtmlEntity.DeEntitize(text.InnerText) ?? string.Empty;
        return decoded.Trim(' ', '\t', '\r', '\n', '\f', '\v');
    }

    /// <summary>True when one text node carries a run an author is using to line something up.</summary>
    private static bool HasRun(string visible) =>
        RunPattern.IsMatch(visible.Replace(NoBreakSpace, ' '));

    /// <summary>
    /// Every problem with the spacing in <paramref name="html"/>, as sentences an author can act on.
    /// Empty when the content has none — which is the case for all 31 shipped templates.
    /// </summary>
    public static IReadOnlyList<(string Vi, string En)> Problems(string? html)
    {
        var problems = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(html)) return problems;

        // Plain text arrives here too — a subject is not markup. HtmlAgilityPack parses it as one text
        // node, which is exactly the right answer.
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var offenders = new List<string>();

        foreach (var node in doc.DocumentNode.DescendantsAndSelf())
        {
            if (node.NodeType != HtmlNodeType.Text) continue;
            if (IsSystemOwned(node)) continue;

            var visible = VisibleText(node);
            if (visible.Length == 0 || !HasRun(visible)) continue;

            offenders.Add(visible.Length > 60 ? visible[..60] + "…" : visible);
        }

        if (offenders.Count == 0) return problems;

        var sample = offenders[0].Replace(NoBreakSpace, ' ');

        problems.Add((
            "Nội dung đang có nhiều khoảng trắng liên tiếp — ví dụ: \"" + sample + "\". "
            + "Khoảng trắng này có thể làm email hiển thị sai trên điện thoại. "
            + "Vui lòng dùng căn lề, thụt lề hoặc bảng trước khi lưu hoặc xem trước kết quả.",
            "The content has runs of consecutive spaces — for example: \"" + sample + "\". "
            + "They make the email render incorrectly on phones. "
            + "Please use alignment, indentation or a table before saving or previewing."));

        return problems;
    }

    /// <summary>
    /// Throws when authored content carries a spacing run (see <see cref="Problems"/>).
    ///
    /// <para>
    /// Called on the finalize and send paths, where there is no issue list to return and nothing to
    /// negotiate. It sits there rather than only in the editor because the browser is an affordance: a
    /// request posted straight at the API skips every warning the screen would have shown, and the whole
    /// point of the rule is what lands in the recipient's inbox.
    /// </para>
    /// </summary>
    public static void AssertUsable(string? html)
    {
        var problems = Problems(html);
        if (problems.Count == 0) return;

        throw new PEMS.Application.Common.Exceptions.ValidationException(
            problems[0].Vi, EmailErrorCodes.AuthoredSpaceRunUnsupported);
    }
}
