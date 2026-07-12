/**
 * Vietnam-time display contract (AC-02): the UI renders Asia/Ho_Chi_Minh wall-clock
 * regardless of the browser timezone.
 *
 * Two layers:
 *   1. Pure util contract — the shared vietnamTime helpers imported directly (they use
 *      Intl with a fixed timeZone, so results must be identical on any host).
 *   2. Real UI — the home NewsSection renders `publishedAt` through the shared util;
 *      with the API mocked, the SAME payload must render the SAME Vietnam date under
 *      Asia/Ho_Chi_Minh and America/New_York browser timezones. The second payload's
 *      instant falls on the PREVIOUS calendar day in New York — a browser-local
 *      formatter would show 01/07 there; the Vietnam formatter must keep 02/07.
 */
import { test, expect, type Page } from '@playwright/test';
import {
  parseApiDate,
  formatVietnamDate,
  formatVietnamDateTime,
  formatVietnamTime,
  toVietnamDateTimeLocalInput,
  toVietnamDateInput,
  toVietnamCalendarDate,
  getVietnamDateTimeParts,
  toVietnamDateKey
} from '../src/shared/utils/vietnamTime';

// ── 1. Util contract (host-timezone independent) ────────────────────────────

test.describe('vietnamTime util contract', () => {
  test('offset, Z and bare inputs all render the same Vietnam wall-clock', () => {
    expect(formatVietnamDateTime('2026-07-02T20:00:00+07:00')).toBe('02/07/2026 20:00');
    expect(formatVietnamDateTime('2026-07-02T13:00:00Z')).toBe('02/07/2026 20:00');
    // Bare strings are Vietnam wall-clock — NEVER interpreted as UTC.
    expect(formatVietnamDateTime('2026-07-02T20:00:00')).toBe('02/07/2026 20:00');
    expect(formatVietnamDateTime('2026-07-02T20:00')).toBe('02/07/2026 20:00');
    expect(formatVietnamDate('2026-07-02T20:00:00+07:00')).toBe('02/07/2026');
    expect(formatVietnamTime('2026-07-02T20:00:00+07:00')).toBe('20:00');
  });

  test('datetime-local hydration round-trips without drift (AC-03)', () => {
    // API +07:00 → input value → (submit as bare string) → parse → same instant.
    const input = toVietnamDateTimeLocalInput('2026-07-15T09:00:00+07:00');
    expect(input).toBe('2026-07-15T09:00');
    const reparsed = parseApiDate(input)!;
    expect(toVietnamDateTimeLocalInput(reparsed)).toBe('2026-07-15T09:00');
  });

  test('null/invalid input falls back safely', () => {
    expect(formatVietnamDateTime(null)).toBe('—');
    expect(formatVietnamDate(undefined, { fallback: '' })).toBe('');
    expect(parseApiDate('not-a-date')).toBeNull();
  });
});

// ── 1b. toVietnamCalendarDate — calendar-view contract (§ audit) ─────────────
// The re-based Date's LOCAL getters must return the Vietnam calendar parts. These
// assertions go through the real util (Intl with fixed Asia/Ho_Chi_Minh under the
// hood), so the extracted parts are host-timezone independent.

