using System;
using System.Collections.Generic;

namespace PEMS.Application.Common.DTOs;

// ──────────────────────────────────────────────────────────────────────────────
// Per-campus AMENDMENT payloads (plan §16.6, Phase E). The requester proposes the FULL new value of every
// amendable field of ONE campus instance (same full-snapshot convention as the edit flows); the backend
// diffs against the ACTIVE detail, stores immutable change rows and NOTHING mutates until the campus
// Staff Leader approves. Active snapshot stays authoritative throughout.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Proposed new content for one instance (full snapshot of the amendable subset).</summary>
public sealed record VisitAmendmentProposalDto(
    // concurrency + base state the requester saw
    int ExpectedInstanceRowVersion,
    uint BaseFormRevision,
    uint BaseApprovalRevision,
    string? Reason,

    // approval-sensitive content
    string DelegationName,
    string VisitType,
    string? VisitTypeOther,
    string Purpose,
    string? WorkingContent,
    string WorkingLanguage,
    ContactPointDto OperationalContact,
    IList<VisitorDto> Visitors,
    IList<SupportTeamMemberDto> ExternalSupportMembers,

    // structural (decided-campus schedule change rides the amendment, plan §16.6)
    DateTime PlannedStartAt,
    DateTime PlannedEndAt,

    /// <summary>
    /// Durable "who in the delegation the operational contact IS" reference (NP-03), mirroring
    /// <c>CampusVisitFormDto.OperationalContactClientMemberKey</c> used by create/edit. Names one of
    /// <see cref="Visitors"/>/<see cref="ExternalSupportMembers"/>' own <c>ClientMemberKey</c> values —
    /// null/absent means "outside the delegation". Never a fallback string-match target.
    ///
    /// <para>
    /// EPHEMERAL: only meaningful in the SAME proposal as a genuine member-list change, since a
    /// <c>ClientMemberKey</c> is minted fresh by the submitting form and resolves only against the
    /// rows THIS proposal is about to insert. When <see cref="Visitors"/>/<see
    /// cref="ExternalSupportMembers"/> are unchanged, use <see cref="OperationalContactGuestMemberId"/>
    /// instead — see its own doc comment for why the two cannot substitute for each other.
    /// </para>
    /// </summary>
    string? OperationalContactClientMemberKey = null,

    /// <summary>
    /// PERSISTENT "who in the delegation the operational contact IS" reference (plan CanhIter3FixBug
    /// "Đầu mối hiện tại có nằm trong danh sách đoàn không?"). Names an EXISTING member of this
    /// instance by its stable <c>GuestMemberId</c> — null means "outside the delegation".
    ///
    /// <para>
    /// This is the field that makes "same members, different relationship pick" a real, submittable
    /// amendment. Before it existed, the ONLY way to record who the contact is were the member-list
    /// change rows themselves (via <see cref="OperationalContactClientMemberKey"/>), which are written
    /// ONLY when the member list actually changed — so a user who left the delegation exactly as it
    /// was and only changed the relationship pick produced zero change rows at all, and the whole
    /// amendment was refused as "no changes" even though a real business fact had changed.
    /// </para>
    /// <para>
    /// The backend uses this field's value ONLY when the member lists are unchanged; when they DID
    /// change, <see cref="OperationalContactClientMemberKey"/> is authoritative instead, because a
    /// replaced row has no persistent id yet to be named by. The two are never both consulted for the
    /// same amendment — see <c>VisitAmendmentService.BuildChangeRows</c>.
    /// </para>
    /// </summary>
    ulong? OperationalContactGuestMemberId = null);

public sealed record VisitAmendmentChangeDto(
    string FieldPath,
    string ChangeClass,
    string? OldValueJson,
    string? NewValueJson);

public sealed record VisitAmendmentDto(
    ulong AmendmentId,
    ulong VisitRequestId,
    ulong VisitInstanceId,
    uint AmendmentNo,
    string Status,
    uint BaseFormRevision,
    uint BaseApprovalRevision,
    ulong RequestedBy,
    string? RequestedByName,
    DateTime RequestedAt,
    string? Reason,
    ulong? DecidedBy,
    string? DecidedByName,
    DateTime? DecidedAt,
    string? DecisionNote,
    DateTime? ExpiresAt,
    IReadOnlyList<VisitAmendmentChangeDto> Changes);

public sealed record VisitAmendmentDecisionResponse(
    ulong AmendmentId,
    ulong VisitInstanceId,
    string Status,
    uint? NewFormRevision,
    uint? NewApprovalRevision,
    string Message);
