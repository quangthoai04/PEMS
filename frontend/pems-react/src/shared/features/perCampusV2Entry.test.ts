import { describe, expect, it } from 'vitest';
import {
  resolvePublicVisitEntry,
  resolveAuthenticatedCreateEntry,
  V2_PUBLIC_REGISTRATION_PATH,
  V2_AUTHENTICATED_CREATE_PATH,
} from './perCampusV2Entry';

describe('perCampusV2Entry decision', () => {
  it('public CTA → v2 route when enabled', () => {
    expect(resolvePublicVisitEntry(true)).toEqual({ kind: 'v2-route', to: V2_PUBLIC_REGISTRATION_PATH });
  });

  it('public CTA → v1 popup when disabled', () => {
    expect(resolvePublicVisitEntry(false)).toEqual({ kind: 'v1-popup' });
  });

  it('authenticated create → v2 route when enabled', () => {
    expect(resolveAuthenticatedCreateEntry(true)).toEqual({ kind: 'v2-route', to: V2_AUTHENTICATED_CREATE_PATH });
  });

  it('authenticated create → v1 popup when disabled', () => {
    expect(resolveAuthenticatedCreateEntry(false)).toEqual({ kind: 'v1-popup' });
  });

  it('canonical paths match the app routes', () => {
    expect(V2_PUBLIC_REGISTRATION_PATH).toBe('/visit-registration/v2');
    expect(V2_AUTHENTICATED_CREATE_PATH).toBe('/visit/create-v2');
  });
});
