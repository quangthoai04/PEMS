using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>Which cascade level supplied a field's effective value.</summary>
public static class EmailContactPolicyLevels
{
    public const string Template = "TEMPLATE";
    public const string Campus = "CAMPUS";
    public const string Department = "DEPARTMENT";
    public const string System = "SYSTEM";

    /// <summary>No stored row said anything: the value comes from <see cref="EmailContactPolicyDefaults"/>.</summary>
    public const string ShippedDefault = "SHIPPED_DEFAULT";
}

/// <summary>
/// Where each field of a resolved policy came from.
///
/// <para>
/// One label per field rather than one per policy, because the cascade is applied per field: a campus row
/// that only hides telephone numbers supplies <see cref="ShowPhone"/> and nothing else, and every other
/// field still belongs to whichever level did answer. A single "is this inherited" flag cannot describe
/// that, and the version of this screen that had one derived it from "does a TEMPLATE row exist" — which
/// is answered <c>true</c> for all 31 seeded templates and so could never say anything at all.
/// </para>
/// </summary>
public sealed record EmailContactPolicyProvenance(
    string Requirement,
    string ContactSource,
    string ShowEmail,
    string ShowPhone,
    string ShowDepartment,
    string ShowCampus,
    string ShowSender,
    string Heading,
    string ReplyToSource)
{
    /// <summary>True when at least one field's value was not supplied by this template's own row.</summary>
    public bool HasInheritedField => new[]
    {
        Requirement, ContactSource, ShowEmail, ShowPhone, ShowDepartment,
        ShowCampus, ShowSender, Heading, ReplyToSource,
    }.Any(level => level != EmailContactPolicyLevels.Template);
}

/// <summary>Reads the configured contact policy and applies the cascade.</summary>
public interface IEmailContactPolicyStore
{
    /// <summary>
    /// The effective policy for a template in a given campus/department context, after
    /// <c>Template → Campus → Department → System → shipped default</c> has been applied field by field.
    /// </summary>
    Task<EmailContactPolicyResolution> ResolveAsync(
        string templateCode, ulong? campusId, ulong? departmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same resolution, plus the level that supplied each field. Used by the settings screen, which
    /// has to show an operator which values are this template's own and which it is inheriting; the send
    /// path uses <see cref="ResolveAsync"/> and never needs to know.
    /// </summary>
    Task<(EmailContactPolicyResolution Policy, EmailContactPolicyProvenance Provenance)>
        ResolveWithProvenanceAsync(
            string templateCode, ulong? campusId, ulong? departmentId,
            CancellationToken cancellationToken = default);
}

/// <summary>
/// Database-backed policy store.
///
/// <para>
/// The cascade is applied <b>per field</b>, not per row. A campus that only wants to hide telephone
/// numbers writes one column and inherits requirement, source and headings from below it; if the winner
/// were the whole most-specific row, that campus would also have to restate every other field and would
/// silently freeze them at whatever they were on the day it was written.
/// </para>
/// <para>
/// This is exactly why every column on <see cref="EmailContactPolicy"/> is nullable: NULL means "say
/// nothing, ask the next level", and it has to stay distinguishable from <c>false</c>, which means "no".
/// </para>
/// <para>
/// Not cached, for the same reason <c>EmailTemplateRenderer</c> is not: an operator who changes a policy
/// must see it on the next preview and the next send. Four small rows are nothing next to the SMTP
/// round-trip they precede.
/// </para>
/// </summary>
public sealed class EmailContactPolicyStore : IEmailContactPolicyStore
{
    private readonly IApplicationDbContext _db;

    public EmailContactPolicyStore(IApplicationDbContext db) => _db = db;

    public async Task<EmailContactPolicyResolution> ResolveAsync(
        string templateCode, ulong? campusId, ulong? departmentId, CancellationToken cancellationToken = default)
        => (await ResolveWithProvenanceAsync(templateCode, campusId, departmentId, cancellationToken)).Policy;

