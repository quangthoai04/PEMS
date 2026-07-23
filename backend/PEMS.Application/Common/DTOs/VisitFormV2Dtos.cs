namespace PEMS.Application.Common.DTOs;

// ──────────────────────────────────────────────────────────────────────────────
// Per-campus visit form v2 create payload (plan §5). The frontend always sends a
// FULLY RESOLVED snapshot for every campus — "same for all" is a one-time UI copy,
// never a backend inheritance. The backend NEVER trusts client-sent visitScope,
// hasMixedCampusDetails, status/revision, coordinator/approval
// state or visitorUserId: those are all derived server-side. There is no schema
// discriminator in the payload — every create is per-campus.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Request-level registrant snapshot (the submitter).</summary>
public record RegistrantInputV2(
    string FullName,
    string Nationality,
    string Organization,
    string JobTitle,
    string Phone,
    string Email);

/// <summary>
/// Per-campus processing intent for the AUTHENTICATED create (SEND_FOR_REVIEW / SELF_HOST / ASSIGN_HOST).
/// Null / SEND_FOR_REVIEW means "route to this campus's Staff Leader" — the only value a public submit
/// may produce. <see cref="ConfirmedHostConflict"/> is the user's explicit acknowledgement of the
/// non-blocking host schedule overlap warning for THIS campus (mirrors the v1 create contract); without it
/// an overlapping host assignment is rejected with HOST_SCHEDULE_CONFLICT_CONFIRMATION_REQUIRED.
/// </summary>
public record CampusProcessingV2Dto(
    string Mode,
    ulong? HostUserId,
    bool ConfirmedHostConflict = false);

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
/// The complete per-campus create payload. <c>visitScope</c> and <c>hasMixedCampusDetails</c> are NOT
/// accepted from the client — the backend derives them from the campus set.
/// </summary>
public record VisitRequestFormDataV2(
    string SubmissionId,             // idempotency key; a retry with the same id returns the same request
    RegistrantInputV2 Registrant,
    ContactPointDto PrimaryContact,  // request-level primary contact / request manager (a VISITOR account)
    ulong? PartnerId,
    IList<CampusVisitFormDto> CampusVisits);
