namespace PEMS.Application.Common.DTOs;

// ──────────────────────────────────────────────────────────────────────────────
// Nested input records for the UC-17 visit-request form.
// SQL v8.3 has no pending_visit_requests table: the draft is held by the frontend
// (sessionStorage) and resubmitted at the verify step; only the OTP lives server-side.
// ──────────────────────────────────────────────────────────────────────────────

public record VisitSlotDto(
    string CampusId,   // campus CODE (e.g. "HN", "HCM"); resolved to campus_id on create
    DateTime StartDatetime,
    DateTime EndDatetime);

// Guest member fields mirror visit_guest_members in pems_full(3).sql — there is no
// passport/identity column, so none is collected or sent.
public record VisitorDto(
    string FullName,
    string Email,
    string Nationality,
    string? JobTitle,
    string? Organization);

public record SupportTeamMemberDto(
    string FullName,
    string JobTitle,
    string Organization,
    string Nationality);

public record ContactPointDto(
    string FullName,
    string Organization,
    string Phone,
    string Email);

/// <summary>
/// The complete visit-request form payload. Carried by the frontend between the
/// initiate (OTP) and verify (create) steps — never persisted before verification.
/// </summary>
public record VisitRequestFormData(
    // ── Registration info ────────────────────────────────
    string RegisterFullName,
    string RegisterNationality,
    string RegisterOrganization,
    string RegisterJobTitle,
    string RegisterPhone,
    string RegisterEmail,

    // ── Visit info ───────────────────────────────────────
    string DelegationName,
    string VisitScope,               // SINGLE_CAMPUS | MULTI_CAMPUS
    IList<VisitSlotDto> VisitSlots,
    string Purpose,
    string? WorkingContent,

    // ── Attendees ────────────────────────────────────────
    IList<VisitorDto> Visitors,
    IList<SupportTeamMemberDto> SupportTeam,

    // ── Contact ──────────────────────────────────────────
    ContactPointDto ContactPoint,
    bool IsContactSelf,              // true → contact email = register email

    // ── Additional ───────────────────────────────────────
    string Language,                 // EN | VI
    string? Vehicle,
    string? Notes);
