using System;
using System.Collections.Generic;
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

/// <summary>Reads the configured contact policy and applies the cascade.</summary>
public interface IEmailContactPolicyStore
{
    /// <summary>
    /// The effective policy for a template in a given campus/department context, after
    /// <c>Template → Campus → Department → System → shipped default</c> has been applied field by field.
    /// </summary>
    Task<EmailContactPolicyResolution> ResolveAsync(
        string templateCode, ulong? campusId, ulong? departmentId, CancellationToken cancellationToken = default);
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
    {
        var code = templateCode?.Trim() ?? string.Empty;

        var campusKey = campusId?.ToString(CultureInfo.InvariantCulture);
        var departmentKey = departmentId?.ToString(CultureInfo.InvariantCulture);

        // One query for every level this send could possibly read.
        var rows = await _db.EmailContactPolicies
            .AsNoTracking()
            .Where(p =>
                (p.ScopeType == EmailContactScopeType.TEMPLATE && p.ScopeKey == code)
                || (p.ScopeType == EmailContactScopeType.CAMPUS && campusKey != null && p.ScopeKey == campusKey)
                || (p.ScopeType == EmailContactScopeType.DEPARTMENT && departmentKey != null && p.ScopeKey == departmentKey)
                || p.ScopeType == EmailContactScopeType.SYSTEM)
            .ToListAsync(cancellationToken);

        // Most specific first — First(non-null) wins per field.
        var ordered = new[]
        {
            rows.FirstOrDefault(r => r.ScopeType == EmailContactScopeType.TEMPLATE),
            rows.FirstOrDefault(r => r.ScopeType == EmailContactScopeType.CAMPUS),
            rows.FirstOrDefault(r => r.ScopeType == EmailContactScopeType.DEPARTMENT),
            rows.FirstOrDefault(r => r.ScopeType == EmailContactScopeType.SYSTEM),
        }.Where(r => r is not null).Cast<EmailContactPolicy>().ToList();

        // The shipped default is the floor. A database with no rows at all therefore behaves exactly like
        // a freshly seeded one, which is what keeps a half-run patch from silently disabling every block.
        var shipped = EmailContactPolicyDefaults.For(code);

        var resolution = new EmailContactPolicyResolution(
            Requirement: Pick(ordered, r => r.Requirement) ?? shipped.Requirement,
            ContactSource: Pick(ordered, r => r.ContactSource) ?? shipped.ContactSource,
            ShowEmail: Pick(ordered, r => r.ShowEmail) ?? shipped.ShowEmail,
            ShowPhone: Pick(ordered, r => r.ShowPhone) ?? shipped.ShowPhone,
            ShowDepartment: Pick(ordered, r => r.ShowDepartment) ?? shipped.ShowDepartment,
            ShowCampus: Pick(ordered, r => r.ShowCampus) ?? shipped.ShowCampus,
            ShowSender: Pick(ordered, r => r.ShowSender) ?? shipped.ShowSender,
            HeadingVi: Text(ordered, r => r.HeadingVi) ?? shipped.HeadingVi,
            HeadingEn: Text(ordered, r => r.HeadingEn) ?? shipped.HeadingEn,
            ReplyToSource: Pick(ordered, r => r.ReplyToSource) ?? shipped.ReplyToSource);

        Validate(code, resolution);

        return resolution;
    }

    private static T? Pick<T>(IEnumerable<EmailContactPolicy> ordered, Func<EmailContactPolicy, T?> field)
        where T : struct
    {
        foreach (var row in ordered)
            if (field(row) is { } value) return value;
        return null;
    }

    private static string? Text(IEnumerable<EmailContactPolicy> ordered, Func<EmailContactPolicy, string?> field)
    {
        foreach (var row in ordered)
        {
            var value = field(row);
            if (!string.IsNullOrWhiteSpace(value)) return value!.Trim();
        }
        return null;
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
