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
}
