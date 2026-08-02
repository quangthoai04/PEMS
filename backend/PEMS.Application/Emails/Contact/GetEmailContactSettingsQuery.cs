using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// The contact settings for one template, as the template screen needs to show them.
/// </summary>
/// <param name="TemplateCode">The template these settings belong to.</param>
/// <param name="Requirement">NONE / OPTIONAL / REQUIRED.</param>
/// <param name="ContactSource">Which source resolves the contact.</param>
/// <param name="BlockPlaceholder">
/// The placeholder the body must contain when <paramref name="Requirement"/> is REQUIRED. Sent to the
/// client so the editor can check the body itself rather than hard-coding the string in a screen.
/// </param>
/// <param name="BodyCarriesBlock">
/// Whether the stored body actually has it, per language — so the screen can warn BEFORE a save is
/// attempted instead of only relaying the refusal afterwards.
/// </param>
/// <param name="IsDefault">False once an operator has saved an override for this template.</param>
/// <param name="AvailableRequirements">Legal values, from the backend rather than a list in the screen.</param>
public sealed record EmailContactSettingsDto(
    string TemplateCode,
    string Requirement,
    string ContactSource,
    bool ShowEmail,
    bool ShowPhone,
    bool ShowDepartment,
    bool ShowCampus,
    bool ShowSender,
    string HeadingVi,
    string HeadingEn,
    string ReplyToSource,
    string BlockPlaceholder,
    bool BodyCarriesBlockVi,
    bool BodyCarriesBlockEn,
    bool IsDefault,
    IReadOnlyList<string> AvailableRequirements,
    IReadOnlyList<string> AvailableSources,
    IReadOnlyList<string> AvailableReplyToSources);

public sealed class GetEmailContactSettingsQuery : IRequest<EmailContactSettingsDto>
{
    public string TemplateCode { get; set; } = string.Empty;
}

public sealed class GetEmailContactSettingsQueryHandler
    : IRequestHandler<GetEmailContactSettingsQuery, EmailContactSettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailContactPolicyStore _policies;

    public GetEmailContactSettingsQueryHandler(IApplicationDbContext db, IEmailContactPolicyStore policies)
    {
        _db = db;
        _policies = policies;
    }

    public async Task<EmailContactSettingsDto> Handle(
        GetEmailContactSettingsQuery request, CancellationToken cancellationToken)
    {
        var code = request.TemplateCode?.Trim() ?? string.Empty;

        _ = SystemEmailTemplates.Find(code)
            ?? throw new NotFoundException(
                $"Mã template email '{code}' không nằm trong danh mục hệ thống.",
                EmailErrorCodes.TemplateNotFound);

        // Resolved WITHOUT a campus or department: the template screen edits the template level, and
        // showing a value that only applies inside one campus would misdescribe what the operator is
        // about to change.
        var policy = await _policies.ResolveAsync(code, null, null, cancellationToken);

        var hasOverride = await _db.EmailContactPolicies
            .AsNoTracking()
            .AnyAsync(p => p.ScopeType == EmailContactScopeType.TEMPLATE && p.ScopeKey == code, cancellationToken);

        var bodies = await _db.EmailTemplates
            .AsNoTracking()
            .Where(t => t.TemplateCode == code)
            .Select(t => new { t.BodyVi, t.BodyEn })
            .FirstOrDefaultAsync(cancellationToken);

        var marker = "{{" + EmailTrustedBlocks.ContactInformationBlock + "}}";

        return new EmailContactSettingsDto(
            code,
            policy.Requirement.ToString(),
            policy.ContactSource.ToString(),
            policy.ShowEmail,
            policy.ShowPhone,
            policy.ShowDepartment,
            policy.ShowCampus,
            policy.ShowSender,
            policy.HeadingVi,
            policy.HeadingEn,
            policy.ReplyToSource.ToString(),
            marker,
            bodies?.BodyVi?.Contains(marker, StringComparison.Ordinal) ?? false,
            bodies?.BodyEn?.Contains(marker, StringComparison.Ordinal) ?? false,
            !hasOverride,
            Enum.GetNames<EmailContactRequirement>(),
            Enum.GetNames<EmailContactSource>(),
            Enum.GetNames<EmailReplyToSource>());
    }
}
