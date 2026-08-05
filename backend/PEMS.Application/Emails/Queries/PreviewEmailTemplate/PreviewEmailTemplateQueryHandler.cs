using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Preview;

namespace PEMS.Application.Emails.Queries.PreviewEmailTemplate;

/// <summary>
/// Renders a system template for the "Xem trước email" modal through the SAME renderer the send uses,
/// then hands back the editable content with the action block removed and a disabled copy of that block
/// for read-only display. No persistence, no tokens, no SMTP.
///
/// <para>
/// It goes through <see cref="IEmailTemplateRenderer"/> for one reason: a preview that renders by
/// different rules is not a preview. The previous implementation had its own regex renderer with a
/// silent fallback table — a missing variable became the text "Chưa có thông tin" here while the real
/// send refused to go out at all — plus a cross-language fallback the renderer deliberately does not
/// have. An operator could therefore approve a preview that could never be sent, or edit a template and
/// see a preview that no recipient would ever receive.
/// </para>
/// <para>
/// <b>The sender is resolved for real, in both kinds of preview, and substituted INTO the body.</b> The
/// contact block it replaces could not work that way: it was markup the backend appended, so an
/// operational preview had to return it SEPARATELY and beg the client not to merge it — because a host
/// who edited a body containing the stand-in card sent the stand-in back as authored content and the
/// dispatcher appended the real card underneath it. Sender variables are values, so there is one body,
/// it already reads correctly, and editing it cannot duplicate anything.
/// </para>
/// </summary>
public sealed class PreviewEmailTemplateQueryHandler
    : IRequestHandler<PreviewEmailTemplateQuery, PreviewEmailTemplateResponse>
{
    /// <summary>
    /// How long a prepared preview stays usable.
    ///
    /// <para>
    /// Long enough to read a message, edit it and think again; short enough that a token left in a closed
    /// tab overnight cannot be replayed the next morning against a template somebody has since re-worded.
    /// The revision check makes that last case a refusal rather than a wrong send, so this is a second
    /// line rather than the only one.
    /// </para>
    /// </summary>
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(30);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly Sender.IEmailSenderVariableResolver _senders;
    private readonly IEmailPreviewTokenService _tokens;

    public PreviewEmailTemplateQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IEmailTemplateRenderer renderer,
        Sender.IEmailSenderVariableResolver senders,
        IEmailPreviewTokenService tokens)
    {
        _db = db;
        _currentUser = currentUser;
        _renderer = renderer;
        _senders = senders;
        _tokens = tokens;
    }

    public async Task<PreviewEmailTemplateResponse> Handle(
        PreviewEmailTemplateQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } actorId)
            throw new ForbiddenException();

        if (string.IsNullOrWhiteSpace(request.TemplateCode))
            throw new ValidationException("Thiếu mã template email.");

        var code = request.TemplateCode.Trim();
        var language = EmailLanguages.Normalize(request.Language);
        var spec = EmailActionTemplates.For(code);

        var trustedBlocks = new Dictionary<string, string>
        {
            // Supplied unconditionally because a template that does not use the placeholder never
            // substitutes it, while a template that does would otherwise fail the preview closed on an
            // unresolved variable.
            [EmailTrustedBlocks.SetupSummaryBlock] =
                EmailComposition.DisabledSetupSummaryBlock(language),
        };

        string? disabledActionBlock = null;

        if (spec is not null)
        {
            // The buttons a preview shows are dead on purpose: real tokens are minted only by a real send.
            // They are still passed as a trusted block so the body is assembled exactly as it will be, and
            // so the strip below has a well-formed block to remove.
            disabledActionBlock = EmailActionTemplates.DisabledBlockFor(code, language);
            trustedBlocks[EmailTrustedBlocks.ActionBlock] =
                EmailComposition.ActionBlockStart + disabledActionBlock + EmailComposition.ActionBlockEnd;
        }

        // Sample values come from the backend contract — never from a dictionary compiled into a
        // screen, which is how preview and send ended up substituting from two different tables (G11-J).
        // They are layered UNDER the caller's context, so a caller that supplies real data always wins.
        //
        // Only when the caller asked for them. See UseSampleData on the query: filling gaps silently is
        // right for an operator editing wording and wrong for a host about to send a real message.
        var context = request.UseSampleData
            ? new Dictionary<string, string>(
                EmailTemplateContracts.PreviewSample(code, language), StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        // The sender, resolved for real — always the SIGNED-IN account, never a value that travelled in
        // the request body. Layered above the samples and below the caller's context: on the
        // template-management screen it replaces the invented "Nguyễn Văn An" sample with whoever is
        // actually looking at the screen, which is both truer and the same value their own test send
        // would carry.
        //
        // Restricted to what the template declares, for the same reason the dispatcher restricts it: the
        // renderer refuses a supplied variable a template does not declare.
        var sender = await _senders.ResolveAsync(actorId, code, cancellationToken);
        var declared = SystemEmailTemplates.Find(code)?.DeclaredVariables;

        if (declared is not null)
        {
            var senderValues = sender.ToVariableValues();
            foreach (var name in declared)
            {
                if (senderValues.TryGetValue(name, out var value)) context[name] = value;
            }
        }

        foreach (var pair in request.Context ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) continue;

            // A caller may not supply a trusted block: those are the only route by which markup, and
            // therefore a live action URL, enters a rendered message.
            if (EmailTrustedBlocks.All.Contains(pair.Key)) continue;

            // …nor a sender variable. It is an ordinary variable in every other respect, but its VALUE
            // identifies a person, and accepting one from the client would let a caller present anybody as
            // the sender of an official message — the one thing resolving it from authentication prevents.
            if (Sender.EmailSenderVariableNames.IsSenderVariable(pair.Key)) continue;

            context[pair.Key] = pair.Value;
        }

        var rendered = await _renderer.RenderAsync(
            new EmailRenderRequest(code, language, context, trustedBlocks), cancellationToken);

        var replyTo = ReplyToOf(sender);
        var editable = Sender.EmailSenderVariableCapabilities.IsRuntimeEditable(code);

        // The token is issued only where an editor can be opened. A read-only template has nothing to
        // hand to final-preview, and minting a token nobody can spend would invite a client to try.
        string? previewToken = null;
        string? expiresAt = null;

        if (editable)
        {
            var revision = await _db.EmailTemplates
                .AsNoTracking()
                .Where(t => t.TemplateCode == code)
                .Select(t => t.Revision)
                .FirstOrDefaultAsync(cancellationToken);

            var expiry = VietnamTime.Now().Add(PreviewLifetime);

            // ContentHash is empty at this stage on purpose. What the sender approves is decided at the
            // FINAL preview, and binding the template's wording here would refuse the ordinary case —
            // opening the editor and changing a sentence, which is the entire point of the flow.
            previewToken = _tokens.Issue(new EmailPreviewTokenPayload(
                EmailPreviewPurposes.Prepare,
                actorId,
                code,
                revision,
                request.ScopeKey ?? string.Empty,
                ContentHash: string.Empty,
                AttachmentHash: string.Empty,
                ReplyToEmail: replyTo,
                ExpiresAt: expiry));

            expiresAt = expiry.ToString("O", CultureInfo.InvariantCulture);
        }

        // Whether this template actually HAS an action area is read off the rendered body rather than
        // guessed: the block was substituted only if the body asked for it. A registered template whose
        // stored body has lost the placeholder therefore previews as a plain one — the drift is reported
        // by the content validator, which is where it can be repaired, rather than faked here.
        var usesActionBlock = spec is not null
            && rendered.Body.Contains(EmailComposition.ActionBlockStart, StringComparison.Ordinal);

        if (!usesActionBlock)
        {
            // Plain template: the whole body is editable, no system action block.
            //
            // Still assembled through the shared composer rather than returned bare. A message with no
            // buttons still has a shell, and the eye icon has to show the shell — otherwise "no action
            // block" would quietly mean "no branded preview either", which is a different claim.
            return new PreviewEmailTemplateResponse(
                rendered.TemplateCode, rendered.Subject, rendered.Body,
                EmailComposition.HtmlToPlainText(rendered.Body),
                EmailPreviewComposition.Assemble(
                    rendered.TemplateCode, rendered.Body, language, rendered.BodyFormat),
                false, null, null, Array.Empty<string>(), true, rendered.BodyFormat.ToString(),
                replyTo, editable, previewToken, expiresAt);
        }

        // Action template: the editable content keeps the action area IN PLACE, as an inert system node.
        //
        // It used to be cut out entirely (StripActionArtifacts) and returned alongside, which is what made
        // the buttons migrate to the bottom of every edited message: the author's copy had no action area
        // at all, so the send had nowhere to put the real block except the end. Substituting the node
        // leaves the position exactly where the stored template put it, and lets the author move it.
        //
        // StripActionArtifacts still runs first, and still matters: it removes any action anchor or
        // placeholder the TEMPLATE carries outside the canonical block. Only the canonical span becomes
        // the node.
        var editableContent = EmailSystemBlockNodes.ReplaceInjectedBlockWithNode(rendered.Body);

        // previewToken/expiresAt are passed HERE too, and their absence was a real defect: this is the
        // return the editable templates actually take. Every one of the eight the capability map marks
        // AVAILABLE_EDITABLE_RUNTIME — the participant, student and department-leader invitations, the
        // department staff assignment, the three logistics messages, the setup-progress update — carries
        // an action block, so they all leave through this path and not the plain one above. Reporting
        // runtimeEditable: true with no token told the modal to offer "Chỉnh sửa" and then gave it
        // nothing to hand to final-preview, so "Xem trước kết quả" could not succeed and the message
        // could never be sent. The edit flow was dead for precisely the templates it was built for.
        // The finished message the eye icon shows. Built from the SAME editable content the editor gets,
        // through the SAME composer the final preview runs, so "open and send" and "open, edit nothing,
        // look at the final preview and send" cannot show two different messages. Before this the first
        // preview had no assembled form at all and the browser pasted the buttons in itself.
        var initialFinalPreview = EmailPreviewComposition.Assemble(
            rendered.TemplateCode, editableContent, language, rendered.BodyFormat);

        return new PreviewEmailTemplateResponse(
            rendered.TemplateCode, rendered.Subject, editableContent,
            EmailComposition.HtmlToPlainText(editableContent),
            initialFinalPreview,
            true, spec!.SystemActionDescription, disabledActionBlock,
            spec.RequiredActionPlaceholders, true, rendered.BodyFormat.ToString(),
            replyTo, editable, previewToken, expiresAt);
    }

    /// <summary>
    /// The address the send would put in Reply-To, worked out the same way the dispatcher does.
    ///
    /// <para>
    /// Duplicated deliberately rather than shared: the dispatcher's version produces an
    /// <c>EmailRecipient</c> for an SMTP header and this one produces a string for a sentence on a screen.
    /// What must not drift is the RULE, and both read it from the same resolved sender rather than from a
    /// policy either of them interprets.
    /// </para>
    /// </summary>
    private static string? ReplyToOf(Sender.EmailSenderVariables sender)
        => !string.IsNullOrWhiteSpace(sender.Email)
           && EmailRecipientValidator.IsWellFormed(sender.Email!)
            ? sender.Email!.Trim()
            : null;
}
