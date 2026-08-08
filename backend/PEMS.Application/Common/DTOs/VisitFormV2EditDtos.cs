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
    string? Notes)
{
    /// <summary>Projects the campus content into the shared create DTO shape so create/edit reuse ONE
    /// canonical recompute (<c>VisitRequestV2Canonical</c>). The host arrangement is not campus CONTENT, so it is not part of this projection — it is edited through its own endpoint.</summary>
    public CampusVisitFormDto ToFormDto() => new(
        CampusId, PlannedStartAt, PlannedEndAt, DelegationName, VisitType, VisitTypeOther, Purpose, WorkingContent,
        Visitors, ExternalSupportMembers, OperationalContact, WorkingLanguage, TransportationNote,
        MediaConsentStatus, Notes, HostSelection: null);
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

/// <summary>
/// The Host a "Lưu và duyệt" names, in the same payload as the edit. Approving assigns a Host or it does
/// not happen at all, so the two travel together rather than as a save followed by a hopeful approve.
/// </summary>
public sealed record ApproveAfterSaveDto(ulong HostUserId, string? DecisionNote);

/// <summary>
/// A per-campus pending edit: the campus's full content snapshot, plus the two transient decisions the
/// actor may be taking alongside it.
/// </summary>
/// <param name="OverrideLeadTimeConfirmed">
/// The campus Staff Leader's explicit "yes, this schedule, with less than 72 hours' notice". It grants
/// nothing on its own — the backend honours it only for the leader of THIS campus, so anyone else
/// sending true is refused exactly as if they had not.
/// </param>
public sealed record VisitInstancePendingEditDto(
    CampusVisitEditV2Dto Content,
    bool OverrideLeadTimeConfirmed = false,
    ApproveAfterSaveDto? ApproveAfterSave = null);
