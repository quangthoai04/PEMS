using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Queries.PreviewEmailTemplate;

/// <summary>
/// Loads the requested template (must exist and be ACTIVE), picks the VI or EN subject/body, and
/// substitutes {{variable}} placeholders with the provided context values. For action templates the
/// action artifacts are stripped from the editable body and a disabled action block is returned for
/// read-only display. No persistence, no tokens, no SMTP.
/// </summary>
public sealed class PreviewEmailTemplateQueryHandler
    : IRequestHandler<PreviewEmailTemplateQuery, PreviewEmailTemplateResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public PreviewEmailTemplateQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PreviewEmailTemplateResponse> Handle(
        PreviewEmailTemplateQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        if (string.IsNullOrWhiteSpace(request.TemplateCode))
            throw new ValidationException("Thiếu mã template email.");

        var code = request.TemplateCode.Trim();
        var template = await _db.EmailTemplates
            .FirstOrDefaultAsync(t => t.TemplateCode == code, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy template email '{code}'.");

        if (!string.Equals(template.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException($"Template email '{code}' đang không hoạt động.");

        var useEnglish = string.Equals(request.Language?.Trim(), "EN", StringComparison.OrdinalIgnoreCase);
        var subject = useEnglish ? (template.SubjectEn ?? template.SubjectVi) : (template.SubjectVi ?? template.SubjectEn);
        var body = useEnglish ? (template.BodyEn ?? template.BodyVi) : (template.BodyVi ?? template.BodyEn);

        var context = request.Context ?? new Dictionary<string, string>();

        string contextType = "GENERAL";
        if (code == "VISIT_PARTICIPANT_INVITATION" || code == "VISIT_DEPARTMENT_LEADER_INVITATION" || code == "VISIT_STUDENT_SUPPORT_INVITATION")
        {
            contextType = "PARTICIPANT_INVITATION";
            // Strip the entire coordination note line if present in the legacy DB template
            body = Regex.Replace(body ?? string.Empty, @"<br/>\s*<strong>Nội dung phối hợp:</strong>\s*\{\{coordinationNote\}\}", "", RegexOptions.IgnoreCase);
            body = Regex.Replace(body ?? string.Empty, @"<br/>\s*<strong>Coordination note:</strong>\s*\{\{coordinationNote\}\}", "", RegexOptions.IgnoreCase);
        }
        else if (code == "LOGISTICS_REQUEST_TO_DEPARTMENT")
        {
            contextType = "LOGISTICS_REQUEST";
        }

        var renderedSubject = EmailComposition.RenderTemplate(subject ?? string.Empty, context, contextType);
        var renderedBody = EmailComposition.RenderTemplate(body ?? string.Empty, context, contextType);

        var spec = EmailActionTemplates.For(code);
        if (spec is null)
        {
            // Plain template: the whole body is editable, no system action block.
            return new PreviewEmailTemplateResponse(
                code, renderedSubject, renderedBody, EmailComposition.HtmlToPlainText(renderedBody),
                false, null, null, Array.Empty<string>(), true, template.BodyFormat.ToString());
        }

        // Action template: editable content is the body WITHOUT the action artifacts; the action
        // block is shown read-only (disabled). Real tokens are only minted on the actual send.
        var editableContent = EmailComposition.StripActionArtifacts(renderedBody);
        var locked = spec.HasLogisticsAction
            ? EmailComposition.DisabledLogisticsActionBlock()
            : spec.HasDetailLink
                ? EmailComposition.DisabledDetailLinkBlock()
                : EmailComposition.DisabledAcceptDeclineBlock(spec.HasAssignLink);

        return new PreviewEmailTemplateResponse(
            code, renderedSubject, editableContent, EmailComposition.HtmlToPlainText(editableContent),
            true, spec.SystemActionDescription, locked, spec.RequiredActionPlaceholders, true, template.BodyFormat.ToString());
    }

}
