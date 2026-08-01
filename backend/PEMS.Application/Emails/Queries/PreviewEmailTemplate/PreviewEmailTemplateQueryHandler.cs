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

        // The buttons a preview shows are dead on purpose: real tokens are minted only by a real send.
        // They are still passed as a trusted block so the body is assembled exactly as it will be, and
        // so the strip below has a well-formed block to remove.
        //
        // A template with NO registry entry still gets a block — a neutral one (G11 / R-106). Nine of the
        // fourteen templates that use {{actionBlock}} are unregistered, and supplying nothing left the
        // placeholder unresolved, which the renderer refuses: the operator saw an error where a preview
        // should have been. The block is inert either way; a template that does not use the placeholder
        // simply never substitutes it, so passing one costs nothing.
        var disabled = spec is null
            ? EmailComposition.DisabledUnspecifiedActionBlock(language)
            : spec.HasLogisticsAction
                ? EmailComposition.DisabledLogisticsActionBlock()
                : spec.HasDetailLink
                    ? EmailComposition.DisabledDetailLinkBlock()
                    : EmailComposition.DisabledAcceptDeclineBlock(spec.HasAssignLink);

        var trustedBlocks = new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] =
                EmailComposition.ActionBlockStart + disabled + EmailComposition.ActionBlockEnd,

            // Same reasoning as the action block above: supplied unconditionally because a template
            // that does not use the placeholder never substitutes it, while a template that does would
            // otherwise fail the preview closed on an unresolved variable.
            [EmailTrustedBlocks.SetupSummaryBlock] =
                EmailComposition.DisabledSetupSummaryBlock(language),
        };

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

        if (spec is null)
        {
            // Whether this template actually HAS an action area is read off the rendered body rather than
            // guessed: the block was substituted only if the body asked for it.
            var usesActionBlock = rendered.Body.Contains(EmailComposition.ActionBlockStart, StringComparison.Ordinal);

            if (!usesActionBlock)
            {
                // Plain template: the whole body is editable, no system action block.
                return new PreviewEmailTemplateResponse(
                    rendered.TemplateCode, rendered.Subject, rendered.Body,
                    EmailComposition.HtmlToPlainText(rendered.Body),
                    false, null, null, Array.Empty<string>(), true, rendered.BodyFormat.ToString());
            }

            // Unregistered action template: shown the same way a registered one is — editable words on
            // one side, an inert block on the other — so the operator cannot accidentally edit the action
            // area into the template. No required placeholders are claimed, because there is no contract
            // to claim them from.
            var contentWithoutBlock = EmailComposition.StripActionArtifacts(rendered.Body);
            var neutralDescription = language == EmailLanguages.En
                ? "This template has an action area the system fills in when the email is sent. Its buttons are not configured yet, so the preview shows the position only."
                : "Template này có khu vực nút thao tác do hệ thống gắn khi gửi. Các nút cụ thể chưa được cấu hình, nên bản xem trước chỉ hiển thị vị trí.";

            return new PreviewEmailTemplateResponse(
                rendered.TemplateCode, rendered.Subject, contentWithoutBlock,
                EmailComposition.HtmlToPlainText(contentWithoutBlock),
                true, neutralDescription, disabled,
                Array.Empty<string>(), true, rendered.BodyFormat.ToString());
        }

        // Action template: editable content is the body WITHOUT the action artifacts; the block itself
        // is returned separately so the modal can show it as read-only.
        var editableContent = EmailComposition.StripActionArtifacts(rendered.Body);

        return new PreviewEmailTemplateResponse(
            rendered.TemplateCode, rendered.Subject, editableContent,
            EmailComposition.HtmlToPlainText(editableContent),
            true, spec.SystemActionDescription, disabled,
            spec.RequiredActionPlaceholders, true, rendered.BodyFormat.ToString());
    }
}
