import { describe, expect, it } from 'vitest';
import { resolveVisitEntryOutcome } from './useVisitEntryCta';

// Regression for the entry-point cutover defect: the four capability states must map to four
// DISTINCT outcomes. The bug being locked down is a fetch failure (or an in-flight check) silently
// downgrading users to the legacy v1 form — that must never happen; only a real backend OFF opens v1.
describe('resolveVisitEntryOutcome', () => {
  it('ready + enabled → the v2 modal (opened over the current page, never a navigation)', () => {
    expect(resolveVisitEntryOutcome('ready', true)).toBe('v2-modal');
  });

  it('ready + disabled → disabled (the ONLY path that was V1, now disabled)', () => {
    expect(resolveVisitEntryOutcome('ready', false)).toBe('disabled');
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

  it('disabled is reachable ONLY from ready+disabled', () => {
    const combos: Array<['ready' | 'loading' | 'error', boolean]> = [
      ['ready', true], ['ready', false], ['loading', true], ['loading', false], ['error', true], ['error', false],
    ];
    const disabledCombos = combos.filter(([s, e]) => resolveVisitEntryOutcome(s, e) === 'disabled');
    expect(disabledCombos).toEqual([['ready', false]]);
  });
});
