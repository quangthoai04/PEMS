namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// One addressee on an outbound email. Which envelope group it belongs to (TO / CC / BCC) is expressed
/// by WHICH list on <see cref="OutboundEmail"/> holds it, not by a field here — so a recipient cannot
/// carry a group that disagrees with the list it sits in.
/// </summary>
/// <param name="Email">The mailbox address. Validated by <c>EmailRecipientValidator</c> before dispatch.</param>
/// <param name="DisplayName">
/// Optional friendly name. Like the address it must not contain CR/LF: both end up in message headers,
/// where a newline would let the value inject headers of its own.
/// </param>
public sealed record EmailRecipient(string Email, string? DisplayName = null)
{
    /// <summary>Lower-cased address used for duplicate detection. Never persisted — the original casing is.</summary>
    public string NormalizedEmail => Email.Trim().ToLowerInvariant();

    public override string ToString()
        => string.IsNullOrWhiteSpace(DisplayName) ? Email : $"{DisplayName} <{Email}>";
}

/// <summary>The three envelope groups, matching <c>sent_email_recipients.recipient_type</c>.</summary>
public static class EmailRecipientTypes
{
    public const string To = "TO";
    public const string Cc = "CC";
    public const string Bcc = "BCC";
}
