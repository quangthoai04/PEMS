using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>One account a sender is allowed to present as the reply contact.</summary>
/// <param name="UserId">What the client sends back. The only field it sends back.</param>
/// <param name="HasEmail">
/// Whether this person can actually be reached by mail. Surfaced so the picker can say so BEFORE the
/// choice is made, rather than letting the sender pick somebody and meet a refusal on send.
/// </param>
public sealed record EmailContactCandidateDto(
    ulong UserId,
    string FullName,
    string? Email,
    string? Phone,
    string? DepartmentName,
    string? CampusName,
    bool HasEmail);

/// <summary>
/// Who a given sender may name as the reply contact on a given message, and the search behind the
/// picker.
///
/// <para>
/// One service rather than an authorisation check next to the resolver and a query next to the endpoint,
/// because those two must agree exactly: a picker that offers a colleague the send then refuses is a bug
/// the user cannot act on, and a picker narrower than the check quietly hides people who were allowed.
/// Both callers go through <see cref="Scope"/>, so the rule is stated once.
/// </para>
/// <para>
/// The scope is deliberately NOT derived from "which role code is this" alone. It is derived from the
/// role AND the message's own campus/department, because the same Staff Leader is allowed a different set
/// of names depending on which visit they are writing about — and the visit is the thing the send has
/// already checked they may act on.
/// </para>
/// </summary>
public interface IEmailContactCandidateService
{
    /// <summary>
    /// The people this actor may choose for this message, matching <paramref name="term"/> on name or
    /// address. Never returns an account that is not <c>ACTIVE</c>.
    /// </summary>
    Task<IReadOnlyList<EmailContactCandidateDto>> SearchAsync(
        EmailContactRequest context, ulong actorUserId, string? term, int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The chosen account as a contact, or null when this actor may not choose them — including when the
    /// account does not exist or is not ACTIVE. The three are one answer on purpose: distinguishing them
    /// would let the picker be used to probe for accounts by id.
    /// </summary>
    Task<EmailContactInformation?> ResolveChoiceAsync(
        EmailContactRequest context, ulong actorUserId, ulong candidateUserId,
        CancellationToken cancellationToken = default);
}

public sealed class EmailContactCandidateService : IEmailContactCandidateService
{
    /// <summary>
    /// A ceiling on what one search may return. The picker is a search box, not a directory export, and an
    /// unbounded query against <c>users</c> behind a template screen is a slow page waiting to happen.
    /// </summary>
    public const int MaxTake = 25;

    private readonly IApplicationDbContext _db;

    public EmailContactCandidateService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<EmailContactCandidateDto>> SearchAsync(
        EmailContactRequest context, ulong actorUserId, string? term, int take,
        CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(context, actorUserId, cancellationToken);
        var limit = take <= 0 ? 10 : Math.Min(take, MaxTake);

        var query = Eligible(scope);

        var needle = term?.Trim();
        if (!string.IsNullOrEmpty(needle))
            query = query.Where(u => u.FullName.Contains(needle) || u.Email.Contains(needle));

        var rows = await query
            .OrderBy(u => u.FullName)
            .Take(limit)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Phone,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                CampusName = u.PrimaryCampus != null ? u.PrimaryCampus.Name : null,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new EmailContactCandidateDto(
                r.UserId, r.FullName, r.Email, r.Phone, r.DepartmentName, r.CampusName,
                HasEmail: !string.IsNullOrWhiteSpace(r.Email)))
            .ToList();
    }

    public async Task<EmailContactInformation?> ResolveChoiceAsync(
        EmailContactRequest context, ulong actorUserId, ulong candidateUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(context, actorUserId, cancellationToken);

        var row = await Eligible(scope)
            .Where(u => u.UserId == candidateUserId)
            .Select(u => new
            {
                u.FullName,
                u.Email,
                u.Phone,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                CampusName = u.PrimaryCampus != null ? u.PrimaryCampus.Name : null,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null) return null;

        // A chosen contact with neither address nor telephone is not a contact. Reported as "not allowed"
        // by the caller rather than rendered as a name under a heading with nothing beneath it.
        if (string.IsNullOrWhiteSpace(row.Email) && string.IsNullOrWhiteSpace(row.Phone)) return null;

        var en = EmailLanguages.Normalize(context.Language) == EmailLanguages.En;

        return new EmailContactInformation(
            // The branch is recorded as the manual-choice one so the audit and the preview can say the
            // contact was chosen for this message rather than resolved from the policy.
            EmailContactSource.SENDER,
            row.FullName,
            RoleLabel: en ? "Contact for this email" : "Đầu mối cho email này",
            DepartmentName: row.DepartmentName,
            CampusName: row.CampusName,
            Email: row.Email,
            Phone: row.Phone);
    }

    // ── Scope ───────────────────────────────────────────────────────────────

    /// <summary>What an actor may reach for this particular message.</summary>
    /// <param name="Unrestricted">HO and Admin: the system, subject to the ACTIVE filter.</param>
    /// <param name="CampusId">The message's campus, or the actor's own when the message has none.</param>
    /// <param name="DepartmentIds">Departments this actor may name somebody from.</param>
    /// <param name="SelfUserId">Always allowed: naming yourself needs no wider permission.</param>
    private sealed record CandidateScope(
        bool Unrestricted,
        ulong? CampusId,
        IReadOnlyCollection<ulong> DepartmentIds,
        ulong SelfUserId);

    private async Task<CandidateScope> ScopeAsync(
        EmailContactRequest context, ulong actorUserId, CancellationToken cancellationToken)
    {
        var actor = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserId == actorUserId)
            .Select(u => new { u.PrimaryCampusId, u.DepartmentId, RoleCode = u.Role.RoleCode, u.SubRole })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null) return new CandidateScope(false, null, Array.Empty<ulong>(), actorUserId);

