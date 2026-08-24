namespace PEMS.Application.Delegations.Services;

/// <summary>
/// Shared audit vocabulary for the two OperationalContact writers Commit 3 (Contact History
/// Integrity — Fix Group C/D) surfaces onto the business-history timeline: a profile detail
/// correction, and a contact replacement. Named here, once, so
/// <c>UpdateOperationalContactProfileCommandHandler</c>/<c>ReplaceOperationalContactCommandHandler</c>
/// and the history readers cannot drift apart the way <see cref="CampusDecisionAudit"/> already
/// exists to prevent for campus decisions.
///
/// <para>
/// Every OTHER OperationalContact writer (transfer, accept, decline, resend, reinvite, cancel,
/// expiry) already has a fully-scoped <c>VisitRequestIdentityChangeEvent</c> row to hang its own
/// timeline entry off, and is read from <c>visit_request_identity_change_events</c> — not from
/// here. Only these two actions have no such row (see each handler's own remarks for why), so only
/// these two are named.
/// </para>
/// </summary>
public static class OperationalContactHistoryAudit
{
    /// <summary>audit_logs.source_type these two writers use — not filtered on directly (the exact
    /// Action string is unambiguous by itself), but kept here for anyone reading this file to see the
    /// full shape of the row being matched.</summary>
    public const string SourceType = "IDENTITY";

    /// <summary>Written by UpdateOperationalContactProfileCommandHandler. Never touches the address,
    /// operational_contact_user_id, or any campus/request status — see that handler's own remarks.</summary>
    public const string ProfileUpdated = "OPERATIONAL_CONTACT_PROFILE_UPDATED";

    /// <summary>
    /// Written by VisitSafeEditService's contact branch (plan CanhIter3FixBug) whenever which delegation
    /// member the operational contact IS changes (null→A / A→null / A→B) — a direct metadata update,
    /// never an amendment. The single AuditLogChange row's OldValueText/NewValueText already carry
    /// human-readable display names (or the neutral "not in delegation" phrase) resolved at mutation
    /// time, not a raw GuestMemberId.
    /// </summary>
    public const string RelationUpdated = "OPERATIONAL_CONTACT_RELATION_UPDATED";

    /// <summary>
    /// Written by ReplaceOperationalContactCommandHandler for BOTH of its outcomes (external address
    /// → new invitation; registrant self-match → linked immediately). The two are NOT told apart by
    /// Action — both use this same string — but by whether the audit's own
    /// <c>operational_contact_user_id</c> AuditLogChange landed non-null: null means the campus was
    /// cleared pending a new invitation (already fully told by that invitation's own
    /// OPERATIONAL_CONTACT_INVITATION_CREATED / _SUPERSEDED identity events, so this action must NOT
    /// also be surfaced for that outcome — it would duplicate one business action as two timeline
    /// rows); non-null means the self-match outcome, which has no other event representing it and so
    /// is the only case this action is ever surfaced for.
    /// </summary>
    public const string Replaced = "OPERATIONAL_CONTACT_REPLACED";
}
