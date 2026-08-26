/**
 * Proportional scaling used by AgendaSetupPanel's preview — must stay identical to the backend's
 * AgendaTemplateTimelineScaler (same ratio formula, same minute rounding, same last-item-pinned rule),
 * otherwise the preview the host sees could drift from what Apply actually persists.
 */
import { describe, expect, it } from 'vitest';
import { agendaTemplatesAdapter } from '../agendaTemplatesAdapter';

const START = '2026-09-01T09:00:00';

// Offset-less datetime strings (the app's wall-clock convention, see vietnamTime.ts) are parsed by
// `new Date(...)` as LOCAL time — so building the "+N minutes" fixture string must also go back
// through LOCAL getters (not toISOString(), which is UTC and would silently shift by the runner's
// timezone offset).
const pad = (n: number) => String(n).padStart(2, '0');
const toLocalIsoString = (d: Date): string =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
const addMinutes = (iso: string, minutes: number) => {
  const d = new Date(iso);
  return toLocalIsoString(new Date(d.getTime() + minutes * 60_000));
};

const threeItemTemplate = [
  { startOffsetMinutes: 0, durationMinutes: 20 },  // 0-20
  { startOffsetMinutes: 20, durationMinutes: 70 }, // 20-90
  { startOffsetMinutes: 90, durationMinutes: 30 }, // 90-120
];

function assertBoundaryMinutes(boundary: { start: Date; end: Date }, expectedStartMin: number, expectedEndMin: number) {
  const base = new Date(START).getTime();
  expect(Math.round((boundary.start.getTime() - base) / 60_000)).toBe(expectedStartMin);
  expect(Math.round((boundary.end.getTime() - base) / 60_000)).toBe(expectedEndMin);
}

describe('agendaTemplatesAdapter.computeTemplateSpanMinutes', () => {
  it('is the furthest endpoint, not the summed duration', () => {
    // gap: item A 0-20, item B 30-80 -> sum would be 70, real span is 80.
    expect(agendaTemplatesAdapter.computeTemplateSpanMinutes([
      { startOffsetMinutes: 0, durationMinutes: 20 },
      { startOffsetMinutes: 30, durationMinutes: 50 },
    ])).toBe(80);
  });

  it('is 0 for an empty template', () => {
    expect(agendaTemplatesAdapter.computeTemplateSpanMinutes([])).toBe(0);
  });
});

describe('agendaTemplatesAdapter.scaleTemplateItems — core cases', () => {
  it('keeps the baseline timeline when the visit matches the template span', () => {
    const end = addMinutes(START, 120);
    const result = agendaTemplatesAdapter.scaleTemplateItems(START, end, threeItemTemplate);

    assertBoundaryMinutes(result[0], 0, 20);
    assertBoundaryMinutes(result[1], 20, 90);
    assertBoundaryMinutes(result[2], 90, 120);
  });

  it('scales every boundary down proportionally for a shorter visit (60 min)', () => {
    const end = addMinutes(START, 60);
    const result = agendaTemplatesAdapter.scaleTemplateItems(START, end, threeItemTemplate);

    assertBoundaryMinutes(result[0], 0, 10);
    assertBoundaryMinutes(result[1], 10, 45);
    assertBoundaryMinutes(result[2], 45, 60);
  });

  it('scales every boundary up proportionally for a longer visit (240 min)', () => {
    const end = addMinutes(START, 240);
    const result = agendaTemplatesAdapter.scaleTemplateItems(START, end, threeItemTemplate);

    assertBoundaryMinutes(result[0], 0, 40);
    assertBoundaryMinutes(result[1], 40, 180);
    assertBoundaryMinutes(result[2], 180, 240);
  });

  it('always pins the last items end exactly to plannedEnd', () => {
    for (const visitMinutes of [37, 60, 120, 241, 500]) {
      const end = addMinutes(START, visitMinutes);
      const result = agendaTemplatesAdapter.scaleTemplateItems(START, end, threeItemTemplate);
      expect(result[result.length - 1].end.toISOString()).toBe(new Date(end).toISOString());
    }
  });
});

describe('agendaTemplatesAdapter.scaleTemplateItems — gaps preserved proportionally', () => {
  it('scales the gap between items, without auto-joining them', () => {
    const items = [
      { startOffsetMinutes: 0, durationMinutes: 20 }, // 0-20
      { startOffsetMinutes: 30, durationMinutes: 30 }, // 30-60 (10-min gap out of 60 span)
    ];
    const end = addMinutes(START, 120); // double the template span
    const result = agendaTemplatesAdapter.scaleTemplateItems(START, end, items);

    assertBoundaryMinutes(result[0], 0, 40);
    assertBoundaryMinutes(result[1], 60, 120);
    const gapMinutes = (result[1].start.getTime() - result[0].end.getTime()) / 60_000;
    expect(gapMinutes).toBe(20); // original 10-min gap doubled, same 1/6 ratio of span
  });
});

describe('agendaTemplatesAdapter.scaleTemplateItems — invalid input fails soft (returns [])', () => {
  it('returns [] when plannedEnd is not after plannedStart', () => {
    expect(agendaTemplatesAdapter.scaleTemplateItems(START, START, threeItemTemplate)).toEqual([]);
    expect(agendaTemplatesAdapter.scaleTemplateItems(START, addMinutes(START, -10), threeItemTemplate)).toEqual([]);
  });

  it('returns [] when dates are missing or unparsable', () => {
    expect(agendaTemplatesAdapter.scaleTemplateItems(null, addMinutes(START, 60), threeItemTemplate)).toEqual([]);
    expect(agendaTemplatesAdapter.scaleTemplateItems(START, undefined, threeItemTemplate)).toEqual([]);
    expect(agendaTemplatesAdapter.scaleTemplateItems('not-a-date', addMinutes(START, 60), threeItemTemplate)).toEqual([]);
  });

  it('returns [] for an empty item list', () => {
    expect(agendaTemplatesAdapter.scaleTemplateItems(START, addMinutes(START, 60), [])).toEqual([]);
  });
});

describe('agendaTemplatesAdapter.scaleTemplateItems — legacy-shaped templates need no migration', () => {
  it('applies a template shaped exactly like existing seed data (sequential, no gaps)', () => {
    const items = [
      { startOffsetMinutes: 0, durationMinutes: 15 },
      { startOffsetMinutes: 15, durationMinutes: 45 },
      { startOffsetMinutes: 60, durationMinutes: 30 },
      { startOffsetMinutes: 90, durationMinutes: 30 },
    ];
    const end = addMinutes(START, 90);
    const result = agendaTemplatesAdapter.scaleTemplateItems(START, end, items);

    expect(result).toHaveLength(4);
    expect(result[0].start.toISOString()).toBe(new Date(START).toISOString());
    expect(result[result.length - 1].end.toISOString()).toBe(new Date(end).toISOString());
    for (const boundary of result) {
      expect(boundary.end.getTime()).toBeGreaterThan(boundary.start.getTime());
    }
  });
});
