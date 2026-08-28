/**
 * PEMS time policy (frontend side):
 * - The API returns every timestamp as ISO 8601 WITH the +07:00 offset
 *   (e.g. "2026-07-02T20:00:00+07:00") — Vietnam wall-clock.
 * - The UI always DISPLAYS Asia/Ho_Chi_Minh regardless of the browser timezone.
 * - Legacy/loose inputs without an offset ("2026-07-02T20:00:00", "2026-07-02")
 *   are treated as Vietnam wall-clock — NEVER as UTC. Do not append 'Z' anywhere.
 * - <input type="datetime-local"> values are plain Vietnam wall-clock strings
 *   ("YYYY-MM-DDTHH:mm"); send them to the API as-is (the backend reads bare
 *   strings as Vietnam time) and never round-trip them through toISOString().
 */

export const VIETNAM_TIME_ZONE = 'Asia/Ho_Chi_Minh';

const OFFSET_RE = /(Z|[+-]\d{2}:?\d{2})$/i;
const DATE_ONLY_RE = /^\d{4}-\d{2}-\d{2}$/;
const HAS_SECONDS_RE = /^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}/;

/**
 * Parses an API/legacy timestamp into a Date (an absolute instant).
 * Strings without an explicit offset are interpreted as Vietnam wall-clock.
 * Returns null for null/undefined/empty/invalid input.
 */
export function parseApiDate(value: string | Date | null | undefined): Date | null {
  if (value == null || value === '') return null;
  if (value instanceof Date) return isNaN(value.getTime()) ? null : value;

  const raw = value.trim();
  if (!raw) return null;

  let normalized: string;
  if (OFFSET_RE.test(raw)) {
    normalized = raw.replace(' ', 'T');
  } else if (DATE_ONLY_RE.test(raw)) {
    normalized = `${raw}T00:00:00+07:00`;
  } else {
    const base = raw.replace(' ', 'T');
    normalized = `${HAS_SECONDS_RE.test(base) ? base : `${base}:00`}+07:00`;
  }

  const parsed = new Date(normalized);
  if (!isNaN(parsed.getTime())) return parsed;

  // Last resort for non-ISO legacy strings — native parsing (browser-dependent).
  const fallback = new Date(raw);
  return isNaN(fallback.getTime()) ? null : fallback;
}

type Parts = { year: string; month: string; day: string; weekday: string; hour: string; minute: string; second: string };

function vietnamParts(date: Date): Parts {
  const fmt = new Intl.DateTimeFormat('en-GB', {
    timeZone: VIETNAM_TIME_ZONE,
    year: 'numeric', month: '2-digit', day: '2-digit',
    weekday: 'short',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
    hour12: false,
  });
  const parts: Record<string, string> = {};
  for (const p of fmt.formatToParts(date)) parts[p.type] = p.value;
  // 'en-GB' may render midnight as "24" with hour12:false — normalize.
  if (parts.hour === '24') parts.hour = '00';
  return parts as Parts;
}

/** "dd/MM/yyyy HH:mm" in Vietnam time. Returns fallback ('—' by default) when empty/invalid. */
export function formatVietnamDateTime(
  value: string | Date | null | undefined,
  options?: { seconds?: boolean; fallback?: string },
): string {
  const date = parseApiDate(value);
  if (!date) return options?.fallback ?? '—';
  const p = vietnamParts(date);
  const time = options?.seconds ? `${p.hour}:${p.minute}:${p.second}` : `${p.hour}:${p.minute}`;
  return `${p.day}/${p.month}/${p.year} ${time}`;
}

/** "dd/MM/yyyy" in Vietnam time. */
export function formatVietnamDate(
  value: string | Date | null | undefined,
  options?: { fallback?: string },
): string {
  const date = parseApiDate(value);
  if (!date) return options?.fallback ?? '—';
  const p = vietnamParts(date);
  return `${p.day}/${p.month}/${p.year}`;
}

/** "HH:mm" in Vietnam time. */
export function formatVietnamTime(
  value: string | Date | null | undefined,
  options?: { fallback?: string },
): string {
  const date = parseApiDate(value);
  if (!date) return options?.fallback ?? '—';
  const p = vietnamParts(date);
  return `${p.hour}:${p.minute}`;
}

/** Smart range format: "dd/MM/yyyy | HH:mm - HH:mm" if same day, else "dd/MM/yyyy HH:mm → dd/MM/yyyy HH:mm". */
export function formatSmartVietnamRange(
  startValue: string | Date | null | undefined,
  endValue: string | Date | null | undefined,
  options?: { fallback?: string },
): string {
  const startDate = parseApiDate(startValue);
  const endDate = parseApiDate(endValue);
  if (!startDate || !endDate) return options?.fallback ?? '—';
  const startD = formatVietnamDate(startDate);
  const endD = formatVietnamDate(endDate);
  const startT = formatVietnamTime(startDate);
  const endT = formatVietnamTime(endDate);
  if (startD === endD) {
    return `${startD} | ${startT} - ${endT}`;
  }
  return `${startD} ${startT} → ${endD} ${endT}`;
}

