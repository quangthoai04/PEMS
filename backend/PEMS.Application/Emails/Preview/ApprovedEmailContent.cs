using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Utils;

namespace PEMS.Application.Emails.Preview;

/// <summary>
/// The message a person looked at in the FINAL preview and approved, presented back at send time.
///
/// <para>
/// This replaces <c>EmailOverride</c>, and the difference is the token. The old type carried a subject and
/// a body and nothing that tied them to anything: whatever arrived was sanitised and sent, so "the mail
/// that went out is the mail the sender approved" was a hope about the client rather than a property of
/// the system. Here the content is checked against a hash the backend signed while the sender was looking
/// at it — the same bytes, the same attachments, the same Reply-To, or the send is refused.
/// </para>
/// <para>
/// <b>The content travels WITH the token rather than being looked up from it.</b> Looking it up would
/// mean storing it, which is the draft table this design exists without (see
/// <see cref="EmailPreviewTokenPayload"/>). Carrying it costs nothing in trust: the token's hash is what
/// decides, so a client that alters a single character between the preview and the send is refused, and
/// one that alters nothing is indistinguishable from one that could not.
/// </para>
/// <para>
/// Null on the ordinary path. A sender who opens the preview, reads it and presses "Gửi email" without
/// editing sends the TEMPLATE — there is nothing of theirs to approve, so there is no token, and the
/// content is rendered from <c>email_templates</c> at send time as it always was.
/// </para>
/// </summary>
public sealed record ApprovedEmailContent(
    string FinalPreviewToken,
    string Subject,
    string? BodyHtml = null,
    string? BodyText = null,
    IReadOnlyList<EmailComposeAttachmentInput>? Attachments = null,
    string? ReplyToEmail = null);

/// <summary>
/// Turns an <see cref="ApprovedEmailContent"/> into content the dispatcher will send, or refuses it.
///
/// <para>
/// Every send path calls this rather than reading the fields itself. That is what makes the guarantee
/// uniform: four commands offer a runtime editor, and a fifth added later inherits the same checks by
/// calling the same method instead of by remembering to repeat five of them.
/// </para>
/// </summary>
public static class ApprovedEmailContentVerifier
{
    /// <summary>
    /// The content this send should use.
    /// </summary>
    /// <param name="approved">What the sender approved, or null for an unedited send.</param>
    /// <param name="actorUserId">
    /// The signed-in account. A token issued to somebody else is refused — a token that leaked from one
    /// person's browser must not let another send under their name.
    /// </param>
    /// <param name="currentTemplateRevision">
    /// The template's revision AS IT IS NOW. If an operator re-saved the template between the preview and
    /// the send, the message the sender approved was built from wording that no longer exists, so the send
    /// is refused as stale rather than delivered from either version — one would be the wording they
    /// approved and is now wrong, the other is wording they never saw.
    /// </param>
    /// <param name="scopeKey">
    /// Recomputed by the CALLER from its own arguments, never taken from the request. It is what stops a
    /// token prepared for one invitee being replayed to send the same approved wording to another.
    /// </param>
    public static async Task<SystemEmailContent> ResolveAsync(
        ApprovedEmailContent? approved,
        IEmailPreviewTokenService tokens,
        IHtmlSanitizerService sanitizer,
        IEmailImageLayoutNormalizer normalizer,
        ulong? actorUserId,
        string templateCode,
        uint currentTemplateRevision,
        string scopeKey,
        CancellationToken cancellationToken = default)
    {
        if (approved is null) return SystemEmailContent.FromTemplate.Instance;

        var payload = tokens.Verify(approved.FinalPreviewToken)
            ?? throw new ValidationException(
                "Bản xem trước đã hết hạn hoặc không còn hợp lệ. Vui lòng mở lại email và xem trước "
                + "trước khi gửi.",
                EmailErrorCodes.PreviewTokenInvalid);

        // A PREPARE token sent straight to the send endpoint. Refused rather than accepted-because-it-
        // verifies: only the FINAL token binds the content somebody actually approved, so accepting the
        // earlier one would let a client skip the approval step while appearing to have passed it.
        if (!payload.IsFinal)
            throw new ValidationException(
                "Email chưa được xem trước lần cuối. Vui lòng bấm “Xem trước kết quả” rồi gửi.",
                EmailErrorCodes.PreviewNotFinalized);

        if (actorUserId is null || payload.ActorUserId != actorUserId)
            throw new ValidationException(
                "Bản xem trước này không thuộc về tài khoản đang đăng nhập.",
                EmailErrorCodes.PreviewTokenInvalid);

        if (payload.TemplateCode != templateCode || payload.ScopeKey != scopeKey)
            throw new ValidationException(
                "Bản xem trước thuộc về một email khác. Vui lòng mở lại email cần gửi.",
                EmailErrorCodes.PreviewTokenInvalid);

        if (payload.TemplateRevision != currentTemplateRevision)
            throw new ConflictException(
                "Mẫu email đã được cập nhật sau khi bạn xem trước. Vui lòng xem trước lại để đối chiếu "
                + "nội dung trước khi gửi.",
                EmailErrorCodes.PreviewStale);

        // Exactly the four steps BuildFinalEmailPreviewCommandHandler ran, in the same order, through the
        // same helpers — see PrepareAuthoredAsync. The hash is taken over the SANITISED result rather than
        // over what arrived, because the sanitiser and the image normaliser both rewrite HTML: hashing the
        // raw input would make every send whose content those two touched fail its own integrity check,
        // and hashing at different points on the two sides would let a difference through unnoticed.
        var authored = await PrepareAuthoredAsync(
            approved, sanitizer, normalizer, cancellationToken);

        if (EmailPreviewFingerprint.OfContent(authored.Subject, authored.BodyHtml) != payload.ContentHash)
            throw new ValidationException(
                "Nội dung email đã thay đổi sau khi xem trước lần cuối. Vui lòng xem trước lại rồi gửi.",
                EmailErrorCodes.PreviewStale);

        if (EmailPreviewFingerprint.OfAttachments(approved.Attachments) != payload.AttachmentHash)
            throw new ValidationException(
                "Tệp đính kèm đã thay đổi sau khi xem trước lần cuối. Vui lòng xem trước lại rồi gửi.",
                EmailErrorCodes.PreviewStale);

        return authored;
    }