test.describe('toVietnamCalendarDate calendar parts', () => {
  const parts = (d: Date) => ({
    y: d.getUTCFullYear(), m: d.getUTCMonth() + 1, day: d.getUTCDate(),
    h: d.getUTCHours(), min: d.getUTCMinutes(),
  });

  test('01:30 +07:00 stays on 02/07 (never the previous day)', () => {
    expect(parts(toVietnamCalendarDate('2026-07-02T01:30:00+07:00')!))
      .toEqual({ y: 2026, m: 7, day: 2, h: 1, min: 30 });
  });

  test('year-end 23:30 +07:00 keeps 31/12', () => {
    expect(parts(toVietnamCalendarDate('2026-12-31T23:30:00+07:00')!))
      .toEqual({ y: 2026, m: 12, day: 31, h: 23, min: 30 });
  });

  test('UTC input converts to the same VN calendar day (13:00Z → 20:00 VN)', () => {
    expect(parts(toVietnamCalendarDate('2026-07-02T13:00:00Z')!))
      .toEqual({ y: 2026, m: 7, day: 2, h: 20, min: 0 });
    // 18:30Z on 31/12 is already 01:30 on 01/01 in Vietnam.
    expect(parts(toVietnamCalendarDate('2026-12-31T18:30:00Z')!))
      .toEqual({ y: 2027, m: 1, day: 1, h: 1, min: 30 });
  });

  test('date-only input does not shift the day', () => {
    expect(parts(toVietnamCalendarDate('2026-07-02')!))
      .toEqual({ y: 2026, m: 7, day: 2, h: 0, min: 0 });
    expect(toVietnamDateInput('2026-07-02')).toBe('2026-07-02');
  });

  test('day/month navigation on the calendar view is exact', () => {
    const d = toVietnamCalendarDate('2026-03-01T10:00:00+07:00')!;
    d.setUTCDate(d.getUTCDate() - 1); // previous day across a month boundary
    expect(parts(d)).toMatchObject({ y: 2026, m: 2, day: 28 });
    d.setUTCMonth(d.getUTCMonth() + 1);
    expect(parts(d)).toMatchObject({ y: 2026, m: 3, day: 28 });
  });

  test('instant math must use parseApiDate — durations are exact', () => {
    // Sorting/duration go through real instants, never the re-based view.
    const start = parseApiDate('2026-07-02T09:00:00+07:00')!;
    const end = parseApiDate('2026-07-02T12:00:00+07:00')!;
    expect(end.getTime() - start.getTime()).toBe(3 * 60 * 60 * 1000);

    // Same instant expressed as Z and +07:00 compares equal as instants.
    expect(parseApiDate('2026-07-02T13:00:00Z')!.getTime())
      .toBe(parseApiDate('2026-07-02T20:00:00+07:00')!.getTime());
  });

  test('DST transition days (Spring Forward 08/03/2026)', () => {
    // US Spring Forward happens at 02:00 on 08/03/2026 (local time skips to 03:00)
    // The UTC carrier and getVietnamDateTimeParts must report EXACTLY the Vietnam parts.
    const check = (input: string, h: number, min: number) => {
      const parts = getVietnamDateTimeParts(input)!;
      expect(parts).toMatchObject({ year: 2026, month: 3, day: 8, hour: h, minute: min });
      expect(toVietnamDateKey(input)).toBe('2026-03-08');
      
      const d = toVietnamCalendarDate(input)!;
      expect(d.getUTCFullYear()).toBe(2026);
      expect(d.getUTCMonth()).toBe(2);
      expect(d.getUTCDate()).toBe(8);
      expect(d.getUTCHours()).toBe(h);
      expect(d.getUTCMinutes()).toBe(min);
    };

    check('2026-03-08T01:30:00+07:00', 1, 30);
    check('2026-03-08T02:30:00+07:00', 2, 30);
    check('2026-03-08T03:30:00+07:00', 3, 30);

    // 2026-03-07T19:30:00Z → 2026-03-08 02:30 giờ Việt Nam
    check('2026-03-07T19:30:00Z', 2, 30);
  });

  test('DST transition days (Fall Back 01/11/2026)', () => {
    const check = (input: string, h: number, min: number) => {
      const parts = getVietnamDateTimeParts(input)!;
      expect(parts).toMatchObject({ year: 2026, month: 11, day: 1, hour: h, minute: min });
      expect(toVietnamDateKey(input)).toBe('2026-11-01');
      
      const d = toVietnamCalendarDate(input)!;
      expect(d.getUTCFullYear()).toBe(2026);
      expect(d.getUTCMonth()).toBe(10);
      expect(d.getUTCDate()).toBe(1);
      expect(d.getUTCHours()).toBe(h);
      expect(d.getUTCMinutes()).toBe(min);
    };

    check('2026-11-01T01:30:00+07:00', 1, 30);
    check('2026-11-01T02:30:00+07:00', 2, 30);
  });

  test('Leap year date-only (2024-02-29)', () => {
    const parts = getVietnamDateTimeParts('2024-02-29')!;
    expect(parts).toMatchObject({ year: 2024, month: 2, day: 29, hour: 0, minute: 0 });
    const d = toVietnamCalendarDate('2024-02-29')!;
    expect(d.getUTCFullYear()).toBe(2024);
    expect(d.getUTCMonth()).toBe(1);
    expect(d.getUTCDate()).toBe(29);
  });
});

