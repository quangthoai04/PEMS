import { describe, expect, it } from 'vitest';
import { canSubmitReminders } from '../visitProcessGuards';

describe('canSubmitReminders — never save over an unknown schedule', () => {
  it('allows saving only when configurable, the schedule loaded, and nothing is in flight', () => {
    expect(canSubmitReminders({ canConfigurePrep: true, remindersLoadFailed: false, busy: false })).toBe(true);
  });

  it('blocks the save when the saved schedule failed to load', () => {
    expect(canSubmitReminders({ canConfigurePrep: true, remindersLoadFailed: true, busy: false })).toBe(false);
  });

  it('blocks the save while a submit is in flight or when prep is not allowed', () => {
    expect(canSubmitReminders({ canConfigurePrep: true, remindersLoadFailed: false, busy: true })).toBe(false);
    expect(canSubmitReminders({ canConfigurePrep: false, remindersLoadFailed: false, busy: false })).toBe(false);
  });
});
