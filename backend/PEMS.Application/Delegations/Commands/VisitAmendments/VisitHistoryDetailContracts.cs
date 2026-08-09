using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Commands.VisitAmendments;

/// <summary>
/// "What actually changed in this event." The timeline says an edit happened; this says which fields
/// moved and who joined or left the delegation.
/// </summary>
public sealed record GetVisitHistoryDetailQuery(ulong VisitRequestId, string EventId)
    : IRequest<VisitHistoryDetailDto>;

/// <summary>
/// How a timeline entry identifies itself. Events come from four different tables with independent
/// primary keys, so the id names its SOURCE as well as its key — an opaque integer would collide the
/// moment two sources both had a row 42.
/// </summary>
public static class VisitHistoryEventSources
{
    /// <summary>A per-campus form revision (visit_instance_form_revision_history).</summary>
    public const string InstanceRevision = "IREV";
    /// <summary>A request-level revision (visit_request_revision_history).</summary>
    public const string RequestRevision = "RREV";
    /// <summary>An amendment proposal (visit_instance_amendments).</summary>
    public const string AmendmentSubmitted = "AMDS";
    /// <summary>An amendment decision (approve / reject / withdraw).</summary>
    public const string AmendmentDecided = "AMDD";
    /// <summary>An audit-only event such as a Host handover (audit_logs).</summary>
    public const string Audit = "AUD";
    /// <summary>A contact-identity transition (visit_request_identity_change_events).</summary>
    public const string IdentityChange = "IDCH";

    public static string Build(string source, ulong key) => $"{source}:{key}";

    /// <summary>Splits "SRC:key". Returns false for anything that is not exactly that shape.</summary>
    public static bool TryParse(string? eventId, out string source, out ulong key)
    {
        source = string.Empty;
        key = 0;
        if (string.IsNullOrWhiteSpace(eventId)) return false;
        var parts = eventId.Split(':', 2);
        if (parts.Length != 2) return false;
        if (!ulong.TryParse(parts[1], out key)) return false;
        source = parts[0];
        return true;
    }
}

/// <summary>
/// Turns a stored <c>visit_request_identity_change_events.event_type</c> into the timeline's own
/// vocabulary.
///
/// The stored values are the workflow's internal names and there are more of them than the timeline
/// needs to distinguish; this is the one place the two vocabularies meet, so the list handler and the
/// detail handler cannot describe the same row differently. Anything unrecognised degrades to the
/// generic code rather than leaking a raw enum onto the screen.
/// </summary>
public static class VisitContactIdentityEventCodes
{
    public static string For(string? eventType) => eventType switch
    {
        // Written by the submit path for the first invitation of a campus.
        "CREATED" => VisitHistoryEventCodes.ContactInitialConfirmationCreated,
        "OPERATIONAL_CONTACT_INVITATION_CREATED" => VisitHistoryEventCodes.ContactInitialConfirmationCreated,
        "OPERATIONAL_CONTACT_TRANSFER_REQUESTED" => VisitHistoryEventCodes.ContactTransferRequested,
        "OPERATIONAL_CONTACT_INVITATION_RESENT" => VisitHistoryEventCodes.ContactInvitationResent,
        "OPERATIONAL_CONTACT_INVITATION_CANCELLED" => VisitHistoryEventCodes.ContactInvitationCancelled,
        "OPERATIONAL_CONTACT_INVITATION_SUPERSEDED" => VisitHistoryEventCodes.ContactInvitationSuperseded,
        "OPERATIONAL_CONTACT_CONFIRMED" => VisitHistoryEventCodes.ContactConfirmed,
        "OPERATIONAL_CONTACT_TRANSFER_APPLIED" => VisitHistoryEventCodes.ContactTransferAccepted,
        "OPERATIONAL_CONTACT_CONFIRMATION_DECLINED" => VisitHistoryEventCodes.ContactConfirmationDeclined,
        "OPERATIONAL_CONTACT_TRANSFER_DECLINED" => VisitHistoryEventCodes.ContactTransferDeclined,
        "OPERATIONAL_CONTACT_CONFIRMATION_EXPIRED" or "OPERATIONAL_CONTACT_TRANSFER_EXPIRED"
            => VisitHistoryEventCodes.ContactInvitationExpired,
        _ => VisitHistoryEventCodes.ContactIdentityChanged,
    };
}

/// <summary>One field that moved, before and after.</summary>
public sealed record VisitHistoryFieldChangeDto(
    string FieldCode,
    /// <summary>Translation key stem the client resolves; the backend never ships display text.</summary>
    string LabelKey,
    string? BeforeValue,
    string? AfterValue,
    /// <summary>
    /// True when there IS no recorded "before" for this field — the previous snapshot is missing or
    /// empty, not a snapshot that says the value was blank. Rendering both as "(trống)" claims the old
    /// value was empty when in fact nobody knows what it was, which is how a history invents facts.
    /// </summary>
    bool BeforeUnknown = false);

/// <summary>
/// A change to a LIST rather than a field — someone joined the delegation, left it, or had their
/// details corrected. Rendering these as "visitors: [12 names] → [13 names]" would be unreadable, so
/// membership is diffed into per-person rows.
/// </summary>
public sealed record VisitHistoryCollectionChangeDto(
    /// <summary>VISITORS or SUPPORT_MEMBERS.</summary>
    string CollectionCode,
    /// <summary>ADDED, REMOVED or UPDATED.</summary>
    string ChangeType,
    /// <summary>Who this row is about — the person's name, used to pair before with after.</summary>
    string? ItemKey,
    IReadOnlyDictionary<string, string>? Before,
    IReadOnlyDictionary<string, string>? After);

public static class VisitHistoryCollectionCodes
{
    public const string Visitors = "VISITORS";
    public const string SupportMembers = "SUPPORT_MEMBERS";
}

public static class VisitHistoryChangeTypes
{
    public const string Added = "ADDED";
    public const string Removed = "REMOVED";
    public const string Updated = "UPDATED";
}

/// <summary>
/// The drawer payload. Field-level and list-level diffs, plus who did it and why.
///
/// Deliberately NOT the raw snapshot JSON: those blobs carry every field of the form whether or not
/// it changed, they are shaped for storage rather than reading, and dumping them into a drawer would
/// hand a Staff Leader a wall of camelCase and expect them to spot the difference.
/// </summary>
public sealed record VisitHistoryDetailDto(
    string EventId,
    string EventCode,
    System.DateTime OccurredAt,
    string? ActorName,
    long? CampusId,
    string? CampusName,
    string? Reason,
    uint? BeforeRevision,
    uint? AfterRevision,
    IReadOnlyList<VisitHistoryFieldChangeDto> FieldChanges,
    IReadOnlyList<VisitHistoryCollectionChangeDto> CollectionChanges);
