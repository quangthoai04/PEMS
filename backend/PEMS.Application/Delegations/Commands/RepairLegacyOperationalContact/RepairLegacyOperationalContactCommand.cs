using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Commands.RepairLegacyOperationalContact;

/// <summary>
/// Forensic repair for campuses the pre-fix destructive REPLACE bug corrupted: a confirmed operational
/// contact A cleared outright (rather than handed over) whenever the registrant changed the address on a
/// campus that already had one, before that bug was fixed. ADMIN-only, same console as
/// <c>BackfillVisitHistoryCommand</c> — but deliberately a SEPARATE command: that one repairs audit
/// METADATA, this one repairs live business state (<c>operational_contact_user_id</c>, the campus
/// snapshot, the campus status) that the application actually reads and acts on, which is a materially
/// different risk to review.
///
/// <para>
/// <see cref="Mode"/> defaults to a read-only DRY RUN. Nothing is ever applied unless it is the exact
/// literal <c>"APPLY"</c> — never inferred from an omitted parameter, a bare boolean, or any other value.
/// A dry run runs the identical detection logic an apply would and reports identical candidate counts,
/// without writing anything (structurally: it never opens a write transaction).
/// </para>
/// <para>
/// Every SAFE_AUTO_REPAIR candidate is applied in its OWN transaction — lock the target
/// VisitRequest/VisitRequestCampus, reload tracked, re-check every safety predicate against the LIVE
/// row, repair, recompute the aggregate through <c>IVisitRequestAggregateStatusService</c>, commit — so
/// one candidate's repair can never block or be rolled back by another's, and a row whose state changed
/// between the scan and the write is skipped rather than clobbered.
/// </para>
/// </summary>
public sealed record RepairLegacyOperationalContactCommand(string? Mode)
    : IRequest<RepairLegacyOperationalContactResponse>;

/// <summary>One corrupting-REPLACE audit's classification and (if repaired) outcome.</summary>
public sealed class LegacyContactRepairCandidateDto
{
    public ulong VisitRequestId { get; init; }
    public ulong VisitInstanceId { get; init; }
    public ulong? CampusId { get; init; }
    public ulong CorruptingAuditLogId { get; init; }
    public ulong OldContactUserId { get; init; }
    /// <summary>SAFE_AUTO_REPAIR or MANUAL_REVIEW — NOT_CORRUPTED rows are not listed individually.</summary>
    public string Classification { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    /// <summary>True only when this row was actually written in this run (always false in a dry run).</summary>
    public bool Repaired { get; init; }
}

public sealed class RepairLegacyOperationalContactResponse
{
    /// <summary>True only when <c>Mode == "APPLY"</c> was honored; false for every other value.</summary>
    public bool Applied { get; init; }

    /// <summary>Every AuditLog row matching the OPERATIONAL_CONTACT_REPLACED action — the full universe
    /// examined, most of which are ordinary no-holder replaces or self-matches, not corruption.</summary>
    public int Scanned { get; init; }

    /// <summary>The subset carrying the exact corruption fingerprint (operational_contact_user_id:
    /// non-null → null). Candidates = SafeAutoRepair + ManualReview + NotCorrupted, always.</summary>
    public int Candidates { get; init; }

    public int SafeAutoRepair { get; init; }
    public int ManualReview { get; init; }
    public int NotCorrupted { get; init; }

    /// <summary>Candidates that could not even be evaluated (malformed/unreadable evidence). Counted
    /// separately from ManualReview, which means "evaluated, and the evidence itself says stop".</summary>
    public int Errors { get; init; }

    /// <summary>Rows actually written in this run. Always 0 when <see cref="Applied"/> is false.</summary>
    public int Repaired { get; init; }

    public List<LegacyContactRepairCandidateDto> SafeAutoRepairCandidates { get; init; } = new();
    public List<LegacyContactRepairCandidateDto> ManualReviewCandidates { get; init; } = new();

    public string Message { get; init; } = string.Empty;
}
