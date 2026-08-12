import { describe, expect, it } from 'vitest';
import {
  participantIdentityKey,
  selectNewSyncCandidates,
  type ParticipantIdentityFields,
} from '../utils/participantIdentity';

/**
 * "Đồng bộ người mới" appends whatever the backend offers that the draft does not already have.
 * Matching on ids alone missed the one case that actually happens: a person who is BOTH an invited
 * internal supporter and a listed member of the delegation holds a userId in one list and a
 * guestMemberId in the other, so they were appended a second time as a guest.
 */

const internal_ = (userId: number, over: Partial<ParticipantIdentityFields> = {}): ParticipantIdentityFields => ({
  userId,
  guestMemberId: null,
  fullNameSnapshot: 'Nguyễn Văn A',
  roleSnapshot: 'Cán bộ IC',
  organizationSnapshot: 'ABC University',
  ...over,
});

const guest = (guestMemberId: number, over: Partial<ParticipantIdentityFields> = {}): ParticipantIdentityFields => ({
  userId: null,
  guestMemberId,
  fullNameSnapshot: 'Nguyễn Văn A',
  roleSnapshot: 'Cán bộ IC',
  organizationSnapshot: 'ABC University',
  ...over,
});

describe('participantIdentityKey', () => {
  it('ignores letter case and stray whitespace', () => {
    expect(participantIdentityKey(guest(1, { fullNameSnapshot: '  nguyễn   VĂN a ' })))
      .toBe(participantIdentityKey(guest(2)));
  });

  it('is empty for a row with no name, so an unnamed row never merges', () => {
    expect(participantIdentityKey(guest(1, { fullNameSnapshot: '   ' }))).toBe('');
  });

  it('keeps accents — they distinguish real names', () => {
    expect(participantIdentityKey(guest(1, { fullNameSnapshot: 'Nguyen Van A' })))
      .not.toBe(participantIdentityKey(guest(2)));
  });
});

describe('selectNewSyncCandidates', () => {
  it('drops a guest who is already in the draft as an internal person', () => {
    expect(selectNewSyncCandidates([internal_(7)], [guest(31)])).toEqual([]);
  });

  it('drops a guest duplicating an internal person accepted earlier in the same batch', () => {
    const fresh = selectNewSyncCandidates([], [internal_(7), guest(31)]);
    expect(fresh).toHaveLength(1);
    expect(fresh[0].userId).toBe(7);
  });

  it('keeps a guest whose organisation differs', () => {
    const fresh = selectNewSyncCandidates(
      [internal_(7)], [guest(31, { organizationSnapshot: 'XYZ Corp' })]);
    expect(fresh).toHaveLength(1);
    expect(fresh[0].guestMemberId).toBe(31);
  });

  it('keeps a guest whose role differs', () => {
    const fresh = selectNewSyncCandidates(
      [internal_(7)], [guest(31, { roleSnapshot: 'Trưởng đoàn' })]);
    expect(fresh).toHaveLength(1);
  });

  it('never merges two guests with each other', () => {
    // Both are members of the delegation; dropping one would quietly shrink it.
    expect(selectNewSyncCandidates([], [guest(31), guest(32)])).toHaveLength(2);
  });

  it('still drops plain id duplicates', () => {
    expect(selectNewSyncCandidates([internal_(7), guest(31)], [internal_(7), guest(31)])).toEqual([]);
  });

  it('does not treat an unnamed guest as a duplicate of an internal row', () => {
    const nameless = guest(31, { fullNameSnapshot: '' });
    expect(selectNewSyncCandidates([internal_(7, { fullNameSnapshot: '' })], [nameless])).toHaveLength(1);
  });
});
