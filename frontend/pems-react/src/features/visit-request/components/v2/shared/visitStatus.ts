/**
 * The ONE status vocabulary for every v2 visit surface.
 *
 * Two separate enums live in the database and they are NOT interchangeable:
 *   • `visit_requests.status`          — the aggregate of the whole request;
 *   • `visit_request_campuses.status`  — where ONE campus actually is.
 * Both are listed here exactly as the schema declares them, so a value that cannot occur is never
 * given a label and a value that can occur is never rendered raw. Anything unrecognised falls back
 * to a neutral "unknown" label rather than leaking `WAITING_REQUEST_APPROVAL` at a user.
 */

export type VisitStatusTone = 'neutral' | 'waiting' | 'progress' | 'success' | 'danger' | 'muted';

/** Tailwind classes per tone — one place, so no surface invents its own palette. */
export const VISIT_STATUS_TONE_CLASSES: Record<VisitStatusTone, string> = {
  neutral: 'bg-slate-100 text-slate-700 ring-slate-200',
  waiting: 'bg-amber-50 text-amber-800 ring-amber-200',
  progress: 'bg-[#004c91]/10 text-[#004c91] ring-[#004c91]/20',
  success: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
  danger: 'bg-red-50 text-red-700 ring-red-200',
  muted: 'bg-slate-100 text-slate-500 ring-slate-200',
};

/** `visit_request_campuses.status` — the per-campus lifecycle. */
export const VISIT_INSTANCE_STATUS_TONES: Record<string, VisitStatusTone> = {
  // The first gate stage: this campus's operational contact has not answered its invitation yet.
  // It is a campus value only — the whole-request counterpart is PENDING_CONTACT_CONFIRMATION below.
  WAITING_CONTACT_CONFIRMATION: 'waiting',
  WAITING_REQUEST_APPROVAL: 'waiting',
  ASSIGNED: 'success',
  BEFORE_VISIT: 'progress',
  DURING_VISIT: 'progress',
  AFTER_VISIT: 'progress',
  CLOSED: 'muted',
  CANCELLED: 'muted',
  REJECTED: 'danger',
};

/** `visit_requests.status` — the aggregate derived from the campus decisions. */
export const VISIT_REQUEST_STATUS_TONES: Record<string, VisitStatusTone> = {
  // The confirmation gate, held shut for the WHOLE request while any active campus is still waiting
  // for its operational contact. Never an instance value — a campus in that state is
  // WAITING_CONTACT_CONFIRMATION above.
  PENDING_CONTACT_CONFIRMATION: 'waiting',
  PENDING_APPROVAL: 'waiting',
  PARTIALLY_APPROVED: 'progress',
  APPROVED: 'success',
  REJECTED: 'danger',
  CANCELLED: 'muted',
};

export type VisitStatusKind = 'instance' | 'request';

/** Whether this value is one the schema can actually produce for that column. */
export const isKnownVisitStatus = (kind: VisitStatusKind, status: string | null | undefined): boolean => {
  const key = (status ?? '').trim().toUpperCase();
  const table = kind === 'instance' ? VISIT_INSTANCE_STATUS_TONES : VISIT_REQUEST_STATUS_TONES;
  return key.length > 0 && key in table;
};

export const visitStatusTone = (kind: VisitStatusKind, status: string | null | undefined): VisitStatusTone => {
  const key = (status ?? '').trim().toUpperCase();
  const table = kind === 'instance' ? VISIT_INSTANCE_STATUS_TONES : VISIT_REQUEST_STATUS_TONES;
  return table[key] ?? 'neutral';
};

/**
 * i18n key for a status. Callers pass the result to `t()` WITH the "unknown" fallback below —
 * never with the raw status as the default, which is how enum values leaked into the UI before.
 */
export const visitStatusI18nKey = (kind: VisitStatusKind, status: string | null | undefined): string => {
  const key = (status ?? '').trim().toUpperCase();
  return isKnownVisitStatus(kind, key)
    ? `visitRequestV2:status.${kind === 'instance' ? 'instance' : 'request'}.${key}`
    : 'visitRequestV2:status.unknown';
};
