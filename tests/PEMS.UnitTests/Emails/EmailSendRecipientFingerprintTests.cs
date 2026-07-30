using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Emails.Commands.ReplytoEmail;
using PEMS.Application.Emails.Commands.SendEmail;
using PEMS.Application.Emails.Idempotency;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The normalised recipient set is part of a send's identity (G11-H §7.4).
///
/// <para>
/// <b>Why it has to be.</b> A reused idempotency key means "this is the same attempt". If the recipients
/// are not part of what makes a request itself, then adding an address and pressing send again would be
/// answered "already sent" — and the person just added would never receive anything, with the UI showing
/// success. Including them turns that into a refusal the user can act on.
/// </para>
/// <para>
/// Normalisation matters just as much in the other direction: a retry that re-serialises the same chips
/// in a different order, or with different casing, must NOT look like a new request, or one click becomes
/// two emails.
/// </para>
/// </summary>
public sealed class EmailSendRecipientFingerprintTests
{
    private const ulong Actor = 42;

    private static SendEmailCommand Compose(
        IEnumerable<string>? to = null, IEnumerable<string>? cc = null, IEnumerable<string>? bcc = null,
        string subject = "Chủ đề", string body = "<p>Nội dung</p>")
        => new()
        {
            Subject = subject,
            Body = body,
            To = (to ?? new[] { "a@fpt.edu.vn" }).Select(e => new EmailRecipientDto { Email = e }).ToList(),
            Cc = (cc ?? System.Array.Empty<string>()).Select(e => new EmailRecipientDto { Email = e }).ToList(),
            Bcc = (bcc ?? System.Array.Empty<string>()).Select(e => new EmailRecipientDto { Email = e }).ToList(),
        };

    private static string Fingerprint(SendEmailCommand c) => EmailSendFingerprint.Compute(c, Actor);

    // ── Same request ────────────────────────────────────────────────────────

    [Fact]
    public void The_same_compose_request_fingerprints_the_same_way_twice()
        => Assert.Equal(Fingerprint(Compose()), Fingerprint(Compose()));

    [Fact]
    public void Recipient_order_does_not_change_the_fingerprint()
        => Assert.Equal(
            Fingerprint(Compose(to: new[] { "a@fpt.edu.vn", "b@fpt.edu.vn" })),
            Fingerprint(Compose(to: new[] { "b@fpt.edu.vn", "a@fpt.edu.vn" })));

    [Fact]
    public void Recipient_casing_does_not_change_the_fingerprint()
        => Assert.Equal(
            Fingerprint(Compose(to: new[] { "Person@FPT.edu.vn" })),
            Fingerprint(Compose(to: new[] { "person@fpt.edu.vn" })));

    [Fact]
    public void Surrounding_whitespace_does_not_change_the_fingerprint()
        => Assert.Equal(
            Fingerprint(Compose(to: new[] { "  person@fpt.edu.vn  " })),
            Fingerprint(Compose(to: new[] { "person@fpt.edu.vn" })));

    [Fact]
    public void Repeating_one_address_does_not_change_the_fingerprint()
        => Assert.Equal(
            Fingerprint(Compose(to: new[] { "person@fpt.edu.vn", "PERSON@fpt.edu.vn" })),
            Fingerprint(Compose(to: new[] { "person@fpt.edu.vn" })));

    // ── Different request ───────────────────────────────────────────────────

    [Fact]
    public void Adding_a_to_recipient_changes_the_fingerprint()
        => Assert.NotEqual(
            Fingerprint(Compose(to: new[] { "a@fpt.edu.vn" })),
            Fingerprint(Compose(to: new[] { "a@fpt.edu.vn", "b@fpt.edu.vn" })));

    [Fact]
    public void Adding_a_cc_recipient_changes_the_fingerprint()
        => Assert.NotEqual(
            Fingerprint(Compose()),
            Fingerprint(Compose(cc: new[] { "copied@fpt.edu.vn" })));

    [Fact]
    public void Adding_a_bcc_recipient_changes_the_fingerprint()
        => Assert.NotEqual(
            Fingerprint(Compose()),
            Fingerprint(Compose(bcc: new[] { "hidden@fpt.edu.vn" })));

