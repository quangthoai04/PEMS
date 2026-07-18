namespace PEMS.Application.Common.DTOs;

// ──────────────────────────────────────────────────────────────────────────────
// Per-campus form v2 SAFE-EDIT payload (plan §16.6, Phase E). The client sends the FULL current value
// of every safe field it wants managed (same convention as the edit flows) — the backend diffs against
// the active data, applies ONLY genuine changes and rejects any change that falls outside the SAFE
// allowlist (fail closed; approval-sensitive/structural changes go through amendments instead).
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

/// <summary>Per-instance safe subset. Every field is the FULL new value (null = clear for nullables).</summary>
public sealed record SafeInstancePatchDto(
    ulong VisitInstanceId,
    int ExpectedRowVersion,
    string? TransportationNote,
    string? NoteToFptu,
    string MediaConsentStatus,   // AGREED | DECLINED
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
