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
    string? Phone,
    string Email);

/// <summary>
/// Per-campus reception-host ARRANGEMENT for the authenticated create/edit — "Phương án người phụ
/// trách tiếp đón". It records an intention, not a decision: nothing here ever names a current host.
///
/// <para>
/// <see cref="Mode"/> is SELF / SELECTED / WAIT_FOR_LATER (<see cref="PEMS.Shared.HostSelectionModes"/>);
/// null or absent means WAIT_FOR_LATER, which is also the only value an external/Visitor submit can
/// produce — the backend forces it regardless of what the payload says. <see cref="ProposedHostUserId"/>
/// is required for SELECTED, must equal the caller for SELF (and is resolved server-side when omitted),
/// and must be absent for WAIT_FOR_LATER.
/// </para>
///
/// <para>
/// <see cref="ConfirmedHostConflict"/> acknowledges the non-blocking schedule-overlap warning for this
/// campus, with the semantics it has always had: a warning, not a blocker.
/// </para>
/// </summary>
public record CampusHostSelectionV2Dto(
    string? Mode,
    ulong? ProposedHostUserId,
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
    string? Notes,                   // "Ghi chú gửi FPTU" — one general remark, independent of consent

    // ── Reception-host arrangement (authenticated create only; forced to WAIT_FOR_LATER otherwise) ──
    CampusHostSelectionV2Dto? HostSelection,

    /// <summary>
    /// WHICH member of this campus the operational contact is — the <c>ClientMemberKey</c> of one row
    /// of <see cref="Visitors"/> or <see cref="ExternalSupportMembers"/> (NP-03).
    ///
    /// <para>
    /// Optional, and null is a normal answer — plenty of campuses are coordinated by somebody who is
    /// not travelling with the delegation. When it IS supplied it is the only evidence used: a
    /// name/role/organisation match is a guess, and this is an answer.
    /// </para>
    /// <para>
    /// A key rather than an array index. An index names a POSITION, and a position stops meaning the
    /// same person the moment a row is inserted above it, removed, or reordered — the previous version
    /// of this field re-aimed the contact at a stranger on any of those, silently. A key rather than
    /// an id because these members have no id until the insert; the service resolves the two together
    /// inside one transaction.
    /// </para>
    /// <para>
    /// Support staff are eligible on purpose: an interpreter or a coordinator travelling with the
    /// delegation is frequently the person a campus actually rings. What is NOT eligible is anybody on
    /// FPTU's side — hosts and internal participants live in different tables entirely and cannot be
    /// named here at all.
    /// </para>
    /// </summary>
    string? OperationalContactClientMemberKey = null);

/// <summary>
/// The complete per-campus create payload. <c>visitScope</c> and <c>hasMixedCampusDetails</c> are NOT
/// accepted from the client — the backend derives them from the campus set.
///
/// <para>
/// There is no request-level contact. Every campus names its OWN operational contact inside
/// <see cref="CampusVisitFormDto.OperationalContact"/>, and each of those addresses confirms for that
/// campus alone. The old single <c>primaryContact</c> is what let one confirmation grant authority
/// over campuses its owner was never invited to.
/// </para>
/// </summary>
public record VisitRequestFormDataV2(
    string SubmissionId,             // idempotency key; a retry with the same id returns the same request
    RegistrantInputV2 Registrant,
    ulong? PartnerId,
    IList<CampusVisitFormDto> CampusVisits);
