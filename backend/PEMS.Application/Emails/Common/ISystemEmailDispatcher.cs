using System.Collections.Generic;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// One system email, described by what it means rather than by how it looks. The subject and body are
/// not here because they are not the caller's to decide — they come from <c>email_templates</c>.
/// </summary>
/// <param name="TemplateCode">A registered system template.</param>
/// <param name="To">The single addressee. System mail is one message per person.</param>
/// <param name="Variables">Exactly the variables the template declares.</param>
/// <param name="Language">VI or EN.</param>
/// <param name="TrustedBlocks">
/// Backend-built HTML for <c>{{actionBlock}}</c> and friends. The only route by which markup — and
/// therefore any one-time link — can enter a rendered body.
/// </param>
/// <param name="RelatedType">Domain object this message is about, for the email history.</param>
/// <param name="RelatedId">Id of that object.</param>
/// <param name="SentBy">Acting user, when there is one. Null for system-initiated mail.</param>
/// <param name="Attachments">Resolved attachment bytes (reports carry a PDF).</param>
/// <param name="Cc">
/// Carbon copies. Only honoured for a <see cref="EmailRecipientPolicy.CallerControlled"/> template —
/// <see cref="EmailRecipientPolicyEnforcer"/> still rejects it for a single-recipient one, exactly as it
/// would for a caller that tried to CC an OTP mail.
/// </param>
/// <param name="ReplyTo">Overrides the configured Reply-To for this message only. Null keeps the default.</param>
public sealed record SystemEmailRequest(
    string TemplateCode,
    EmailRecipient To,
    IReadOnlyDictionary<string, string> Variables,
    string Language = EmailLanguages.Vi,
    IReadOnlyDictionary<string, string>? TrustedBlocks = null,
    string? RelatedType = null,
    ulong? RelatedId = null,
    ulong? SentBy = null,
    IReadOnlyList<OutboundAttachment>? Attachments = null,
    IReadOnlyList<EmailRecipient>? Cc = null,
    EmailRecipient? ReplyTo = null)
{
    /// <summary>
    /// Where the words come from: the template (the default, and the only source when nobody has edited
    /// anything) or content a person was authorised to write. Everything else about the message is
    /// unaffected by this choice — see <see cref="SystemEmailContent"/>.
    /// </summary>
    public SystemEmailContent Content { get; init; } = SystemEmailContent.FromTemplate.Instance;
}

