namespace PEMS.Application.Common.DTOs;

// ──────────────────────────────────────────────────────────────────────────────
// Nested input records for the visit-request form.
// SQL v8.3 has no pending_visit_requests table: the draft is held by the frontend
// (sessionStorage) and resubmitted at the verify step; only the OTP lives server-side.
//
// The request-level V1 shapes that used to live here (VisitRequestFormData, VisitSlotDto,
// IVisitRequestFormCommand and its validator) are gone. Every submit is per-campus now:
// see VisitFormV2Dtos.cs. They were unreachable from any endpoint and referenced only by
// tests of themselves, and their `ContactPerson` was the single request-level contact whose
// removal this cutover is about.
// ──────────────────────────────────────────────────────────────────────────────

// Guest member fields mirror visit_guest_members in pems_full(3).sql — there is no
// passport/identity column, so none is collected or sent.
public record VisitorDto(
    string FullName,
    string Nationality,
    string JobTitle,
    string Organization);

public record SupportTeamMemberDto(
    string FullName,
    string JobTitle,
    string Organization,
    string Nationality);

/// <summary>
/// One person's contact details as typed on the form — a SNAPSHOT, never a login. Identity at
/// runtime is decided by <c>visit_request_campuses.operational_contact_user_id</c>, never by a name,
/// phone or email stored here.
/// </summary>
/// <remarks>
/// Job title used to be an optional trailing parameter, which meant the create form never asked for
/// one and every detail screen rendered a labelled blank — the field was declared, stored, read and
/// displayed, and only never filled. It is now required and in reading order, so a call site that
/// omits it does not compile.
///
/// The phone stays OPTIONAL: a contact who gives an email and no number is still a usable contact,
/// and the column behind it is nullable to match.
/// </remarks>
public record ContactPointDto(
    string FullName,
    string Organization,
    string JobTitle,
    string? Phone,
    string Email);
