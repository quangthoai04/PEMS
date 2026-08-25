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
/// <param name="Organization">
/// Display snapshot of the organization, as typed or as picked. Kept verbatim so an old request
/// still reads the way it was filed even after the partner is renamed.
/// </param>
/// <param name="OrganizationPartnerId">
/// The partner profile the user PICKED from the dropdown, or null for free text. Defaulted so every
/// existing positional construction (tests, mappers, revision snapshots) stays valid; the backend
/// never trusts <paramref name="Organization"/> to identify a partner — only this id (PART-01).
/// </param>
/// <param name="ClientMemberKey">
/// A client-minted identity for THIS row, stable for as long as the form is open, so the payload can
/// point at a person who does not exist in the database yet.
///
/// <para>
/// Rows are created and destroyed on screen before anything is saved, which leaves the submit with a
/// list of people and no ids. The form used to name the operational contact by ARRAY POSITION, and a
/// position is not an identity: inserting a row above the chosen one, deleting one, or sorting the
/// list re-aimed the contact at somebody else without a single visible change. This key is minted
/// once per row (<c>crypto.randomUUID()</c>) and never regenerated, so it survives all three.
/// </para>
/// <para>
/// It is a CORRELATION token for one request, never stored and never authorization: the backend maps
/// it to the <c>guest_member_id</c> it has just inserted, inside the same transaction, and forgets it.
/// Null is normal — a caller that names no contact member (or an older client) simply sends none.
/// </para>
/// </param>
/// <param name="GuestMemberId">
/// The row's OWN persisted id, as read back from the server and echoed on an edit/resubmit/amendment
/// payload for a row that already exists — never for a genuinely new row, and never trusted as a
/// database lookup key on Create (plan CanhIter3FixBug, operational-contact consistency fix). This is
/// what makes copy-on-write member continuity provable: <c>ClientMemberKey</c> alone is re-minted every
/// time a form opens and proves nothing about which persisted row a payload is really describing, but
/// this id is real and stable. Used ONLY as continuity EVIDENCE (does the currently-linked Operational
/// Contact's persisted member still appear in this payload, under the same key) — never as a lookup
/// target, and never on Create, where every row is new and this must be null.
/// </param>
public record VisitorDto(
    string FullName,
    string Nationality,
    string JobTitle,
    string Organization,
    ulong? OrganizationPartnerId = null,
    string? ClientMemberKey = null,
    ulong? GuestMemberId = null);

/// <inheritdoc cref="VisitorDto"/>
public record SupportTeamMemberDto(
    string FullName,
    string JobTitle,
    string Organization,
    string Nationality,
    ulong? OrganizationPartnerId = null,
    string? ClientMemberKey = null,
    ulong? GuestMemberId = null);

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
