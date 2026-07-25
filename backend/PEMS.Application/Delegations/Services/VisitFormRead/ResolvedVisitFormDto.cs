namespace PEMS.Application.Delegations.Services.VisitFormRead;

/// <summary>
/// Per-campus form read model returned by <see cref="IVisitFormReadService"/>. Every campus carries its
/// own content, resolved from that campus's detail row. It only
/// ever contains the campus instances the caller is authorized to see — hidden campuses never appear,
/// and their count/detail never leaks. Sensitive fields (raw tokens, pending-snapshot JSON, full
/// amendment JSON, audit IP/UA) are intentionally excluded.
/// </summary>
public sealed class ResolvedVisitFormDto
{
    public long VisitRequestId { get; init; }
    public string RequestCode { get; init; } = "";
    /// <summary>Request-level optimistic-concurrency token — the edit/resubmit v2 payload echoes it back as
    /// <c>ExpectedRequestRowVersion</c> so a stale editor gets a stable 409 instead of clobbering a concurrent change.</summary>
    public int RowVersion { get; init; }
    public bool HasMixedCampusDetails { get; init; }
    public string VisitScope { get; init; } = "";
    public string RequestStatus { get; init; } = "";
    public string CreatedSource { get; init; } = "";
    public DateTime SubmittedAt { get; init; }
    public long? PartnerId { get; init; }

    // ── Cancellation outcome (UC-136) ──
    // Populated only when the REQUEST itself was cancelled. The detail screen has to explain how a
    // request ended, and "CANCELLED" on its own leaves the reader hunting through the timeline for who
    // did it and why. Request-level cancellation is not campus-scoped, so there is nothing to hide here:
    // a caller who can read the request can read how it ended.
    public ulong? CancelledByUserId { get; init; }
    public string? CancelledByName { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }

    public ResolvedRegistrantDto Registrant { get; init; } = new();
    public ResolvedPrimaryContactDto PrimaryContact { get; init; } = new();

    /// <summary>Only the campus instances the caller may view, ordered by planned start.</summary>
    public List<ResolvedCampusVisitDto> CampusVisits { get; init; } = new();

    /// <summary>
    /// Backend-derived viewer capabilities (never trusted from the client). PR-3 exposes read-scope
    /// facts only; mutation actions (edit / amend / transfer / cancel) are added by the write PRs and
    /// are always re-authorized in their command handlers.
    /// </summary>
    public ResolvedViewerContextDto Viewer { get; init; } = new();
}

public sealed class ResolvedRegistrantDto
{
    public string FullName { get; init; } = "";
    public string Organization { get; init; } = "";
    public string JobTitle { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public string Nationality { get; init; } = "";
}

public sealed class ResolvedPrimaryContactDto
{
    public string FullName { get; init; } = "";
    public string Organization { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    /// <summary>PENDING_CONFIRMATION or ACTIVE.</summary>
    public string AccessStatus { get; init; } = "";
    public DateTime? VerifiedAt { get; init; }
}

public sealed class ResolvedViewerContextDto
{
    /// <summary>HOST / STAFF_LEADER / HO / VISITOR_OWNER / REGISTRANT / IC_SUPPORT / DEPT_SUPPORT / STUDENT / NONE.</summary>
    public string Relation { get; init; } = "NONE";
    /// <summary>True when the caller sees every campus of the request (registrant, primary contact, HO).</summary>
    public bool CanViewAllCampuses { get; init; }
    /// <summary>True for HO — monitoring only, no business action.</summary>
    public bool IsReadOnly { get; init; }
    /// <summary>Read capabilities only in PR-3 (e.g. "VIEW"). Write actions arrive in later PRs.</summary>
    public List<string> AllowedActions { get; init; } = new();
}

public sealed class ResolvedCampusVisitDto
{
    public long VisitInstanceId { get; init; }
    public long CampusId { get; init; }
    public string CampusCode { get; init; } = "";
    public string CampusName { get; init; } = "";
    public DateTime PlannedStartAt { get; init; }
    public DateTime PlannedEndAt { get; init; }
    public string Timezone { get; init; } = "Asia/Ho_Chi_Minh";

    public string InstanceStatus { get; init; } = "";
    public long? CurrentHostUserId { get; init; }
    public string? CurrentHostName { get; init; }
    public long? DecidedByUserId { get; init; }
    public string? DecidedByName { get; init; }
    public DateTime? DecidedAt { get; init; }
    public string? DecisionActorRole { get; init; }
    public string? DecisionNote { get; init; }

    // ── Per-campus cancellation (UC-136) ──
    // A campus can be cancelled on its own without the whole request being cancelled, so this is
    // separate from the request-level block above. It is projected from the instance the caller is
    // already authorized to see, so it carries no cross-campus information.
    public ulong? CancelledByUserId { get; init; }
    public string? CancelledByName { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationActorType { get; init; }
    public string? CancellationSource { get; init; }
    public string? CancellationReason { get; init; }

    // This campus's form content, from its own visit_instance_form_details row.
    public string DelegationName { get; init; } = "";
    public string VisitType { get; init; } = "";
    public string? VisitTypeOther { get; init; }
    public string Purpose { get; init; } = "";
    public string? WorkingContent { get; init; }
    public List<ResolvedMemberDto> Visitors { get; init; } = new();
    public List<ResolvedMemberDto> SupportMembers { get; init; } = new();
    public ResolvedOperationalContactDto OperationalContact { get; init; } = new();
    public string WorkingLanguage { get; init; } = "";
    public string? TransportationNote { get; init; }
    public string MediaConsentStatus { get; init; } = "";
    public string? MediaConsentNote { get; init; }
    public string? NoteToFptu { get; init; }

    public uint FormRevision { get; init; }
    public uint ApprovalRevision { get; init; }
    public int RowVersion { get; init; }

    /// <summary>Present only when the actor may see it; carries no PII/full-diff JSON (summary only).</summary>
    public ResolvedActiveAmendmentDto? ActiveAmendment { get; init; }

    /// <summary>
    /// Backend-derived mutation actions the caller may take on THIS instance (per-campus scoped) — e.g.
    /// SUBMIT_AMENDMENT / APPROVE_AMENDMENT / REJECT_AMENDMENT / WITHDRAW_AMENDMENT. The frontend gates
    /// per-instance UI on this list; command handlers re-authorize. Empty for read-only viewers.
    /// </summary>
    public List<string> AllowedActions { get; init; } = new();
}

public sealed class ResolvedMemberDto
{
    public long GuestMemberId { get; init; }
    public string MemberType { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Organization { get; init; } = "";
    public string JobTitle { get; init; } = "";
    public string Nationality { get; init; } = "";
    public int DisplayOrder { get; init; }
}

public sealed class ResolvedOperationalContactDto
{
    public string FullName { get; init; } = "";
    public string Organization { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
}

public sealed class ResolvedActiveAmendmentDto
{
    public long AmendmentId { get; init; }
    public uint AmendmentNo { get; init; }
    public string Status { get; init; } = "";
    public DateTime RequestedAt { get; init; }
    public int ChangedFieldCount { get; init; }
}
