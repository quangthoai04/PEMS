import { describe, expect, it } from 'vitest';
import {
  canSubmitReminders,
  canAssignResponsible,
  candidatesAreGenuinelyEmpty,
} from '../visitProcessGuards';

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

describe('canAssignResponsible — a failed candidate load is not "no candidates"', () => {
  it('allows assignment only when editable AND the candidate list loaded', () => {
    expect(canAssignResponsible({ canEditAgenda: true, candidatesLoadFailed: false })).toBe(true);
    expect(canAssignResponsible({ canEditAgenda: true, candidatesLoadFailed: true })).toBe(false);
    expect(canAssignResponsible({ canEditAgenda: false, candidatesLoadFailed: false })).toBe(false);
  });
});

describe('candidatesAreGenuinelyEmpty — only trust emptiness after a successful load', () => {
  it('is true only when the load succeeded and returned no supporting candidates', () => {
    expect(candidatesAreGenuinelyEmpty({ candidatesLoadFailed: false, supportingCandidateCount: 0 })).toBe(true);
  });

  it('is false when candidates exist', () => {
    expect(candidatesAreGenuinelyEmpty({ candidatesLoadFailed: false, supportingCandidateCount: 3 })).toBe(false);
  });

  it('is false when the emptiness is an artifact of a failed load', () => {
    expect(candidatesAreGenuinelyEmpty({ candidatesLoadFailed: true, supportingCandidateCount: 0 })).toBe(false);
  });
});
