using System.Text.Json;
using System.Text.Json.Serialization;

namespace PEMS.Application.Notifications.Common;

/// <summary>
/// Stable, language-neutral codes for the Guest/Visitor-reachable notification templates.
///
/// The `Title`/`Message` columns stay Vietnamese-only (unchanged, still the source of truth for
/// every other role and for VI-mode Visitor) — these keys travel ONLY in `MetadataJson` as
/// `{"messageKey":"...","params":{...}}` and let the frontend render a fully independent EN string
/// via i18n (`notifications:messages.&lt;KEY&gt;`) when the recipient is a VISITOR reading in English.
/// A notification with no metadata (created before this existed, or not in this list) simply falls
/// back to the raw Vietnamese Title/Message exactly as before — no behavior change, no migration.
/// </summary>
public static class NotificationMessageKeys
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
    /// Builds the `MetadataJson` payload for a <see cref="NotificationMessageKeys"/> template.
    /// Property names are pinned with <see cref="JsonPropertyNameAttribute"/> so the shape never
    /// drifts with an ambient serializer naming policy — the frontend parses these two keys literally.
    /// </summary>
    public static string BuildMetadata(string messageKey, object parameters)
        => JsonSerializer.Serialize(new MetadataPayload(messageKey, parameters));

    private sealed record MetadataPayload(
        [property: JsonPropertyName("messageKey")] string MessageKey,
        [property: JsonPropertyName("params")] object Params);
}
