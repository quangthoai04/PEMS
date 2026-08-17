namespace PEMS.Application.Common.DTOs;

// ──────────────────────────────────────────────────────────────────────────────
// Per-campus form v2 SAFE-EDIT payload (plan §16.6, Phase E). A SPARSE patch: the client sends only
// what it actually changed, and the backend diffs against the active data, applies ONLY genuine
// changes and rejects anything outside the SAFE allowlist (fail closed; approval-sensitive and
// structural changes go through amendments instead).
//
// It used to be a FULL snapshot of every safe field of every campus. That made a one-word note
// correction re-send the media-consent decision and the notes of every other campus in the request —
// so a campus whose window had closed was dragged into the payload and refused the whole edit, and a
// value that had changed server-side since the form loaded was silently overwritten with a stale one.
//
// NULL now means "not part of this edit". To CLEAR a nullable field, send an empty string.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>The only request-level safe subset left: the registrant's display snapshot (no email).</summary>
/// <param name="Nationality">
/// Required (no default) so every existing 4-arg caller fails to compile rather than silently sending
/// an empty nationality — the field is mandatory on the request, never optional here.
/// </param>
/// <param name="PartnerId">
/// The partner profile <see cref="Organization"/> was picked from, or null for free text. Travels
/// ATOMICALLY with Organization: the service resolves the canonical name server-side from this id
/// rather than trusting the client-supplied text next to it.
/// </param>
public sealed record SafeRegistrantPatchDto(
    string FullName,
    string? Organization,
    string? JobTitle,
    string? Phone,
    string Nationality,
    ulong? PartnerId = null);

/// <summary>
/// The display half of ONE campus's operational-contact snapshot.
/// </summary>
/// <remarks>
/// RETIRED (plan PEMS_CONTACT_ONE_DOOR): the contact profile has exactly one door now — "Manage the
/// contact role" — so <see cref="SafeInstancePatchDto.OperationalContact"/> is never populated by the
/// current client any more. The record stays only so an old client's payload still DESERIALIZES;
/// <c>VisitSafeEditService</c> refuses the request outright (fail closed) the moment this is non-null,
/// rather than silently applying or silently dropping it.
/// </remarks>
public sealed record SafeContactPatchDto(
    string FullName,
    string? Organization,
    string? JobTitle,
    string Phone);

/// <summary>
/// Per-instance safe subset, sparse: null = this field is not part of the edit, "" = clear it.
/// A campus that changed nothing must not appear in <see cref="VisitRequestSafeEditDto.Instances"/> at all.
/// </summary>
/// <param name="OperationalContact">RETIRED — see <see cref="SafeContactPatchDto"/>. Must be null; a non-null value is refused.</param>
public sealed record SafeInstancePatchDto(
    ulong VisitInstanceId,
    int ExpectedRowVersion,
    SafeContactPatchDto? OperationalContact,
    string? TransportationNote,
    string? MediaConsentStatus,   // AGREED | DECLINED | null = unchanged
    string? Notes);               // "Ghi chú gửi FPTU" | null = unchanged

public sealed record VisitRequestSafeEditDto(
    int ExpectedRequestRowVersion,
    SafeRegistrantPatchDto? Registrant,   // null = request-level registrant fields untouched
    System.Collections.Generic.IList<SafeInstancePatchDto>? Instances); // null/empty = no instance-level safe edits

/// <summary>One applied field-level change (stable path; values masked where policy requires).</summary>
public sealed record SafeEditAppliedChange(string FieldPath, ulong? VisitInstanceId, string ChangeClass);

public sealed record VisitRequestSafeEditResponse(
    ulong VisitRequestId,
    System.Collections.Generic.IReadOnlyList<SafeEditAppliedChange> AppliedChanges,
    int RequestRowVersion,
    System.Collections.Generic.IReadOnlyDictionary<ulong, int> InstanceRowVersions,
    string Message);
