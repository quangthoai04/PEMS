using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Emails.Common;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// Who may read a sent message, and how much of its recipient list they get.
///
/// <para>
/// The scenario is the same one throughout, because the whole point is that ONE message gives different
/// answers to different readers: sender <b>A</b>, primary recipient <b>B</b>, carbon copy <b>C</b>, two
/// blind copies <b>D</b> and <b>E</b>, a visit coordinator <b>F</b> who reaches it through the linked
/// object, an unrelated <b>G</b>, and two senior roles <b>H</b> (HO) and <b>I</b> (Admin) who were never
/// on it at all.
/// </para>
/// <para>
/// The failure this guards against is not theoretical: the detail query used to return every recipient
/// row to any of the five internal roles, so B could read the message and learn that D and E had been
/// blind-copied — the one thing a blind copy promises will not happen.
/// </para>
/// </summary>
public class SentEmailAccessTests
{
    private const ulong SenderA = 1;
    private const ulong RecipientB = 2;
    private const ulong CopiedC = 3;
    private const ulong BlindD = 4;
    private const ulong BlindE = 5;
    private const ulong CoordinatorF = 6;
    private const ulong UnrelatedG = 7;
    private const ulong HoH = 8;
    private const ulong AdminI = 9;

    private const string A = "a.sender@fpt.edu.vn";
    private const string B = "b.to@fpt.edu.vn";
    private const string C = "c.cc@fpt.edu.vn";
    private const string D = "d.bcc@fpt.edu.vn";
    private const string E = "e.bcc@fpt.edu.vn";
    private const string F = "f.coordinator@fpt.edu.vn";
    private const string G = "g.unrelated@fpt.edu.vn";
    private const string H = "h.ho@fpt.edu.vn";
    private const string I = "i.admin@fpt.edu.vn";

    private sealed record Row(string RecipientEmail, string RecipientType);

    private static readonly List<Row> Envelope = new()
    {
        new(B, "TO"),
        new(C, "CC"),
        new(D, "BCC"),
        new(E, "BCC"),
    };

    private static SentEmailAccess.Relation Relation(
        ulong viewerId, string viewerEmail, bool linkedObject = false)
        => SentEmailAccess.Resolve(
            viewerId, viewerEmail, SenderA, Envelope,
            r => r.RecipientEmail, r => r.RecipientType, linkedObject);

    private static IReadOnlyList<string> VisibleBcc(ulong viewerId, string viewerEmail, bool linkedObject = false)
    {
        var relation = Relation(viewerId, viewerEmail, linkedObject);
        return SentEmailAccess
            .FilterRecipients(Envelope, relation, viewerEmail, r => r.RecipientEmail, r => r.RecipientType)
            .Where(r => r.RecipientType == "BCC")
            .Select(r => r.RecipientEmail)
            .ToList();
    }

    private static IReadOnlyList<string> VisibleAll(ulong viewerId, string viewerEmail, bool linkedObject = false)
    {
        var relation = Relation(viewerId, viewerEmail, linkedObject);
        return SentEmailAccess
            .FilterRecipients(Envelope, relation, viewerEmail, r => r.RecipientEmail, r => r.RecipientType)
            .Select(r => r.RecipientEmail)
            .ToList();
    }

    // ── 1. The sender sees the blind copies they themselves chose ────────────

    [Fact]
    public void Sender_sees_every_blind_copy()
    {
        Assert.Equal(SentEmailAccess.Relation.Sender, Relation(SenderA, A));
        Assert.Equal(new[] { D, E }, VisibleBcc(SenderA, A));
    }

    // ── 2-3. Visible recipients see the envelope their own copy showed ───────

    [Fact]
    public void Primary_recipient_sees_no_blind_copy()
    {
        Assert.Equal(SentEmailAccess.Relation.VisibleRecipient, Relation(RecipientB, B));
        Assert.Empty(VisibleBcc(RecipientB, B));
        Assert.Equal(new[] { B, C }, VisibleAll(RecipientB, B));
    }

