using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The step that turns what the composer posts — one flat list of addresses, each labelled TO, CC or
/// BCC — into the three groups an envelope is made of.
///
/// <para>
/// <see cref="EmailRecipientValidatorTests"/> covers the address rules themselves (duplicates,
/// malformed addresses, header breaks, the ceiling) against <c>EmailRecipientValidator</c>. What is
/// left, and what is tested here, is the layer above it: reading the label, defaulting it, refusing an
/// unknown one, and keeping each address in the group and the order the author put it in. A bug here
/// does not produce an error — it produces an email where a blind copy arrived as a visible one.
/// </para>
/// <para>
/// This was <c>EmailDraftRecipientContractTests</c>, written against the rows that were about to be
/// written to <c>email_draft_recipients</c>. The rules never belonged to the draft: the same call now
/// validates a message on its way straight out, and the participant-invite and logistics-request paths
/// were already using it without a draft at all. The one case that did belong to the draft — "a draft
/// being edited may have no TO yet" — is kept below as the <c>requireTo</c> opt-out, because the
/// distinction it protects is still real: a caller checking a half-assembled envelope wants the
/// address rules without "you have not addressed it yet" being an error.
/// </para>
/// </summary>
public class EmailComposeRecipientContractTests
{
    private const int Ceiling = 50;

    [Fact]
    public void Splits_a_flat_list_into_the_three_groups()
    {
        var envelope = Validate(
            R("to@fpt.edu.vn", "TO"),
            R("cc@fpt.edu.vn", "CC"),
            R("bcc@fpt.edu.vn", "BCC"));

        Assert.Equal(new[] { "to@fpt.edu.vn" }, envelope.To.Select(r => r.Email));
        Assert.Equal(new[] { "cc@fpt.edu.vn" }, envelope.Cc.Select(r => r.Email));
        Assert.Equal(new[] { "bcc@fpt.edu.vn" }, envelope.Bcc.Select(r => r.Email));
    }

    /// <summary>
    /// An unlabelled address is a TO. It has to default to the visible group: defaulting to BCC would
    /// turn a missing field into a silently blind copy, which is the failure nobody catches by reading
    /// the compose screen.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_recipient_type_reads_as_TO(string? type)
    {
        var envelope = Validate(R("someone@fpt.edu.vn", type));

        Assert.Equal(new[] { "someone@fpt.edu.vn" }, envelope.To.Select(r => r.Email));
        Assert.Empty(envelope.Cc);
        Assert.Empty(envelope.Bcc);
    }

    /// <summary>
    /// The label is normalised before it is read, so a lower-cased or padded "bcc" from the client is
    /// still a blind copy rather than an unrecognised label that falls through to the visible group.
    /// </summary>
    [Theory]
    [InlineData("cc", "CC")]
    [InlineData("Cc", "CC")]
    [InlineData(" bcc ", "BCC")]
    [InlineData("BcC", "BCC")]
    public void The_label_is_read_regardless_of_case_or_padding(string written, string expectedGroup)
    {
        // A TO is present because every send needs one; the address under test is the second line.
        var envelope = Validate(R("to@fpt.edu.vn", "TO"), R("someone@fpt.edu.vn", written));

        var group = expectedGroup == "CC" ? envelope.Cc : envelope.Bcc;
        var other = expectedGroup == "CC" ? envelope.Bcc : envelope.Cc;

        Assert.Equal(new[] { "someone@fpt.edu.vn" }, group.Select(r => r.Email));
        Assert.Empty(other);
    }

    /// <summary>
    /// An unknown label is refused rather than quietly treated as TO. A typo'd "BC" that fell through
    /// to the default would put a mailbox the author meant to hide into the visible header.
    /// </summary>
    [Theory]
    [InlineData("BC")]
    [InlineData("REPLY-TO")]
    [InlineData("TO;CC")]
    public void Rejects_a_recipient_type_that_is_not_one_of_the_three(string type)
    {
        var ex = Assert.Throws<ValidationException>(() => Validate(R("someone@fpt.edu.vn", type)));

        Assert.Contains("TO, CC, BCC", ex.Message);
    }

    [Fact]
    public void Sending_requires_a_TO()
    {
        Assert.Throws<ValidationException>(() => Validate(R("cc-only@fpt.edu.vn", "CC")));
    }

