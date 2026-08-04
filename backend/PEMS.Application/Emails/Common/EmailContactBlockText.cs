using System;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// The one place the literal <c>{{contactInformationBlock}}</c> is spelled out, and the one place a body
/// is asked whether it carries one.
///
/// <para>
/// It exists because the same two lines — <c>"{{" + EmailTrustedBlocks.ContactInformationBlock + "}}"</c>
/// followed by <c>body.Contains(marker, StringComparison.Ordinal)</c> — had been written out separately in
/// the settings query, the settings command, the settings restore and the renderer. Four copies of a
/// literal is four chances for one of them to answer differently from the rest, and the answers are used
/// to decide whether a save is refused; the frontend mirrors this same token in
/// <c>features/emails/utils/contactBlock.ts</c>.
/// </para>
/// <para>
/// <b>Only the exact literal counts.</b> No whitespace tolerance inside the braces and no case folding:
/// the renderer's substitution pattern is what ultimately decides whether a placeholder gets replaced, and
/// a detector that matched MORE than the renderer would refuse content that sends perfectly well, while
/// one that matched less would let an unsubstituted placeholder reach a recipient. Matching exactly what
/// the renderer matches is the only setting with neither failure.
/// </para>
/// </summary>
public static class EmailContactBlockText
{
    /// <summary>The placeholder as it appears in a stored body.</summary>
    public static readonly string Marker = "{{" + EmailTrustedBlocks.ContactInformationBlock + "}}";

    /// <summary>True when this content carries the contact placeholder at least once.</summary>
    public static bool Contains(string? content)
        => content is not null && content.Contains(Marker, StringComparison.Ordinal);

    /// <summary>
    /// Removes every occurrence. Used only by tests and by the shipped-default parity checks — an
    /// operator's body is never rewritten by the backend, because a deletion they did not make is one they
    /// cannot see. The editor offers the removal instead; see <c>removeContactInformationBlock</c>.
    /// </summary>
    public static string Remove(string? content)
        => content is null ? string.Empty : content.Replace(Marker, string.Empty, StringComparison.Ordinal);
}
