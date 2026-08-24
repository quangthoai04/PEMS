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
/// Tri-state relation patch (plan CanhIter3FixBug §8.1) — distinguishes "not part of this edit" from
/// "explicitly clear it" from "explicitly link this member", which a single nullable <c>ulong?</c>
/// cannot: <c>MemberLink == null</c> means the relation is untouched by this edit;
/// <c>MemberLink is not null &amp;&amp; GuestMemberId == null</c> means explicit unlink;
/// <c>MemberLink.GuestMemberId == X</c> means explicit link to member X.
/// </summary>
public sealed record SafeContactMemberLinkPatchDto(ulong? GuestMemberId);

/// <summary>
/// ONE campus's same-person operational-contact correction — metadata plus, optionally, which
/// delegation member the contact IS (plan CanhIter3FixBug). The address travels here too, but only so
/// the backend can PROVE it is unchanged (<c>VisitSafeEditService</c> rejects outright if it differs);
/// this is never how the address itself is changed — that is Replace/Transfer's job.
/// </summary>
/// <param name="MemberLink">Tri-state — see <see cref="SafeContactMemberLinkPatchDto"/>. Null = relation not part of this edit.</param>
public sealed record SafeContactPatchDto(
    string FullName,
    string? Organization,
    string JobTitle,
    string? Phone,
    string Email,
    SafeContactMemberLinkPatchDto? MemberLink);

/// <summary>
/// Per-instance safe subset, sparse: null = this field is not part of the edit, "" = clear it.
/// A campus that changed nothing must not appear in <see cref="VisitRequestSafeEditDto.Instances"/> at all.
/// </summary>
/// <param name="OperationalContact">This campus's same-person contact correction, or null if this edit does not touch it.</param>
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
