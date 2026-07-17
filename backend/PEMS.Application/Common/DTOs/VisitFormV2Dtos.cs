namespace PEMS.Application.Common.DTOs;

// ──────────────────────────────────────────────────────────────────────────────
// Per-campus visit form v2 create payload (plan §5). The frontend always sends a
// FULLY RESOLVED snapshot for every campus — "same for all" is a one-time UI copy,
// never a backend inheritance. The backend NEVER trusts client-sent visitScope,
// hasMixedCampusDetails, formSchemaVersion, status/revision, coordinator/approval
// state or visitorUserId: those are all derived server-side.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Request-level registrant snapshot (the submitter).</summary>
public record RegistrantInputV2(
    string FullName,
    string Nationality,
    string Organization,
    string JobTitle,
    string Phone,
    string Email);

/// <summary>Per-campus processing intent for authenticated create (SEND_FOR_REVIEW / SELF_HOST / ASSIGN_HOST).</summary>
public record CampusProcessingV2Dto(
    string Mode,
    ulong? HostUserId);

/// <summary>The complete, independent form snapshot for ONE campus instance.</summary>
public record CampusVisitFormDto(
    // ── Schedule ──
    string CampusId,                 // campus CODE (e.g. "HN"); resolved to campus_id on create
    DateTime PlannedStartAt,
    DateTime PlannedEndAt,

    // ── Visit content (per-campus) ──
    string DelegationName,
    string VisitType,
    string? VisitTypeOther,
    string Purpose,
    string? WorkingContent,

    // ── People (per-campus, independent) ──
    IList<VisitorDto> Visitors,
    IList<SupportTeamMemberDto> ExternalSupportMembers,

    // ── Operational (working) contact — a per-campus snapshot, never a login ──
    ContactPointDto OperationalContact,

    // ── Additional requirements (per-campus) ──
    string WorkingLanguage,          // EN | VI
    string? TransportationNote,
    string MediaConsentStatus,       // AGREED | DECLINED
    string? MediaConsentNote,
    string? Notes,

    // ── Internal processing (authenticated create only; null for public visitor submit) ──
    CampusProcessingV2Dto? Processing);

/// <summary>
/// The complete per-campus v2 create payload. <c>visitScope</c> / <c>hasMixedCampusDetails</c> /
/// <c>formSchemaVersion</c> are NOT accepted from the client — the backend derives them.
/// </summary>
public record VisitRequestFormDataV2(
    string SubmissionId,             // idempotency key; a retry with the same id returns the same request
    RegistrantInputV2 Registrant,
    ContactPointDto PrimaryContact,  // request-level primary contact / request manager (a VISITOR account)
    ulong? PartnerId,
    IList<CampusVisitFormDto> CampusVisits);
