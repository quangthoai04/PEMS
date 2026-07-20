import { describe, expect, it } from 'vitest';
import { resolveVisitEntryOutcome } from './useVisitEntryCta';

// Regression for the entry-point cutover defect: the four capability states must map to four
// DISTINCT outcomes. The bug being locked down is a fetch failure (or an in-flight check) silently
// downgrading users to the legacy v1 form — that must never happen; only a real backend OFF opens v1.
describe('resolveVisitEntryOutcome', () => {
  it('ready + enabled → v2 route (regardless of any transient history)', () => {
    expect(resolveVisitEntryOutcome('ready', true)).toBe('v2-route');
  });

  it('ready + disabled → v1 popup (the ONLY path that opens v1)', () => {
    expect(resolveVisitEntryOutcome('ready', false)).toBe('v1-popup');
  });

  it('error → error (never a silent v1 fallback on CORS/timeout/network failure)', () => {
    // enabled is meaningless while errored; both must still resolve to error.
    expect(resolveVisitEntryOutcome('error', false)).toBe('error');
    expect(resolveVisitEntryOutcome('error', true)).toBe('error');
  });

  it('loading → loading (wait for the check, do not open v1)', () => {
    expect(resolveVisitEntryOutcome('loading', false)).toBe('loading');
    expect(resolveVisitEntryOutcome('loading', true)).toBe('loading');
  });

  it('v1 is reachable ONLY from ready+disabled', () => {
    const combos: Array<['ready' | 'loading' | 'error', boolean]> = [
      ['ready', true], ['ready', false], ['loading', true], ['loading', false], ['error', true], ['error', false],
    ];
    const v1Combos = combos.filter(([s, e]) => resolveVisitEntryOutcome(s, e) === 'v1-popup');
    expect(v1Combos).toEqual([['ready', false]]);
  });
});