/**
 * Locale-aware long form (e.g. "12 tháng 7, 2026 20:00") — timezone is ALWAYS Vietnam;
 * only the wording follows the locale.
 */
export function formatVietnamDateTimeLocale(
  value: string | Date | null | undefined,
  locale: string = 'vi-VN',
  options?: Intl.DateTimeFormatOptions & { fallback?: string },
): string {
  const date = parseApiDate(value);
  if (!date) return options?.fallback ?? '—';
  const { fallback: _fallback, ...intl } = options ?? {};
  return new Intl.DateTimeFormat(locale, {
    timeZone: VIETNAM_TIME_ZONE,
    ...(Object.keys(intl).length > 0
      ? intl
      : { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }),
  }).format(date);
}

/**
 * Value for <input type="datetime-local"> — Vietnam wall-clock "YYYY-MM-DDTHH:mm".
 * Hydrating a form from an API "+07:00" timestamp through this NEVER shifts the time,
 * no matter the browser timezone (AC-03: edit → save must not drift).
 */
export function toVietnamDateTimeLocalInput(value: string | Date | null | undefined): string {
  const date = parseApiDate(value);
  if (!date) return '';
  const p = vietnamParts(date);
  return `${p.year}-${p.month}-${p.day}T${p.hour}:${p.minute}`;
}

/** Value for <input type="date"> — Vietnam wall-clock "YYYY-MM-DD". */
export function toVietnamDateInput(value: string | Date | null | undefined): string {
  const date = parseApiDate(value);
  if (!date) return '';
  const p = vietnamParts(date);
  return `${p.year}-${p.month}-${p.day}`;
}

/**
 * Passes a datetime-local form value ("YYYY-MM-DDTHH:mm") through to the API contract.
 * The backend interprets bare strings as Vietnam wall-clock, so no conversion happens —
 * this exists so callers never "fix" the value with toISOString() (which would shift it).
 */
export function fromDateTimeLocalInput(value: string | null | undefined): string | null {
  const v = value?.trim();
  return v ? v : null;
}

/** Current Vietnam wall-clock as "YYYY-MM-DDTHH:mm" (e.g. min= for datetime-local inputs). */
export function vietnamNowDateTimeLocal(): string {
  return toVietnamDateTimeLocalInput(new Date());
}

const WEEKDAY_MAP: Record<string, number> = {
  'Sun': 0, 'Mon': 1, 'Tue': 2, 'Wed': 3, 'Thu': 4, 'Fri': 5, 'Sat': 6
};

export interface VietnamDateTimeParts {
  year: number;
  month: number;      // 1..12
  day: number;        // 1..31
  weekday: number;    // 0=Sun, 1=Mon, ..., 6=Sat
  hour: number;       // 0..23
  minute: number;
  second: number;
}

/**
 * Parses the input into Vietnam wall-clock parts.
 * Does not depend on the browser's local timezone.
 */
export function getVietnamDateTimeParts(value: string | Date | null | undefined): VietnamDateTimeParts | null {
  const date = parseApiDate(value);
  if (!date) return null;
  const p = vietnamParts(date);
  return {
    year: parseInt(p.year, 10),
    month: parseInt(p.month, 10),
    day: parseInt(p.day, 10),
    weekday: WEEKDAY_MAP[p.weekday] ?? 0,
    hour: parseInt(p.hour, 10),
    minute: parseInt(p.minute, 10),
    second: parseInt(p.second, 10)
  };
}

export function toVietnamDateKey(value: string | Date | null | undefined): string {
  const parts = getVietnamDateTimeParts(value);
  if (!parts) return '';
  return `${parts.year}-${String(parts.month).padStart(2, '0')}-${String(parts.day).padStart(2, '0')}`;
}

export interface CalendarDateParts {
  year: number;
  month: number; // 1..12
  day: number;
}

export function parseDateKey(value: string): CalendarDateParts | null {
  if (!value) return null;
  const [y, m, d] = value.split('-').map(Number);
  if (isNaN(y) || isNaN(m) || isNaN(d)) return null;
  return { year: y, month: m, day: d };
}

/**
 * Re-bases an instant so that the UTC getters (getUTCFullYear, getUTCMonth, getUTCDate,
 * getUTCDay, getUTCHours, getUTCMinutes, ...) return the VIETNAM wall-clock parts.
 *
 * Use for calendar/grid placement code that needs a Date carrier.
 * ALWAYS use UTC getters/setters on the returned Date.
 */
export function toVietnamCalendarDate(value: string | Date | null | undefined): Date | null {
  const parts = getVietnamDateTimeParts(value);
  if (!parts) return null;
  return new Date(Date.UTC(parts.year, parts.month - 1, parts.day, parts.hour, parts.minute, parts.second));
}

