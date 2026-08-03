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
    private readonly Contact.IEmailContactResolver? _contacts;

    public SystemEmailDispatcher(
        IApplicationDbContext db,
        IEmailTemplateRenderer renderer,
        IEmailService email,
        IOptions<EmailRecipientOptions>? recipientOptions = null,
        Contact.IEmailContactResolver? contacts = null)
    {
        _db = db;
        _renderer = renderer;
        _email = email;
        _recipientOptions = recipientOptions?.Value ?? new EmailRecipientOptions();
        _contacts = contacts;
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
            new[] { request.To }, cc: request.Cc, bcc: null, _recipientOptions.MaxRecipients);
        EmailRecipientPolicyEnforcer.Assert(request.TemplateCode, envelope);

        // 1b) Who the recipient should contact. Resolved HERE rather than in each of the ~30 callers, for
        //     the same reason the render is: it is the only way "preview, draft and send show the same
        //     contact" can be true without every caller remembering to do it. A REQUIRED template that
        //     cannot resolve one throws, before any row is written.
        var contactBlock = await ResolveContactBlockAsync(request, cancellationToken);

        var trustedBlocks = contactBlock is null
            ? request.TrustedBlocks
            : Merge(request.TrustedBlocks, EmailTrustedBlocks.ContactInformationBlock, contactBlock.BlockHtml);

        // 2) Content. A missing/inactive/mis-declared template throws a stable error here and no history
        //    row is written — there is nothing truthful to record about a message that cannot exist. The
        //    same call handles authored content: one renderer, one set of guards, both modes.
        var rendered = await _renderer.RenderAsync(
            new EmailRenderRequest(request.TemplateCode, request.Language, request.Variables, trustedBlocks)
            {
                Content = request.Content,
                // The RESOLVED policy, not the shipped one: whether a body may go out without the card is
                // decided by what this send actually produced, so the renderer's refusal can never
                // contradict a save the editor accepted under a policy an operator had changed.
                ContactBlockRequired =
                    contactBlock?.Policy.Requirement == Domain.Enums.EmailContactRequirement.REQUIRED,
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
        foreach (var cc in envelope.Cc)
        {
            sentEmail.Recipients.Add(new SentEmailRecipient
            {
                RecipientEmail = cc.Email,
                RecipientName = cc.DisplayName,
                RecipientType = EmailRecipientTypes.Cc,
                DeliveryStatus = "QUEUED",
                CreatedAt = now,
            });
        }

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
            Cc = envelope.Cc,
            // An explicit Reply-To from the caller wins: it is a decision somebody made about this one
            // message, and a policy default must not silently overrule it.
            ReplyTo = request.ReplyTo ?? ReplyToFrom(contactBlock),
        };
    }

    /// <summary>
    /// Resolves the reply contact for this send, or null when the feature has nothing to say about it.
    ///
    /// <para>
    /// The resolver is optional in the constructor so the many unit tests that build a dispatcher by hand
    /// keep working. That is a test-ergonomics allowance, not a bypass: in the running application it is
    /// always registered, and the fail-closed refusal for a REQUIRED template lives inside the resolver
    /// where both this path and any future one must go through it.
    /// </para>
    /// </summary>
    private async Task<Contact.EmailContactResolution?> ResolveContactBlockAsync(
        SystemEmailRequest request, CancellationToken cancellationToken)
    {
        if (_contacts is null) return null;

        var scope = request.ContactScope;

        return await _contacts.ResolveAsync(
            new Contact.EmailContactRequest(
                request.TemplateCode,
                request.Language,
                scope.VisitInstanceId,
                scope.CampusId,
                scope.DepartmentId,
                request.SentBy),
            cancellationToken);
    }

    private static EmailRecipient? ReplyToFrom(Contact.EmailContactResolution? resolution)
        => resolution?.ReplyTo is { } address
            ? new EmailRecipient(address.Email, address.DisplayName)
            : null;

    /// <summary>
    /// Adds one trusted block to the caller's map without mutating it — callers pass dictionaries they
    /// still own, and several reuse one across a loop of recipients.
    /// </summary>
    private static IReadOnlyDictionary<string, string> Merge(
        IReadOnlyDictionary<string, string>? existing, string name, string html)
    {
        var merged = existing is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(existing, StringComparer.Ordinal);

        merged[name] = html;
        return merged;
    }

    public async Task<EmailDeliveryResult> DeliverAsync(
        PreparedSystemEmail prepared, CancellationToken cancellationToken = default)
    {
        // TemplateCode travels with the message so the dispatch layer re-checks the recipient policy
        // itself rather than trusting this class to have built the envelope correctly.
        var delivery = await _email.TrySendAsync(new OutboundEmail
        {
            To = new[] { prepared.To },
            Cc = prepared.Cc,
            ReplyTo = prepared.ReplyTo,
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
