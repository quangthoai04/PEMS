/**
 * The single localization layer for every notification surface across every role — Guest/Visitor,
 * Staff, Staff Leader, Department, HO (NotificationBellButton, NotificationsPage,
 * NotificationDetailModal, VisitorNotificationsSection). No component reads `item.title` /
 * `item.message` / `item.timeAgoText` directly — they all go through
 * `resolveNotificationPresentation`.
 *
 * Architecture: the backend stores WHAT HAPPENED (an `eventKey` + structured `params` in
 * `metadataJson`, plus `createdAt`), never a pre-built sentence. Rendering the sentence in
 * either language is entirely a frontend job:
 *
 *   eventKey + params  --(this file)-->  i18next key + interpolation  --(i18next)-->  VI or EN
 *
 * VI and EN both go through the SAME i18n lookup — neither language trusts the raw backend
 * Title/Message for a known eventKey, so switching the site language re-renders the same
 * notification row in the other language with no backend re-fetch and no new notification row.
 *
 * `legacyTitle`/`legacyMessage` (the backend's original Vietnamese Title/Message columns) are
 * used ONLY as a fallback for a row with no eventKey — a notification created before this
 * architecture existed, or of a type this file hasn't mapped. VI still shows that legacy
 * Vietnamese text (correct — it always was Vietnamese, nothing leaked). EN never does: an
 * unmapped row renders a generic localized placeholder instead, because the legacy text is
 * backend system prose, and rendering unknown system prose in English would silently show
 * untranslated Vietnamese. (A genuinely USER-entered string, if one is ever added here, would
 * need to be threaded through as a `param`, not read off `legacyTitle`/`legacyMessage` — see the
 * `reason` handling in the CAMPUS_REJECTED event.)
 */
import { formatLocalizedRelativeTime, type UiLanguage } from '../../../shared/utils/vietnamTime';
import { parseNotificationSemantic } from './notificationSemantic';

/**
 * Exported so the coverage guard (notificationEventCoverage.test.ts) can assert every LIVE backend
 * eventKey (read straight from NotificationEventKeys.cs) is recognized here — an eventKey missing
 * from this set silently falls back to legacy/placeholder presentation instead of failing loudly.
 */
export const KNOWN_EVENT_KEYS = new Set([
  // Guest/Visitor-facing (original set).
  'CAMPUS_APPROVED',
  'CAMPUS_REJECTED',
  'FEEDBACK_INVITE_VISITOR',
  'VISIT_CLOSED',
  'VISIT_CANCELLED_BY_HOST',
  'OPCONTACT_TRANSFER_FROM',
  'OPCONTACT_TRANSFER_TO',
  'AMENDMENT_APPROVED',
  'AMENDMENT_REJECTED',
  'HOST_CHANGED',
  'ACCOUNT_CREATED',
  'ACCOUNT_LOCKED',
  'ACCOUNT_UNLOCKED',
  // Staff / Staff Leader / Department / HO — visit request lifecycle.
  'VISIT_REQUEST_WAITING_APPROVAL',
  'VISIT_REQUEST_UPDATED_PENDING',
  'VISIT_REQUEST_RESUBMITTED',
  'VISIT_PRIVACY_CONSENT_WITHDRAWN',
  'HOST_ASSIGNED',
  'HOST_PROPOSAL_PENDING',
  'HOST_REASSIGNMENT_REQUIRED',
  'HOST_TRANSFER_INCOMING',
  'HOST_TRANSFER_OUTGOING',
  'CAMPUS_APPROVED_HO_VISIBILITY',
  'CAMPUS_REJECTED_HO_VISIBILITY',
  'VISIT_CANCELLED_HO_VISIBILITY',
  'HOST_CHANGED_HO_VISIBILITY',
  'VISIT_CANCELLED_STAFF_LEADER',
  'HO_CAMPUS_UNPROCESSED_ALERT',
  'AMENDMENT_PROPOSED',
  'MULTI_CAMPUS_REQUEST_SUBMITTED_HO_VISIBILITY',
  'VISIT_REQUEST_PARTIALLY_APPROVED_HO_VISIBILITY',
  'VISIT_REQUEST_FULLY_PROCESSED_HO_VISIBILITY',
  'VISIT_REQUEST_CANCELLED_BEFORE_APPROVAL',
  // Participation.
  'PARTICIPATION_INVITED',
  'PARTICIPATION_ACCEPTED',
  'PARTICIPATION_DECLINED',
  // Agenda / Minutes / Action items.
  'AGENDA_UPDATED',
  'MINUTES_UPDATED',
  'ACTION_ITEM_ASSIGNED',
  'ACTION_ITEM_DUE',
  // Logistics.
  'LOGISTICS_REQUEST_CREATED',
  'LOGISTICS_ASSIGNED',
  'LOGISTICS_ASSIGNEE_ACCEPTED',
  'LOGISTICS_ASSIGNEE_DECLINED',
  'LOGISTICS_PROPOSAL_CREATED',
  'LOGISTICS_PROPOSAL_ACCEPTED',
  'LOGISTICS_PROPOSAL_REJECTED',
  'LOGISTICS_HANDOVER_SIGNED',
  'LOGISTICS_EXPENSE_REMINDER',
  // News / Partner.
  'NEWS_PENDING_APPROVAL',
  'NEWS_APPROVED',
  'NEWS_REJECTED',
  'PARTNER_PENDING_APPROVAL',
  'PARTNER_APPROVED',
  'PARTNER_REJECTED',
  // Feedback / reminders.
  'HOST_FEEDBACK_INVITE',
  'VISITOR_FEEDBACK_RECEIVED',
  'HOST_FEEDBACK_RECEIVED',
  'VISIT_REMINDER',
  // Accounts.
  'ACCOUNT_STATUS_ACTIVATED',
  'ACCOUNT_STATUS_DEACTIVATED',
]);

export type NotificationPresentationInput = {
  /** Raw `{"eventKey":"...","params":{...}}` JSON from the backend, or null/undefined. */
  metadataJson?: string | null;
  createdAt: string;
  /** The backend's original Vietnamese Title/Message — legacy fallback only, see file header. */
  legacyTitle: string;
  legacyMessage?: string | null;
};

export type NotificationPresentation = {
  title: string;
  message: string | null;
  relativeTime: string;
};

type Translate = (key: string, opts?: Record<string, unknown>) => string;

function parseEvent(metadataJson: string | null | undefined): { eventKey: string; params: Record<string, unknown> } | null {
  const event = parseNotificationSemantic(metadataJson);
  if (!event || !KNOWN_EVENT_KEYS.has(event.eventKey)) return null;
  return event;
}

export function resolveNotificationPresentation(
  input: NotificationPresentationInput,
  language: UiLanguage,
  t: Translate,
): NotificationPresentation {
  const relativeTime = formatLocalizedRelativeTime(input.createdAt, language, t);
  const event = parseEvent(input.metadataJson);

  if (event) {
    // Same i18n key for both languages — i18next resolves VI/EN off the active language, so a
    // language switch alone re-renders this exact notification row in the other language.
    return {
      title: t(`notifications:events.${event.eventKey}.title`, event.params),
      message: t(`notifications:events.${event.eventKey}.message`, event.params),
      relativeTime,
    };
  }

  if (language === 'en') {
    // Unmapped/legacy row: legacyTitle/legacyMessage are Vietnamese system prose, not user
    // content — never render them in English. Generic, honest, no data leak.
    return {
      title: t('notifications:events.unknown.title'),
      message: null,
      relativeTime,
    };
  }

  return { title: input.legacyTitle, message: input.legacyMessage ?? null, relativeTime };
}
