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
    public const string HostChanged = "HOST_CHANGED";
    public const string AccountCreated = "ACCOUNT_CREATED";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string AccountUnlocked = "ACCOUNT_UNLOCKED";

    // --- Added for the Staff/Staff Leader/Department/HO notification-system audit (2026-08-19):
    // every business event below used to create a Title/Message-only row with MetadataJson=null,
    // which fell back to a generic placeholder on EN and could not carry structured params. See
    // docs/CanhIter3FixBug/GopYCQuyen/PEMS_Fix_Notification_Presentation_Semantic_Routing.md.

    // Visit request lifecycle — Staff Leader / HO / Host facing.
    public const string VisitRequestWaitingApproval = "VISIT_REQUEST_WAITING_APPROVAL";
    public const string VisitRequestUpdatedPending = "VISIT_REQUEST_UPDATED_PENDING";
    public const string VisitRequestResubmitted = "VISIT_REQUEST_RESUBMITTED";
    public const string VisitPrivacyConsentWithdrawn = "VISIT_PRIVACY_CONSENT_WITHDRAWN";
    public const string HostAssigned = "HOST_ASSIGNED";
    public const string HostProposalPending = "HOST_PROPOSAL_PENDING";
    public const string HostReassignmentRequired = "HOST_REASSIGNMENT_REQUIRED";
    public const string HostTransferIncoming = "HOST_TRANSFER_INCOMING";
    public const string HostTransferOutgoing = "HOST_TRANSFER_OUTGOING";
    public const string CampusApprovedHoVisibility = "CAMPUS_APPROVED_HO_VISIBILITY";
    public const string CampusRejectedHoVisibility = "CAMPUS_REJECTED_HO_VISIBILITY";
    public const string VisitCancelledHoVisibility = "VISIT_CANCELLED_HO_VISIBILITY";
    public const string HostChangedHoVisibility = "HOST_CHANGED_HO_VISIBILITY";
    public const string VisitCancelledStaffLeader = "VISIT_CANCELLED_STAFF_LEADER";
    public const string HoCampusUnprocessedAlert = "HO_CAMPUS_UNPROCESSED_ALERT";

    // Participation invitations.
    public const string ParticipationInvited = "PARTICIPATION_INVITED";
    public const string ParticipationAccepted = "PARTICIPATION_ACCEPTED";
    public const string ParticipationDeclined = "PARTICIPATION_DECLINED";

    // Agenda / Minutes / Action items.
    public const string AgendaUpdated = "AGENDA_UPDATED";
    public const string MinutesUpdated = "MINUTES_UPDATED";
    public const string ActionItemAssigned = "ACTION_ITEM_ASSIGNED";
    public const string ActionItemDue = "ACTION_ITEM_DUE";

    // Logistics.
    public const string LogisticsRequestCreated = "LOGISTICS_REQUEST_CREATED";
    public const string LogisticsAssigned = "LOGISTICS_ASSIGNED";
    public const string LogisticsAssigneeAccepted = "LOGISTICS_ASSIGNEE_ACCEPTED";
    public const string LogisticsAssigneeDeclined = "LOGISTICS_ASSIGNEE_DECLINED";
    public const string LogisticsProposalCreated = "LOGISTICS_PROPOSAL_CREATED";
    public const string LogisticsProposalAccepted = "LOGISTICS_PROPOSAL_ACCEPTED";
    public const string LogisticsProposalRejected = "LOGISTICS_PROPOSAL_REJECTED";
    public const string LogisticsHandoverSigned = "LOGISTICS_HANDOVER_SIGNED";
    public const string LogisticsExpenseReminder = "LOGISTICS_EXPENSE_REMINDER";

    // News / Partner.
    public const string NewsPendingApproval = "NEWS_PENDING_APPROVAL";
    public const string NewsApproved = "NEWS_APPROVED";
    public const string NewsRejected = "NEWS_REJECTED";
    public const string PartnerPendingApproval = "PARTNER_PENDING_APPROVAL";
    public const string PartnerApproved = "PARTNER_APPROVED";
    public const string PartnerRejected = "PARTNER_REJECTED";

    // Feedback / reminders.
    public const string HostFeedbackInvite = "HOST_FEEDBACK_INVITE";
    public const string VisitorFeedbackReceived = "VISITOR_FEEDBACK_RECEIVED";
    public const string HostFeedbackReceived = "HOST_FEEDBACK_RECEIVED";
    public const string VisitReminder = "VISIT_REMINDER";

    // Accounts — the plain ACTIVE/INACTIVE toggle (HO/Staff Leader), distinct from the ADMIN
    // security actions (AccountLocked/AccountUnlocked above).
    public const string AccountStatusActivated = "ACCOUNT_STATUS_ACTIVATED";
    public const string AccountStatusDeactivated = "ACCOUNT_STATUS_DEACTIVATED";

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
