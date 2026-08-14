using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Partners;

namespace PEMS.Application.Partners.Common;

/// <summary>
/// Who may be offered — and therefore who may be SUBMITTED — as the organization of a delegation
/// member (<c>visit_guest_members.organization_partner_id</c>).
///
/// <para>The audience split is the point. The public form is filled in by people outside FPTU, so it
/// may only ever reveal profiles that are already published: anything else would leak the existence
/// of internal partner records to anonymous callers. Staff filling the same form from inside need the
/// internal set, or they end up retyping an organization that already exists — which is precisely how
/// a stable partner id gets lost and the minutes screen ends up asking them to "create" it again
/// (PART-03).</para>
///
/// <para>Both the option query and the create/edit write path run through here, so what the dropdown
/// offers and what the backend accepts can never drift apart. The backend never infers the partner
/// from the submitted organization NAME — a name is a display snapshot, not an identity.</para>
/// </summary>
public static class GuestOrganizationPartnerPolicy
{
    /// <summary>The only combination an anonymous/Visitor caller may see or select.</summary>
    public static IQueryable<Partner> PublicSelectable(IQueryable<Partner> partners) =>
        partners.Where(p =>
            p.CooperationStatus == "ACTIVE"
            && p.ProfileStatus == PartnerProfileStatuses.Approved
            && p.Visibility == PartnerVisibilities.Public);

    /// <summary>
    /// What an authenticated internal caller may see or select: every APPROVED non-private profile,
    /// plus their OWN campus's profiles still awaiting a decision — a Staff Leader deciding tomorrow
    /// is no reason to make staff retype the organization today. DRAFT and REJECTED are excluded from
    /// both sides: neither is a profile anybody may attach a person to (PART-04).
    /// </summary>
    public static IQueryable<Partner> InternalSelectable(IQueryable<Partner> partners, ulong? actorCampusId) =>
        partners.Where(p =>
            (p.ProfileStatus == PartnerProfileStatuses.Approved
             && p.Visibility != PartnerVisibilities.Private)
            || (p.ProfileStatus == PartnerProfileStatuses.PendingApproval
                && actorCampusId != null && p.OwnerCampusId == actorCampusId));

    /// <summary>
    /// Whether this caller gets the internal option set. A Visitor editing their own request is on the
    /// public side of the split even though they are logged in — the account is theirs, the partner
    /// records are not.
    ///
    /// <para>A null <paramref name="user"/> means no session was resolved, which answers "public".
    /// The unknown case therefore lands on the NARROWER set, never the wider one.</para>
    /// </summary>
    public static bool IsInternalAudience(ICurrentUserService? user) =>
        user is not null && PartnerAccess.CanViewPartnerModule(user);

    /// <summary>Validates the ids carried by every member of a per-campus form payload.</summary>
    public static Task EnsureFormSelectableAsync(
        IApplicationDbContext db,
        IEnumerable<ulong?> submittedIds,
        ICurrentUserService? user,
        CancellationToken ct) =>
        EnsureSelectableAsync(
            db,
            submittedIds.Where(id => id.HasValue).Select(id => id!.Value),
            isPublicAudience: !IsInternalAudience(user),
            actorCampusId: user?.PrimaryCampusId,
            ct);

    /// <summary>
    /// Rejects every submitted <c>organizationPartnerId</c> the caller was not entitled to pick.
    ///
    /// <para>Validates the ids as a SET in one round trip rather than per member — a delegation of
    /// thirty people from three organizations is three ids, not thirty queries.</para>
    /// </summary>
    /// <param name="isPublicAudience">
    /// True for the visitor/anonymous submit paths. Derived from the create path itself, never from
    /// the payload: a client that says it is internal does not make it so.
    /// </param>
    public static async Task EnsureSelectableAsync(
        IApplicationDbContext db,
        IEnumerable<ulong> partnerIds,
        bool isPublicAudience,
        ulong? actorCampusId,
        CancellationToken ct)
    {
        var ids = partnerIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var baseQuery = db.Partners.AsNoTracking().Where(p => ids.Contains(p.PartnerId));
        var allowed = await (isPublicAudience
                ? PublicSelectable(baseQuery)
                : InternalSelectable(baseQuery, actorCampusId))
            .Select(p => p.PartnerId)
            .ToListAsync(ct);

        var rejected = ids.Except(allowed).ToList();
        if (rejected.Count == 0) return;

        // One message for "does not exist" and for "not yours": distinguishing them turns the form
        // into a probe for which partner ids exist.
        throw new BusinessRuleException(
            "Tổ chức đã chọn cho thành viên trong đoàn không hợp lệ hoặc không còn khả dụng.",
            "INVALID_MEMBER_ORGANIZATION_PARTNER");
    }
}
