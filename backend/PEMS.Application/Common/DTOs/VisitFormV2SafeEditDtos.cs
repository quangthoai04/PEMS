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

/// <summary>Request-level safe subset: registrant display snapshot (no email) + contact snapshot (no email).</summary>
public sealed record SafeRegistrantPatchDto(
    string FullName,
    string? Organization,
    string? JobTitle,
    string? Phone);

public sealed record SafeContactPatchDto(
    string FullName,
    string? Organization,
    string Phone);

/// <summary>
/// Per-instance safe subset, sparse: null = this field is not part of the edit, "" = clear it.
/// A campus that changed nothing must not appear in <see cref="VisitRequestSafeEditDto.Instances"/> at all.
/// </summary>
public sealed record SafeInstancePatchDto(
    ulong VisitInstanceId,
    int ExpectedRowVersion,
    string? TransportationNote,
    string? NoteToFptu,
    string? MediaConsentStatus,   // AGREED | DECLINED | null = unchanged
    string? MediaConsentNote);

public sealed record VisitRequestSafeEditDto(
    int ExpectedRequestRowVersion,
    SafeRegistrantPatchDto? Registrant,   // null = request-level registrant fields untouched
    SafeContactPatchDto? Contact,         // null = contact snapshot untouched
    System.Collections.Generic.IList<SafeInstancePatchDto>? Instances); // null/empty = no instance-level safe edits

/// <summary>One applied field-level change (stable path; values masked where policy requires).</summary>
public sealed record SafeEditAppliedChange(string FieldPath, ulong? VisitInstanceId, string ChangeClass);

public sealed record VisitRequestSafeEditResponse(
    ulong VisitRequestId,
    System.Collections.Generic.IReadOnlyList<SafeEditAppliedChange> AppliedChanges,
    int RequestRowVersion,
    System.Collections.Generic.IReadOnlyDictionary<ulong, int> InstanceRowVersions,
    string Message);