// ── 2. Real UI under two browser timezones ──────────────────────────────────

const NEWS_PAYLOAD = {
  items: [
    {
      newsId: 9001,
      title: '[TZ-TEST] Đoàn thăm buổi tối',
      summary: 'Kiểm thử timezone 20:00 giờ Việt Nam.',
      coverUrl: null,
      publishedAt: '2026-07-02T20:00:00+07:00',
    },
    {
      newsId: 9002,
      title: '[TZ-TEST] Đoàn thăm sáng sớm',
      summary: 'Instant này là 01/07 ở New York nhưng vẫn phải hiện 02/07.',
      coverUrl: null,
      publishedAt: '2026-07-02T01:30:00+07:00',
    },
    {
      newsId: 9003,
      title: '[TZ-TEST] Bài chỉ có ngày',
      summary: 'Date-only không được lùi ngày ở browser múi giờ âm.',
      coverUrl: null,
      publishedAt: '2026-07-02',
    },
    {
      newsId: 9004,
      title: '[TZ-TEST] DST Spring Forward',
      summary: '08/03/2026 02:30 giờ Việt Nam',
      coverUrl: null,
      publishedAt: '2026-03-08T02:30:00+07:00',
    },
    {
      newsId: 9005,
      title: '[TZ-TEST] DST Fall Back',
      summary: '01/11/2026 01:30 giờ Việt Nam',
      coverUrl: null,
      publishedAt: '2026-11-01T01:30:00+07:00',
    },
  ],
  totalCount: 5,
  pageIndex: 1,
  pageSize: 5,
};

async function openHomeWithMockedNews(page: Page) {
  await page.route('**/public/news**', (route) => route.fulfill({ json: NEWS_PAYLOAD }));
  await page.addInitScript(() => window.localStorage.setItem('pems.language', 'vi'));
  await page.goto('/');
  await page.waitForLoadState('domcontentloaded');
}

for (const timezoneId of ['Asia/Ho_Chi_Minh', 'America/New_York']) {
  test.describe(`NewsSection under ${timezoneId}`, () => {
    test.use({ timezoneId });

    test('renders the Vietnam calendar date for both payloads', async ({ page }) => {
      await openHomeWithMockedNews(page);

      const eveningCard = page.getByRole('link', { name: /\[TZ-TEST\] Đoàn thăm buổi tối/ });
      await expect(eveningCard).toBeVisible();
      await expect(eveningCard.getByText('02/07/2026')).toBeVisible();

      // Boundary case: 01:30 +07:00 is the previous day in New York — must stay 02/07.
      const earlyCard = page.getByRole('link', { name: /\[TZ-TEST\] Đoàn thăm sáng sớm/ });
      await expect(earlyCard).toBeVisible();
      await expect(earlyCard.getByText('02/07/2026')).toBeVisible();

      // Date-only payload: must not slip to 01/07 in negative-offset browsers.
      const dateOnlyCard = page.getByRole('link', { name: /\[TZ-TEST\] Bài chỉ có ngày/ });
      await expect(dateOnlyCard).toBeVisible();
      await expect(dateOnlyCard.getByText('02/07/2026')).toBeVisible();

      // DST Spring Forward case
      const springCard = page.getByRole('link', { name: /\[TZ-TEST\] DST Spring Forward/ });
      await expect(springCard).toBeVisible();
      await expect(springCard.getByText('08/03/2026').first()).toBeVisible();
      // Since it's news, it shows date. We verify it didn't shift date (e.g. to 07/03).

      // DST Fall Back case
      const fallCard = page.getByRole('link', { name: /\[TZ-TEST\] DST Fall Back/ });
      await expect(fallCard).toBeVisible();
      await expect(fallCard.getByText('01/11/2026').first()).toBeVisible();
    });
  });
}
