using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Who may read a sent email, and how much of its recipient list they may see.
///
/// <para>
/// This exists as ONE rule because the leak it prevents is a disagreement between surfaces, not a missing
/// check on any single one. The list query scoped results to the viewer while the detail query had its
/// authorisation deliberately removed ("HO, StaffLeader… xem chi tiết quản lý email đã gửi"), so any of
/// the five internal roles could read any message by id — subject, body, error text, attachments and the
/// full BCC list of a conversation they had nothing to do with. A rule that lives in one place cannot
/// drift between the surfaces that apply it.
/// </para>
/// <para>
/// <b>BCC is the point.</b> A blind copy is a promise to the sender that the other recipients will not
/// learn who else received the message. The promise is kept in the MIME — BCC never enters a header — and
/// it has to be kept in the history too, or the API undoes what the transport was careful about.
/// </para>
/// <para>
/// The methods take the address and type as selectors rather than demanding an interface, so a caller can
/// apply the rule to whatever shape it already has — an EF entity, or the anonymous projection a Pomelo
/// query returns — without first copying rows into a different type.
/// </para>
/// </summary>
public static class SentEmailAccess
{
    /// <summary>What a viewer is to a particular message. Ordered from least to most entitled.</summary>
    public enum Relation
    {
        /// <summary>Not the sender, not addressed, no object scope — may not read the message at all.</summary>
        None = 0,

        /// <summary>Reached the message through the linked business object (a visit, a report), not the envelope.</summary>
        LinkedObject,

        /// <summary>Addressed blind. Sees the visible envelope plus their own blind entry, and no other.</summary>
        BlindCopy,

        /// <summary>Addressed in TO or CC. Sees exactly what the message itself showed them.</summary>
        VisibleRecipient,

        /// <summary>Wrote the message. Sees everything, including who was blind-copied.</summary>
        Sender,
    }

    /// <summary>
    /// Works out the viewer's relation to a message from the sender id and the recipient list.
    ///
    /// <para>
    /// <paramref name="hasLinkedObjectAccess"/> is decided by the caller against the canonical scope of
    /// whatever the message is attached to. It is never inferred from a role here, because "an HO may open
    /// this visit" is a fact about the visit; it is not a licence to read every message in the system.
    /// </para>
    /// </summary>
    public static Relation Resolve<T>(
        ulong? viewerUserId,
        string? viewerEmail,
        ulong? senderUserId,
        IEnumerable<T> recipients,
        Func<T, string?> emailOf,
        Func<T, string?> typeOf,
        bool hasLinkedObjectAccess = false)
    {
        if (viewerUserId is null) return Relation.None;

        if (senderUserId is not null && senderUserId == viewerUserId)
            return Relation.Sender;

        if (!string.IsNullOrWhiteSpace(viewerEmail))
        {
            var mine = recipients
                .Where(r => Same(emailOf(r), viewerEmail))
                .Select(r => Normalize(typeOf(r)))
                .ToList();

            // Addressed visibly wins over addressed blind: the message itself already showed this person
            // in a header, so there is nothing to conceal from them about their own entry.
            if (mine.Any(t => t != EmailRecipientTypes.Bcc)) return Relation.VisibleRecipient;
            if (mine.Count > 0) return Relation.BlindCopy;
        }

        return hasLinkedObjectAccess ? Relation.LinkedObject : Relation.None;
    }

    /// <summary>True when the viewer may open the message at all.</summary>
    public static bool CanView(Relation relation) => relation != Relation.None;

    /// <summary>
    /// Whether a reply should be offered on a message.
    ///
    /// <para>
    /// This is the affordance rule, and it is deliberately never wider than what
    /// <c>ReplytoEmailCommandHandler</c> will accept: it requires the same envelope relation and the same
    /// real sender to answer. It is narrower in one place — the sender's own message. The command would
    /// permit that (the reply would be addressed back to the author), but a button that mails you your own
    /// message is not a reply, and the list query has always reported <c>CanReply = false</c> for SENT.
    /// </para>
    /// <para>
    /// <paramref name="relation"/> must be the relation resolved from the ENVELOPE alone. A viewer who
    /// reached the message through its linked business object is not party to the conversation, and the
    /// reply command refuses them — offering the button would promise something the server then denies.
    /// </para>
    /// </summary>
    public static bool CanOfferReply(Relation relation, ulong? senderUserId)
        => CanView(relation)
           && relation != Relation.Sender
           && relation != Relation.LinkedObject
           && senderUserId is not null;

    /// <summary>
    /// Whether "đánh dấu đã xử lý" should be offered, and — because both sides call this — whether the
    /// command will accept it.
    ///
    /// <para>
    /// The two must agree. The detail screen reads <c>canMarkComplete</c> off the payload, and the payload
    /// did not carry the field: it was always <c>undefined</c>, so the button never appeared for anyone,
    /// while <c>MarkEmailCompletedCommandHandler</c> was perfectly willing to accept the call. A dead
    /// affordance is the quiet half of that bug; the loud half would be showing a button the server then
    /// refuses. One predicate consulted by both is what keeps them from drifting apart again.
    /// </para>
    /// <para>
    /// Party to the envelope, and not yet completed. A linked-object viewer is excluded on purpose: they
    /// can read the message because they can open the visit it belongs to, which is not the same as being
    /// in the conversation, and closing somebody else's correspondence is not theirs to do.
    /// </para>
    /// </summary>
    public static bool CanMarkComplete(Relation relation, DateTime? deliveredAt)
        => deliveredAt is null
           && relation is Relation.Sender or Relation.VisibleRecipient or Relation.BlindCopy;

    /// <summary>
    /// The recipient rows this viewer may see.
    ///
    /// <list type="bullet">
    /// <item><b>Sender</b> — everything. They chose the blind copies; hiding them would hide their own act.</item>
    /// <item><b>TO/CC, linked-object viewer</b> — TO and CC only. Exactly the headers the message carried.</item>
    /// <item><b>BCC</b> — TO and CC (which their copy did show them) plus their own blind entry, never another's.</item>
    /// </list>
    ///
    /// Note what is returned nowhere: a count, a flag or a total that would let a viewer infer that blind
    /// copies exist. Filtering the list but publishing "3 người nhận ẩn" beside it gives the game away
    /// just as completely as returning the addresses would.
    /// </summary>
    public static IReadOnlyList<T> FilterRecipients<T>(
        IReadOnlyList<T> rows,
        Relation relation,
        string? viewerEmail,
        Func<T, string?> emailOf,
        Func<T, string?> typeOf)
    {
        if (relation == Relation.Sender) return rows;
        if (relation == Relation.None) return Array.Empty<T>();

        var visible = rows.Where(r => Normalize(typeOf(r)) != EmailRecipientTypes.Bcc);

        if (relation != Relation.BlindCopy || string.IsNullOrWhiteSpace(viewerEmail))
            return visible.ToList();

        return visible
            .Concat(rows.Where(r =>
                Normalize(typeOf(r)) == EmailRecipientTypes.Bcc && Same(emailOf(r), viewerEmail)))
            .ToList();
    }

    /// <summary>
    /// An unrecognised or missing type reads as TO, matching the column default. A row whose group cannot
    /// be determined must never be treated as blind: guessing BCC would hide a visible recipient, but
    /// guessing TO can only ever show what the message already showed.
    /// </summary>
    private static string Normalize(string? recipientType)
        => string.IsNullOrWhiteSpace(recipientType)
            ? EmailRecipientTypes.To
            : recipientType.Trim().ToUpperInvariant();

    private static bool Same(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
           && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
