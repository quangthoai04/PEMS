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
/// <param name="HasOwnPolicyRow">
/// Whether a TEMPLATE-scope row exists for this template at all. Reported as the plain fact it is —
/// NOT as "these are the defaults". The seed writes a row for every catalogued template, so this is true
/// almost everywhere and says nothing about whether any particular value was inherited; the per-field
/// <c>*Source</c> labels below are what answer that.
/// </param>
/// <param name="RequirementSource">
/// Which cascade level supplied this field: TEMPLATE / CAMPUS / DEPARTMENT / SYSTEM / SHIPPED_DEFAULT.
/// One label per field because the cascade is applied per field.
/// </param>
/// <param name="AvailableRequirements">
/// Legal values, from the backend rather than a list in the screen — and NARROWED by capability, so the
/// screen can render the radios it is given instead of deciding which ones to hide. Empty on a template
/// that cannot carry the block at all; without NONE on one whose text tells the reader to make contact.
/// </param>
/// <param name="Capability">
/// UNSUPPORTED / SUPPORTED / REQUIRED — whether the block may appear AT ALL, which is a different
/// question from <paramref name="Requirement"/>. See <see cref="EmailContactCapabilities"/>.
/// </param>
/// <param name="Editable">
/// False when there is nothing here an operator could change. The screen shows
/// <paramref name="CapabilityReasonVi"/> in place of the form rather than a form that saves nothing.
/// </param>
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
    bool HasOwnPolicyRow,
    string RequirementSource,
    string ContactSourceSource,
    string ShowEmailSource,
    string ShowPhoneSource,
    string ShowDepartmentSource,
    string ShowCampusSource,
    string ShowSenderSource,
    string ReplyToSourceSource,
    string HeadingSource,
    IReadOnlyList<string> AvailableRequirements,
    IReadOnlyList<string> AvailableSources,
    IReadOnlyList<string> AvailableReplyToSources,
    string Capability,
    bool Editable,
    string CapabilityReasonCode,
    string CapabilityReasonVi,
    string CapabilityReasonEn)
{
    /// <summary>True when at least one field is not this template's own — the only honest trigger for an
    /// "inheriting" notice on the screen.</summary>
    public bool HasInheritedField =>
        new[]
        {
            RequirementSource, ContactSourceSource, ShowEmailSource, ShowPhoneSource,
            ShowDepartmentSource, ShowCampusSource, ShowSenderSource, ReplyToSourceSource, HeadingSource,
        }.Any(level => level != EmailContactPolicyLevels.Template);
}

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

        var capability = EmailContactCapabilities.For(code);

        // Resolved WITHOUT a campus or department: the template screen edits the template level, and
        // showing a value that only applies inside one campus would misdescribe what the operator is
        // about to change.
        var (policy, provenance) =
            await _policies.ResolveWithProvenanceAsync(code, null, null, cancellationToken);

        // An UNSUPPORTED template reports NONE whatever a stored row says. A row that turned the block on
        // for a credential-bearing mail is a fault to be told about, not a state to be presented as
        // current: the send path will not render the block, the validator will not admit it into the
        // body, and a screen that showed OPTIONAL here would be describing something that cannot happen.
        if (!capability.Supported)
            policy = policy with { Requirement = EmailContactRequirement.NONE };

        var hasOwnRow = await _db.EmailContactPolicies
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
            hasOwnRow,
            provenance.Requirement,
            provenance.ContactSource,
            provenance.ShowEmail,
            provenance.ShowPhone,
            provenance.ShowDepartment,
            provenance.ShowCampus,
            provenance.ShowSender,
            provenance.ReplyToSource,
            provenance.Heading,
            capability.AvailableRequirements,
            Enum.GetNames<EmailContactSource>(),
            Enum.GetNames<EmailReplyToSource>(),
            capability.Capability.ToString(),
            capability.Supported,
            capability.ReasonCode,
            capability.ReasonVi,
            capability.ReasonEn);
    }
}