    /// <summary>
    /// Raw edit → HTML → inline images normalised → validated and sanitised.
    ///
    /// <para>
    /// The single definition of that pipeline, called by the final preview and by every send. It exists as
    /// one method rather than as four identical fragments because the hash comparison is only meaningful
    /// while both sides produce the same string: two copies that agree today are two copies that can be
    /// changed independently tomorrow, and the symptom would be every edited send failing as "stale" with
    /// nothing actually stale about it.
    /// </para>
    /// </summary>
    public static async Task<SystemEmailContent.AuthoredByUser> PrepareAuthoredAsync(
        ApprovedEmailContent approved,
        IHtmlSanitizerService sanitizer,
        IEmailImageLayoutNormalizer normalizer,
        CancellationToken cancellationToken = default)
    {
        var raw = !string.IsNullOrWhiteSpace(approved.BodyText)
            ? EmailComposition.PlainTextToHtml(approved.BodyText)
            : approved.BodyHtml ?? string.Empty;

        // Normalised BEFORE the content is fixed: the normaliser exists for images the sender pasted, and
        // a template body has none.
        var normalized = await normalizer.NormalizeHtmlAsync(raw, cancellationToken);

        return SystemEmailContent.AuthoredByUser.Create(approved.Subject, normalized, sanitizer);
    }

    /// <summary>
    /// The Reply-To the sender approved, when the token carries one.
    ///
    /// <para>
    /// Read from the TOKEN, not from the request. The request's copy is what the hash check above proves
    /// unchanged for the subject and body; Reply-To is not part of that hash because it is not part of the
    /// body, so taking it from the signed payload is what keeps it equally unforgeable.
    /// </para>
    /// </summary>
    public static string? ApprovedReplyTo(
        ApprovedEmailContent? approved, IEmailPreviewTokenService tokens)
        => approved is null ? null : tokens.Verify(approved.FinalPreviewToken)?.ReplyToEmail;
}
