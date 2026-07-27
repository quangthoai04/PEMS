using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The recipient rules that stand between a compose screen and SMTP. Every case here is a way a message
/// could previously have gone out wrong: a blind copy exposed by also being in TO, a header injected
/// through a display name, or a list with no TO at all.
/// </summary>
public class EmailRecipientValidatorTests
{
    private const int Limit = 50;

    private static EmailRecipient R(string email, string? name = null) => new(email, name);

    private static ValidatedEnvelope Validate(
        IEnumerable<EmailRecipient>? to = null,
        IEnumerable<EmailRecipient>? cc = null,
        IEnumerable<EmailRecipient>? bcc = null,
        int limit = Limit)
        => EmailRecipientValidator.Validate(to?.ToList(), cc?.ToList(), bcc?.ToList(), limit);

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public void Keeps_all_three_groups_separate()
    {
        var envelope = Validate(
            to: new[] { R("host@fpt.edu.vn", "Host") },
            cc: new[] { R("lead@fpt.edu.vn") },
            bcc: new[] { R("audit@fpt.edu.vn") });

        Assert.Single(envelope.To);
        Assert.Single(envelope.Cc);
        Assert.Single(envelope.Bcc);
        Assert.Equal(3, envelope.Total);
        Assert.True(envelope.HasCopies);
    }

    [Fact]
    public void Preserves_original_casing_while_comparing_case_insensitively()
    {
        var envelope = Validate(to: new[] { R("Ha.Nguyen@fpt.edu.vn") });

        // What the user typed is what gets stored and displayed…
        Assert.Equal("Ha.Nguyen@fpt.edu.vn", envelope.To[0].Email);
        // …but the mailbox identity used for duplicate detection is case-insensitive.
        Assert.Equal("ha.nguyen@fpt.edu.vn", envelope.To[0].NormalizedEmail);
    }

    [Fact]
    public void Trims_addresses_and_drops_blank_entries()
    {
        var envelope = Validate(to: new[] { R("  host@fpt.edu.vn  "), R("   "), R("") });

        Assert.Single(envelope.To);
        Assert.Equal("host@fpt.edu.vn", envelope.To[0].Email);
    }

    [Fact]
    public void Envelope_without_copies_reports_no_copies()
    {
        var envelope = Validate(to: new[] { R("host@fpt.edu.vn") });
        Assert.False(envelope.HasCopies);
    }

    // ── Required TO ──────────────────────────────────────────────────────────

    [Fact]
    public void Rejects_an_envelope_with_no_TO()
    {
        var ex = Assert.Throws<ValidationException>(() => Validate(cc: new[] { R("lead@fpt.edu.vn") }));
        Assert.Equal(EmailErrorCodes.RecipientRequired, ex.ErrorCode);
    }

    [Fact]
    public void Allows_a_TO_less_envelope_only_when_the_caller_opts_out()
    {
        var envelope = EmailRecipientValidator.Validate(
            to: Array.Empty<EmailRecipient>(),
            cc: new[] { R("lead@fpt.edu.vn") },
            bcc: null,
            maxRecipients: Limit,
            requireTo: false);

        Assert.Empty(envelope.To);
        Assert.Single(envelope.Cc);
    }

    // ── Format ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.com")]
    [InlineData("two@at@signs.com")]
    [InlineData("no-dot@localhost")]
    [InlineData("trailing.dot@fpt.")]
    [InlineData("double..dot@fpt..vn")]
    public void Rejects_a_malformed_address(string bad)
    {
        var ex = Assert.Throws<ValidationException>(() => Validate(to: new[] { R(bad) }));
        Assert.Equal(EmailErrorCodes.RecipientInvalid, ex.ErrorCode);
    }

    [Theory]
    [InlineData("host@fpt.edu.vn")]
    [InlineData("first.last+tag@sub.domain.co.uk")]
    public void Accepts_a_well_formed_address(string good)
        => Assert.Single(Validate(to: new[] { R(good) }).To);

