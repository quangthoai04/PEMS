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
    /// </summary>
    string? OperationalContactClientMemberKey = null);

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
