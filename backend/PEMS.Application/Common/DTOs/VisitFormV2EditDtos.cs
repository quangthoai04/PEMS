namespace PEMS.Application.Common.DTOs;

// ──────────────────────────────────────────────────────────────────────────────
// Per-campus visit form v2 EDIT payloads (plan §6.4). Like create, the frontend sends a FULLY RESOLVED
// snapshot for every campus. Optimistic concurrency is explicit: the request carries its expected row_version
// and every EXISTING campus carries its stable visitInstanceId + expected row_version. The backend still
// derives visitScope / hasMixedCampusDetails / fingerprint / projection — never the client.
// Account-binding emails (registrant email, primary-contact email) are IMMUTABLE here; changing the primary
// contact identity is the Phase D identity workflow, not a form edit.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One campus in a v2 edit payload. An EXISTING instance carries its stable <see cref="VisitInstanceId"/> and
/// <see cref="ExpectedRowVersion"/>; a NEW campus to add (pending-edit only) has <see cref="VisitInstanceId"/>
/// = null and no expected version.
/// </summary>
public record CampusVisitEditV2Dto(
    ulong? VisitInstanceId,
    int? ExpectedRowVersion,

    // ── Schedule ──
    string CampusId,                 // campus CODE (e.g. "HN")
    System.DateTime PlannedStartAt,
    System.DateTime PlannedEndAt,

    // ── Visit content (per-campus) ──
    string DelegationName,
    string VisitType,
    string? VisitTypeOther,
    string Purpose,
    string? WorkingContent,

    // ── People (per-campus, independent) ──
    System.Collections.Generic.IList<VisitorDto> Visitors,
    System.Collections.Generic.IList<SupportTeamMemberDto> ExternalSupportMembers,

    // ── Operational (working) contact — a per-campus snapshot, never a login ──
    ContactPointDto OperationalContact,

    // ── Additional requirements (per-campus) ──
    string WorkingLanguage,
    string? TransportationNote,
    string MediaConsentStatus,
    string? MediaConsentNote)
{
    /// <summary>Projects the campus content into the shared create DTO shape so create/edit reuse ONE
    /// canonical recompute (<c>VisitRequestV2Canonical</c>). The host arrangement is not campus CONTENT, so it is not part of this projection — it is edited through its own endpoint.</summary>
    public CampusVisitFormDto ToFormDto() => new(
        CampusId, PlannedStartAt, PlannedEndAt, DelegationName, VisitType, VisitTypeOther, Purpose, WorkingContent,
        Visitors, ExternalSupportMembers, OperationalContact, WorkingLanguage, TransportationNote,
        MediaConsentStatus, MediaConsentNote, HostSelection: null);
}

/// <summary>
/// The complete per-campus v2 edit payload (pending-edit and resubmit share it). Like create it has no
/// request-level contact: each campus carries its own operational contact, and changing one is a
/// per-campus act (replace before the decision, transfer after it) rather than a form field.
/// </summary>
public record VisitRequestEditV2Dto(
    int ExpectedRequestRowVersion,
    RegistrantInputV2 Registrant,
    ulong? PartnerId,
    System.Collections.Generic.IList<CampusVisitEditV2Dto> CampusVisits);
