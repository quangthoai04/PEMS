using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PEMS.Application.Emails.Preview;

/// <summary>Which stage of the preview flow a token was issued for.</summary>
public static class EmailPreviewPurposes
{
    /// <summary>Issued by the read-only VIEW. Proves what was rendered; grants no send.</summary>
    public const string Prepare = "PREPARE";

    /// <summary>
    /// Issued by the final preview. Binds the exact content a person looked at and approved, and is the
    /// only token a send accepts.
    /// </summary>
    public const string Final = "FINAL";
}

/// <summary>
/// What a preview token asserts. Every field is part of the signature, so changing any of them after the
/// fact invalidates the token rather than silently altering what gets sent.
///
/// <para>
/// <b>Why a signed token rather than a draft row.</b> The obvious implementation is a table: write the
/// prepared message, hand back its id, read it at send time. That is what <c>email_drafts</c> used to be
/// and it was deleted for a reason — a draft is a second, mutable copy of the message with its own
/// lifetime, its own cleanup problem and its own opportunity to disagree with the send. A signed token
/// stores nothing: the content travels with the request, and the signature is what makes "this is the
/// content the sender approved" checkable without trusting the client. The plan forbids reintroducing
/// those tables (§6.3), and this is why that is not merely a schema preference.
/// </para>
/// <para>
/// <b>Hashes, not content.</b> The token carries no subject, no body and no recipient address. It is
/// visible to the client and travels in request bodies and logs; putting the message inside it would put
/// a copy of every prepared email somewhere with no retention policy. Hashing gives the same guarantee —
/// the send recomputes the hash of what it is about to send and compares — while the token itself
/// discloses nothing.
/// </para>
/// </summary>
/// <param name="Purpose">One of <see cref="EmailPreviewPurposes"/>.</param>
/// <param name="ActorUserId">
/// Who prepared it. The send refuses a token presented by anybody else, so a token leaked from one
/// person's browser cannot be used to send as them.
/// </param>
/// <param name="TemplateRevision">
/// The <c>email_templates.revision</c> the preview rendered from. An operator who re-saves the template
/// between preview and send makes every outstanding token STALE rather than silently changing what those
/// senders are about to deliver.
/// </param>
/// <param name="ScopeKey">
/// What this message is about, in a form the sending command can recompute from its own arguments — e.g.
/// <c>visitInstance:41|participant:907</c>. It is what stops a token prepared for one invitee being
/// replayed to send the same approved wording to a different one.
/// </param>
/// <param name="ContentHash">Subject + body, canonically hashed. Empty on a PREPARE token.</param>
/// <param name="AttachmentHash">The attachment set, canonically hashed.</param>
/// <param name="ReplyToEmail">Carried in full, not hashed: the send has to USE it, not merely check it.</param>
public sealed record EmailPreviewTokenPayload(
    string Purpose,
    ulong ActorUserId,
    string TemplateCode,
    uint TemplateRevision,
    string ScopeKey,
    string ContentHash,
    string AttachmentHash,
    string? ReplyToEmail,
    DateTime ExpiresAt)
{
    public bool IsFinal => Purpose == EmailPreviewPurposes.Final;
}

/// <summary>
/// Signs and verifies preview tokens. Implemented in Infrastructure because it needs the deployment's
/// signing key; the shape lives here so the handlers depend on the contract rather than on a key.
/// </summary>
public interface IEmailPreviewTokenService
{
    /// <summary>An opaque, signed, URL-safe token.</summary>
    string Issue(EmailPreviewTokenPayload payload);

    /// <summary>
    /// The payload, or null when the token is malformed, not signed by this deployment, or expired.
    ///
    /// <para>
    /// One null for all three. Which check failed is written to the server log and never told to the
    /// caller: distinguishing "bad signature" from "expired" hands somebody probing the endpoint a way to
    /// learn whether they have guessed a valid shape, and the caller's repair — open the preview again —
    /// is identical either way.
    /// </para>
    /// </summary>
    EmailPreviewTokenPayload? Verify(string? token);
}

/// <summary>
/// The canonical hashes a token binds.
///
/// <para>
/// Canonical means the same message always produces the same hash regardless of how the client happened
/// to serialise it: line endings normalised, attachment order fixed, whitespace at the ends trimmed. It
/// deliberately does NOT normalise anything a recipient would see differently — two bodies that render
/// differently must hash differently, or the check permits exactly the substitution it exists to prevent.
/// </para>
/// </summary>
public static class EmailPreviewFingerprint
{
    /// <summary>
    /// Subject + body, as one hash. The two are joined by a NUL, which cannot occur in either — a
    /// subject carrying a control character is refused outright and a body is sanitised. Concatenating
    /// them directly would let a different split of the same characters produce the same hash.
    /// </summary>
    public static string OfContent(string? subject, string? bodyHtml)
        => Sha256("s:" + Normalize(subject) + "\u0000" + "b:" + Normalize(bodyHtml));

    /// <summary>
    /// The attachment set. Ordered by file id so a client that reshuffles the array without changing what
    /// it references still matches — the recipient receives the same files either way, and refusing that
    /// would fail sends for a difference nobody can observe.
    /// </summary>
    public static string OfAttachments(IEnumerable<Common.EmailComposeAttachmentInput>? attachments)
    {
        if (attachments is null) return Sha256(string.Empty);

        var parts = attachments
            .Where(a => a is not null)
            .Select(a => string.Join(
                '|',
                a.FileId.ToString(CultureInfo.InvariantCulture),
                (a.AttachmentType ?? "ATTACHMENT").Trim().ToUpperInvariant(),
                (a.ContentId ?? string.Empty).Trim()))
            .OrderBy(s => s, StringComparer.Ordinal);

        return Sha256(string.Join('\u001F', parts));
    }

    /// <summary>
    /// A scope key from its parts, so every caller spells one the same way. Null parts are skipped rather
    /// than rendered as "null" — a token prepared before an id existed must not accidentally match one
    /// prepared after.
    /// </summary>
    public static string Scope(params (string Name, object? Value)[] parts)
        => string.Join('|', parts
            .Where(p => p.Value is not null)
            .Select(p => $"{p.Name}:{Convert.ToString(p.Value, CultureInfo.InvariantCulture)}"));

    private static string Normalize(string? value)
        => (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    private static string Sha256(string value)
        => Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
