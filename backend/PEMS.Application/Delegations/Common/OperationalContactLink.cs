using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
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

        // 2. No pick, from a client that mints keys → "nobody", and that is an ANSWER rather than a gap.
        //
        //    Such a client asks before it submits: an unlinked contact whose name, job title and
        //    organisation match exactly one member raises "cùng một người, hay hai người khác nhau?".
        //    So the payload arriving here with keys but no pick means the user was shown that question
        //    and said they are two people. Falling through to the fingerprint below would answer it
        //    again, the other way, and silently — which is precisely what the question exists to stop:
        //    the biên bản then held ONE row badged "Khách · Đầu mối" for what the user had just said
        //    were two humans, and the two buttons in the dialog produced identical data.
        //
        //    Only a client with no keys at all can still be guessed for, and only because it never
        //    asked: an amendment replaying member lists stored days ago, or a legacy payload.
        if (members.Any(m => !string.IsNullOrWhiteSpace(m.ClientMemberKey))) return null;

        // 3. Otherwise match the snapshot. Guests first: an "external support" row is somebody FPTU's
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

        // 4. No match is a real, normal answer: plenty of campuses are coordinated by somebody who is
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
    /// <returns>
    /// Whether any of the three fields actually changed. Existing callers that only cared about the
    /// write (Create) can keep ignoring the return value; callers that need to know whether anything
    /// moved (the post-COW sync on Pending Edit/Resubmit/Amendment approve) read it directly instead of
    /// re-diffing before and after themselves.
    /// </returns>
    public static bool ApplySnapshotFromMember(VisitInstanceFormDetail detail, VisitGuestMember member)
    {
        var newOrganization = string.IsNullOrWhiteSpace(member.Organization) ? null : member.Organization;
        var changed =
            !string.Equals(detail.OperationalContactFullName, member.FullName, StringComparison.Ordinal)
            || !string.Equals(detail.OperationalContactJobTitle, member.JobTitle, StringComparison.Ordinal)
            || !string.Equals(detail.OperationalContactOrganization, newOrganization, StringComparison.Ordinal);

        detail.OperationalContactFullName = member.FullName;
        detail.OperationalContactJobTitle = member.JobTitle;
        detail.OperationalContactOrganization = newOrganization;
        return changed;
    }

    /// <summary>
    /// Confirms <paramref name="candidateId"/> names exactly one ELIGIBLE member of <paramref
    /// name="members"/> — the PERSISTENT-identity counterpart to <see cref="FindPicked"/>'s
    /// ephemeral-key check (plan CanhIter3FixBug "Đầu mối hiện tại có nằm trong danh sách đoàn
    /// không?"), used when the member list itself is untouched and every row already has a stable
    /// database id to name.
    ///
    /// <para>
    /// <paramref name="members"/> must already be scoped to THIS instance (e.g.
    /// <c>V2CanonicalRefresh.MembersOf(request, instance)</c>) — the check never looks beyond what it
    /// is given, so a sibling campus's member id is refused here exactly like one that does not exist
    /// at all, never accepted because it happens to exist somewhere else on the same request.
    /// </para>
    /// <para>
    /// Every way this can fail throws, exactly like the key-based path: a candidate naming nobody, or
    /// naming someone not eligible for the role, is refused rather than silently accepted or silently
    /// turned into null.
    /// </para>
    /// </summary>
    public static void EnsureGuestMemberIdEligible(IReadOnlyList<VisitGuestMember> members, ulong candidateId)
    {
        var found = members.FirstOrDefault(m => m.GuestMemberId == candidateId);
        if (found is null)
            throw new BusinessRuleException(
                OperationalContactMessages.MemberNotInDelegation,
                OperationalContactErrorCodes.MemberNotFound);
        if (!IsEligible(found))
            throw new BusinessRuleException(
                OperationalContactMessages.MemberNotEligible,
                OperationalContactErrorCodes.MemberNotEligible);
    }

    /// <summary>
    /// Whether <paramref name="member"/>'s FullName/JobTitle/Organization describe the same person as
    /// the proposed contact profile — the ONE identity-match primitive, reused by every caller that
    /// needs it (Safe Edit, the standalone profile endpoint) instead of each duplicating its own
    /// <see cref="PersonIdentity.Key"/> comparison. Deliberately a FACT, not a throwing guard: which
    /// business error a mismatch means depends on what the CALLER was trying to do — creating a new
    /// link to a mismatched member is <c>RelationProfileMismatch</c>, retyping a linked contact's
    /// profile away from the member it's linked to is <c>LinkedProfileRequiresMemberUpdate</c> — and a
    /// primitive that threw one hard-coded code could not tell those apart.
    /// </summary>
    public static bool RelationMatchesContact(
        string fullName, string jobTitle, string? organization, VisitGuestMember member)
        => string.Equals(
            PersonIdentity.Key(fullName, jobTitle, organization),
            PersonIdentity.Key(member.FullName, member.JobTitle, member.Organization),
            StringComparison.Ordinal);

    /// <summary>
    /// Copies a linked member's shared fields onto the contact snapshot AND records the real old→new
    /// audit entries, in one call — the only way callers can get this right, because the audit "before"
    /// values must be read BEFORE <see cref="ApplySnapshotFromMember"/> mutates <paramref
    /// name="detail"/>, and a caller that applied first and diffed after would log a no-op
    /// (<c>"Senior Director" → "Senior Director"</c>) instead of the real change. Used by Pending Edit,
    /// Resubmit and Amendment Approve after they re-link the same logical member across a copy-on-write
    /// rewrite; never by Create (which has no "before" to diff) and never by Safe Edit (which mutates
    /// the snapshot from typed input, not from a member row).
    /// </summary>
    /// <returns>Whether anything actually changed (mirrors <see cref="ApplySnapshotFromMember"/>).</returns>
    public static bool SyncSnapshotFromLinkedMember(
        AuditLog audit, VisitInstanceFormDetail detail, VisitGuestMember member, DateTime now)
    {
        var oldFullName = detail.OperationalContactFullName;
        var oldJobTitle = detail.OperationalContactJobTitle;
        var oldOrganization = detail.OperationalContactOrganization;

        var changed = ApplySnapshotFromMember(detail, member);
        if (!changed) return false;

        void AddIfChanged(string field, string? oldValue, string? newValue)
        {
            if (string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal))
                return;
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = field,
                OldValueText = oldValue,
                NewValueText = newValue,
                CreatedAt = now,
            });
        }

        AddIfChanged("operational_contact_full_name", oldFullName, detail.OperationalContactFullName);
        AddIfChanged("operational_contact_job_title", oldJobTitle, detail.OperationalContactJobTitle);
        AddIfChanged("operational_contact_organization", oldOrganization, detail.OperationalContactOrganization);
        return true;
    }

    /// <summary>
    /// What a copy-on-write member-list rewrite (or a member-list-unchanged relation echo) proves, or
    /// fails to prove, about continuity of the campus's Operational Contact relation (operational-contact
    /// consistency fix). A FACT, not a business error — see <see cref="CheckPreservesExistingMemberRelation"/>.
    /// </summary>
    public enum ContactMemberContinuityResult
    {
        /// <summary>Nothing to protect (was unlinked, stays unlinked), or the same persisted member is
        /// proven present under the same key (was linked, stays linked to the same person).</summary>
        Preserved,
        /// <summary>
        /// The row the proposed key names exists but carries no persisted <c>GuestMemberId</c> — the
        /// structural signature of a client built before that field existed. Only reachable when there
        /// is no other evidence the currently-linked member is present at all; once such evidence
        /// exists this never fires (see <see cref="RelationKeyPointsElsewhere"/>).
        /// </summary>
        MissingIdentityEvidence,
        /// <summary>The currently-linked persisted member does not appear anywhere in the incoming rows.</summary>
        CurrentMemberMissing,
        /// <summary>More than one incoming row carries the currently-linked persisted id — never "accept the first".</summary>
        DuplicatePersistentId,
        /// <summary>
        /// The currently-linked persisted member IS present, but the proposed key names something else —
        /// null, unresolvable, a different real persisted member, or a brand-new row. A repoint attempt,
        /// regardless of what the other key resolves to.
        /// </summary>
        RelationKeyPointsElsewhere,
        /// <summary>
        /// There is currently no linked contact (<c>currentGuestMemberId</c> is null) but a non-empty
        /// key was proposed anyway — an attempt to ESTABLISH a link through a workflow that may only
        /// preserve one, never create or remove one.
        /// </summary>
        RelationIntroduced,
    }

    /// <summary>
    /// Proves — or refuses to assume — that <paramref name="proposedClientMemberKey"/> still names the
    /// SAME persisted member as <paramref name="currentGuestMemberId"/>, using the member's own
    /// persistent id as evidence rather than trusting the ephemeral key alone (operational-contact
    /// consistency fix). <paramref name="incomingRows"/> is the full incoming Visitors+ExternalSupport
    /// set, in any order — every row carries its own (possibly null, for a brand-new row)
    /// <c>GuestMemberId</c> alongside its <c>ClientMemberKey</c>.
    ///
    /// <para>
    /// Classification order matters and is NOT "resolve the proposed key first": a payload proving the
    /// currently-linked member's persisted id is present must never be waved through just because the
    /// proposed key happens to differ in some way that superficially looks like "missing evidence" — a
    /// key naming a brand-new null-id row while the real member is ALSO present is a repoint attempt,
    /// not a stale client, and must reject the same way an outright Kim→Moon repoint does. The
    /// stale-client shape (<see cref="ContactMemberContinuityResult.MissingIdentityEvidence"/>) can only
    /// be true when there is NO other evidence the current member is present — i.e. only in the
    /// zero-current-id-matches branch.
    /// </para>
    /// </summary>
    public static ContactMemberContinuityResult CheckPreservesExistingMemberRelation(
        ulong? currentGuestMemberId,
        IEnumerable<(ulong? GuestMemberId, string? ClientMemberKey)> incomingRows,
        string? proposedClientMemberKey)
    {
        if (currentGuestMemberId is null)
            return string.IsNullOrWhiteSpace(proposedClientMemberKey)
                ? ContactMemberContinuityResult.Preserved
                : ContactMemberContinuityResult.RelationIntroduced;

        var rows = incomingRows as IReadOnlyList<(ulong? GuestMemberId, string? ClientMemberKey)>
            ?? incomingRows.ToList();

        var currentMatches = rows.Where(r => r.GuestMemberId == currentGuestMemberId).ToList();

        if (currentMatches.Count > 1)
            return ContactMemberContinuityResult.DuplicatePersistentId;

        if (currentMatches.Count == 1)
        {
            // The currently-linked member IS proven present. From here, the ONLY acceptable proposed
            // key is that exact row's own key — any other value is a repoint attempt, full stop, no
            // matter what that other key resolves to (null, nothing, a different real member, or a
            // brand-new row). Never re-classified as "missing evidence": evidence for the CURRENT
            // member already exists, so that state cannot also mean "no evidence".
            return string.Equals(
                currentMatches[0].ClientMemberKey, proposedClientMemberKey, StringComparison.Ordinal)
                ? ContactMemberContinuityResult.Preserved
                : ContactMemberContinuityResult.RelationKeyPointsElsewhere;
        }

        // No row proves the current member is present at all. Only now can the stale-client shape
        // apply: the row the OLD, key-only logic would have picked exists, but carries no id.
        // An empty/absent proposed key never counts as "naming" a row — otherwise a brand-new row
        // that also happens to carry a null ClientMemberKey would spuriously "match" a caller that
        // proposed no key at all, and a genuinely removed member would be misreported as stale
        // instead of missing.
        var keyedMatches = string.IsNullOrWhiteSpace(proposedClientMemberKey)
            ? new List<(ulong? GuestMemberId, string? ClientMemberKey)>()
            : rows.Where(r => string.Equals(r.ClientMemberKey, proposedClientMemberKey, StringComparison.Ordinal))
                .ToList();

        return keyedMatches.Count == 1 && keyedMatches[0].GuestMemberId is null
            ? ContactMemberContinuityResult.MissingIdentityEvidence
            : ContactMemberContinuityResult.CurrentMemberMissing;
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

    /// <summary>
    /// The lifecycle window for correcting a same-person operational-contact's DETAILS — a WIDER window
    /// than replacing/transferring or than generic Safe Edit, because nothing about authority moves.
    /// PUBLIC (unlike <c>OperationalContactGuards</c>, which is internal to the Commands.OperationalContact
    /// namespace) so both <c>UpdateOperationalContactProfileCommandHandler</c> (PEMS.Application) and
    /// <c>VisitSafeEditService</c> (PEMS.Infrastructure, a different assembly) call the SAME
    /// implementation — <c>OperationalContactGuards.EnsureProfileUpdateAllowed</c> now delegates here
    /// rather than duplicating the rule (plan CanhIter3FixBug, decision M).
    ///
    /// <para>
    /// The window is the four statuses before the visit starts, and it is a positive whitelist rather
    /// than a list of dead ends: a campus that is already running, finished or closed has a contact
    /// record the visit was actually received against, and rewriting the name or phone on it after the
    /// fact edits history rather than correcting a plan. The two statuses that never had a plan to
    /// correct — cancelled and rejected — are refused by the same rule. No clock is consulted: an
    /// approved campus starting in six hours is precisely when a corrected phone number matters most.
    /// </para>
    /// </summary>
    public static void EnsureProfileUpdateLifecycleAllowed(VisitRequest visit, VisitRequestCampus instance)
    {
        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new BusinessRuleException(
                "Đơn đã bị hủy nên không thể thay đổi đầu mối vận hành.",
                OperationalContactErrorCodes.ChangeConflict);

        if (instance.Status is
            VisitInstanceStatuses.WaitingContactConfirmation
            or VisitInstanceStatuses.WaitingRequestApproval
            or VisitInstanceStatuses.Assigned
            or VisitInstanceStatuses.BeforeVisit)
            return;

        throw new ConflictException(
            instance.Status is VisitInstanceStatuses.Cancelled or VisitInstanceStatuses.Rejected
                ? "Lịch thăm tại cơ sở này đã kết thúc quy trình nên không thể sửa thông tin đầu mối vận hành."
                : "Chuyến thăm tại cơ sở này đã bắt đầu nên không thể sửa thông tin đầu mối vận hành.",
            OperationalContactErrorCodes.ChangeConflict);
    }
}
