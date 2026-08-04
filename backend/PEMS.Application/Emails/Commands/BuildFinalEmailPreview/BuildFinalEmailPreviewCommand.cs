using System.Collections.Generic;
using MediatR;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Commands.BuildFinalEmailPreview;

/// <summary>
/// "Xem trước kết quả" — turns what the sender wrote in the editor into the exact message that will be
/// delivered, and signs it.
///
/// <para>
/// It is a COMMAND rather than a query despite storing nothing, because it issues a credential: the
/// returned token is what a later send accepts as proof that a person read and approved this content. A
/// query that mints something a mutation later trusts is a query in name only.
/// </para>
/// </summary>
public sealed class BuildFinalEmailPreviewCommand : IRequest<BuildFinalEmailPreviewResponse>
{
    /// <summary>The token the VIEW stage issued. Proves which template, revision and message this edit belongs to.</summary>
    public string PreviewToken { get; set; } = null!;

    public string Subject { get; set; } = null!;

    /// <summary>
    /// The edited message as readable plain text — what the modal's editor binds to. Preferred over
    /// <see cref="EditableBodyHtml"/> when both arrive, matching how the send resolves them, so the hash
    /// signed here is over the same string the send will hash.
    /// </summary>
    public string? EditableBodyText { get; set; }

    /// <summary>The edited message as HTML, for the rich editor (already cid-rewritten for inline images).</summary>
    public string? EditableBodyHtml { get; set; }

    public List<EmailComposeAttachmentInput>? Attachments { get; set; }

    /// <summary>VI or EN — decides which stored wording the locked blocks are drawn from.</summary>
    public string? Language { get; set; }
}

/// <param name="FinalPreviewHtml">
/// The whole message as it will arrive: the sender's words, the locked action block beneath them, inside
/// the branded shell. This is what the FINAL_PREVIEW screen displays and nothing else is added afterwards.
/// </param>
/// <param name="FinalPreviewToken">
/// Presented to the send. Binds the actor, the template, its revision, the scope, the content hash, the
/// attachment hash and the Reply-To — so a send that differs from this preview in any of them is refused
/// rather than delivered.
/// </param>
public sealed record BuildFinalEmailPreviewResponse(
    string Subject,
    string FinalPreviewHtml,
    string FinalPreviewToken,
    string? ReplyToEmail,
    string ExpiresAt);
