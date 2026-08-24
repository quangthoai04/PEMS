namespace PEMS.Application.Common.DTOs;

// ──────────────────────────────────────────────────────────────────────────────
// Per-campus visit form v2 EDIT payloads (plan §6.4). Like create, the frontend sends a FULLY RESOLVED
// snapshot for every campus. Optimistic concurrency is explicit: the request carries its expected row_version
// and every EXISTING campus carries its stable visitInstanceId + expected row_version. The backend still
// derives visitScope / hasMixedCampusDetails / fingerprint / projection — never the client.
// Account-binding emails (registrant email, primary-contact email) are IMMUTABLE here; changing the primary
// contact identity is the Phase D identity workflow, not a form edit.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One campus in a v2 edit payload. An EXISTING instance carries its stable <see cref="VisitInstanceId"/> and
/// <see cref="ExpectedRowVersion"/>; a NEW campus to add (pending-edit only) has <see cref="VisitInstanceId"/>
/// = null and no expected version.
/// </summary>
public record CampusVisitEditV2Dto(
    ulong? VisitInstanceId,
    int? ExpectedRowVersion,

    // ── Schedule ──
    string CampusId,                 // campus CODE (e.g. "HN")
    System.DateTime PlannedStartAt,
    System.DateTime PlannedEndAt,

    // ── Visit content (per-campus) ──
    string DelegationName,
    string VisitType,
    string? VisitTypeOther,
    string Purpose,
    string? WorkingContent,

    // ── People (per-campus, independent) ──
    System.Collections.Generic.IList<VisitorDto> Visitors,
    System.Collections.Generic.IList<SupportTeamMemberDto> ExternalSupportMembers,

    // ── Operational (working) contact — a per-campus snapshot, never a login ──
    ContactPointDto OperationalContact,

    // ── Additional requirements (per-campus) ──
    string WorkingLanguage,
    string? TransportationNote,
    string MediaConsentStatus,
    string? Notes,

    /// <inheritdoc cref="CampusVisitFormDto.OperationalContactClientMemberKey"/>
    /// <remarks>
    /// On an EXISTING campus the five contact snapshot columns are immutable through this path, but
    /// WHICH member they describe still has to travel: the edit replaces every member row (copy-on-
    /// write), so the id the contact held names a deleted row by the time the links are rebuilt. The
    /// key is what re-finds the same person among the new rows — without it the link falls back to
    /// comparing strings, which is the failure this whole change removes.
    /// </remarks>
    string? OperationalContactClientMemberKey = null,

    /// <summary>
    /// The PERSISTENT counterpart to <see cref="OperationalContactClientMemberKey"/> — plan
    /// CanhIter3FixBug "Đầu mối hiện tại có nằm trong danh sách đoàn không?" (pending-edit
    /// lifecycle stage). Every row on an EXISTING campus already has a stable <c>GuestMemberId</c>
    /// before this edit ever touches it, so when the member list itself is NOT being replaced this
    /// is the only reliable way to say "the contact is now this row" — the client key is re-minted
    /// on every page load and carries no meaning the backend can verify on its own.
    ///
    /// <para>
    /// Deliberately NOT part of <see cref="ToFormDto"/>'s projection and therefore NOT part of
    /// <c>VisitRequestV2Canonical.CanonicalContent</c> — the relationship is a fact about the
    /// campus, not "content" in the copy-on-write sense, and change detection for it is computed
    /// independently in <c>VisitRequestV2EditService</c> (relationChanged), never folded into
    /// contentChanged/scheduleChanged.
    /// </para>
    /// <para>
    /// Mutually exclusive with <see cref="OperationalContactClientMemberKey"/> in effect, never in
    /// shape: both may be present on the wire (the frontend always resolves the current pick's own
    /// id when it has one), but the service reads only ONE of them per save — the client key when
    /// the member list changed (copy-on-write mints new ids, so only the key survives), this one
    /// when it did not (every id is already real and directly checkable against THIS instance's own
    /// members via <c>OperationalContactLink.EnsureGuestMemberIdEligible</c>). Null means either "no
    /// pick" (outside the delegation) or "member list changed, use the key instead" — the service
    /// tells the two apart from its OWN contentChanged, never by inspecting which field is null.
    /// </para>
    /// </summary>
    ulong? OperationalContactGuestMemberId = null)
{
    /// <summary>Projects the campus content into the shared create DTO shape so create/edit reuse ONE
    /// canonical recompute (<c>VisitRequestV2Canonical</c>). The host arrangement is not campus CONTENT, so it is not part of this projection — it is edited through its own endpoint.</summary>
    public CampusVisitFormDto ToFormDto() => new(
        CampusId, PlannedStartAt, PlannedEndAt, DelegationName, VisitType, VisitTypeOther, Purpose, WorkingContent,
        Visitors, ExternalSupportMembers, OperationalContact, WorkingLanguage, TransportationNote,
        MediaConsentStatus, Notes, HostSelection: null,
        OperationalContactClientMemberKey: OperationalContactClientMemberKey);
}

