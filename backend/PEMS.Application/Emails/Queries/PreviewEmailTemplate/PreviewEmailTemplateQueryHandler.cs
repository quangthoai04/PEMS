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
        var spec = EmailActionTemplates.For(code);

        // The buttons a preview shows are dead on purpose: real tokens are minted only by a real send.
        // They are still passed as a trusted block so the body is assembled exactly as it will be, and
        // so the strip below has a well-formed block to remove.
        var disabled = spec is null
            ? null
            : spec.HasLogisticsAction
                ? EmailComposition.DisabledLogisticsActionBlock()
                : spec.HasDetailLink
                    ? EmailComposition.DisabledDetailLinkBlock()
                    : EmailComposition.DisabledAcceptDeclineBlock(spec.HasAssignLink);

        var trustedBlocks = disabled is null
            ? null
            : new Dictionary<string, string>
            {
                [EmailTrustedBlocks.ActionBlock] =
                    EmailComposition.ActionBlockStart + disabled + EmailComposition.ActionBlockEnd,
            };

        var rendered = await _renderer.RenderAsync(
            new EmailRenderRequest(
                code,
                EmailLanguages.Normalize(request.Language),
                request.Context ?? new Dictionary<string, string>(),
                trustedBlocks),
            cancellationToken);

        if (spec is null)
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
            true, spec.SystemActionDescription, disabled,
            spec.RequiredActionPlaceholders, true, rendered.BodyFormat.ToString());
    }
}
