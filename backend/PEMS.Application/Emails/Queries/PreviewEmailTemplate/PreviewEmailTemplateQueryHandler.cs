using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Queries.PreviewEmailTemplate;

/// <summary>
/// Loads the requested template (must exist and be ACTIVE), picks the VI or EN subject/body, and
/// substitutes {{variable}} placeholders with the provided context values. Unresolved placeholders
/// are left intact so the previewer can see what was not supplied. No persistence, no SMTP.
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
        return new PreviewEmailTemplateResponse(
            Render(subject ?? string.Empty, context),
            Render(body ?? string.Empty, context));
    }

    /// <summary>Replaces {{ key }} (any inner whitespace, case-insensitive key) with its context value.</summary>
    private static string Render(string template, Dictionary<string, string> context)
    {
        if (string.IsNullOrEmpty(template) || context.Count == 0) return template;

        return Regex.Replace(template, @"\{\{\s*([\w]+)\s*\}\}", match =>
        {
            var key = match.Groups[1].Value;
            foreach (var kvp in context)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value ?? string.Empty;
            }
            return match.Value; // leave unresolved placeholders intact
        });
    }
}