/// <summary>
/// The complete per-campus v2 edit payload (pending-edit and resubmit share it). Like create it has no
/// request-level contact: each campus carries its own operational contact, and changing one is a
/// per-campus act (replace before the decision, transfer after it) rather than a form field.
/// </summary>
public record VisitRequestEditV2Dto(
    int ExpectedRequestRowVersion,
    RegistrantInputV2 Registrant,
    ulong? PartnerId,
    System.Collections.Generic.IList<CampusVisitEditV2Dto> CampusVisits);

/// <summary>
/// The Host a "Lưu và duyệt" names, in the same payload as the edit. Approving assigns a Host or it does
/// not happen at all, so the two travel together rather than as a save followed by a hopeful approve.
/// </summary>
public sealed record ApproveAfterSaveDto(ulong HostUserId, string? DecisionNote);

/// <summary>
/// A per-campus pending edit: the campus's full content snapshot, plus the two transient decisions the
/// actor may be taking alongside it.
/// </summary>
/// <param name="OverrideLeadTimeConfirmed">
/// The campus Staff Leader's explicit "yes, this schedule, with less than 72 hours' notice". It grants
/// nothing on its own — the backend honours it only for the leader of THIS campus, so anyone else
/// sending true is refused exactly as if they had not.
/// </param>
public sealed record VisitInstancePendingEditDto(
    CampusVisitEditV2Dto Content,
    bool OverrideLeadTimeConfirmed = false,
    ApproveAfterSaveDto? ApproveAfterSave = null);

/// <summary>
/// The payload for resubmitting ONE rejected campus (plan CanhIter3FixBug FIX-G/H). SCHEDULE-ONLY: the
/// UI offers nothing else to edit here, and the old contract — a full <see cref="CampusVisitEditV2Dto"/>
/// snapshot, same as a full content edit — let the service silently copy-on-write every member row on a
/// "just fix the date" resubmit, dropping <c>OrganizationPartnerId</c> (the frontend never echoed it —
/// see the deleted fields in <c>InstanceResubmitPanel.tsx</c>) and forcing the operational-contact link
/// to re-resolve by name/org/title fingerprint instead of staying pinned to the same row.
///
/// <para>
/// <see cref="CampusId"/> is still required, not because this may change it (it may not — the service
/// still refuses a mismatch, same as before), but because the campus-immutability guard needs something
/// from the payload to check against; it is not optional metadata.
/// </para>
/// </summary>
public sealed record InstanceResubmitScheduleDto(
    int ExpectedRowVersion,
    string CampusId,               // campus CODE (e.g. "HN") — must match the instance's own; never moves it
    System.DateTime PlannedStartAt,
    System.DateTime PlannedEndAt);
