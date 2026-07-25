/**
 * Wall-clock arithmetic for the visit schedule.
 *
 * Every value here is a Vietnam wall-clock "YYYY-MM-DDTHH:mm" string — the exact shape a
 * <input type="datetime-local"> holds and the exact shape the API takes (see
 * `shared/utils/vietnamTime`). The helpers below deliberately do NOT build a Date from the
 * host timezone: they carry the components through `Date.UTC`, which makes every result a
 * pure function of the string. A browser in Los Angeles and one in Hanoi therefore compute
 * the same duration, and adding an hour to 23:30 rolls the DATE the same way in both.
 */

const LOCAL_RE = /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})/;

/** Minutes since the epoch, treating the wall-clock as its own frame. Null when unparseable. */
export const wallClockToMinutes = (value: string | null | undefined): number | null => {
  if (!value) return null;
  const m = LOCAL_RE.exec(value.trim());
  if (!m) return null;
  const [, y, mo, d, h, mi] = m;
  const ms = Date.UTC(Number(y), Number(mo) - 1, Number(d), Number(h), Number(mi));
  return Number.isNaN(ms) ? null : ms / 60_000;
};

const pad = (n: number) => String(n).padStart(2, '0');

/** Inverse of `wallClockToMinutes`. */
export const minutesToWallClock = (minutes: number): string => {
  const d = new Date(minutes * 60_000);
  return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}T${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}`;
};

export interface DateTimeParts {
  /** "YYYY-MM-DD" */
  date: string;
  /** "HH:mm" */
  time: string;
}

export const splitWallClock = (value: string | null | undefined): DateTimeParts | null => {
  if (!value) return null;
  const m = LOCAL_RE.exec(value.trim());
  if (!m) return null;
  const [, y, mo, d, h, mi] = m;
  return { date: `${y}-${mo}-${d}`, time: `${h}:${mi}` };
};

export const joinWallClock = (date: string, time: string): string =>
  date && time ? `${date}T${time}` : '';

export const addMinutes = (value: string, minutes: number): string => {
  const base = wallClockToMinutes(value);
  return base === null ? '' : minutesToWallClock(base + minutes);
};

/** Difference in minutes, or null when either end is unusable. */
export const durationMinutes = (start: string, end: string): number | null => {
  const a = wallClockToMinutes(start);
  const b = wallClockToMinutes(end);
  return a === null || b === null ? null : b - a;
};

/**
 * Accepts what a person actually types — "8:30", "08.30", "0830", "8" — and returns "HH:mm",
 * or null when it cannot be read as a time. Rejecting a typo is better than guessing at it:
 * the caller keeps the previous committed value rather than storing something the user did
 * not mean.
 */
export const normalizeTimeInput = (raw: string): string | null => {
  const s = raw.trim();
  if (!s) return null;

  const colon = /^(\d{1,2})\s*[:.h]\s*(\d{1,2})$/.exec(s);
  if (colon) {
    const h = Number(colon[1]);
    const m = Number(colon[2]);
    return h <= 23 && m <= 59 ? `${pad(h)}:${pad(m)}` : null;
  }

  const digits = /^(\d{1,4})$/.exec(s);
  if (digits) {
    const d = digits[1];
    if (d.length <= 2) {
      const h = Number(d);
      return h <= 23 ? `${pad(h)}:00` : null;
    }
    const h = Number(d.slice(0, d.length - 2));
    const m = Number(d.slice(-2));
    return h <= 23 && m <= 59 ? `${pad(h)}:${pad(m)}` : null;
  }

  return null;
};

/** "HH:mm" slots across one day at `step` minutes. */
export const timeSlots = (step = 15): string[] => {
  const out: string[] = [];
  for (let m = 0; m < 24 * 60; m += step) out.push(`${pad(Math.floor(m / 60))}:${pad(m % 60)}`);
  return out;
};

export interface DurationLabelParts {
  days: number;
  hours: number;
  minutes: number;
}

export const splitDuration = (totalMinutes: number): DurationLabelParts => {
  const abs = Math.max(0, totalMinutes);
  return {
    days: Math.floor(abs / (24 * 60)),
    hours: Math.floor((abs % (24 * 60)) / 60),
    minutes: abs % 60,
  };
};

/** Today's Vietnam date as "YYYY-MM-DD" plus the earliest date the schedule may start on. */
export const earliestStart = (minAdvanceHours: number, now: Date = new Date()): string => {
  const vn = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', hour12: false,
  });
  const p: Record<string, string> = {};
  for (const part of vn.formatToParts(now)) p[part.type] = part.value;
  if (p.hour === '24') p.hour = '00';
  const nowLocal = `${p.year}-${p.month}-${p.day}T${p.hour}:${p.minute}`;
  return addMinutes(nowLocal, Math.round(minAdvanceHours * 60));
};
