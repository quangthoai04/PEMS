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
/// <para><b>The split is by USE CASE, not by who is logged in (PART-09).</b> It used to be by
/// audience: an authenticated Staff Leader got the internal option set, which includes their own
/// campus's profiles still awaiting a decision. That put "Hồ sơ chờ duyệt · FPT University Hà Nội"
/// into the "Đơn vị công tác" dropdown of a registration form — a profile nobody has approved,
/// attached to a delegation member, on a request that then travels through approval carrying it.
/// Being signed in is not a reason to be offered an unapproved partner; the question the form asks is
/// "which published organisation does this guest work for", and that question has the same answer for
/// everybody.</para>
///
/// <list type="bullet">
///   <item><c>REQUEST_FORM</c> (<see cref="RequestFormSelectable"/>) — the registration form, for
///   every caller: ACTIVE + APPROVED + PUBLIC and nothing else.</item>
///   <item><c>PARTNER_MANAGEMENT</c> (<see cref="InternalSelectable"/>) — the partner module, where
///   seeing a pending or internal profile IS the job. Unchanged, and deliberately so: this class does
///   not narrow who may VIEW partners, only who may be attached to a delegation member.</item>
///   <item><c>PARTNER_MATCHING</c> — the duplicate matcher, which must keep showing pending/rejected
///   candidates so a second profile is not created for an organisation that already has one. It has
///   its own endpoint (<c>/partners/match</c>) and is untouched here.</item>
/// </list>
///
/// <para>Both the option query and the create/edit write path run through here, so what the dropdown
/// offers and what the backend accepts can never drift apart. The backend never infers the partner
/// from the submitted organization NAME — a name is a display snapshot, not an identity — and it
/// never trusts an id just because the client sent it.</para>
/// </summary>
public static class GuestOrganizationPartnerPolicy
{
    /// <summary>
    /// Refused id on a registration form. Distinct from the module's own access errors: nothing is
    /// forbidden to the CALLER here — the profile is simply not in a state a request may cite.
    /// </summary>
    public const string NotSelectableCode = "PARTNER_NOT_SELECTABLE";

    public const string NotSelectableMessage =
        "Đối tác này chưa được duyệt và công khai nên không thể sử dụng trong đơn đăng ký. "
        + "Vui lòng chọn lại đơn vị công tác cho thành viên trong đoàn.";

    /// <summary>The only combination an anonymous/Visitor caller may see or select.</summary>
    public static IQueryable<Partner> PublicSelectable(IQueryable<Partner> partners) =>
        partners.Where(p =>
            p.CooperationStatus == "ACTIVE"
            && p.ProfileStatus == PartnerProfileStatuses.Approved
            && p.Visibility == PartnerVisibilities.Public);

    /// <summary>
    /// What a REGISTRATION FORM may offer and accept, for every caller alike (PART-09).
    ///
    /// <para>Identical to <see cref="PublicSelectable"/> today, and named separately on purpose: the
    /// two answer different questions and only happen to agree. "What may an anonymous caller see"
    /// is about disclosure; "what may a request cite as a member's employer" is about the state of the
    /// profile. Should the disclosure rule ever loosen, this one must not follow it silently.</para>
    /// </summary>
    public static IQueryable<Partner> RequestFormSelectable(IQueryable<Partner> partners) =>
        PublicSelectable(partners);

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

    /// <summary>
    /// Validates the ids carried by every member of a per-campus form payload.
    ///
    /// <para>The caller is no longer consulted (PART-09). A Staff Leader's session widened the option
    /// set AND widened what the write path would accept, so a pending profile picked from the widened
    /// dropdown sailed through to <c>visit_guest_members</c>. Both halves are now the form's rule
    /// rather than the session's, which is also what makes a direct API call carrying a pending id
    /// fail in the same way as the UI: <c>user</c> is kept in the signature only so call sites read
    /// the same, and is deliberately unused.</para>
    /// </summary>
    public static Task EnsureFormSelectableAsync(
        IApplicationDbContext db,
        IEnumerable<ulong?> submittedIds,
        ICurrentUserService? user,
        CancellationToken ct) =>
        EnsureRequestFormSelectableAsync(
            db, submittedIds.Where(id => id.HasValue).Select(id => id!.Value), ct);

    /// <summary>
    /// Rejects every submitted <c>organizationPartnerId</c> a registration form may not cite —
    /// anything that is not ACTIVE + APPROVED + PUBLIC, whoever is submitting.
    ///
    /// <para>Validates the ids as a SET in one round trip rather than per member — a delegation of
    /// thirty people from three organizations is three ids, not thirty queries.</para>
    /// </summary>
    public static async Task EnsureRequestFormSelectableAsync(
        IApplicationDbContext db,
        IEnumerable<ulong> partnerIds,
        CancellationToken ct)
    {
        var ids = partnerIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var allowed = await RequestFormSelectable(
                db.Partners.AsNoTracking().Where(p => ids.Contains(p.PartnerId)))
            .Select(p => p.PartnerId)
            .ToListAsync(ct);

        if (ids.Except(allowed).Any())
            // One message for "does not exist", for "not approved" and for "not public": telling them
            // apart turns the form into a probe for which partner ids exist and what state they are in.
            throw new BusinessRuleException(NotSelectableMessage, NotSelectableCode);
    }
}
