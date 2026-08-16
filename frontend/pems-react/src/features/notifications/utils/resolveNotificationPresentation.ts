/**
 * The single localization layer for every Guest/Visitor-reachable notification surface
 * (NotificationBellButton, NotificationsPage, NotificationDetailModal,
 * VisitorNotificationsSection). No component reads `item.title` / `item.message` /
 * `item.timeAgoText` directly — they all go through `resolveNotificationPresentation`.
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

const KNOWN_EVENT_KEYS = new Set([
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
  if (!metadataJson) return null;
  try {
    const parsed = JSON.parse(metadataJson) as { eventKey?: string; params?: Record<string, unknown> };
    if (!parsed.eventKey || !KNOWN_EVENT_KEYS.has(parsed.eventKey)) return null;
    return { eventKey: parsed.eventKey, params: parsed.params ?? {} };
  } catch {
    return null;
  }
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
