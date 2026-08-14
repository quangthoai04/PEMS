using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// One member row paired with the client-minted key the submitting form knew it by.
///
/// <para>
/// The pairing only exists between the insert and the link: the key identifies a person the database
/// has never seen, the entity identifies the row it has just become. Nothing persists the key.
/// </para>
/// </summary>
public readonly record struct KeyedMember(VisitGuestMember Member, string? ClientMemberKey);

/// <summary>
/// Keeps <see cref="VisitInstanceFormDetail.OperationalContactGuestMemberId"/> pointing at the right
/// delegation member — the one place that decides it, for create, for edit, and for a backfill (NP-03).
///
/// <para>
/// The contact used to be five free-text columns with no relation to anything, so "is the person
/// coordinating this visit also one of the people attending it?" could only be answered by comparing
/// strings. That produced both failure modes at once: a contact who WAS in the delegation list turned
/// up twice in the biên bản, and a contact who was NOT in it turned up nowhere.
/// </para>
///
/// <para><b>What identifies the pick.</b> A <c>ClientMemberKey</c> — minted per row by the form and
/// stable for as long as it is open. The previous version used the member's ARRAY POSITION, which is
/// not an identity at all: inserting a row above the chosen one, removing one, or reordering the list
/// re-aimed the contact at a different person with nothing on screen to show for it. Positions are
/// gone from this file entirely; there is no index-shaped parameter left to reach for.</para>
///
/// <para><b>Who is eligible.</b> Any member of THIS campus — <c>GUEST</c> or <c>EXTERNAL_SUPPORT</c>.
/// The support list is where an interpreter, an assistant or a travelling coordinator is written down,
/// and those are exactly the people a campus rings. Restricting the pick to GUEST rows meant the
/// obvious answer could not be given, so the contact was retyped by hand and the link lost. FPTU's own
/// people are not eligible and cannot be: hosts and internal participants are not in
/// <c>visit_guest_members</c> at all, so there is no key that could name one.</para>
///
/// <para><b>Snapshot vs link.</b> The five <c>operational_contact_*</c> columns stay: they record what
/// was agreed, at the moment it was agreed, and must not follow later edits to the member row. The id
/// answers the separate question "which person is that". Callers on the CREATE side additionally run
/// <see cref="ApplySnapshotFromMember"/>, so a payload can never claim one person's id under another
/// person's name.</para>
/// </summary>
public static class OperationalContactLink
{
    /// <summary>
    /// The member types that may hold the role. Both are guest-side rows of this campus's own
    /// delegation; <c>visit_guest_members.member_type</c> has no other values.
    /// </summary>
    public static readonly IReadOnlyCollection<string> EligibleMemberTypes =
        new[] { GuestMemberType.Guest, GuestMemberType.ExternalSupport };

    public static bool IsEligible(VisitGuestMember member) =>
        member.MemberType == GuestMemberType.Guest || member.MemberType == GuestMemberType.ExternalSupport;

    /// <summary>
    /// Pairs freshly staged member rows with the keys they arrived under. Both sequences are in
    /// PAYLOAD ORDER (visitors, then support) because the rows were built from it in one pass — the
    /// only ordering assumption in this file, and the callers that build both are the same two methods.
    /// </summary>
    public static IReadOnlyList<KeyedMember> Pair(
        IReadOnlyList<VisitGuestMember> rows, IReadOnlyList<string?>? clientMemberKeys)
    {
        var paired = new List<KeyedMember>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
            paired.Add(new KeyedMember(rows[i], clientMemberKeys is not null && i < clientMemberKeys.Count
                ? clientMemberKeys[i]
                : null));
        return paired;
    }

    /// <summary>
    /// Points the detail's contact at one of <paramref name="members"/>, or leaves it null.
    ///
    /// <param name="detail">The campus's form detail — its contact snapshot is the thing being matched.</param>
    /// <param name="members">This campus's members paired with the keys the payload used.</param>
    /// <param name="pickedClientMemberKey">
    /// The member the user chose in "Đầu mối là ai trong đoàn?", if they chose one. A key that names
    /// nobody is REFUSED rather than ignored: the only ways to produce one are deleting the person who
    /// is the contact (which has to be told, not absorbed) and a client sending something it made up.
    /// </param>
    /// </summary>
    public static void Resolve(
        VisitInstanceFormDetail detail,
        IReadOnlyList<KeyedMember> members,
        string? pickedClientMemberKey)
    {
        detail.OperationalContactGuestMemberId = Match(detail, members, pickedClientMemberKey);
    }

