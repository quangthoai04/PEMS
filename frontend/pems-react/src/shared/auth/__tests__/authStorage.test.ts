/**
 * FE-AUTH-05 (auth 401 / stale session audit): clearing an expired/revoked auth session must
 * never take the user's language choice down with it. `pems.language` is read by i18n's own
 * `getInitialLanguage()` independently of anything in `authStorage`, so a VI user who gets
 * force-logged-out (session revoked, campus deactivated, refresh failed) and lands back on the
 * login screen must still see it in Vietnamese.
 */
import { describe, expect, it, afterEach } from 'vitest';
import { authStorage } from '../authStorage';
import type { AuthUser } from '../../../features/authentication/types/authentication.types';

const user: AuthUser = {
  userId: '1',
  fullName: 'Nguyen Van A',
  email: 'a@fpt.edu.vn',
  roleCode: 'STAFF',
  subRole: 'LEADER',
  mustChangePassword: false,
  mustSetPassword: false,
  effectiveRole: 'STAFF',
  status: 'ACTIVE',
};

describe('authStorage.clear()', () => {
  afterEach(() => localStorage.clear());

  it('never removes the user-selected language', () => {
    localStorage.setItem('pems.language', 'vi');
    authStorage.setTokens('access-token', 'refresh-token');
    authStorage.setUser(user);
    authStorage.setLoginPortal('INTERNAL');
    authStorage.setSelectedCampusId('1');

    authStorage.clear();

    expect(localStorage.getItem('pems.language')).toBe('vi');
  });

  it('does remove every auth-specific key', () => {
    authStorage.setTokens('access-token', 'refresh-token');
    authStorage.setUser(user);
    authStorage.setLoginPortal('INTERNAL');
    authStorage.setSelectedCampusId('1');

    authStorage.clear();

    expect(authStorage.getAccessToken()).toBeNull();
    expect(authStorage.getRefreshToken()).toBeNull();
    expect(authStorage.getUser()).toBeNull();
    expect(authStorage.getLoginPortal()).toBeNull();
    expect(authStorage.getSelectedCampusId()).toBeNull();
    expect(localStorage.getItem('currentUser')).toBeNull();
  });
});