    /// <summary>
    /// Moving one address between groups is a different request. It changes who can see whom, which is
    /// exactly the kind of change that must not be swallowed as "already sent".
    /// </summary>
    [Fact]
    public void Moving_a_recipient_from_cc_to_bcc_changes_the_fingerprint()
        => Assert.NotEqual(
            Fingerprint(Compose(cc: new[] { "someone@fpt.edu.vn" })),
            Fingerprint(Compose(bcc: new[] { "someone@fpt.edu.vn" })));

    [Fact]
    public void Changing_the_subject_or_body_changes_the_fingerprint()
    {
        Assert.NotEqual(Fingerprint(Compose(subject: "Một")), Fingerprint(Compose(subject: "Hai")));
        Assert.NotEqual(Fingerprint(Compose(body: "<p>Một</p>")), Fingerprint(Compose(body: "<p>Hai</p>")));
    }

    [Fact]
    public void A_different_actor_never_shares_a_fingerprint()
        => Assert.NotEqual(
            EmailSendFingerprint.Compute(Compose(), Actor),
            EmailSendFingerprint.Compute(Compose(), Actor + 1));

    // ── Reply and Reply All ─────────────────────────────────────────────────

    private static ReplytoEmailCommand Reply(bool all = false, ulong original = 7, string body = "<p>Trả lời</p>",
        IEnumerable<string>? cc = null, IEnumerable<string>? bcc = null)
        => new()
        {
            OriginalEmailId = original,
            Body = body,
            ReplyAll = all,
            Cc = (cc ?? System.Array.Empty<string>()).Select(e => new EmailRecipientInput { Email = e }).ToList(),
            Bcc = (bcc ?? System.Array.Empty<string>()).Select(e => new EmailRecipientInput { Email = e }).ToList(),
        };

    /// <summary>
    /// Reply and Reply All reserve under different operation codes, so one key used against both is two
    /// independent reservations rather than a false replay. They send to different people; treating them
    /// as the same attempt would mean the second set never hears from anyone.
    /// </summary>
    [Fact]
    public void Reply_and_reply_all_use_different_operation_codes()
    {
        Assert.Equal(EmailSendOperations.ManualReply, Reply(all: false).OperationCode);
        Assert.Equal(EmailSendOperations.ManualReplyAll, Reply(all: true).OperationCode);
    }

    [Fact]
    public void The_reply_mode_is_part_of_the_fingerprint()
        => Assert.NotEqual(
            EmailSendFingerprint.Compute(Reply(all: false), Actor),
            EmailSendFingerprint.Compute(Reply(all: true), Actor));

    [Fact]
    public void Changing_a_replys_copies_changes_its_fingerprint()
        => Assert.NotEqual(
            EmailSendFingerprint.Compute(Reply(), Actor),
            EmailSendFingerprint.Compute(Reply(cc: new[] { "copied@fpt.edu.vn" }), Actor));

    [Fact]
    public void Replying_to_a_different_message_changes_the_fingerprint()
        => Assert.NotEqual(
            EmailSendFingerprint.Compute(Reply(original: 7), Actor),
            EmailSendFingerprint.Compute(Reply(original: 8), Actor));

    [Fact]
    public void A_reply_fingerprints_the_same_way_twice()
        => Assert.Equal(
            EmailSendFingerprint.Compute(Reply(cc: new[] { "a@fpt.edu.vn" }), Actor),
            EmailSendFingerprint.Compute(Reply(cc: new[] { "A@FPT.edu.vn" }), Actor));

    // ── Coverage ────────────────────────────────────────────────────────────

    /// <summary>
    /// Compose and both reply shapes now declare themselves idempotent. Asserted by operation code rather
    /// than by counting types, so the sibling contract test stays the one that guards the total.
    /// </summary>
    [Fact]
    public void The_client_addressed_sends_are_all_covered()
    {
        Assert.Equal(EmailSendOperations.ManualCompose, Compose().OperationCode);

        Assert.All(EmailSendOperations.Manual, code => Assert.InRange(code.Length, 1, 64));
        Assert.Equal(EmailSendOperations.Manual.Length,
            EmailSendOperations.Manual.Distinct(System.StringComparer.Ordinal).Count());

        foreach (var code in EmailSendOperations.Manual)
            Assert.Contains(code, EmailSendOperations.All);
    }
}
