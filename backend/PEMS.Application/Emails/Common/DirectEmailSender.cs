using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Emails;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// One composed message, as the author left it, on its way straight out.
///
/// <para>
/// This is what a message looked like while it was a row in <c>email_drafts</c>. It is a parameter now
/// rather than a table: the composer holds the message in the browser and posts the whole thing when the
/// author presses send, so there is no half-written record on the server to load, to reconcile, or to
/// fail to find.
/// </para>
/// </summary>
public sealed record DirectEmailRequest(
    string? Subject,
    string? BodyContent,
    string? BodyFormat,
    IReadOnlyList<EmailComposeRecipientInput>? Recipients,
    IReadOnlyList<EmailComposeAttachmentInput>? Attachments,
    string? RelatedType,
    ulong? RelatedId,
    SentEmail? InReplyTo = null);

/// <summary>
/// What the author would be sending, checked but NOT sent.
///
/// <para>
/// The body is the sanitised one, so the preview shows what the recipient will get rather than what was
/// typed — the two differ whenever the sanitiser removes something, and a preview that hides that is
/// worse than no preview.
/// </para>
/// </summary>
public sealed record DirectEmailPreview(
    string Subject,
    string Body,
    bool IsHtml,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    IReadOnlyList<string> Bcc,
    IReadOnlyList<string> Attachments);

/// <summary>
/// Everything that has to be true of a manual message, in one place, for every caller that sends one.
///
/// <para>
/// It replaces <c>EmailDraftDispatcher</c>, which did the same work but could only be handed a saved
/// draft. The rules are unchanged and deliberately so: content validation, the envelope rules, the
/// send-time re-check of attachment scope, the fail-closed refusal when an attachment's bytes cannot be
/// read, and one MIME message per send. What is gone is the DRAFT → SENT claim, which was the mechanism
/// that stopped a double click becoming two emails; that job now belongs to <c>Idempotency-Key</c>,
/// which the two send commands already declare through <c>IIdempotentEmailSend</c>.
/// </para>
/// <para>
/// Authorisation is NOT here, exactly as it was not in the dispatcher. Each caller decides who may send:
/// manual compose checks the signed-in user, the setup-progress path re-checks the host and the visit's
/// stage. This service refuses only on things that are true of the message itself.
/// </para>
/// </summary>
public interface IDirectEmailSender
{
    /// <summary>
    /// Runs every check a send would run and returns the result instead of sending. Nothing is written
    /// and nothing reaches a provider, so a preview cannot cost the author anything.
    /// </summary>
    Task<DirectEmailPreview> PreviewAsync(
        DirectEmailRequest request, ulong actorUserId, CancellationToken cancellationToken);

