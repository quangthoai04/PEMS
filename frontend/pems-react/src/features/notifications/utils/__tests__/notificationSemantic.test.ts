import { describe, expect, it } from 'vitest';
import { parseNotificationSemantic } from '../notificationSemantic';

describe('parseNotificationSemantic', () => {
  it('parses a valid eventKey + params payload', () => {
    expect(parseNotificationSemantic(JSON.stringify({ eventKey: 'HOST_ASSIGNED', params: { hostName: 'A' } })))
      .toEqual({ eventKey: 'HOST_ASSIGNED', params: { hostName: 'A' } });
  });

  it('defaults params to {} when absent', () => {
    expect(parseNotificationSemantic(JSON.stringify({ eventKey: 'HOST_ASSIGNED' })))
      .toEqual({ eventKey: 'HOST_ASSIGNED', params: {} });
  });

  it('returns null for invalid JSON', () => {
    expect(parseNotificationSemantic('{not valid json')).toBeNull();
  });

  it('returns null when eventKey is missing', () => {
    expect(parseNotificationSemantic(JSON.stringify({ params: {} }))).toBeNull();
  });

  it('returns null for null/undefined/empty input (legacy row with no metadata)', () => {
    expect(parseNotificationSemantic(null)).toBeNull();
    expect(parseNotificationSemantic(undefined)).toBeNull();
    expect(parseNotificationSemantic('')).toBeNull();
  });

  it('does not filter by a known-eventKey allowlist — that is the caller’s job', () => {
    // resolveNotificationPresentation applies its OWN KNOWN_EVENT_KEYS filter on top of this; the
    // shared parser itself must stay a pure structural read so navigation (which recognizes a
    // different/larger set of keys) is never silently starved by presentation's allowlist.
    expect(parseNotificationSemantic(JSON.stringify({ eventKey: 'SOME_FUTURE_EVENT', params: {} })))
      .toEqual({ eventKey: 'SOME_FUTURE_EVENT', params: {} });
  });
});
