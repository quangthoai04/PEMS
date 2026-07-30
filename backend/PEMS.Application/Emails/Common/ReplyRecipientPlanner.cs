using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>Which of the two reply shapes is being composed.</summary>
public enum ReplyMode
{
    /// <summary>Answers the original sender alone.</summary>
    SenderOnly,

    /// <summary>Answers the original sender plus everyone who was visibly on the message.</summary>
    All,
}

/// <summary>One recipient row of the message being answered, reduced to what planning needs.</summary>
public sealed record ReplySourceRecipient(string Email, string? Name, string RecipientType);

/// <summary>
/// Works out who a reply is addressed to. Pure and side-effect free so the rules below can be tested
/// directly, rather than only through a send.
///
/// <para>
/// <b>Blind copies are never read here.</b> Not filtered late, not "excluded at the end" — the BCC rows of
/// the original message are not an input to this function at all. Reply All that carried them would tell
/// every visible recipient exactly who had been included quietly, which is the one thing BCC promises will
/// not happen. The caller's OWN blind copies are a different matter: those are a choice this author is
/// making now, and they pass through.
/// </para>
/// <para>
/// <b>Precedence when an address appears twice.</b> TO beats CC beats BCC. The validator refuses an address
/// present in two groups, and it is right to — a message cannot both show and hide the same person — so the
/// duplicate has to be resolved before it gets there, and resolving it upward keeps the recipient at least
/// as visible as the original message had them. Deciding this explicitly matters because the source rows
/// genuinely can contain the same mailbox as both TO and CC.
/// </para>
/// </summary>
public static class ReplyRecipientPlanner
{
    /// <summary>
    /// Builds the reply envelope.
    /// </summary>
    /// <param name="mode">Reply, or Reply All.</param>
    /// <param name="originalSender">The person being answered. Always the first TO.</param>
    /// <param name="originalRecipients">
    /// The recipient rows of the message being answered. BCC rows are ignored; passing them is harmless
    /// but pointless.
    /// </param>
    /// <param name="callerCc">Copies this author chose to add.</param>
    /// <param name="callerBcc">Blind copies this author chose to add.</param>
    /// <param name="currentUserEmail">
    /// The replier's own mailbox, excluded from every group in Reply All. Nobody needs a copy of their own
    /// reply, and in a thread of any length self-addressing compounds.
    /// </param>
    public static ReplyRecipientPlan Plan(
        ReplyMode mode,
        EmailRecipient originalSender,
        IReadOnlyList<ReplySourceRecipient>? originalRecipients,
        IReadOnlyList<EmailRecipient>? callerCc,
        IReadOnlyList<EmailRecipient>? callerBcc,
        string? currentUserEmail)
    {
        var to = new List<EmailRecipient> { originalSender };
        var cc = new List<EmailRecipient>();
        var bcc = new List<EmailRecipient>(callerBcc ?? Array.Empty<EmailRecipient>());

        if (mode == ReplyMode.All)
        {
            var source = originalRecipients ?? Array.Empty<ReplySourceRecipient>();

            to.AddRange(source
                .Where(r => IsType(r, EmailRecipientTypes.To))
                .Select(r => new EmailRecipient(r.Email, r.Name)));

            cc.AddRange(source
                .Where(r => IsType(r, EmailRecipientTypes.Cc))
                .Select(r => new EmailRecipient(r.Email, r.Name)));
        }

        cc.AddRange(callerCc ?? Array.Empty<EmailRecipient>());

        // Self-exclusion applies to Reply All only. In a plain Reply the single TO is the person being
        // answered, and dropping them because they happen to be you would leave nothing to send to; a
        // reply to your own message is a legitimate, if unusual, thing to do.
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (mode == ReplyMode.All && !string.IsNullOrWhiteSpace(currentUserEmail))
            excluded.Add(currentUserEmail!.Trim());

        // Resolved highest-group-first, so an address in both TO and CC survives only as a TO.
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var finalTo = Reduce(to, claimed, excluded);
        var finalCc = Reduce(cc, claimed, excluded);
        var finalBcc = Reduce(bcc, claimed, excluded);

        return new ReplyRecipientPlan(finalTo, finalCc, finalBcc);
    }

    private static bool IsType(ReplySourceRecipient r, string type)
        => string.Equals(r.RecipientType, type, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Drops blanks, drops the excluded mailbox, and keeps the first occurrence of each address — where
    /// "first" spans groups, because <paramref name="claimed"/> is shared and filled TO first.
    /// </summary>
    private static List<EmailRecipient> Reduce(
        IEnumerable<EmailRecipient> source, HashSet<string> claimed, HashSet<string> excluded)
    {
        var result = new List<EmailRecipient>();

        foreach (var candidate in source)
        {
            var email = candidate?.Email?.Trim();
            if (string.IsNullOrEmpty(email)) continue;
            if (excluded.Contains(email)) continue;
            if (!claimed.Add(email)) continue;

            result.Add(new EmailRecipient(email, string.IsNullOrWhiteSpace(candidate!.DisplayName) ? null : candidate.DisplayName));
        }

        return result;
    }
}

/// <summary>The three groups a reply will be sent to, before envelope validation.</summary>
public sealed record ReplyRecipientPlan(
    IReadOnlyList<EmailRecipient> To,
    IReadOnlyList<EmailRecipient> Cc,
    IReadOnlyList<EmailRecipient> Bcc);
