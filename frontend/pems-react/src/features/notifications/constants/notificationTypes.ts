export const NotificationTypes = {
  VisitRequestSubmitted: "VISIT_REQUEST_SUBMITTED",
  VisitRequestApproved: "VISIT_REQUEST_APPROVED",
  VisitRequestRejected: "VISIT_REQUEST_REJECTED",
  HostAssigned: "HOST_ASSIGNED",
  VisitStatusChanged: "VISIT_STATUS_CHANGED",
  VisitCancelled: "VISIT_CANCELLED",
  VisitReadyToClose: "VISIT_READY_TO_CLOSE",
  VisitClosed: "VISIT_CLOSED",

  ParticipationInvited: "PARTICIPATION_INVITED",
  ParticipationResponded: "PARTICIPATION_RESPONDED",
  ParticipationRemoved: "PARTICIPATION_REMOVED",

  LogisticsRequestCreated: "LOGISTICS_REQUEST_CREATED",
  LogisticsAssigned: "LOGISTICS_ASSIGNED",
  LogisticsAssigneeResponded: "LOGISTICS_ASSIGNEE_RESPONDED",
  LogisticsProposalCreated: "LOGISTICS_PROPOSAL_CREATED",
  LogisticsProposalResponded: "LOGISTICS_PROPOSAL_RESPONDED",
  LogisticsReady: "LOGISTICS_READY",
  LogisticsDone: "LOGISTICS_DONE",
  LogisticsHandoverRequired: "LOGISTICS_HANDOVER_REQUIRED",
  LogisticsHandoverSigned: "LOGISTICS_HANDOVER_SIGNED",
  LogisticsDueSoon: "LOGISTICS_DUE_SOON",
  LogisticsOverdue: "LOGISTICS_OVERDUE",

  AgendaRequired: "AGENDA_REQUIRED",
  AgendaUpdated: "AGENDA_UPDATED",
  VisitReminder: "VISIT_REMINDER",

  MinutesCreated: "MINUTES_CREATED",
  MinutesUpdated: "MINUTES_UPDATED",
  ActionItemAssigned: "ACTION_ITEM_ASSIGNED",

  NewsPendingApproval: "NEWS_PENDING_APPROVAL",
  NewsReviewed: "NEWS_REVIEWED",
  PartnerPendingApproval: "PARTNER_PENDING_APPROVAL",
  PartnerReviewed: "PARTNER_REVIEWED",

  AccountCreated: "ACCOUNT_CREATED",
  AccountStatusChanged: "ACCOUNT_STATUS_CHANGED",
  SystemAlert: "SYSTEM_ALERT",
} as const;