/** Short relative label in Vietnamese ("Vừa xong", "5 phút trước", …); falls back to dd/MM/yyyy. */
export function formatVietnamRelative(value: string | Date | null | undefined): string {
  const date = parseApiDate(value);
  if (!date) return '';
  const diffMs = Date.now() - date.getTime();
  const minutes = Math.floor(diffMs / 60_000);
  if (minutes < 1) return 'Vừa xong';
  if (minutes < 60) return `${minutes} phút trước`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} giờ trước`;
  const days = Math.floor(hours / 24);
  if (days < 7) return `${days} ngày trước`;
  return formatVietnamDate(date);
}

// ── Locale-aware formatting (VI/EN wording, timezone always Asia/Ho_Chi_Minh) ──────────────
//
// The functions above (`formatVietnamDate*`) are intentionally VI-only and still used by
// Staff/HO/Department screens that never switch language. Guest/VISITOR-facing screens must use
// the `formatLocalized*` variants below instead, so English mode never renders a Vietnamese
// weekday/month or a hardcoded Vietnamese relative-time string. Business timezone is unaffected —
// only the wording changes with `language`.

export type UiLanguage = 'vi' | 'en';

const INTL_LOCALE: Record<UiLanguage, string> = { vi: 'vi-VN', en: 'en-GB' };

/** Locale-aware "dd/MM/yyyy HH:mm" (or with seconds). VI/EN wording, always Vietnam time. */
export function formatLocalizedDateTime(
  value: string | Date | null | undefined,
  language: UiLanguage,
  options?: { seconds?: boolean; fallback?: string },
): string {
  const date = parseApiDate(value);
  if (!date) return options?.fallback ?? '—';
  return new Intl.DateTimeFormat(INTL_LOCALE[language], {
    timeZone: VIETNAM_TIME_ZONE,
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    ...(options?.seconds ? { second: '2-digit' } : {}),
    hour12: false,
  }).format(date);
}

/** Locale-aware "dd/MM/yyyy". VI/EN wording, always Vietnam time. */
export function formatLocalizedDate(
  value: string | Date | null | undefined,
  language: UiLanguage,
  options?: { fallback?: string },
): string {
  const date = parseApiDate(value);
  if (!date) return options?.fallback ?? '—';
  return new Intl.DateTimeFormat(INTL_LOCALE[language], {
    timeZone: VIETNAM_TIME_ZONE,
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date);
}

/** Locale-aware "HH:mm". VI/EN wording, always Vietnam time. */
export function formatLocalizedTime(
  value: string | Date | null | undefined,
  language: UiLanguage,
  options?: { fallback?: string },
): string {
  const date = parseApiDate(value);
  if (!date) return options?.fallback ?? '—';
  return new Intl.DateTimeFormat(INTL_LOCALE[language], {
    timeZone: VIETNAM_TIME_ZONE,
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date);
}

/**
 * Locale-aware long form (e.g. "12 thg 7, 2026, 20:00" / "12 Jul 2026, 20:00"). Timezone is ALWAYS
 * Vietnam; only the wording follows `language`.
 */
export function formatLocalizedDateTimeLong(
  value: string | Date | null | undefined,
  language: UiLanguage,
  options?: Intl.DateTimeFormatOptions & { fallback?: string },
): string {
  const date = parseApiDate(value);
  if (!date) return options?.fallback ?? '—';
  const { fallback: _fallback, ...intl } = options ?? {};
  return new Intl.DateTimeFormat(INTL_LOCALE[language], {
    timeZone: VIETNAM_TIME_ZONE,
    ...(Object.keys(intl).length > 0
      ? intl
      : { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }),
  }).format(date);
}

/**
 * Locale-aware relative time ("Just now" / "Vừa xong", "5 minutes ago" / "5 phút trước", …),
 * falling back to `formatLocalizedDate` past 7 days. `t` is the `i18next` translate function
 * (namespace `common`, keys under `relativeTime.*` with i18next `_one`/`_other` plural suffixes).
 */
export function formatLocalizedRelativeTime(
  value: string | Date | null | undefined,
  language: UiLanguage,
  t: (key: string, opts?: Record<string, unknown>) => string,
): string {
  const date = parseApiDate(value);
  if (!date) return '';
  const diffMs = Date.now() - date.getTime();
  const minutes = Math.floor(diffMs / 60_000);
  if (minutes < 1) return t('common:relativeTime.justNow');
  if (minutes < 60) return t('common:relativeTime.minutesAgo', { count: minutes });
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return t('common:relativeTime.hoursAgo', { count: hours });
  const days = Math.floor(hours / 24);
  if (days < 7) return t('common:relativeTime.daysAgo', { count: days });
  return formatLocalizedDate(date, language);
}