    [Fact]
    public void Carbon_copy_sees_no_blind_copy()
    {
        Assert.Equal(SentEmailAccess.Relation.VisibleRecipient, Relation(CopiedC, C));
        Assert.Empty(VisibleBcc(CopiedC, C));
        Assert.Equal(new[] { B, C }, VisibleAll(CopiedC, C));
    }

    // ── 4-5. A blind copy sees itself and no other blind copy ────────────────

    [Fact]
    public void Blind_copy_sees_only_its_own_entry()
    {
        Assert.Equal(SentEmailAccess.Relation.BlindCopy, Relation(BlindD, D));
        Assert.Equal(new[] { D }, VisibleBcc(BlindD, D));
        // Their copy of the message did carry the To/Cc headers, so those are theirs to see.
        Assert.Equal(new[] { B, C, D }, VisibleAll(BlindD, D));
    }

    [Fact]
    public void The_other_blind_copy_sees_only_its_own_entry()
    {
        Assert.Equal(new[] { E }, VisibleBcc(BlindE, E));
        Assert.DoesNotContain(D, VisibleAll(BlindE, E));
    }

    // ── 6. Object scope opens the message but never the blind copies ─────────

    [Fact]
    public void Linked_object_viewer_reads_the_message_without_the_blind_copies()
    {
        Assert.Equal(SentEmailAccess.Relation.LinkedObject, Relation(CoordinatorF, F, linkedObject: true));
        Assert.True(SentEmailAccess.CanView(Relation(CoordinatorF, F, linkedObject: true)));
        Assert.Empty(VisibleBcc(CoordinatorF, F, linkedObject: true));
    }

    // ── 7-9. Nobody gets in on a role ────────────────────────────────────────

    [Fact]
    public void An_unrelated_reader_is_refused()
    {
        Assert.Equal(SentEmailAccess.Relation.None, Relation(UnrelatedG, G));
        Assert.False(SentEmailAccess.CanView(Relation(UnrelatedG, G)));
        Assert.Empty(VisibleAll(UnrelatedG, G));
    }

    [Fact]
    public void Ho_has_no_implicit_access_and_no_implicit_bcc()
    {
        // Seniority is not a relation to a message. An HO who is on the visit gets in through the object
        // scope like anyone else — and still without the blind copies.
        Assert.False(SentEmailAccess.CanView(Relation(HoH, H)));
        Assert.Empty(VisibleBcc(HoH, H, linkedObject: true));
    }

    [Fact]
    public void Admin_has_no_implicit_access_and_no_implicit_bcc()
    {
        Assert.False(SentEmailAccess.CanView(Relation(AdminI, I)));
        Assert.Empty(VisibleBcc(AdminI, I, linkedObject: true));
    }

    // ── Edges ────────────────────────────────────────────────────────────────

    [Fact]
    public void Address_matching_ignores_case_and_surrounding_space()
    {
        Assert.Equal(SentEmailAccess.Relation.VisibleRecipient, Relation(RecipientB, "  B.To@FPT.edu.VN "));
        Assert.Equal(SentEmailAccess.Relation.BlindCopy, Relation(BlindD, "D.Bcc@FPT.EDU.VN"));
    }

    [Fact]
    public void Being_addressed_visibly_outranks_also_being_blind_copied()
    {
        // Cross-group duplicates are rejected at send, so this shape should not exist. If a legacy row
        // pair does, the safe reading is the visible one: it reveals nothing the message did not.
        var rows = new List<Row> { new(B, "TO"), new(B, "BCC"), new(D, "BCC") };

        var relation = SentEmailAccess.Resolve(
            RecipientB, B, SenderA, rows, r => r.RecipientEmail, r => r.RecipientType);

        Assert.Equal(SentEmailAccess.Relation.VisibleRecipient, relation);
        Assert.DoesNotContain(
            SentEmailAccess.FilterRecipients(rows, relation, B, r => r.RecipientEmail, r => r.RecipientType),
            r => r.RecipientType == "BCC");
    }

