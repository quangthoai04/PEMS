using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// Who a reply is addressed to (G11-H §7.2).
///
/// <para>
/// Tested directly rather than only through a send, because the rule that matters most here is a
/// NEGATIVE one — the original's blind copies never appear — and a negative is far better proved against
/// the function that decides than against a message that happens not to contain them this time.
/// </para>
/// </summary>
public sealed class ReplyRecipientPlannerTests
{
    private static readonly EmailRecipient Sender = new("author@fpt.edu.vn", "Tác giả");

    private static ReplySourceRecipient To(string email, string? name = null)
        => new(email, name, EmailRecipientTypes.To);

    private static ReplySourceRecipient Cc(string email, string? name = null)
        => new(email, name, EmailRecipientTypes.Cc);

    private static ReplySourceRecipient Bcc(string email, string? name = null)
        => new(email, name, EmailRecipientTypes.Bcc);

    private static ReplyRecipientPlan Plan(
        ReplyMode mode,
        IReadOnlyList<ReplySourceRecipient>? source = null,
        IReadOnlyList<EmailRecipient>? cc = null,
        IReadOnlyList<EmailRecipient>? bcc = null,
        string? me = "me@fpt.edu.vn")
        => ReplyRecipientPlanner.Plan(mode, Sender, source, cc, bcc, me);

    private static IEnumerable<string> Addresses(IReadOnlyList<EmailRecipient> group)
        => group.Select(r => r.Email);

    // ── Reply ───────────────────────────────────────────────────────────────

    [Fact]
    public void Reply_addresses_the_original_sender_and_nobody_else()
    {
        var plan = Plan(ReplyMode.SenderOnly, new[]
        {
            To("someone@fpt.edu.vn"), Cc("copied@fpt.edu.vn"), Bcc("hidden@fpt.edu.vn"),
        });

        Assert.Equal(new[] { Sender.Email }, Addresses(plan.To));
        Assert.Empty(plan.Cc);
        Assert.Empty(plan.Bcc);
    }

    [Fact]
    public void Reply_keeps_the_copies_this_author_chose()
    {
        var plan = Plan(ReplyMode.SenderOnly,
            cc: new[] { new EmailRecipient("newcc@fpt.edu.vn") },
            bcc: new[] { new EmailRecipient("newbcc@fpt.edu.vn") });

        Assert.Equal(new[] { Sender.Email }, Addresses(plan.To));
        Assert.Equal(new[] { "newcc@fpt.edu.vn" }, Addresses(plan.Cc));
        Assert.Equal(new[] { "newbcc@fpt.edu.vn" }, Addresses(plan.Bcc));
    }

    /// <summary>
    /// Replying to your own message still addresses you: self-exclusion belongs to Reply All, and
    /// applying it here would leave nothing to send to.
    /// </summary>
    [Fact]
    public void Reply_to_your_own_message_still_addresses_you()
    {
        var plan = ReplyRecipientPlanner.Plan(
            ReplyMode.SenderOnly, Sender, null, null, null, currentUserEmail: Sender.Email);

        Assert.Equal(new[] { Sender.Email }, Addresses(plan.To));
    }

    // ── Reply All ───────────────────────────────────────────────────────────

    [Fact]
    public void Reply_all_addresses_the_sender_and_the_original_visible_recipients()
    {
        var plan = Plan(ReplyMode.All, new[]
        {
            To("first@fpt.edu.vn"), To("second@fpt.edu.vn"), Cc("copied@fpt.edu.vn"),
        });

        Assert.Equal(new[] { Sender.Email, "first@fpt.edu.vn", "second@fpt.edu.vn" }, Addresses(plan.To));
        Assert.Equal(new[] { "copied@fpt.edu.vn" }, Addresses(plan.Cc));
    }

    /// <summary>The whole point of the feature's danger: a blind copy must never resurface.</summary>
    [Fact]
    public void Reply_all_never_carries_a_blind_copy_from_the_original()
    {
        var plan = Plan(ReplyMode.All, new[]
        {
            To("visible@fpt.edu.vn"), Bcc("hidden@fpt.edu.vn"), Bcc("alsohidden@fpt.edu.vn"),
        });

        var everyone = Addresses(plan.To).Concat(Addresses(plan.Cc)).Concat(Addresses(plan.Bcc)).ToList();

        Assert.DoesNotContain("hidden@fpt.edu.vn", everyone);
        Assert.DoesNotContain("alsohidden@fpt.edu.vn", everyone);
    }

