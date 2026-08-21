import { describe, expect, it } from 'vitest';
import { hasAction, hasConfirmedOperationalContact, VisitV2Action } from '../utils/visitV2Actions';

describe('hasConfirmedOperationalContact', () => {
  it('treats CONFIRMED as a confirmed holder', () => {
    expect(hasConfirmedOperationalContact('CONFIRMED')).toBe(true);
  });

  it('treats TRANSFER_PENDING as a confirmed holder too — the current contact keeps every right until the handover is accepted', () => {
    expect(hasConfirmedOperationalContact('TRANSFER_PENDING')).toBe(true);
  });

  it('does not treat PENDING as a confirmed holder', () => {
    expect(hasConfirmedOperationalContact('PENDING')).toBe(false);
  });

  it('does not treat NO_ACTIVE_INVITATION as a confirmed holder', () => {
    expect(hasConfirmedOperationalContact('NO_ACTIVE_INVITATION')).toBe(false);
  });

  it('does not treat DECLINED or EXPIRED as a confirmed holder', () => {
    expect(hasConfirmedOperationalContact('DECLINED')).toBe(false);
    expect(hasConfirmedOperationalContact('EXPIRED')).toBe(false);
  });

  it('is fail-safe for undefined/null', () => {
    expect(hasConfirmedOperationalContact(undefined)).toBe(false);
    expect(hasConfirmedOperationalContact(null)).toBe(false);
  });
});

describe('hasAction (sanity — unchanged by the handover fix)', () => {
  it('finds a granted action', () => {
    expect(hasAction([VisitV2Action.InitiateContactTransfer], VisitV2Action.InitiateContactTransfer)).toBe(true);
  });

  it('fails safe when the action is absent, undefined, or empty', () => {
    expect(hasAction([VisitV2Action.ReplaceOperationalContact], VisitV2Action.InitiateContactTransfer)).toBe(false);
    expect(hasAction(undefined, VisitV2Action.InitiateContactTransfer)).toBe(false);
    expect(hasAction([], VisitV2Action.InitiateContactTransfer)).toBe(false);
  });
});
