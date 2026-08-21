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
    private readonly Sender.IEmailSenderVariableResolver? _senders;

    public SystemEmailDispatcher(
        IApplicationDbContext db,
        IEmailTemplateRenderer renderer,
        IEmailService email,
        IOptions<EmailRecipientOptions>? recipientOptions = null,
        Sender.IEmailSenderVariableResolver? senders = null)
    {
        _db = db;
        _renderer = renderer;
        _email = email;
        _recipientOptions = recipientOptions?.Value ?? new EmailRecipientOptions();
        _senders = senders;
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

        // 1b) Who this message is FROM. Resolved HERE rather than in each of the ~30 callers, for the same
        //     reason the render is: it is the only way "the preview, the final preview and the sent mail
        //     name the same sender" can be true without every caller remembering to do it. Callers keep
        //     passing only their own business variables.
        var senderValues = await ResolveSenderVariablesAsync(request, cancellationToken);

        var variables = MergeSenderVariables(request.TemplateCode, request.Variables, senderValues);

        // 2) Content. A missing/inactive/mis-declared template throws a stable error here and no history
        //    row is written — there is nothing truthful to record about a message that cannot exist. The
        //    same call handles authored content: one renderer, one set of guards, both modes.
        var rendered = await _renderer.RenderAsync(
            new EmailRenderRequest(request.TemplateCode, request.Language, variables, request.TrustedBlocks)
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
            // message, and a default must not silently overrule it.
            ReplyTo = request.ReplyTo ?? ReplyToFrom(senderValues),
        };
    }

    /// <summary>
    /// Who this message is from, read fresh from the account it is recorded against.
    ///
    /// <para>
    /// The resolver is optional in the constructor so the many unit tests that build a dispatcher by hand
    /// keep working. That is a test-ergonomics allowance and not a bypass: with no resolver wired, a
    /// template that declares sender variables simply gets empty values, which the renderer accepts and a
    /// recipient sees as an absence. In the running application it is always registered.
    /// </para>
    /// </summary>
    private async Task<Sender.EmailSenderVariables?> ResolveSenderVariablesAsync(
        SystemEmailRequest request, CancellationToken cancellationToken)
        => _senders is null
            ? null
            // SentBy, never a field from the request body. It is the account the message is RECORDED
            // against, so the name printed to the recipient and the name in the email history cannot
            // diverge — and there is no route by which a client could name somebody else as the sender.
            : await _senders.ResolveAsync(request.SentBy, request.TemplateCode, cancellationToken);

    /// <summary>
    /// Layers the resolved sender values UNDER the caller's variables, restricted to what the template
    /// declares.
    ///
    /// <para>
    /// Restricted, because the renderer refuses a variable the template does not declare — supplying all
    /// six to a template that declares none would fail every send with
    /// <c>EMAIL_TEMPLATE_VARIABLE_UNKNOWN</c>. Restricted to DECLARED rather than to "used in the body" so
    /// an administrator can add <c>{{senderPhone}}</c> to a body at any time and have it resolve on the
    /// next send with no code change.
    /// </para>
    /// <para>
    /// Under rather than over: a caller that has already supplied a sender variable itself keeps its value.
    /// No caller does today, and the ordering is what makes that a fact about the callers rather than an
    /// assumption this method depends on.
    /// </para>
    /// </summary>
    private static IReadOnlyDictionary<string, string> MergeSenderVariables(
        string templateCode,
        IReadOnlyDictionary<string, string> callerVariables,
        Sender.EmailSenderVariables? sender)
    {
        var declared = SystemEmailTemplates.Find(templateCode)?.DeclaredVariables;
        if (declared is null) return callerVariables;

        // Empty rather than absent when no sender could be resolved.
        //
        // The renderer compares the DECLARED set against the SUPPLIED set in both directions and fails
        // closed, so a declared name with no value is not a blank line in the mail — it is a refusal to
        // send at all. Returning the caller's variables untouched here therefore did not mean "no sender
        // shown": it meant every template declaring the six sender names failed with "thiếu giá trị cho
        // biến: senderName, …", and on the claim/transfer path — where the send is best-effort and its
        // exception is logged rather than raised — the invitation email simply never arrived, with the
        // command still reporting success.
        //
        // The paragraph above this method promised this behaviour before the code did.
        var senderValues = sender?.ToVariableValues();
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in declared)
        {
            if (!Sender.EmailSenderVariableNames.IsSenderVariable(name)) continue;

            merged[name] = senderValues is not null && senderValues.TryGetValue(name, out var value)
                ? value
                : string.Empty;
        }

        if (merged.Count == 0) return callerVariables;

        foreach (var pair in callerVariables) merged[pair.Key] = pair.Value;

        return merged;
    }

    /// <summary>
    /// Where a reply goes when the caller did not say: the sender's own address, or the configured support
    /// address for mail nobody pressed send on (plan §9.1).
    ///
    /// <para>
    /// Both cases come out of the same resolver, so there is no branch here on "was this automated" — the
    /// resolver has already answered that, and asking again would give the answer two places to be wrong.
    /// An address that is not well-formed produces no Reply-To rather than a broken header: a profile with
    /// a malformed address is a data fault to fix, not a reason to refuse a message the recipient needs.
    /// </para>
    /// </summary>
    private static EmailRecipient? ReplyToFrom(Sender.EmailSenderVariables? sender)
        => sender is not null
           && !string.IsNullOrWhiteSpace(sender.Email)
           && EmailRecipientValidator.IsWellFormed(sender.Email!)
            ? new EmailRecipient(sender.Email!.Trim(), sender.Name)
            : null;


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
            // Derived from the SentEmail row PrepareAsync already committed — stable across every retry of
            // THIS logical message, so a provider transport that retries a network-ambiguous outcome can
            // never create a second copy of it.
            DeliveryIdempotencyKey = $"pems-system-{prepared.SentEmailId}",
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

            // The machine code is kept alongside the human message (EmailAttemptRecord.Format). It is
            // the only thing that later tells a recovery sweep whether this failure happened BEFORE the
            // provider was contacted — and by then the EmailDeliveryResult itself is long gone.
            case EmailDeliveryStatus.Failed:
                sentEmail.Status = "FAILED";
                sentEmail.ErrorMessage = EmailAttemptRecord.Format(delivery);
                if (recipient is not null)
                {
                    recipient.DeliveryStatus = "FAILED";
                    recipient.ErrorMessage = delivery.SafeMessage;
                }
                break;

            default: // Skipped
                sentEmail.Status = "QUEUED";
                sentEmail.ErrorMessage = EmailAttemptRecord.Format(delivery);
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