/// <summary>
/// Renders a system email from the database, records it in the email history, sends it, and writes back
/// the truthful outcome.
///
/// <para>
/// It exists so those four steps cannot drift apart across the ~30 places that send mail. Before it, each
/// caller wrote its own HTML, most wrote no history row at all, and the two that did marked a message
/// <c>DELIVERED</c> the moment SMTP accepted it. Going through one pipeline is what makes
/// "content comes from the database", "history matches what was sent" and "acceptance is not delivery"
/// true everywhere rather than in the handlers somebody remembered.
/// </para>
/// </summary>
public interface ISystemEmailDispatcher
{
    /// <summary>
    /// Sends and returns the outcome without throwing on a delivery failure. A BROKEN TEMPLATE still
    /// throws: that is a configuration fault an operator must see, not a delivery outcome to record.
    ///
    /// <para>
    /// <b>Precondition — call this AFTER your business change is saved or committed.</b> The
    /// implementation shares the caller's <see cref="IApplicationDbContext"/> and calls
    /// <c>SaveChangesAsync</c> to record the message. EF has no partial save, so any OTHER entity the
    /// caller still has tracked-and-modified at that moment would be written too — a business change
    /// committed early, by a side effect of sending mail.
    /// </para>
    /// <para>
    /// Every account handler already follows this order, and for a reason that predates this interface:
    /// a failed or skipped email must never roll back an account that was successfully created. Keeping
    /// the send strictly after the commit is what makes both properties true at once.
    /// </para>
    /// <para>
    /// The implementation opens no transaction of its own, so it never interferes with one the caller
    /// has already committed and disposed.
    /// </para>
    /// </summary>
    Task<SystemEmailDispatchResult> SendAsync(SystemEmailRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders, validates and RECORDS the message without sending it, returning the identifiers of the
    /// rows it wrote. For callers that must link their own rows to this message atomically —
    /// <c>email_action_tokens.sent_email_id</c> is NOT NULL, so an invitation's accept token cannot be
    /// written before the <c>sent_emails</c> row it belongs to exists.
    ///
    /// <para>
    /// Call it INSIDE your transaction, then <see cref="DeliverAsync"/> AFTER you commit. Splitting the
    /// two is what lets the caller keep transaction ownership and still never hand a message to SMTP that
    /// a rollback would erase: <see cref="SendAsync"/> is literally these two steps back to back, so
    /// neither path can drift from the other.
    /// </para>
    /// <para>
    /// The same precondition as <see cref="SendAsync"/> applies — this calls <c>SaveChangesAsync</c>, and
    /// EF has no partial save, so any other entity you have tracked-and-modified at that moment is
    /// written too.
    /// </para>
    /// </summary>
    Task<PreparedSystemEmail> PrepareAsync(SystemEmailRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message that <see cref="PrepareAsync"/> already recorded, and writes back the truthful
    /// outcome. Never throws on a delivery failure — the failure is recorded on the existing rows.
    /// </summary>
    Task<EmailDeliveryResult> DeliverAsync(PreparedSystemEmail prepared, CancellationToken cancellationToken = default);
}

/// <summary>
/// A message that has been rendered, checked and written to the email history but not yet sent.
///
/// <para>
/// It carries the exact bytes that will be delivered, so nothing is re-rendered between the two phases:
/// re-rendering would open a window in which an operator's template edit lands between the recorded
/// snapshot and the delivered message, making the history a record of something nobody received.
/// </para>
/// </summary>
/// <param name="SentEmailId">The <c>sent_emails</c> row — link your own rows to this.</param>
/// <param name="SentEmailRecipientId">The single <c>sent_email_recipients</c> row.</param>
/// <param name="EmailTemplateId">The template the content came from, in both content modes.</param>
/// <param name="TemplateCode">Travels to the sender so the recipient policy is re-checked there.</param>
/// <param name="To">The addressee.</param>
/// <param name="Subject">The final subject.</param>
/// <param name="Body">The final body, including the branded shell and the action block.</param>
/// <param name="IsHtml">Whether <paramref name="Body"/> is HTML.</param>
public sealed record PreparedSystemEmail(
    ulong SentEmailId,
    ulong SentEmailRecipientId,
    ulong EmailTemplateId,
    string TemplateCode,
    EmailRecipient To,
    string Subject,
    string Body,
    bool IsHtml)
{
    /// <summary>
    /// Attachment bytes for delivery. Init-only so a caller that resolves them late — streaming a file
    /// from storage only once the business change is committed — can supply them with
    /// <c>prepared with { Attachments = … }</c> instead of loading storage inside a transaction.
    /// </summary>
    public IReadOnlyList<OutboundAttachment> Attachments { get; init; } = System.Array.Empty<OutboundAttachment>();

    /// <summary>Carbon copies already validated and recorded by <see cref="ISystemEmailDispatcher.PrepareAsync"/>.</summary>
    public IReadOnlyList<EmailRecipient> Cc { get; init; } = System.Array.Empty<EmailRecipient>();

    /// <summary>Reply-To override for this message, when the request supplied one.</summary>
    public EmailRecipient? ReplyTo { get; init; }
}

/// <summary>How much of a rendered body may be kept in <c>sent_emails.body_snapshot</c>.</summary>
public enum HistoryBodyPolicy
{
    /// <summary>Store what was sent. Nothing in the message grants access on its own.</summary>
    Full,

    /// <summary>
    /// Store the body with the action block removed. The message is worth keeping — somebody may need
    /// to see what an invitee was told — but the one-time links inside the action block are credentials
    /// and must not survive in a table the history API serves to every internal role.
    /// </summary>
    ActionBlockStripped,

    /// <summary>
    /// Store nothing. Used when the secret IS the message: an OTP mail stripped of its code is not a
    /// redacted record, it is an empty one.
    /// </summary>
    None,
}

/// <summary>
/// Decides how much of a message the email history may keep, from the template's own classification —
/// never from a hand-maintained list of codes.
///
/// <para>
/// <c>sent_emails.body_snapshot</c> is readable through the email-history API by every internal role and
/// without a recipient check, so anything stored there is effectively shared with the whole staff. That
/// makes the question "is the secret the content, or is it a link inside the content?". A reset code is
/// the content; an invitation's accept link is not, and removing just the link leaves a record that is
/// both useful and harmless.
/// </para>
/// </summary>
public static class SensitiveEmailHistory
{
    /// <summary>The policy for a template code (an unregistered code is user-authored mail).</summary>
    public static HistoryBodyPolicy PolicyFor(string? templateCode)
    {
        var template = SystemEmailTemplates.Find(templateCode);
        if (template is null || !template.HasSensitiveAction) return HistoryBodyPolicy.Full;

        // A declared credential variable means the code/token is interpolated INTO the text.
        return SensitiveEmailVariables.DeclaredBy(template).Count > 0
            ? HistoryBodyPolicy.None
            : HistoryBodyPolicy.ActionBlockStripped;
    }

    /// <summary>True when the template's rendered body must not be stored at all.</summary>
    public static bool OmitsBody(string? templateCode)
        => PolicyFor(templateCode) == HistoryBodyPolicy.None;

    /// <summary>Applies the policy to a rendered body.</summary>
    public static string? Apply(string? templateCode, string body)
        => PolicyFor(templateCode) switch
        {
            HistoryBodyPolicy.None => null,
            HistoryBodyPolicy.ActionBlockStripped => EmailComposition.StripActionArtifacts(body),
            _ => body,
        };
}

/// <summary>Outcome of one dispatch, including the history row it produced.</summary>
/// <param name="Delivery">The truthful provider outcome.</param>
/// <param name="SentEmailId">The <c>sent_emails</c> row written for this message.</param>
/// <param name="EmailTemplateId">The template the content came from.</param>
public sealed record SystemEmailDispatchResult(
    EmailDeliveryResult Delivery,
    ulong SentEmailId,
    ulong EmailTemplateId)
{
    /// <summary>
    /// The string status the account APIs already surface to the UI: SENT / SKIPPED / FAILED.
    /// </summary>
    public string NotificationStatus => Delivery.Status switch
    {
        EmailDeliveryStatus.Sent => "SENT",
        EmailDeliveryStatus.Skipped => "SKIPPED",
        _ => "FAILED",
    };
}
