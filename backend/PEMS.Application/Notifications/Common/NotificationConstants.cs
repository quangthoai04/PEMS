namespace PEMS.Application.Notifications.Common;

public static class NotificationTypes
{
    public const string VisitRequestSubmitted = "VISIT_REQUEST_SUBMITTED";
    public const string CrossCampusRequestSubmitted = "CROSS_CAMPUS_REQUEST_SUBMITTED";
    public const string VisitRequestApproved = "VISIT_REQUEST_APPROVED";
    public const string VisitRequestRejected = "VISIT_REQUEST_REJECTED";
    public const string WaitingHostAssignment = "WAITING_HOST_ASSIGNMENT";
    public const string HostAssigned = "HOST_ASSIGNED";
    public const string VisitStatusChanged = "VISIT_STATUS_CHANGED";
    public const string VisitCancelled = "VISIT_CANCELLED";
    public const string VisitReadyToClose = "VISIT_READY_TO_CLOSE";
    public const string VisitClosed = "VISIT_CLOSED";

    public const string ParticipationInvited = "PARTICIPATION_INVITED";
    public const string ParticipationResponded = "PARTICIPATION_RESPONDED";
    public const string ParticipationRemoved = "PARTICIPATION_REMOVED";

    public const string LogisticsRequestCreated = "LOGISTICS_REQUEST_CREATED";
    public const string LogisticsAssigned = "LOGISTICS_ASSIGNED";
    public const string LogisticsAssigneeResponded = "LOGISTICS_ASSIGNEE_RESPONDED";
    public const string LogisticsProposalCreated = "LOGISTICS_PROPOSAL_CREATED";
    public const string LogisticsProposalResponded = "LOGISTICS_PROPOSAL_RESPONDED";
    public const string LogisticsReady = "LOGISTICS_READY";
    public const string LogisticsDone = "LOGISTICS_DONE";
    public const string LogisticsHandoverRequired = "LOGISTICS_HANDOVER_REQUIRED";
    public const string LogisticsHandoverSigned = "LOGISTICS_HANDOVER_SIGNED";
    public const string LogisticsDueSoon = "LOGISTICS_DUE_SOON";
    public const string LogisticsOverdue = "LOGISTICS_OVERDUE";

    public const string AgendaRequired = "AGENDA_REQUIRED";
    public const string AgendaUpdated = "AGENDA_UPDATED";
    public const string VisitReminder = "VISIT_REMINDER";

    public const string MinutesCreated = "MINUTES_CREATED";
    public const string MinutesUpdated = "MINUTES_UPDATED";
    public const string ActionItemAssigned = "ACTION_ITEM_ASSIGNED";

    public const string NewsPendingApproval = "NEWS_PENDING_APPROVAL";
    public const string NewsReviewed = "NEWS_REVIEWED";

    public const string PartnerPendingApproval = "PARTNER_PENDING_APPROVAL";
    public const string PartnerReviewed = "PARTNER_REVIEWED";

    public const string AccountCreated = "ACCOUNT_CREATED";
    public const string AccountStatusChanged = "ACCOUNT_STATUS_CHANGED";

    public const string SystemAlert = "SYSTEM_ALERT";
}

public static class NotificationRelatedTypes
{
    public const string VisitRequest = "VISIT_REQUEST";
    public const string VisitInstance = "VISIT_INSTANCE";
    public const string VisitParticipant = "VISIT_PARTICIPANT";
    public const string LogisticsItem = "LOGISTICS_ITEM";
    public const string LogisticsHandover = "LOGISTICS_HANDOVER";
    public const string Agenda = "AGENDA";
    public const string Minutes = "MINUTES";
    public const string MinuteActionItem = "MINUTE_ACTION_ITEM";
    public const string News = "NEWS";
    public const string Partner = "PARTNER";
    public const string CalendarEvent = "CALENDAR_EVENT";
    public const string Account = "ACCOUNT";
    public const string System = "SYSTEM";
}