        var effectiveRole = ResolveRole(actor.RoleCode, actor.SubRole);

        // HO and Admin are not given the system by virtue of their role code alone — they are given it
        // because the endpoints that reach this are already behind the role gate that grants them
        // campus-wide reach, and a narrower rule here would only make the picker disagree with what they
        // can already do on the same screen.
        if (effectiveRole is EffectiveRole.Ho or EffectiveRole.Admin)
            return new CandidateScope(true, null, Array.Empty<ulong>(), actorUserId);

        var departments = new HashSet<ulong>();
        if (actor.DepartmentId is { } ownDepartment) departments.Add(ownDepartment);

        // A department person stays inside their own department, whatever the message is about. The
        // message's department is NOT added for them: a Leader writing about a visit their department is
        // supporting must not thereby be able to name somebody from the department on the other side of
        // that conversation.
        if (effectiveRole is EffectiveRole.DepartmentLead or EffectiveRole.Department)
            return new CandidateScope(false, null, departments, actorUserId);

        // Staff and Staff Leader work per campus, and the campus that matters is the MESSAGE's — a Host
        // writing about the Cần Thơ instance names Cần Thơ people. Falling back to their own campus when
        // the message has none keeps the rule closed rather than open.
        var campusId = await MessageCampusAsync(context, cancellationToken) ?? actor.PrimaryCampusId;

        // …and for a message addressed to or about a department, that department too: the logistics
        // request the Host is sending IS the department-related mail the rule means.
        if (context.DepartmentId is { } messageDepartment) departments.Add(messageDepartment);

        return new CandidateScope(false, campusId, departments, actorUserId);
    }

    /// <summary>
    /// The campus this message belongs to: the per-campus visit instance when there is one, else the
    /// explicit campus. Never widened to the parent request — that is the distinction that keeps a
    /// multi-campus visit from borrowing another campus's people.
    /// </summary>
    private async Task<ulong?> MessageCampusAsync(EmailContactRequest context, CancellationToken ct)
    {
        if (context.CampusId is { } explicitCampus) return explicitCampus;
        if (context.VisitInstanceId is not { } instanceId) return null;

        return await _db.VisitRequestCampuses
            .AsNoTracking()
            .Where(v => v.VisitInstanceId == instanceId)
            .Select(v => (ulong?)v.CampusId)
            .FirstOrDefaultAsync(ct);
    }

    private static string? ResolveRole(string? roleCode, string? subRole)
    {
        if (string.IsNullOrWhiteSpace(roleCode)) return null;

        // A role/sub-role pair the matrix does not recognise is a data fault, and the safe reading of a
        // data fault is the narrow one — not an exception that would take down a search box.
        try { return EffectiveRole.Resolve(roleCode!, subRole); }
        catch (InvalidOperationException) { return null; }
    }

    /// <summary>
    /// The eligibility filter, shared by the search and the single-id check so the picker and the send can
    /// never disagree about who is allowed.
    /// </summary>
    private IQueryable<Domain.Entities.Users.User> Eligible(CandidateScope scope)
    {
        var query = _db.Users.AsNoTracking().Where(u => u.Status == "ACTIVE");

        if (scope.Unrestricted) return query;

        var departmentIds = scope.DepartmentIds.ToList();
        var campusId = scope.CampusId;
        var self = scope.SelfUserId;

        return query.Where(u =>
            u.UserId == self
            || (campusId != null && u.PrimaryCampusId == campusId)
            || (u.DepartmentId != null && departmentIds.Contains(u.DepartmentId.Value)));
    }
}
