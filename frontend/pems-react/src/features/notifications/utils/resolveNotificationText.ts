/**
 * Resolves the display title/message for a notification. Backend Title/Message stay
 * Vietnamese-only (unchanged, single source of truth for every role and for VI-mode Visitor).
 * A notification created for one of the Guest/Visitor-reachable templates additionally carries
 * `metadataJson = {"messageKey":"...","params":{...}}` (see backend `NotificationMessageKeys`) —
 * when the reader is in English mode and the key is recognized, this renders a fully independent
 * EN string from i18n instead. Unknown/missing metadata (including every notification created
 * before this existed) falls back to the raw backend text exactly as before — no regression.
 */
import type { UiLanguage } from '../../../shared/utils/vietnamTime';

const KNOWN_MESSAGE_KEYS = new Set([
  'CAMPUS_APPROVED',
  'CAMPUS_REJECTED',
  'FEEDBACK_INVITE_VISITOR',
  'VISIT_CLOSED',
  'VISIT_CANCELLED_BY_HOST',
  'OPCONTACT_TRANSFER_FROM',
  'OPCONTACT_TRANSFER_TO',
  'AMENDMENT_APPROVED',
  'AMENDMENT_REJECTED',
]);

type RawNotification = {
  title: string;
  message?: string | null;
  metadataJson?: string | null;
};

export function resolveNotificationText(
  item: RawNotification,
  language: UiLanguage,
  t: (key: string, opts?: Record<string, unknown>) => string,
): { title: string; message: string | null } {
  if (language !== 'en' || !item.metadataJson) {
    return { title: item.title, message: item.message ?? null };
  }

  let parsed: { messageKey?: string; params?: Record<string, unknown> } | null = null;
  try {
    parsed = JSON.parse(item.metadataJson);
  } catch {
    parsed = null;
  }

  if (!parsed?.messageKey || !KNOWN_MESSAGE_KEYS.has(parsed.messageKey)) {
    return { title: item.title, message: item.message ?? null };
  }

  const params = parsed.params ?? {};
  return {
    title: t(`notifications:messages.${parsed.messageKey}.title`, params),
    message: t(`notifications:messages.${parsed.messageKey}.body`, params),
  };
}
