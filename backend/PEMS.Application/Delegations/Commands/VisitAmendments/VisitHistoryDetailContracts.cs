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
    /// <summary>
    /// Maps a stored <c>event_type</c> onto the code the timeline renders.
    ///
    /// <para>
    /// Both spellings of every event are listed. Today's writers use the long
    /// <c>OPERATIONAL_CONTACT_*</c> form, but rows written by earlier versions, by the canonical seed
    /// and by migration scripts carry the SHORT form — and an unmapped type falls through to the
    /// generic <c>ContactIdentityChanged</c>, which is why parts of the contact history read "Vai trò
    /// đầu mối liên hệ có thay đổi (...)" instead of naming what actually happened. Aliases cost
    /// nothing and are the difference between a timeline that reads and one that shrugs.
    /// </para>
    /// </summary>
    public static string For(string? eventType) => eventType switch
    {
        // Written by the submit path for the first invitation of a campus.
        "CREATED" => VisitHistoryEventCodes.ContactInitialConfirmationCreated,
        "OPERATIONAL_CONTACT_INVITATION_CREATED" or "INVITATION_CREATED"
            => VisitHistoryEventCodes.ContactInitialConfirmationCreated,
        "OPERATIONAL_CONTACT_TRANSFER_REQUESTED" or "TRANSFER_REQUESTED"
            => VisitHistoryEventCodes.ContactTransferRequested,
        "OPERATIONAL_CONTACT_INVITATION_RESENT" or "INVITATION_RESENT"
            => VisitHistoryEventCodes.ContactInvitationResent,
        // A new invitation to the SAME already-known contact after their previous one lapsed
        // unanswered — distinct from a resend (same row, still live) and from a fresh invitation to
        // a genuinely different contact (INVITATION_CREATED, from Replace or the original submit).
        "OPERATIONAL_CONTACT_REINVITED" => VisitHistoryEventCodes.ContactReinvited,
        "OPERATIONAL_CONTACT_INVITATION_CANCELLED" or "INVITATION_CANCELLED"
            => VisitHistoryEventCodes.ContactInvitationCancelled,
        "OPERATIONAL_CONTACT_INVITATION_SUPERSEDED" or "INVITATION_SUPERSEDED"
            => VisitHistoryEventCodes.ContactInvitationSuperseded,
        "OPERATIONAL_CONTACT_CONFIRMED" or "CONFIRMED"
            => VisitHistoryEventCodes.ContactConfirmed,
        "OPERATIONAL_CONTACT_TRANSFER_APPLIED" or "TRANSFER_APPLIED"
            => VisitHistoryEventCodes.ContactTransferAccepted,
        "OPERATIONAL_CONTACT_CONFIRMATION_DECLINED" or "CONFIRMATION_DECLINED"
            => VisitHistoryEventCodes.ContactConfirmationDeclined,
        "OPERATIONAL_CONTACT_TRANSFER_DECLINED" or "TRANSFER_DECLINED"
            => VisitHistoryEventCodes.ContactTransferDeclined,
        "OPERATIONAL_CONTACT_CONFIRMATION_EXPIRED" or "OPERATIONAL_CONTACT_TRANSFER_EXPIRED"
            or "CONFIRMATION_EXPIRED" or "TRANSFER_EXPIRED"
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
    IReadOnlyList<VisitHistoryCollectionChangeDto> CollectionChanges,
    /// <summary>
    /// Why the change list looks the way it does. Stated by the SERVER so the client never has to
    /// infer business meaning from an empty array:
    ///
    /// <list type="bullet">
    /// <item><b>AVAILABLE</b> — both revisions were found and compared. An empty list here genuinely
    /// means the two snapshots are identical in every field the drawer renders.</item>
    /// <item><b>PREVIOUS_REVISION_MISSING</b> — the revision before this one was never recorded, so
    /// there is nothing to compare against. An empty list here means "we do not know what changed",
    /// which is a completely different claim from "nothing changed".</item>
    /// <item><b>NOT_APPLICABLE</b> — this event is not a revision diff at all (an identity change, a
    /// host handover, an amendment's own change rows).</item>
    /// </list>
    ///
    /// The distinction is the whole point: the drawer used to print "Sự kiện này không có thay đổi
    /// chi tiết nào được ghi nhận" over an edit the user had just made, because a missing baseline
    /// and an empty diff produced the same empty array.
    /// </summary>
    string ComparisonStatus = VisitHistoryComparisonStatuses.NotApplicable);

/// <summary>Values for <see cref="VisitHistoryDetailDto.ComparisonStatus"/>.</summary>
public static class VisitHistoryComparisonStatuses
{
    public const string Available = "AVAILABLE";
    public const string PreviousRevisionMissing = "PREVIOUS_REVISION_MISSING";
    public const string NotApplicable = "NOT_APPLICABLE";
}