    /// <summary>
    /// Snapshot-only resolution, for the paths that have no keys to work with — an amendment applying
    /// member lists stored days earlier, and any legacy client. Matching is a guess and stays one; it
    /// is used only where the alternative is losing the link entirely.
    /// </summary>
    public static void Resolve(VisitInstanceFormDetail detail, IReadOnlyList<VisitGuestMember> members)
    {
        detail.OperationalContactGuestMemberId = Match(
            detail, members.Select(m => new KeyedMember(m, null)).ToList(), null);
    }

    /// <summary>The id <see cref="Resolve"/> would assign. Separated so callers can test/log it.</summary>
    public static ulong? Match(
        VisitInstanceFormDetail detail,
        IReadOnlyList<KeyedMember> members,
        string? pickedClientMemberKey = null)
    {
        // 1. What the user actually chose. Nothing else is consulted when a key is present — including
        //    the snapshot, which by then may have been edited on purpose.
        if (!string.IsNullOrWhiteSpace(pickedClientMemberKey))
            return FindPicked(members, pickedClientMemberKey!).Member.GuestMemberId;

        if (members.Count == 0) return null;

        // 2. Otherwise match the snapshot. Guests first: an "external support" row is somebody FPTU's
        //    side arranged, and the guest-side contact is more likely to be one of the delegation
        //    proper. Within each group the FIRST match wins — deterministically, by display order.
        var key = PersonIdentity.Key(
            detail.OperationalContactFullName,
            detail.OperationalContactJobTitle,
            detail.OperationalContactOrganization);
        if (key.Length == 0) return null;

        var byType = members
            .Select(k => k.Member)
            .Where(m => m.GuestMemberId != 0 && IsEligible(m))
            .OrderBy(m => m.MemberType == GuestMemberType.Guest ? 0 : 1)
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

    /// <summary>
    /// The member a key names, or null when no key was sent. Public so the CREATE path can read the
    /// person BEFORE the flush — early enough to insert the snapshot describing them, rather than
    /// writing the client's version and correcting it afterwards.
    ///
    /// <para>Every way a key can fail to name exactly one eligible person still throws here, so the
    /// two call sites cannot disagree about what a valid pick is.</para>
    /// </summary>
    public static VisitGuestMember? FindByClientKey(
        IReadOnlyList<KeyedMember> members, string? pickedClientMemberKey) =>
        string.IsNullOrWhiteSpace(pickedClientMemberKey)
            ? null
            : FindPicked(members, pickedClientMemberKey!).Member;

    /// <summary>
    /// Rewrites the three shared snapshot fields from the member the user picked, on the paths that
    /// are ALLOWED to write the snapshot (create, and a campus being added).
    ///
    /// <para>
    /// This is what makes "đầu mối là Daniel Kim" one fact instead of two that can disagree. A payload
    /// could otherwise carry Daniel's key beside somebody else's name — by tampering, or simply by a
    /// form that copied the fields once and then let the user edit them — and the request would store a
    /// link to one person described as another. Phone and email are NOT touched: a delegation row has
    /// neither, and blanking them would delete the only way to reach the person.
    /// </para>
    /// </summary>
    public static void ApplySnapshotFromMember(VisitInstanceFormDetail detail, VisitGuestMember member)
    {
        detail.OperationalContactFullName = member.FullName;
        detail.OperationalContactJobTitle = member.JobTitle;
        detail.OperationalContactOrganization = string.IsNullOrWhiteSpace(member.Organization)
            ? null
            : member.Organization;
    }

    /// <summary>
    /// The member a key names, refusing every way it can fail to name exactly one eligible person.
    ///
    /// <para>
    /// Each refusal is its own code because each has its own answer: pick somebody else, pick somebody
    /// from a list that can hold them, or fix the client. They are raised INSIDE the caller's
    /// transaction, so a request whose contact could not be resolved is not written at all — a
    /// half-saved delegation with the wrong coordinator is worse than a refused submission.
    /// </para>
    /// </summary>
    private static KeyedMember FindPicked(IReadOnlyList<KeyedMember> members, string pickedClientMemberKey)
    {
        var matches = members
            .Where(m => string.Equals(m.ClientMemberKey, pickedClientMemberKey, System.StringComparison.Ordinal))
            .ToList();

        if (matches.Count == 0)
            throw new BusinessRuleException(
                OperationalContactMessages.MemberNotInDelegation,
                OperationalContactErrorCodes.MemberNotFound);

        if (matches.Count > 1)
            throw new BusinessRuleException(
                OperationalContactMessages.MemberAmbiguous,
                OperationalContactErrorCodes.MemberAmbiguous);

        var picked = matches[0];
        if (!IsEligible(picked.Member))
            throw new BusinessRuleException(
                OperationalContactMessages.MemberNotEligible,
                OperationalContactErrorCodes.MemberNotEligible);

        return picked;
    }
}
