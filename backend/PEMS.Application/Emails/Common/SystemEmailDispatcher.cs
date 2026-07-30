using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Default <see cref="ISystemEmailDispatcher"/>: check, render from the database, record, send, record
/// the outcome.
///
/// <para>
/// The order matters and is deliberate. Everything that can refuse the message — the envelope, the
/// template, the variable contract, the subject guard — runs BEFORE any row is written, so a rejected
/// send leaves no history, no recipient and no half-record. The history row is then written and committed
/// BEFORE the message is handed to SMTP, so a crash between the two leaves a <c>QUEUED</c> row an
/// operator can see rather than a message that went out with no trace. Nothing here claims the two are
/// atomic; the reverse order would be worse, because it can send mail the system has no record of.
/// </para>
/// </summary>
public sealed class SystemEmailDispatcher : ISystemEmailDispatcher
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IEmailService _email;
    private readonly EmailRecipientOptions _recipientOptions;

    public SystemEmailDispatcher(
        IApplicationDbContext db,
        IEmailTemplateRenderer renderer,
        IEmailService email,
        IOptions<EmailRecipientOptions>? recipientOptions = null)
    {
        _db = db;
        _renderer = renderer;
        _email = email;
        _recipientOptions = recipientOptions?.Value ?? new EmailRecipientOptions();
    }

    /// <summary>Prepare and deliver, back to back. The ordinary path.</summary>
    public async Task<SystemEmailDispatchResult> SendAsync(
        SystemEmailRequest request, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(request, cancellationToken);
        var delivery = await DeliverAsync(prepared, cancellationToken);

        return new SystemEmailDispatchResult(delivery, prepared.SentEmailId, prepared.EmailTemplateId);
    }

    public async Task<PreparedSystemEmail> PrepareAsync(
        SystemEmailRequest request, CancellationToken cancellationToken = default)
    {
        // 1) The envelope, first. The sender checks this too, but by then a history row exists — and a
        //    row recording a message that was never a legal message is worse than no row.
        var envelope = EmailRecipientValidator.Validate(
            new[] { request.To }, cc: null, bcc: null, _recipientOptions.MaxRecipients);
        EmailRecipientPolicyEnforcer.Assert(request.TemplateCode, envelope);

        // 2) Content. A missing/inactive/mis-declared template throws a stable error here and no history
        //    row is written — there is nothing truthful to record about a message that cannot exist. The
        //    same call handles authored content: one renderer, one set of guards, both modes.
        var rendered = await _renderer.RenderAsync(
            new EmailRenderRequest(request.TemplateCode, request.Language, request.Variables, request.TrustedBlocks)
            {
                Content = request.Content,
            },
            cancellationToken);

        var isHtml = rendered.BodyFormat == EmailBodyFormat.HTML;

        // The branded card is applied once, here, instead of being pasted into every template row.
        var body = isHtml ? EmailComposition.BrandedShell(rendered.Body) : rendered.Body;

        // 3) How much of the body the history may keep is decided by SensitiveEmailHistory from the
        //    template's own classification, never by this method. What it must not do is keep a live
        //    credential where it can be read back: `GET /api/emails/viewemail` deliberately has no
        //    sender/recipient filter and is open to every internal role, so a stored reset code or accept
        //    link would be readable by staff who are not the recipient, inside its validity window.
        var snapshot = SensitiveEmailHistory.Apply(rendered.TemplateCode, body);
        AssertSnapshotCarriesNoActionUrl(rendered.TemplateCode, snapshot, request.TrustedBlocks);

        var now = VietnamTime.Now();
        var sentEmail = new SentEmail
        {
            EmailTemplateId = rendered.EmailTemplateId,
            RelatedType = request.RelatedType,
            RelatedId = request.RelatedId,
            // Subjects are safe to keep: no registered template puts a secret in one, the renderer
            // refuses one that would, and an authored subject goes through the same two guards.
            Subject = rendered.Subject,
            BodySnapshot = snapshot,
            BodyFormat = rendered.BodyFormat,
            Status = "QUEUED",
            SentBy = request.SentBy,
            CreatedAt = now,
            LastAttemptAt = now,
        };
        sentEmail.Recipients.Add(new SentEmailRecipient
        {
            RecipientEmail = request.To.Email,
            RecipientName = request.To.DisplayName,
            RecipientType = EmailRecipientTypes.To,
            DeliveryStatus = "QUEUED",
            CreatedAt = now,
        });

        _db.SentEmails.Add(sentEmail);
        await _db.SaveChangesAsync(cancellationToken);

        return new PreparedSystemEmail(
            sentEmail.SentEmailId,
            sentEmail.Recipients.First().SentEmailRecipientId,
            rendered.EmailTemplateId,
            rendered.TemplateCode,
            request.To,
            rendered.Subject,
            body,
            isHtml)
        {
            Attachments = request.Attachments ?? Array.Empty<OutboundAttachment>(),
        };
    }

    public async Task<EmailDeliveryResult> DeliverAsync(
        PreparedSystemEmail prepared, CancellationToken cancellationToken = default)
    {
        // TemplateCode travels with the message so the dispatch layer re-checks the recipient policy
        // itself rather than trusting this class to have built the envelope correctly.
        var delivery = await _email.TrySendAsync(new OutboundEmail
        {
            To = new[] { prepared.To },
            Subject = prepared.Subject,
            Body = prepared.Body,
            IsHtml = prepared.IsHtml,
            TemplateCode = prepared.TemplateCode,
            Attachments = prepared.Attachments,
        }, cancellationToken);

        // Provider acceptance is SENT and nothing more: PEMS has no delivery webhook, so DELIVERED would
        // be a claim the system cannot back up. A Skipped send (SMTP off outside production) stays
        // QUEUED — it never reached a provider at all.
        var completedAt = VietnamTime.Now();

        var sentEmail = await _db.SentEmails
            .FirstOrDefaultAsync(e => e.SentEmailId == prepared.SentEmailId, cancellationToken);
        if (sentEmail is null) return delivery;

        var recipient = await _db.SentEmailRecipients
            .FirstOrDefaultAsync(r => r.SentEmailRecipientId == prepared.SentEmailRecipientId, cancellationToken);

        sentEmail.LastAttemptAt = completedAt;

        switch (delivery.Status)
        {
            case EmailDeliveryStatus.Sent:
                sentEmail.Status = "SENT";
                sentEmail.SentAt = completedAt;
                if (recipient is not null)
                {
                    recipient.DeliveryStatus = "SENT";
                    recipient.SentAt = completedAt;
                }
                break;

            case EmailDeliveryStatus.Failed:
                sentEmail.Status = "FAILED";
                sentEmail.ErrorMessage = delivery.SafeMessage;
                if (recipient is not null)
                {
                    recipient.DeliveryStatus = "FAILED";
                    recipient.ErrorMessage = delivery.SafeMessage;
                }
                break;

            default: // Skipped
                sentEmail.Status = "QUEUED";
                sentEmail.ErrorMessage = delivery.SafeMessage;
                if (recipient is not null) recipient.DeliveryStatus = "QUEUED";
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return delivery;
    }

    /// <summary>
    /// Proves — rather than assumes — that the body about to be stored no longer contains the one-time
    /// links the message carries.
    ///
    /// <para>
    /// The retention policy strips a span delimited by markers; this checks the RESULT against the real
    /// URLs, which is the only evidence that the span was the right one. Reaching here means the strip
    /// silently failed, so the send is refused instead of writing a credential into a table the history
    /// API serves to every internal role.
    /// </para>
    /// </summary>
    private static void AssertSnapshotCarriesNoActionUrl(
        string templateCode, string? snapshot, IReadOnlyDictionary<string, string>? trustedBlocks)
    {
        if (string.IsNullOrEmpty(snapshot)) return;
        if (SensitiveEmailHistory.PolicyFor(templateCode) == HistoryBodyPolicy.Full) return;

        if (EmailComposition.ContainsActionBlockMarker(snapshot))
            throw new BusinessRuleException(
                $"Bản lưu nội dung email '{templateCode}' vẫn còn dấu mốc khối hành động.",
                EmailErrorCodes.HistorySecretLeak);

        foreach (var url in EmailComposition.ExtractActionUrls(trustedBlocks))
        {
            if (url.Length < 8) continue;
            if (!snapshot!.Contains(url, StringComparison.Ordinal)) continue;

            throw new BusinessRuleException(
                $"Bản lưu nội dung email '{templateCode}' vẫn còn liên kết dùng một lần.",
                EmailErrorCodes.HistorySecretLeak);
        }
    }
}