    /// <summary>
    /// The opt-out exists for a caller checking an envelope that is still being assembled. It must
    /// still apply every OTHER rule — an opt-out that skipped the address checks would let a malformed
    /// address through to the send, where the author is no longer looking at the compose screen.
    /// </summary>
    [Fact]
    public void A_half_assembled_envelope_may_have_no_TO_yet_but_still_obeys_every_other_rule()
    {
        var envelope = EmailComposeWriter.ValidateRecipients(
            new List<EmailComposeRecipientInput> { R("cc-only@fpt.edu.vn", "CC") }, Ceiling, requireTo: false);
        Assert.Single(envelope.Cc);

        Assert.Throws<ValidationException>(() => EmailComposeWriter.ValidateRecipients(
            new List<EmailComposeRecipientInput> { R("not-an-address", "CC") }, Ceiling, requireTo: false));
    }

    /// <summary>
    /// Reached THROUGH the split rather than by calling the validator directly. The same mailbox in TO
    /// and BCC leaks the blind copy the moment the TO header is read, and the two layers have to agree
    /// on what "the same mailbox" means for that to be caught.
    /// </summary>
    [Theory]
    [InlineData("CC")]
    [InlineData("BCC")]
    public void The_same_address_in_TO_and_a_copy_group_is_refused_through_the_split(string group)
    {
        Assert.Throws<ValidationException>(() => Validate(
            R("Nguoi.Nhan@fpt.edu.vn", "TO"),
            R("nguoi.nhan@fpt.edu.vn", group)));
    }

    [Fact]
    public void The_ceiling_counts_all_three_groups_together()
    {
        var many = Enumerable.Range(0, Ceiling + 1)
            .Select(i => R($"nguoi{i}@fpt.edu.vn", i % 3 == 0 ? "TO" : i % 3 == 1 ? "CC" : "BCC"))
            .ToArray();

        Assert.Throws<ValidationException>(() => Validate(many));
    }

    /// <summary>
    /// Order within a group is the author's. It is what the recipient sees in the header, and a
    /// reordering would show a different message than the one on the compose screen.
    /// </summary>
    [Fact]
    public void Rows_keep_their_group_and_their_order_within_it()
    {
        var envelope = Validate(
            R("first@fpt.edu.vn", "TO"),
            R("cc-first@fpt.edu.vn", "CC"),
            R("second@fpt.edu.vn", "TO"),
            R("cc-second@fpt.edu.vn", "CC"));

        Assert.Equal(new[] { "first@fpt.edu.vn", "second@fpt.edu.vn" }, envelope.To.Select(r => r.Email));
        Assert.Equal(new[] { "cc-first@fpt.edu.vn", "cc-second@fpt.edu.vn" }, envelope.Cc.Select(r => r.Email));
    }

    /// <summary>
    /// Matching ignores case; what is carried out is what the author typed. Lower-casing the stored
    /// address would rewrite a mailbox whose local part is case-sensitive at its own server.
    /// </summary>
    [Fact]
    public void Original_casing_is_carried_even_though_matching_ignores_it()
    {
        var envelope = Validate(R("Nguyen.Van.A@FPT.edu.vn", "TO"));

        Assert.Equal("Nguyen.Van.A@FPT.edu.vn", envelope.To.Single().Email);
    }

    [Fact]
    public void The_display_name_survives_the_split()
    {
        var envelope = Validate(new EmailComposeRecipientInput
        {
            Email = "khach@doitac.vn", Name = "Trần Cảnh", RecipientType = "TO",
        });

        Assert.Equal("Trần Cảnh", envelope.To.Single().DisplayName);
    }

    [Fact]
    public void An_empty_or_absent_list_is_refused_for_a_send()
    {
        Assert.Throws<ValidationException>(() => Validate());
        Assert.Throws<ValidationException>(
            () => EmailComposeWriter.ValidateRecipients(null, Ceiling, requireTo: true));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ValidatedEnvelope Validate(params EmailComposeRecipientInput[] inputs)
        => EmailComposeWriter.ValidateRecipients(inputs, Ceiling, requireTo: true);

    private static EmailComposeRecipientInput R(string email, string? type) => new()
    {
        Email = email,
        RecipientType = type,
    };
}