    [Fact]
    public void An_unknown_recipient_type_is_treated_as_visible_not_blind()
    {
        var rows = new List<Row> { new(B, ""), new(C, "to"), new(D, "BCC") };

        var visible = SentEmailAccess.FilterRecipients(
            rows, SentEmailAccess.Relation.VisibleRecipient, B, r => r.RecipientEmail, r => r.RecipientType);

        Assert.Equal(new[] { B, C }, visible.Select(r => r.RecipientEmail));
    }

    [Fact]
    public void An_unauthenticated_reader_resolves_to_no_access()
        => Assert.Equal(
            SentEmailAccess.Relation.None,
            SentEmailAccess.Resolve(
                null, A, SenderA, Envelope, r => r.RecipientEmail, r => r.RecipientType, true));

    [Fact]
    public void A_system_message_with_no_sender_grants_nobody_sender_rights()
    {
        var relation = SentEmailAccess.Resolve(
            SenderA, A, null, Envelope, r => r.RecipientEmail, r => r.RecipientType);

        Assert.Equal(SentEmailAccess.Relation.None, relation);
    }

    // ── Whether to offer a reply ──────────────────────────────────────────────
    //
    // The detail response used to carry no `canReply` at all, so the screen read `undefined` and the
    // button never appeared: the reply command existed with nothing able to call it. The flag is decided
    // here rather than on the client, because only the server knows the viewer's relation to the envelope.

    [Fact]
    public void The_primary_recipient_is_offered_a_reply()
        => Assert.True(SentEmailAccess.CanOfferReply(Relation(RecipientB, B), SenderA));

    [Fact]
    public void A_copied_reader_is_offered_a_reply()
        => Assert.True(SentEmailAccess.CanOfferReply(Relation(CopiedC, C), SenderA));

    [Fact]
    public void A_blind_copy_is_offered_a_reply_like_any_other_addressee()
        // Being blind-copied is still being party to the conversation, and the reply command accepts
        // them. Their reply carries their own address, as any reply does — that is the sender's choice.
        => Assert.True(SentEmailAccess.CanOfferReply(Relation(BlindD, D), SenderA));

    [Fact]
    public void The_author_is_not_offered_a_reply_to_their_own_message()
        // The command would allow it and address the reply back to the author. Mailing yourself your own
        // message is not a reply, and the list query has always reported CanReply = false for SENT.
        => Assert.False(SentEmailAccess.CanOfferReply(Relation(SenderA, A), SenderA));

    [Fact]
    public void A_reader_who_arrived_through_the_linked_object_is_not_offered_a_reply()
    {
        var relation = Relation(CoordinatorF, F, linkedObject: true);

        Assert.Equal(SentEmailAccess.Relation.LinkedObject, relation);
        // The reply command resolves the relation from the envelope alone, so it refuses this reader.
        // Offering the button would promise something the server then denies.
        Assert.False(SentEmailAccess.CanOfferReply(relation, SenderA));
    }

    [Fact]
    public void An_unrelated_reader_is_not_offered_a_reply()
        => Assert.False(SentEmailAccess.CanOfferReply(Relation(UnrelatedG, G), SenderA));

    [Fact]
    public void Nobody_is_offered_a_reply_to_a_system_message()
        // No person behind it to answer; the command refuses with a conflict.
        => Assert.False(SentEmailAccess.CanOfferReply(SentEmailAccess.Relation.VisibleRecipient, null));

