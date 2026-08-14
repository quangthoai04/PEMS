using System.Collections.Generic;
using System.Linq;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// Keeps <see cref="VisitInstanceFormDetail.OperationalContactGuestMemberId"/> pointing at the right
/// delegation member — the one place that decides it, for create, for edit, and for a backfill (NP-03).
///
/// <para>
/// Two things call this and they arrive with different amounts of information. A CREATE knows what the
/// user picked ("đầu mối là người số 2 trong đoàn"), which is the strongest answer there is. An EDIT
/// has replaced every member row with a fresh copy (copy-on-write), so the id it used to hold now
/// points at a deleted row and has to be re-derived from the snapshot it still has.
/// </para>
/// <para>
/// Both go through <see cref="Resolve"/>, so the rule cannot differ between them — and it is the rule
/// in <see cref="PersonIdentity"/>, the same one the biên bản auto-fill de-duplicates with.
/// </para>
/// </summary>
public static class OperationalContactLink
{
    /// <summary>
    /// Points the detail's contact at one of <paramref name="members"/>, or leaves it null.
    ///
    /// <param name="detail">The campus's form detail — its contact snapshot is the thing being matched.</param>
    /// <param name="members">
    /// This campus's members, IN FORM ORDER (guests first, then external support) — the same order the
    /// create/edit payload lists them in, which is what makes <paramref name="pickedVisitorIndex"/>
    /// meaningful.
    /// </param>
    /// <param name="pickedVisitorIndex">
    /// The delegation row the user explicitly chose as the contact, if they chose one. Out-of-range and
    /// negative values are ignored rather than throwing: an index is a hint from a client, and a stale
    /// one should degrade to matching, not fail a submission.
    /// </param>
    /// </summary>
    public static void Resolve(
        VisitInstanceFormDetail detail,
        IReadOnlyList<VisitGuestMember> members,
        int? pickedVisitorIndex = null)
    {
        detail.OperationalContactGuestMemberId = Match(detail, members, pickedVisitorIndex);
    }

    /// <summary>The id <see cref="Resolve"/> would assign. Separated so callers can test/log it.</summary>
    public static ulong? Match(
        VisitInstanceFormDetail detail,
        IReadOnlyList<VisitGuestMember> members,
        int? pickedVisitorIndex = null)
    {
        if (members.Count == 0) return null;

        // 1. What the user actually chose. Nothing beats it — in particular, it survives the user
        //    editing the contact's job title afterwards, which is exactly the case a name+role+org
        //    match would silently lose.
        if (pickedVisitorIndex is int index && index >= 0 && index < members.Count)
        {
            var picked = members[index];
            if (picked.GuestMemberId != 0) return picked.GuestMemberId;
        }

        // 2. Otherwise match the snapshot. Guests first: an "external support" row is somebody FPTU's
        //    side arranged, and the guest-side contact is far more likely to be one of the delegation
        //    proper. Within each group the FIRST match wins — deterministically, by display order.
        var key = PersonIdentity.Key(
            detail.OperationalContactFullName,
            detail.OperationalContactJobTitle,
            detail.OperationalContactOrganization);
        if (key.Length == 0) return null;

        var byType = members
            .Where(m => m.GuestMemberId != 0)
            .OrderBy(m => m.MemberType == "GUEST" ? 0 : 1)
            .ThenBy(m => m.DisplayOrder)
            .ThenBy(m => m.GuestMemberId);

        foreach (var member in byType)
        {
            if (PersonIdentity.Key(member.FullName, member.JobTitle, member.Organization) == key)
                return member.GuestMemberId;
        }

        // 3. No match is a real, normal answer: plenty of campuses are coordinated by somebody who is
        //    not travelling with the delegation. The biên bản adds them from the snapshot instead.
        return null;
    }
}