    /// <summary>Sends exactly one message and records exactly one <c>sent_emails</c> row.</summary>
    Task<ManualEmailResult> SendAsync(
        DirectEmailRequest request, ulong actorUserId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IDirectEmailSender"/>
public sealed class DirectEmailSender : IDirectEmailSender
{
    private readonly IApplicationDbContext _db;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IFileStorageService _storage;
    private readonly IManualEmailSender _sender;
    private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;
    private readonly EmailRecipientOptions _recipientOptions;

    public DirectEmailSender(
        IApplicationDbContext db,
        IHtmlSanitizerService sanitizer,
        IFileStorageService storage,
        IManualEmailSender sender,
        PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer,
        IOptions<EmailRecipientOptions> recipientOptions)
    {
        _db = db;
        _sanitizer = sanitizer;
        _storage = storage;
        _sender = sender;
        _normalizer = normalizer;
        _recipientOptions = recipientOptions?.Value ?? new EmailRecipientOptions();
    }

    public async Task<DirectEmailPreview> PreviewAsync(
        DirectEmailRequest request, ulong actorUserId, CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(request, actorUserId, cancellationToken);

        return new DirectEmailPreview(
            prepared.Content.Subject,
            prepared.Body,
            prepared.Content.IsHtml,
            Describe(prepared.Envelope.To),
            Describe(prepared.Envelope.Cc),
            Describe(prepared.Envelope.Bcc),
            prepared.Attachments.Select(a => a.DisplayName ?? $"tệp #{a.FileId}").ToList());
    }

    public async Task<ManualEmailResult> SendAsync(
        DirectEmailRequest request, ulong actorUserId, CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(request, actorUserId, cancellationToken);

        return await _sender.SendAsync(new ManualEmailMessage(
            SenderUserId: actorUserId,
            Subject: prepared.Content.Subject,
            Body: prepared.Body,
            BodyFormat: prepared.Format,
            Envelope: prepared.Envelope,
            Attachments: prepared.Outbound,
            RelatedType: request.RelatedType,
            RelatedId: request.RelatedId,
            InReplyTo: request.InReplyTo), cancellationToken);
    }

    private sealed record Prepared(
        ManualEmailContent.Result Content,
        string Body,
        Domain.Enums.EmailBodyFormat Format,
        ValidatedEnvelope Envelope,
        IReadOnlyList<EmailComposeAttachmentInput> Attachments,
        IReadOnlyList<ManualEmailAttachment> Outbound);

    /// <summary>
    /// Everything that can say "no", run before anything is sent — and in the same order for a preview as
    /// for a send, which is what makes the preview worth looking at. A message that previews cleanly and
    /// then fails on send would send the author back to guess which of the two answers was true.
    /// </summary>
    private async Task<Prepared> PrepareAsync(
        DirectEmailRequest request, ulong actorUserId, CancellationToken cancellationToken)
    {
        var format = EmailComposeWriter.ParseBodyFormat(request.BodyFormat);
        var content = ManualEmailContent.Validate(
            request.Subject, request.BodyContent, format, _sanitizer);

        var envelope = EmailComposeWriter.ValidateRecipients(
            request.Recipients, _recipientOptions.MaxRecipients, requireTo: true);

        var attachments = request.Attachments ?? Array.Empty<EmailComposeAttachmentInput>();

        // Scope, size, mime and inline-cid rules, re-checked against the database at send time. The files
        // were uploaded in an earlier request and may have been removed or replaced since.
        await EmailComposeWriter.ValidateAndLoadFilesAsync(_db, actorUserId, attachments, cancellationToken);

        // Aligned, not compacted: Pair below matches rows to bytes by position, and a compacted list
        // shifts every attachment after a skipped one onto the wrong row's name.
        var loaded = await EmailAttachmentLoader.LoadAlignedAsync(
            _db, _storage,
            attachments.Select(a => (
                a.FileId,
                EmailComposeWriter.ParseAttachmentType(a.AttachmentType),
                string.IsNullOrWhiteSpace(a.ContentId) ? null : a.ContentId!.Trim(),
                (string?)a.DisplayName)).ToList(),
            cancellationToken);

        AssertEveryAttachmentReadable(attachments, loaded);

        var body = content.IsHtml
            ? await _normalizer.NormalizeHtmlAsync(content.Body, cancellationToken)
            : content.Body;

        return new Prepared(content, body, format, envelope, attachments, Pair(attachments, loaded));
    }

    private static IReadOnlyList<string> Describe(IReadOnlyList<EmailRecipient> group)
        => group.Select(r => string.IsNullOrWhiteSpace(r.DisplayName)
                ? r.Email
                : $"{r.DisplayName} <{r.Email}>")
            .ToList();

    /// <summary>
    /// Refuses the send when any attachment has no readable bytes.
    ///
    /// <para>
    /// Fail-closed, for every attachment rather than only the ones some flow has marked mandatory. The
    /// behaviour this replaced quietly dropped an unreadable file and carried on: the provider accepted a
    /// message with one fewer part and the author was told "Đã gửi email tới N người nhận", with nothing
    /// anywhere recording that the document was missing. A person attaches a file because the message is
    /// about the file.
    /// </para>
    /// <para>
    /// Nothing has been written or sent at this point, so a refusal costs the author nothing but the
    /// click — the composer still holds every word they wrote.
    /// </para>
    /// </summary>
    private static void AssertEveryAttachmentReadable(
        IReadOnlyList<EmailComposeAttachmentInput> rows, IReadOnlyList<OutboundAttachment?> loaded)
    {
        var missing = new List<string>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (i < loaded.Count && loaded[i] is not null) continue;
            var name = rows[i].DisplayName;
            missing.Add(string.IsNullOrWhiteSpace(name) ? $"tệp #{rows[i].FileId}" : name!);
        }

        if (missing.Count == 0) return;

        throw new ValidationException(
            $"Không gửi được: {missing.Count} tệp đính kèm không đọc được nội dung "
            + $"({string.Join(", ", missing)}). Tệp có thể đã bị xoá khỏi kho lưu trữ hoặc hệ thống "
            + "không còn quyền đọc. Vui lòng gỡ tệp khỏi email hoặc tải lên lại, rồi gửi lại. "
            + "Nội dung email vẫn được giữ nguyên.",
            EmailErrorCodes.AttachmentUnreadable);
    }

    /// <summary>
    /// Matches each attachment with the bytes loaded for it. <paramref name="loaded"/> is index-aligned
    /// with <paramref name="rows"/> and holds null for a file whose bytes could not be read, so a skipped
    /// file drops out here instead of sliding the following rows onto the wrong content.
    ///
    /// <para>
    /// With <see cref="AssertEveryAttachmentReadable"/> in front of it the null branch is unreachable. It
    /// stays because the alignment contract is what makes the null-skip safe at all.
    /// </para>
    /// </summary>
    private static IReadOnlyList<ManualEmailAttachment> Pair(
        IReadOnlyList<EmailComposeAttachmentInput> rows, IReadOnlyList<OutboundAttachment?> loaded)
    {
        var result = new List<ManualEmailAttachment>(rows.Count);
        for (var i = 0; i < rows.Count && i < loaded.Count; i++)
        {
            if (loaded[i] is not { } bytes) continue;
            var r = rows[i];
            result.Add(new ManualEmailAttachment(
                r.FileId,
                EmailComposeWriter.ParseAttachmentType(r.AttachmentType),
                string.IsNullOrWhiteSpace(r.ContentId) ? null : r.ContentId!.Trim(),
                r.DisplayName,
                (uint)Math.Max(0, r.DisplayOrder),
                bytes));
        }
        return result;
    }
}
