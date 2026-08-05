using System;
using System.Text.RegularExpressions;
using PEMS.Application.Common.Exceptions;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// The placeholder that marks WHERE a system action block sits inside content somebody may edit.
///
/// <para>
/// <b>The problem it solves.</b> The action block is markup the backend owns: real one-time URLs minted
/// at send time, which is why an author may never write, move a token into, or forge one. Until now the
/// way that ownership was enforced was positional — the editable copy had the block cut out of it, and
/// the real block was APPENDED to whatever the author returned. That made the rule easy to state and
/// wrong in practice: an email whose template put the buttons in the middle of a sentence ("choose one of
/// the options below to continue — [Accept] [Decline] — and we will proceed") came back with the buttons
/// at the bottom, under the signature. The author could not put them back, because the thing they would
/// have had to move did not exist in their copy.
/// </para>
/// <para>
/// <b>What replaces it.</b> An empty, inert <c>&lt;div data-system-block="action"&gt;</c>. It carries no
/// URL, no token, no label and no text, so moving it moves a position and nothing else; the block itself
/// is still built by the backend and substituted here at send time. An author may drag it, cut and paste
/// it, or put paragraphs either side of it. They cannot make it do anything, because there is nothing in
/// it to do anything with.
/// </para>
/// <para>
/// <b>Why a node and not the <c>{{actionBlock}}</c> placeholder.</b> That placeholder stays refused in
/// authored content (see <c>SystemEmailContent.AuthoredByUser</c>). A variable placeholder is text: an
/// author can type one, type two, or type one into a subject. A DOM node cannot be typed by accident, is
/// visible in the editor as an object rather than as characters, and — the point that matters — survives
/// the sanitiser as a single indivisible thing that can be counted exactly.
/// </para>
/// </summary>
public static class EmailSystemBlockNodes
{
    /// <summary>The attribute that marks a system block, and the value identifying the action area.</summary>
    public const string Attribute = "data-system-block";
    public const string ActionValue = "action";

    /// <summary>The canonical spelling the backend emits. Authors receive exactly this.</summary>
    public const string ActionNodeHtml = "<div data-system-block=\"action\"></div>";

    /// <summary>
    /// Matches one action node however the editor or sanitiser rewrote its attributes — single or double
    /// quotes, other attributes before or after, an empty body or stray whitespace inside it.
    ///
    /// <para>
    /// Deliberately tolerant on the way IN and strict on the way OUT: what comes back from a browser has
    /// been through Quill's serialiser and the sanitiser, either of which may reorder attributes or add a
    /// class. Refusing those would report "you removed the button" to an author who moved it correctly.
    /// </para>
    /// <para>
    /// <b>Text inside the node is matched and consumed.</b> The canonical node is empty, but the editor
    /// draws a human-readable label inside its copy ("Khối nút phản hồi — hệ thống tự gắn khi gửi") so the
    /// sender can see what they are dragging. The frontend normalises that away before sending; this
    /// pattern tolerates it anyway, because the failure mode otherwise is the worst one available — the
    /// node would not match, the block would be appended at the end instead, and the editor's own label
    /// would be delivered to the recipient as part of the message.
    /// </para>
    /// <para>
    /// <c>[^&lt;]*</c> rather than <c>.*?</c>: the content is text, never nested markup, and a lazy
    /// any-character match would stop at the first <c>&lt;/div&gt;</c> of a nested block and leave a
    /// stray closing tag behind.
    /// </para>
    /// </summary>
    private static readonly Regex ActionNodePattern = new(
        @"<div\b[^>]*\bdata-system-block\s*=\s*(?:""action""|'action'|action)[^>]*>[^<]*</div\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>The canonical injected block, START…END, as one span.</summary>
    private static readonly Regex InjectedBlockPattern = new(
        @"<!--\s*PEMS_ACTION_BLOCK_START\s*-->.*?<!--\s*PEMS_ACTION_BLOCK_END\s*-->",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>True when the content carries at least one action node.</summary>
    public static bool HasActionNode(string? html)
        => !string.IsNullOrEmpty(html) && ActionNodePattern.IsMatch(html!);

    /// <summary>How many action nodes the content carries.</summary>
    public static int CountActionNodes(string? html)
        => string.IsNullOrEmpty(html) ? 0 : ActionNodePattern.Matches(html!).Count;

    /// <summary>
    /// Turns the block the renderer already substituted into the movable node, IN PLACE.
    ///
    /// <para>
    /// This is what makes the position editable at all: the preview renders the template normally — so the
    /// block lands wherever <c>{{actionBlock}}</c> sits in the stored body — and then the live buttons are
    /// swapped for the inert node without moving anything. The author opens the editor on content whose
    /// action area is already in the right place.
    /// </para>
    /// </summary>
    public static string ReplaceInjectedBlockWithNode(string? html)
        => string.IsNullOrEmpty(html)
            ? string.Empty
            : InjectedBlockPattern.Replace(html!, ActionNodeHtml);

    /// <summary>
    /// Substitutes the author's node with the real (or preview-disabled) block, at the node's position.
    ///
    /// <para>
    /// Returns <see langword="false"/> when there was no node to substitute, which is NOT an error here:
    /// content authored before this mechanism existed, and every send that does not go through the runtime
    /// editor, legitimately has none. The caller decides what to do about that — see
    /// <c>EmailTemplateRenderer</c>, which falls back to appending so those paths keep working unchanged.
    /// </para>
    /// </summary>
    public static bool TrySubstituteActionNode(string html, string wrappedBlockHtml, out string result)
    {
        result = html ?? string.Empty;
        if (string.IsNullOrEmpty(result) || !ActionNodePattern.IsMatch(result)) return false;

        // Replace only the FIRST node. A second one is refused by AssertAtMostOneActionNode before this
        // runs, so reaching here with two would mean minting the same one-time token into two buttons.
        result = ActionNodePattern.Replace(result, wrappedBlockHtml.Replace("$", "$$"), 1);
        return true;
    }

    /// <summary>Removes every action node — for the plain-text rendering and the history snapshot.</summary>
    public static string StripActionNodes(string? html)
        => string.IsNullOrEmpty(html) ? string.Empty : ActionNodePattern.Replace(html!, string.Empty);

    /// <summary>
    /// Refuses content carrying more than one action area (§9.5).
    ///
    /// <para>
    /// One is the only workable count: the block is minted from a single one-time token, so two of them
    /// would put two buttons in front of a recipient that consume the same credential — the first click
    /// answers for both, and the second reports an expired link to somebody who did nothing wrong.
    /// Duplication is easy to do by accident (select-all, copy, paste), which is exactly why it is checked
    /// rather than assumed.
    /// </para>
    /// </summary>
    public static void AssertAtMostOneActionNode(string? html)
    {
        var count = CountActionNodes(html);
        if (count <= 1) return;

        throw new BusinessRuleException(
            "Mỗi email chỉ được có một khối nút phản hồi. "
            + $"Nội dung hiện có {count} khối — hãy xóa bớt, chỉ giữ lại một.",
            EmailErrorCodes.ActionBlockMalformed);
    }
}