    public async Task<(EmailContactPolicyResolution Policy, EmailContactPolicyProvenance Provenance)>
        ResolveWithProvenanceAsync(
            string templateCode, ulong? campusId, ulong? departmentId,
            CancellationToken cancellationToken = default)
    {
        var code = templateCode?.Trim() ?? string.Empty;

        var campusKey = campusId?.ToString(CultureInfo.InvariantCulture);
        var departmentKey = departmentId?.ToString(CultureInfo.InvariantCulture);

        List<EmailContactPolicy> rows;
        try
        {
            // One query for every level this send could possibly read.
            rows = await _db.EmailContactPolicies
                .AsNoTracking()
                .Where(p =>
                    (p.ScopeType == EmailContactScopeType.TEMPLATE && p.ScopeKey == code)
                    || (p.ScopeType == EmailContactScopeType.CAMPUS && campusKey != null && p.ScopeKey == campusKey)
                    || (p.ScopeType == EmailContactScopeType.DEPARTMENT && departmentKey != null && p.ScopeKey == departmentKey)
                    || p.ScopeType == EmailContactScopeType.SYSTEM)
                .ToListAsync(cancellationToken);
        }
        catch (DbException ex)
        {
            // Almost always one thing: a database that has not had the contact-information patch applied,
            // so email_contact_policies does not exist. Named rather than allowed to surface as a raw
            // provider error, because the two readers of this failure need to be told different things —
            // an operator needs "run the patch", and a 500 with a MySQL message tells them nothing. The
            // rest of the cascade is deliberately NOT attempted: continuing on the shipped defaults would
            // present an unconfigured database as a working one.
            throw new BusinessRuleException(
                "Không đọc được bảng chính sách liên hệ (email_contact_policies). Database này chưa chạy "
                + "patch docs/database/scripts/patches/2026-08-03_email_contact_information_block.sql.",
                EmailErrorCodes.ContactPolicyStoreUnavailable,
                ex);
        }

        // Most specific first — First(non-null) wins per field.
        var levels = new (string Level, EmailContactPolicy? Row)[]
        {
            (EmailContactPolicyLevels.Template, rows.FirstOrDefault(r => r.ScopeType == EmailContactScopeType.TEMPLATE)),
            (EmailContactPolicyLevels.Campus, rows.FirstOrDefault(r => r.ScopeType == EmailContactScopeType.CAMPUS)),
            (EmailContactPolicyLevels.Department, rows.FirstOrDefault(r => r.ScopeType == EmailContactScopeType.DEPARTMENT)),
            (EmailContactPolicyLevels.System, rows.FirstOrDefault(r => r.ScopeType == EmailContactScopeType.SYSTEM)),
        };

        var ordered = levels
            .Where(l => l.Row is not null)
            .Select(l => (l.Level, Row: l.Row!))
            .ToList();

        // The shipped default is the floor. A database with no rows at all therefore behaves exactly like
        // a freshly seeded one, which is what keeps a half-run patch from silently disabling every block.
        var shipped = EmailContactPolicyDefaults.For(code);

        var requirement = Pick(ordered, r => r.Requirement);
        var contactSource = Pick(ordered, r => r.ContactSource);
        var showEmail = Pick(ordered, r => r.ShowEmail);
        var showPhone = Pick(ordered, r => r.ShowPhone);
        var showDepartment = Pick(ordered, r => r.ShowDepartment);
        var showCampus = Pick(ordered, r => r.ShowCampus);
        var showSender = Pick(ordered, r => r.ShowSender);
        var headingVi = Text(ordered, r => r.HeadingVi);
        var headingEn = Text(ordered, r => r.HeadingEn);
        var replyToSource = Pick(ordered, r => r.ReplyToSource);

        var resolution = new EmailContactPolicyResolution(
            Requirement: requirement.Value ?? shipped.Requirement,
            ContactSource: contactSource.Value ?? shipped.ContactSource,
            ShowEmail: showEmail.Value ?? shipped.ShowEmail,
            ShowPhone: showPhone.Value ?? shipped.ShowPhone,
            ShowDepartment: showDepartment.Value ?? shipped.ShowDepartment,
            ShowCampus: showCampus.Value ?? shipped.ShowCampus,
            ShowSender: showSender.Value ?? shipped.ShowSender,
            HeadingVi: headingVi.Value ?? shipped.HeadingVi,
            HeadingEn: headingEn.Value ?? shipped.HeadingEn,
            ReplyToSource: replyToSource.Value ?? shipped.ReplyToSource);

        // The two headings share one label. They are edited as a pair on the screen and stored on one
        // row, so reporting them separately would offer a distinction the operator cannot act on; a pair
        // where only one side is inherited is reported as inherited, which is the cautious reading.
        var headingLevel = headingVi.Level == EmailContactPolicyLevels.Template
                           && headingEn.Level == EmailContactPolicyLevels.Template
            ? EmailContactPolicyLevels.Template
            : headingVi.Level == headingEn.Level ? headingVi.Level : headingEn.Level;

        var provenance = new EmailContactPolicyProvenance(
            requirement.Level, contactSource.Level, showEmail.Level, showPhone.Level,
            showDepartment.Level, showCampus.Level, showSender.Level, headingLevel, replyToSource.Level);

        Validate(code, resolution);

        return (resolution, provenance);
    }

    private static (T? Value, string Level) Pick<T>(
        IEnumerable<(string Level, EmailContactPolicy Row)> ordered, Func<EmailContactPolicy, T?> field)
        where T : struct
    {
        foreach (var (level, row) in ordered)
            if (field(row) is { } value) return (value, level);
        return (null, EmailContactPolicyLevels.ShippedDefault);
    }

    private static (string? Value, string Level) Text(
        IEnumerable<(string Level, EmailContactPolicy Row)> ordered, Func<EmailContactPolicy, string?> field)
    {
        foreach (var (level, row) in ordered)
        {
            var value = field(row);
            if (!string.IsNullOrWhiteSpace(value)) return (value!.Trim(), level);
        }
        return (null, EmailContactPolicyLevels.ShippedDefault);
    }

    /// <summary>
    /// Refuses a combination that cannot produce a sensible message, at resolve time rather than at send
    /// time. A REQUIRED policy that shows neither an email nor a telephone number would render a heading,
    /// a name and no way to reach it — which is the exact defect this feature exists to remove, arrived at
    /// through configuration instead of through content.
    /// </summary>
    private static void Validate(string templateCode, EmailContactPolicyResolution policy)
    {
        if (policy.Requirement == EmailContactRequirement.NONE) return;

        if (!policy.ShowEmail && !policy.ShowPhone)
            throw new BusinessRuleException(
                $"Cấu hình khối liên hệ của template '{templateCode}' không hợp lệ: mức "
                + $"{policy.Requirement} nhưng đã tắt cả email lẫn số điện thoại, nên khối sẽ không có "
                + "cách liên hệ nào.",
                EmailErrorCodes.ContactConfigurationInvalid);

        if (policy.ReplyToSource == EmailReplyToSource.CONTACT && !policy.ShowEmail)
            throw new BusinessRuleException(
                $"Cấu hình khối liên hệ của template '{templateCode}' không hợp lệ: Reply-To trỏ về đầu "
                + "mối nhưng email của đầu mối bị ẩn, nên người nhận không thấy được nơi thư trả lời sẽ đến.",
                EmailErrorCodes.ContactConfigurationInvalid);
    }
}
