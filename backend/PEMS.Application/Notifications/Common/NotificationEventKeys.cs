using System.Text.Json;
using System.Text.Json.Serialization;

namespace PEMS.Application.Notifications.Common;

/// <summary>
/// Stable, language-neutral semantic event codes for the Guest/Visitor-reachable notification
/// templates — the "what happened", independent of wording in either language.
///
/// <para>
/// Deliberately a SEPARATE vocabulary from <see cref="NotificationTypes"/>, not a replacement for
/// it. `NotificationType` is coarser and already carries other responsibilities elsewhere in the
/// codebase (badge/category business logic in <c>VisitChangeSummaryBuilder</c>, "next task"
/// detection in <c>VisitNextTaskBuilder</c>) — several genuinely different semantic events share
/// one `NotificationType` value today (e.g. `VISIT_STATUS_CHANGED` covers both "invite Visitor to
/// leave feedback" and "HO visibility on a multi-campus status change"; `VISIT_REQUEST_SUBMITTED`
/// covers both "operational contact role transferred" and "amendment decision"). Splitting
/// `NotificationType` itself into one-value-per-event would ripple into that unrelated business
/// logic and risk changing Staff/HO-facing behavior — out of scope for an i18n change. An EventKey
/// is presentation-only: it exists so ONE notification row can render a full VI or EN sentence
/// without the backend ever choosing which language to speak.
/// </para>
///
/// <para>
/// `Title`/`Message` stay Vietnamese, populated exactly as before — legacy/back-compat source for
/// any row or reader that doesn't go through the new localizer. `EventKey` + `Params` are the
/// actual source of truth for the frontend's `resolveNotificationPresentation`, in BOTH languages
/// (not just English) — see `resolveNotificationPresentation.ts`.
/// </para>
/// </summary>
public static class NotificationEventKeys
{
    public const string CampusApproved = "CAMPUS_APPROVED";
    public const string CampusRejected = "CAMPUS_REJECTED";
    public const string FeedbackInviteVisitor = "FEEDBACK_INVITE_VISITOR";
    public const string VisitClosed = "VISIT_CLOSED";
    public const string VisitCancelledByHost = "VISIT_CANCELLED_BY_HOST";
    public const string OperationalContactTransferredFrom = "OPCONTACT_TRANSFER_FROM";
    public const string OperationalContactTransferredTo = "OPCONTACT_TRANSFER_TO";
    public const string AmendmentApproved = "AMENDMENT_APPROVED";
    public const string AmendmentRejected = "AMENDMENT_REJECTED";

    /// <summary>
    /// Builds the `MetadataJson` payload: `{"eventKey":"...","params":{...}}`. `params` must be
    /// STRUCTURED data only (campus name, request code, a user-entered reason verbatim) — never a
    /// pre-built sentence. Property names are pinned with <see cref="JsonPropertyNameAttribute"/>
    /// so the shape never drifts with an ambient serializer naming policy.
    /// </summary>
    public static string BuildMetadata(string eventKey, object parameters)
        => JsonSerializer.Serialize(new MetadataPayload(eventKey, parameters));

    private sealed record MetadataPayload(
        [property: JsonPropertyName("eventKey")] string EventKey,
        [property: JsonPropertyName("params")] object Params);
}