    [Fact]
    public void Offering_a_reply_never_exceeds_what_the_reply_command_accepts()
    {
        // The command's own precondition, verbatim: a viewer it will let through, and a real sender to
        // answer. This asserts the direction of the implication for every combination, so widening the
        // affordance without widening the command fails here rather than at a user's 403.
        foreach (SentEmailAccess.Relation relation in Enum.GetValues<SentEmailAccess.Relation>())
        {
            foreach (var sender in new ulong?[] { null, SenderA })
            {
                var offered = SentEmailAccess.CanOfferReply(relation, sender);
                var commandWouldAccept = SentEmailAccess.CanView(relation) && sender is not null;

                if (offered)
                    Assert.True(commandWouldAccept,
                        $"CanOfferReply({relation}, sender={sender?.ToString() ?? "null"}) is offered but the reply command would refuse it.");
            }
        }
    }

    // ── Whether to offer "đánh dấu đã xử lý" ──────────────────────────────────
    //
    // The same shape of bug as the reply flag, one step further along: the detail payload carried no
    // completion flag either, so the button was invisible to everyone while the command stood ready to
    // accept the call. Both sides now read this one predicate.

    private static readonly DateTime? NotCompleted = null;
    private static readonly DateTime? AlreadyCompleted = new DateTime(2026, 7, 1, 9, 0, 0);

    [Fact]
    public void The_author_may_close_their_own_message()
        => Assert.True(SentEmailAccess.CanMarkComplete(Relation(SenderA, A), NotCompleted));

    [Fact]
    public void The_primary_recipient_may_close_the_message()
        => Assert.True(SentEmailAccess.CanMarkComplete(Relation(RecipientB, B), NotCompleted));

    [Fact]
    public void A_copied_reader_may_close_the_message()
        => Assert.True(SentEmailAccess.CanMarkComplete(Relation(CopiedC, C), NotCompleted));

    [Fact]
    public void A_blind_copy_may_close_the_message()
        // Blind or not, they were addressed. The command matches against the recipient list without
        // caring which group the row is in, so the affordance must not be narrower.
        => Assert.True(SentEmailAccess.CanMarkComplete(Relation(BlindD, D), NotCompleted));

    [Fact]
    public void A_reader_who_arrived_through_the_linked_object_may_not_close_it()
    {
        var relation = Relation(CoordinatorF, F, linkedObject: true);

        Assert.Equal(SentEmailAccess.Relation.LinkedObject, relation);
        // They can read the message because they can open the visit it belongs to. Closing somebody
        // else's correspondence is a different thing, and the command refuses them.
        Assert.False(SentEmailAccess.CanMarkComplete(relation, NotCompleted));
    }

    [Fact]
    public void An_unrelated_reader_may_not_close_the_message()
        => Assert.False(SentEmailAccess.CanMarkComplete(Relation(UnrelatedG, G), NotCompleted));

    [Fact]
    public void A_message_already_closed_is_not_offered_again()
        // The command answers "đã được đánh dấu hoàn thành từ trước"; the button must not invite that.
        => Assert.False(SentEmailAccess.CanMarkComplete(Relation(RecipientB, B), AlreadyCompleted));

    [Fact]
    public void The_author_is_not_offered_a_message_that_is_already_closed()
        => Assert.False(SentEmailAccess.CanMarkComplete(Relation(SenderA, A), AlreadyCompleted));

    [Fact]
    public void Offering_completion_matches_what_the_command_accepts_exactly()
    {
        // Equality, not implication: this affordance and the command are the same predicate, so a
        // divergence in either direction — a dead button, or one the server refuses — fails here.
        foreach (SentEmailAccess.Relation relation in Enum.GetValues<SentEmailAccess.Relation>())
        {
            foreach (var completedAt in new[] { NotCompleted, AlreadyCompleted })
            {
                var offered = SentEmailAccess.CanMarkComplete(relation, completedAt);
                var commandWouldAccept = completedAt is null
                    && relation is SentEmailAccess.Relation.Sender
                                or SentEmailAccess.Relation.VisibleRecipient
                                or SentEmailAccess.Relation.BlindCopy;

                Assert.True(offered == commandWouldAccept,
                    $"CanMarkComplete({relation}, completed={completedAt is not null}) disagrees with the command.");
            }
        }
    }
}