    // ── Duplicates ───────────────────────────────────────────────────────────

    [Fact]
    public void Rejects_the_same_address_twice_in_one_group_regardless_of_case()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            Validate(to: new[] { R("host@fpt.edu.vn"), R("HOST@FPT.EDU.VN") }));

        Assert.Equal(EmailErrorCodes.RecipientDuplicate, ex.ErrorCode);
    }

    [Theory]
    [InlineData("cc")]
    [InlineData("bcc")]
    public void Rejects_the_same_address_in_TO_and_a_copy_group(string group)
    {
        var dup = R("host@fpt.edu.vn");

        var ex = Assert.Throws<ValidationException>(() => group == "cc"
            ? Validate(to: new[] { dup }, cc: new[] { R("HOST@fpt.edu.vn") })
            : Validate(to: new[] { dup }, bcc: new[] { R("HOST@fpt.edu.vn") }));

        // Left unchecked this is a privacy bug, not a tidiness one: the address is visible in the TO
        // header while the sender believes that person was blind-copied.
        Assert.Equal(EmailErrorCodes.RecipientCrossGroupDuplicate, ex.ErrorCode);
    }

    [Fact]
    public void Rejects_the_same_address_in_CC_and_BCC()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            Validate(to: new[] { R("host@fpt.edu.vn") },
                     cc: new[] { R("lead@fpt.edu.vn") },
                     bcc: new[] { R("Lead@fpt.edu.vn") }));

        Assert.Equal(EmailErrorCodes.RecipientCrossGroupDuplicate, ex.ErrorCode);
    }

    // ── Limit ────────────────────────────────────────────────────────────────

    [Fact]
    public void Rejects_a_list_over_the_configured_ceiling()
    {
        var many = Enumerable.Range(1, 4).Select(i => R($"p{i}@fpt.edu.vn")).ToList();

        var ex = Assert.Throws<ValidationException>(() => Validate(to: many, limit: 3));
        Assert.Equal(EmailErrorCodes.RecipientLimitExceeded, ex.ErrorCode);
    }

    [Fact]
    public void Counts_all_three_groups_towards_the_ceiling()
    {
        var ex = Assert.Throws<ValidationException>(() => Validate(
            to: new[] { R("a@fpt.edu.vn") },
            cc: new[] { R("b@fpt.edu.vn") },
            bcc: new[] { R("c@fpt.edu.vn") },
            limit: 2));

        Assert.Equal(EmailErrorCodes.RecipientLimitExceeded, ex.ErrorCode);
    }

    [Fact]
    public void Accepts_a_list_exactly_at_the_ceiling()
        => Assert.Equal(3, Validate(
            to: new[] { R("a@fpt.edu.vn"), R("b@fpt.edu.vn"), R("c@fpt.edu.vn") }, limit: 3).Total);

    // ── Header injection ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("host@fpt.edu.vn\r\nBcc: attacker@evil.test")]
    [InlineData("host@fpt.edu.vn\nBcc: attacker@evil.test")]
    [InlineData("host@fpt.edu.vn\0")]
    public void Rejects_an_address_carrying_a_header_break(string bad)
    {
        var ex = Assert.Throws<ValidationException>(() => Validate(to: new[] { R(bad) }));
        Assert.Equal(EmailErrorCodes.HeaderInvalid, ex.ErrorCode);
    }

    [Fact]
    public void Rejects_a_display_name_carrying_a_header_break()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            Validate(to: new[] { R("host@fpt.edu.vn", "Host\r\nBcc: attacker@evil.test") }));

        Assert.Equal(EmailErrorCodes.HeaderInvalid, ex.ErrorCode);
    }

    [Fact]
    public void AssertNoHeaderBreak_accepts_ordinary_vietnamese_text()
    {
        // Diacritics are fine — only line breaks and NUL are structural.
        EmailRecipientValidator.AssertNoHeaderBreak("Nguyễn Văn A — Phòng Hợp tác", "tiêu đề");
    }
}
