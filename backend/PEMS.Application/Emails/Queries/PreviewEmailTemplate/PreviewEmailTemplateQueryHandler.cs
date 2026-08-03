using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

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
/// </summary>
public sealed class PreviewEmailTemplateQueryHandler
    : IRequestHandler<PreviewEmailTemplateQuery, PreviewEmailTemplateResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailTemplateRenderer _renderer;

    public PreviewEmailTemplateQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IEmailTemplateRenderer renderer)
    {
        _db = db;
        _currentUser = currentUser;
        _renderer = renderer;
    }

    public async Task<PreviewEmailTemplateResponse> Handle(
        PreviewEmailTemplateQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        if (string.IsNullOrWhiteSpace(request.TemplateCode))
            throw new ValidationException("Thiếu mã template email.");

        var code = request.TemplateCode.Trim();
        var language = EmailLanguages.Normalize(request.Language);
        var spec = EmailActionTemplates.For(code);

        var trustedBlocks = new Dictionary<string, string>
        {
            // A preview has no visit, so there is no Host to resolve and no campus to fall back to.
            // A stand-in says where the block goes and what fills it; inventing a plausible name and
            // address would show an operator a person who does not exist and invite them to "correct"
            // contact details the template has no control over.
            [EmailTrustedBlocks.ContactInformationBlock] =
                Contact.EmailContactHtmlRenderer.DisabledBlock(language),

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

        foreach (var pair in request.Context ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) continue;

            // A caller may not supply a trusted block: those are the only route by which markup, and
            // therefore a live action URL, enters a rendered message.
            if (EmailTrustedBlocks.All.Contains(pair.Key)) continue;

            context[pair.Key] = pair.Value;
        }

        var rendered = await _renderer.RenderAsync(
            new EmailRenderRequest(code, language, context, trustedBlocks),
            cancellationToken);

        // Whether this template actually HAS an action area is read off the rendered body rather than
        // guessed: the block was substituted only if the body asked for it. A registered template whose
        // stored body has lost the placeholder therefore previews as a plain one — the drift is reported
        // by the content validator, which is where it can be repaired, rather than faked here.
        var usesActionBlock = spec is not null
            && rendered.Body.Contains(EmailComposition.ActionBlockStart, StringComparison.Ordinal);

        if (!usesActionBlock)
        {
            // Plain template: the whole body is editable, no system action block.
            return new PreviewEmailTemplateResponse(
                rendered.TemplateCode, rendered.Subject, rendered.Body,
                EmailComposition.HtmlToPlainText(rendered.Body),
                false, null, null, Array.Empty<string>(), true, rendered.BodyFormat.ToString());
        }

        // Action template: editable content is the body WITHOUT the action artifacts; the block itself
        // is returned separately so the modal can show it as read-only.
        var editableContent = EmailComposition.StripActionArtifacts(rendered.Body);

        return new PreviewEmailTemplateResponse(
            rendered.TemplateCode, rendered.Subject, editableContent,
            EmailComposition.HtmlToPlainText(editableContent),
            true, spec!.SystemActionDescription, disabledActionBlock,
            spec.RequiredActionPlaceholders, true, rendered.BodyFormat.ToString());
    }
}
