using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// What a table in an email body may be (V4 §7.3, §17).
///
/// <para>
/// The editor offers a table dialog with fixed presets and no raw-HTML field, so content authored
/// through the product already satisfies these. This exists because that is not the only way a table
/// gets into a body: content is pasted from Word and Excel, carried over from an older template, or
/// posted straight at the API. The frontend's constraints are an affordance; these are the rule.
/// </para>
/// <para>
/// It checks STRUCTURE only. Which CSS properties survive and which tags exist at all are the
/// sanitiser's job and are already enforced there — restating them here would give two answers to the
/// same question, and the one that ships is whichever runs last.
/// </para>
/// </summary>
public static class EmailTableRules
{
    /// <summary>
    /// Mirrors the editor's own ceiling. Eight columns is already tight inside the 560px shell every
    /// PEMS mail renders in; past that Outlook stops honouring the widths and the columns crush to their
    /// longest syllable — the failure §7.3 introduced tables to avoid.
    /// </summary>
    public const int MaxColumns = 8;

    // There is deliberately NO row ceiling here, although the dialog has one.
    //
    // The dialog's limit is about the dialog: twenty rows of textareas is as much as anyone can work
    // with in a modal. Rows do not break mail rendering — width does — and the bodies this validates are
    // not all typed by hand. The setup-progress email a Host edits carries system-built tables with one
    // row per guest, per agenda item and per logistics item, so a row limit would have refused the send
    // for a visit that happened to have twenty-one guests, naming the table as the problem when the only
    // problem was the size of the delegation. Overall body size is already bounded by
    // EmailOverrideLimits.BodyMax.

    /// <summary>The tables in the body, deepest first. Empty when there are none.</summary>
    private static IReadOnlyList<HtmlNode> TablesIn(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode.SelectNodes("//table")?.ToList() ?? new List<HtmlNode>();
    }

    /// <summary>
    /// Every problem with the tables in <paramref name="html"/>, as sentences an author can act on.
    /// Empty when the content has no tables or none of them break a rule.
    /// </summary>
    public static IReadOnlyList<(string Vi, string En)> Problems(string? html)
    {
        var problems = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(html)) return problems;

        foreach (var table in TablesIn(html!))
        {
            // A table inside a table cannot be edited by the table dialog — its row/column model has no
            // way to express one — and it is the shape that most often arrives from a pasted Word
            // document. Refused rather than flattened: flattening changes what the message SAYS, and an
            // author who pasted a nested table did not ask for its inner rows to be merged into the
            // outer ones.
            if (table.SelectSingleNode(".//table") is not null)
            {
                problems.Add((
                    "Nội dung có bảng lồng trong bảng. Email không hiển thị ổn định với bảng lồng nhau — "
                    + "hãy tách thành các bảng riêng.",
                    "The content has a table nested inside another table. Nested tables do not render "
                    + "reliably in email — split them into separate tables."));
                continue;   // the inner table is reported through its parent; do not say it twice
            }

            var columns = table.SelectNodes(".//tr")
                ?.Max(tr => tr.SelectNodes("./td|./th")?.Count ?? 0) ?? 0;
            if (columns > MaxColumns)
            {
                problems.Add((
                    $"Bảng có {columns} cột, vượt giới hạn {MaxColumns} cột — email sẽ bị vỡ khung trên "
                    + "điện thoại và Outlook.",
                    $"A table has {columns} columns, over the {MaxColumns}-column limit — the email will "
                    + "break out of its frame on mobile and in Outlook."));
            }
        }

        return problems;
    }

    /// <summary>
    /// Throws when authored content carries a table an email cannot render (see <see cref="Problems"/>).
    /// Called on the send path, where there is no issue list to return and nothing to negotiate.
    /// </summary>
    public static void AssertUsable(string? html)
    {
        var problems = Problems(html);
        if (problems.Count == 0) return;

        throw new PEMS.Application.Common.Exceptions.ValidationException(
            string.Join(" ", problems.Select(p => p.Vi)),
            EmailErrorCodes.AuthoredTableUnsupported);
    }
}
