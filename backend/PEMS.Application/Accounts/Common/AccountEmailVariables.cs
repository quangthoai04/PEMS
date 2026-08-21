using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Builds the variable dictionaries the ACCOUNT_* templates declare.
///
/// <para>
/// This replaces the old <c>AccountConfirmationEmail</c>, which held the subject and the HTML body for
/// the confirmation mail. Five handlers shared that helper and a sixth (account creation) kept its own
/// divergent copy of the same message — so "the same email" was really two emails that had already
/// drifted. Content now lives in <c>email_templates</c>; what is genuinely shared, and what stays here,
/// is the job of turning a user row into the values the template asks for.
/// </para>
/// </summary>
public static class AccountEmailVariables
{
    /// <summary>Shown when an account has no campus — the template always renders the line.</summary>
    private const string NoValue = "—";

    /// <summary>
    /// Variables for <c>ACCOUNT_EMAIL_CONFIRMATION</c>. Role and campus are read from the database, never
    /// from the request, so the email describes the account that actually exists.
    /// </summary>
    public static async Task<Dictionary<string, string>> ForConfirmationAsync(
        IApplicationDbContext db,
        string fullName,
        string roleCode,
        string? subRole,
        ulong? primaryCampusId,
        int expiresInHours,
        CancellationToken cancellationToken)
        => new()
        {
            ["fullName"] = fullName,
            ["roleName"] = AccountRoleDisplayNames.Resolve(roleCode, subRole),
            ["campusName"] = await CampusNameAsync(db, primaryCampusId, cancellationToken),
            ["expiresInHours"] = expiresInHours.ToString(),
        };

    /// <summary>Variables for <c>ACCOUNT_ACTIVATED</c>.</summary>
    public static async Task<Dictionary<string, string>> ForActivatedAsync(
        IApplicationDbContext db,
        User user,
        string roleCode,
        string? subRole,
        CancellationToken cancellationToken)
        => new()
        {
            ["fullName"] = user.FullName,
            ["roleName"] = AccountRoleDisplayNames.Resolve(roleCode, subRole),
            ["campusName"] = await CampusNameAsync(db, user.PrimaryCampusId, cancellationToken),
        };

    /// <summary>
    /// Variables for <c>ACCOUNT_STAFF_LEADER_ASSIGNED</c> — sent to the person taking the role.
    /// </summary>
    public static Dictionary<string, string> ForStaffLeaderAssigned(
        string fullName, string? campusName, DateTime effectiveAt, string reason)
        => new()
        {
            ["fullName"] = fullName,
            ["campusName"] = Display(campusName),
            ["effectiveDate"] = FormatDate(effectiveAt),
            ["reason"] = reason,
        };

    /// <summary>
    /// Variables for <c>ACCOUNT_STAFF_LEADER_REPLACED</c> — sent to the person leaving the role. It names
    /// the successor because the outgoing leader needs to know who now decides on their campus's requests.
    /// </summary>
    public static Dictionary<string, string> ForStaffLeaderReplaced(
        string fullName, string? campusName, string successorName, DateTime effectiveAt, string reason)
        => new()
        {
            ["fullName"] = fullName,
            ["campusName"] = Display(campusName),
            ["successorName"] = successorName,
            ["effectiveDate"] = FormatDate(effectiveAt),
            ["reason"] = reason,
        };

    /// <summary>
    /// The confirm-email button, built by the backend around a one-time token.
    ///
    /// <para>
    /// The label is read from <c>EmailActionTemplates</c> — the same metadata the editor's preview reads
    /// — so what an operator sees while editing is what the recipient gets. The language defaults to VI,
    /// matching <c>SystemEmailRequest.Language</c>, so all eight call sites keep their behaviour.
    /// </para>
    /// </summary>
    public static Dictionary<string, string> ConfirmationBlocks(
        string confirmUrl, string language = EmailLanguages.Vi)
        => new()
        {
            [EmailTrustedBlocks.ActionBlock] = EmailComposition.ConfirmEmailBlock(
                confirmUrl, EmailActionTemplates.ConfirmEmailLabel(language)),
        };

    /// <summary>The sign-in button on the activated-account notice.</summary>
    public static Dictionary<string, string> LoginBlocks(string loginUrl, string language = EmailLanguages.Vi)
        => new() { [EmailTrustedBlocks.ActionBlock] = EmailComposition.LoginBlock(loginUrl, language) };

    /// <summary>
    /// Masks an address for display: <c>ha.nguyen@fpt.edu.vn</c> becomes <c>ha***@fpt.edu.vn</c>. Used
    /// only where the recipient is entitled to know which address is meant but does not need it in full.
    /// </summary>
    public static string MaskEmail(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return NoValue;

        var at = address.IndexOf('@');
        if (at <= 0) return "***";

        var local = address[..at];
        var domain = address[at..];
        var head = local.Length <= 2 ? local[..1] : local[..2];

        return head + "***" + domain;
    }

    /// <summary>The date convention the rest of the PEMS mail already uses (Vietnam wall clock).</summary>
    private static string FormatDate(DateTime value) => value.ToString("dd/MM/yyyy");

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? NoValue : value;

    private static async Task<string> CampusNameAsync(
        IApplicationDbContext db, ulong? campusId, CancellationToken cancellationToken)
    {
        if (campusId is null) return NoValue;

        var name = await db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == campusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(name) ? NoValue : name;
    }
}
