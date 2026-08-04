using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Common;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The draft's side of the recipient contract: splitting a flat input list into TO/CC/BCC, checking it,
/// and turning it back into rows that survive a round-trip.
///
/// <para>
/// A draft used to accept anything at all — the handler looped the input, skipped blanks and inserted
/// whatever remained. So a draft could hold two identical addresses, or the same mailbox in both TO and
/// BCC, and nothing complained until the moment of dispatch, long after the author had moved on.
/// </para>
/// <para>
/// The asymmetry that matters here: while a draft is being edited it may legitimately have no TO yet
/// (someone is still writing it), but at send a TO is required. Same rules, one parameter apart.
/// </para>
/// </summary>
public class EmailDraftRecipientContractTests
{
    private const int Limit = 50;

    private static EmailComposeRecipientInput In(string email, string type = "TO", string? name = null, int order = 0)
        => new() { Email = email, RecipientType = type, Name = name, DisplayOrder = order };

    private static ValidatedEnvelope Validate(IEnumerable<EmailComposeRecipientInput> inputs, bool requireTo = false)
        => EmailComposeWriter.ValidateRecipients(inputs.ToList(), Limit, requireTo);

    private static string CodeOf(Action act)
        => Assert.Throws<ValidationException>(act).ErrorCode ?? string.Empty;

    // ── Splitting ────────────────────────────────────────────────────────────

    [Fact]
    public void Splits_a_flat_list_into_the_three_groups()
    {
        var envelope = Validate(new[]
        {
            In("host@fpt.edu.vn"),
            In("lead@fpt.edu.vn", "CC"),
            In("audit@fpt.edu.vn", "BCC"),
            In("second@fpt.edu.vn", "to"),
        });

        Assert.Equal(new[] { "host@fpt.edu.vn", "second@fpt.edu.vn" }, envelope.To.Select(r => r.Email));
        Assert.Equal(new[] { "lead@fpt.edu.vn" }, envelope.Cc.Select(r => r.Email));
        Assert.Equal(new[] { "audit@fpt.edu.vn" }, envelope.Bcc.Select(r => r.Email));
    }

    [Fact]
    public void A_missing_recipient_type_reads_as_TO()
    {
        var envelope = Validate(new[] { new EmailComposeRecipientInput { Email = "host@fpt.edu.vn" } });

        Assert.Single(envelope.To);
        Assert.Empty(envelope.Bcc);
    }

    [Fact]
    public void Rejects_a_recipient_type_that_is_not_one_of_the_three()
        => Assert.Throws<ValidationException>(() => Validate(new[] { In("host@fpt.edu.vn", "SECRET") }));

    // ── Editing versus sending ───────────────────────────────────────────────

    [Fact]
    public void A_draft_being_edited_may_have_no_TO_yet()
    {
        var envelope = Validate(new[] { In("lead@fpt.edu.vn", "CC") }, requireTo: false);

        Assert.Empty(envelope.To);
        Assert.Single(envelope.Cc);
    }

    [Fact]
    public void Sending_requires_a_TO()
        => Assert.Equal(
            EmailErrorCodes.RecipientRequired,
            CodeOf(() => Validate(new[] { In("lead@fpt.edu.vn", "CC"), In("a@fpt.edu.vn", "BCC") }, requireTo: true)));

    // ── The rules that used to fire only at dispatch ─────────────────────────

    [Theory]
    [InlineData("TO")]
    [InlineData("CC")]
    [InlineData("BCC")]
    public void Rejects_the_same_address_twice_in_the_same_group(string group)
        => Assert.Equal(
            EmailErrorCodes.RecipientDuplicate,
            CodeOf(() => Validate(new[]
            {
                In("host@fpt.edu.vn"),
                In("lead@fpt.edu.vn", group),
                In("LEAD@fpt.edu.vn", group),
            })));

    [Theory]
    [InlineData("CC")]
    [InlineData("BCC")]
    public void Rejects_the_same_address_in_TO_and_a_copy_group(string group)
        => Assert.Equal(
            EmailErrorCodes.RecipientCrossGroupDuplicate,
            CodeOf(() => Validate(new[] { In("host@fpt.edu.vn"), In("Host@FPT.edu.vn", group) })));

    [Fact]
    public void Rejects_the_same_address_in_CC_and_BCC()
        => Assert.Equal(
            EmailErrorCodes.RecipientCrossGroupDuplicate,
            CodeOf(() => Validate(new[]
            {
                In("host@fpt.edu.vn"),
                In("lead@fpt.edu.vn", "CC"),
                In("lead@fpt.edu.vn", "BCC"),
            })));

    [Fact]
    public void Rejects_a_malformed_address()
        => Assert.Equal(
            EmailErrorCodes.RecipientInvalid,
            CodeOf(() => Validate(new[] { In("khong-phai-email") })));

    [Fact]
    public void Rejects_a_display_name_carrying_a_header_break()
        => Assert.Equal(
            EmailErrorCodes.HeaderInvalid,
            CodeOf(() => Validate(new[] { In("host@fpt.edu.vn", "TO", "Hà\r\nBcc: attacker@evil.com") })));

    [Fact]
    public void Rejects_a_list_over_the_ceiling_counting_all_three_groups()
    {
        var many = Enumerable.Range(0, Limit).Select(i => In($"user{i}@fpt.edu.vn")).ToList();
        many.Add(In("one.too.many@fpt.edu.vn", "BCC"));

        Assert.Equal(EmailErrorCodes.RecipientLimitExceeded, CodeOf(() => Validate(many)));
    }

    // ── Round-trip ───────────────────────────────────────────────────────────

    [Fact]
    public void Rows_keep_their_group_and_their_order_within_it()
    {
        var envelope = Validate(new[]
        {
            In("to1@fpt.edu.vn"),
            In("cc1@fpt.edu.vn", "CC"),
            In("to2@fpt.edu.vn", "TO", "Người thứ hai"),
            In("bcc1@fpt.edu.vn", "BCC"),
            In("cc2@fpt.edu.vn", "CC"),
        });

        var rows = EmailComposeWriter.ToDraftRows(42, envelope, new DateTime(2026, 7, 27, 9, 0, 0)).ToList();

        Assert.All(rows, r => Assert.Equal(42ul, r.EmailDraftId));

        var to = rows.Where(r => r.RecipientType == "TO").ToList();
        var cc = rows.Where(r => r.RecipientType == "CC").ToList();
        var bcc = rows.Where(r => r.RecipientType == "BCC").ToList();

        Assert.Equal(new[] { "to1@fpt.edu.vn", "to2@fpt.edu.vn" }, to.Select(r => r.RecipientEmail));
        Assert.Equal(new[] { "cc1@fpt.edu.vn", "cc2@fpt.edu.vn" }, cc.Select(r => r.RecipientEmail));
        Assert.Equal(new[] { "bcc1@fpt.edu.vn" }, bcc.Select(r => r.RecipientEmail));

        Assert.Equal(new uint[] { 0, 1 }, to.Select(r => r.DisplayOrder));
        Assert.Equal("Người thứ hai", to[1].RecipientName);
    }

    [Fact]
    public void Original_casing_is_stored_even_though_matching_ignores_it()
    {
        var envelope = Validate(new[] { In("Ha.Nguyen@fpt.edu.vn") });
        var rows = EmailComposeWriter.ToDraftRows(1, envelope, DateTime.Now).ToList();

        Assert.Equal("Ha.Nguyen@fpt.edu.vn", rows.Single().RecipientEmail);
    }
}