    [Fact]
    public void Reply_all_excludes_the_current_user_from_every_group()
    {
        var plan = Plan(ReplyMode.All,
            new[] { To("me@fpt.edu.vn"), To("other@fpt.edu.vn"), Cc("ME@fpt.edu.vn") },
            cc: new[] { new EmailRecipient("me@FPT.edu.vn") });

        var everyone = Addresses(plan.To).Concat(Addresses(plan.Cc)).Concat(Addresses(plan.Bcc)).ToList();

        Assert.DoesNotContain(everyone, a => a.Equals("me@fpt.edu.vn", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains("other@fpt.edu.vn", everyone);
    }

    [Fact]
    public void Reply_all_deduplicates_case_insensitively()
    {
        var plan = Plan(ReplyMode.All, new[]
        {
            To("Person@fpt.edu.vn"), To("person@FPT.edu.vn"), To(" person@fpt.edu.vn "),
        });

        Assert.Equal(2, plan.To.Count);   // the sender, plus one copy of that person
        Assert.Contains(plan.To, r => r.Email.Equals("Person@fpt.edu.vn", System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// TO beats CC beats BCC. The source rows really can hold one mailbox twice, and the envelope
    /// validator refuses an address present in two groups — so the duplicate has to be resolved here, and
    /// resolving it upward keeps the person at least as visible as the original message had them.
    /// </summary>
    [Fact]
    public void An_address_in_two_groups_survives_only_in_the_more_visible_one()
    {
        var plan = Plan(ReplyMode.All,
            new[] { To("both@fpt.edu.vn"), Cc("both@fpt.edu.vn") },
            cc: new[] { new EmailRecipient("both@fpt.edu.vn") },
            bcc: new[] { new EmailRecipient("both@fpt.edu.vn") });

        Assert.Contains("both@fpt.edu.vn", Addresses(plan.To));
        Assert.DoesNotContain("both@fpt.edu.vn", Addresses(plan.Cc));
        Assert.DoesNotContain("both@fpt.edu.vn", Addresses(plan.Bcc));
    }

    [Fact]
    public void The_original_sender_is_always_first_in_to()
    {
        var plan = Plan(ReplyMode.All, new[] { To("aaa@fpt.edu.vn"), To("zzz@fpt.edu.vn") });

        Assert.Equal(Sender.Email, plan.To[0].Email);
    }

    /// <summary>
    /// The sender is not dropped by the CC branch: if they were also CC'd on their own message, they must
    /// still be the TO of the reply.
    /// </summary>
    [Fact]
    public void A_sender_who_was_also_copied_stays_in_to()
    {
        var plan = Plan(ReplyMode.All, new[] { Cc(Sender.Email) });

        Assert.Contains(Sender.Email, Addresses(plan.To));
        Assert.DoesNotContain(Sender.Email, Addresses(plan.Cc));
    }

    [Fact]
    public void Blank_and_whitespace_addresses_are_dropped()
    {
        var plan = Plan(ReplyMode.All,
            new[] { To("  "), To(""), To("real@fpt.edu.vn") },
            cc: new[] { new EmailRecipient("   ") });

        Assert.Equal(new[] { Sender.Email, "real@fpt.edu.vn" }, Addresses(plan.To));
        Assert.Empty(plan.Cc);
    }

    [Fact]
    public void Addresses_are_trimmed()
    {
        var plan = Plan(ReplyMode.All, new[] { To("  spaced@fpt.edu.vn  ") });

        Assert.Contains("spaced@fpt.edu.vn", Addresses(plan.To));
    }

    /// <summary>
    /// The caller's own blind copies are theirs to choose and do pass through — the rule is about the
    /// ORIGINAL's blind copies, not about forbidding BCC on a reply.
    /// </summary>
    [Fact]
    public void The_authors_own_blind_copies_pass_through_in_reply_all()
    {
        var plan = Plan(ReplyMode.All,
            new[] { To("visible@fpt.edu.vn"), Bcc("hidden@fpt.edu.vn") },
            bcc: new[] { new EmailRecipient("mychoice@fpt.edu.vn") });

        Assert.Equal(new[] { "mychoice@fpt.edu.vn" }, Addresses(plan.Bcc));
        Assert.DoesNotContain("hidden@fpt.edu.vn", Addresses(plan.Bcc));
    }

    [Fact]
    public void A_missing_current_user_email_does_not_exclude_everyone()
    {
        var plan = Plan(ReplyMode.All, new[] { To("someone@fpt.edu.vn") }, me: null);

        Assert.Equal(new[] { Sender.Email, "someone@fpt.edu.vn" }, Addresses(plan.To));
    }

    [Fact]
    public void An_empty_original_recipient_list_reduces_reply_all_to_a_plain_reply()
    {
        var plan = Plan(ReplyMode.All, System.Array.Empty<ReplySourceRecipient>());

        Assert.Equal(new[] { Sender.Email }, Addresses(plan.To));
        Assert.Empty(plan.Cc);
    }

    [Fact]
    public void Display_names_survive_planning()
    {
        var plan = Plan(ReplyMode.All, new[] { To("named@fpt.edu.vn", "Người Nhận") });

        Assert.Equal("Người Nhận", plan.To.Single(r => r.Email == "named@fpt.edu.vn").DisplayName);
    }
}
