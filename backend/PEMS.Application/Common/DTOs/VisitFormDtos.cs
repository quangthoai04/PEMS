namespace PEMS.Application.Common.DTOs;

// ──────────────────────────────────────────────────────────────────────────────
// Nested input records — used both as command properties and serialised into
// PendingVisitRequest.FormDataJson for reconstruction after OTP verification.
// ──────────────────────────────────────────────────────────────────────────────

public record VisitSlotDto(
    string CampusId,
    DateTime StartDatetime,
    DateTime EndDatetime);

public record VisitorDto(
    string FullName,
    string PassportId,
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
/// The complete, deserialised form payload stored inside
/// <c>PendingVisitRequest.FormDataJson</c>.
/// </summary>
public record PendingVisitRequestFormData(
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
