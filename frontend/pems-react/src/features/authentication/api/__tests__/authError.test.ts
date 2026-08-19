/**
 * `getAuthErrorMessage` used to suppress a raw Vietnamese `message` in EN mode but NOT the mirror
 * case — a raw English `message` under a Vietnamese UI rendered verbatim. That asymmetry is why a
 * VI login screen could show "Authentication required." / "Your session has been revoked. Please
 * sign in again." — the exact strings the (now-fixed) backend used to send with no `errorCode`.
 * These tests pin the symmetric fix and the errorCode-first priority the whole contract relies on.
 */
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import type { AxiosError } from 'axios';
import i18n, { changeLanguage } from '../../../../shared/i18n/config';
import { getAuthErrorMessage, translateErrorCode } from '../authError';

const axiosErrorWith = (data: Record<string, unknown>): AxiosError =>
  ({ response: { data, status: 401 } } as unknown as AxiosError);

describe('getAuthErrorMessage', () => {
  const originalLanguage = i18n.language;
  beforeEach(() => changeLanguage('vi'));
  afterEach(() => changeLanguage(originalLanguage as 'vi' | 'en'));

  it('prefers a known errorCode translation over the raw backend message, in VI', () => {
    changeLanguage('vi');
    const msg = getAuthErrorMessage(axiosErrorWith({ errorCode: 'UNAUTHORIZED', message: 'Authentication required.' }));
    expect(msg).toBe('Bạn cần đăng nhập để tiếp tục.');
    expect(msg).not.toContain('Authentication required');
  });

  it('prefers a known errorCode translation over the raw backend message, in EN', () => {
    changeLanguage('en');
    const msg = getAuthErrorMessage(axiosErrorWith({ errorCode: 'SESSION_REVOKED', message: 'Phiên đăng nhập đã bị thu hồi.' }));
    expect(msg).toBe('Your session has been revoked. Please sign in again.');
  });

  it('FE-AUTH-01: INVALID_CREDENTIALS localizes correctly in VI', () => {
    changeLanguage('vi');
    const msg = getAuthErrorMessage(axiosErrorWith({ errorCode: 'INVALID_CREDENTIALS', message: 'Invalid email or password.' }));
    expect(msg).toBe('Email hoặc mật khẩu không đúng.');
  });

  it('FE-AUTH-02: an unlocalized raw English message is NOT shown verbatim under VI — falls back', () => {
    changeLanguage('vi');
    // Simulates a 401 whose body genuinely has no errorCode (the pre-fix shape from
    // SessionValidationMiddleware / JwtBearer's OnChallenge).
    const msg = getAuthErrorMessage(
      axiosErrorWith({ message: 'Authentication required.' }),
      'Đăng nhập thất bại. Vui lòng thử lại.',
    );
    expect(msg).not.toBe('Authentication required.');
    expect(msg).toBe('Đăng nhập thất bại. Vui lòng thử lại.');
  });

  it('FE-AUTH-02b: same as above for the SessionValidationMiddleware wording', () => {
    changeLanguage('vi');
    const msg = getAuthErrorMessage(
      axiosErrorWith({ message: 'Your session has been revoked. Please sign in again.' }),
      'Đăng nhập thất bại. Vui lòng thử lại.',
    );
    expect(msg).not.toContain('Your session has been revoked');
    expect(msg).toBe('Đăng nhập thất bại. Vui lòng thử lại.');
  });

  it('a raw Vietnamese message with no errorCode is still shown as-is under VI', () => {
    changeLanguage('vi');
    const msg = getAuthErrorMessage(axiosErrorWith({ message: 'Tài khoản chưa xác nhận email.' }));
    expect(msg).toBe('Tài khoản chưa xác nhận email.');
  });

  it('FE-AUTH-03: EN mode shows English and suppresses a raw Vietnamese message (existing guard, unchanged)', () => {
    changeLanguage('en');
    const msg = getAuthErrorMessage(
      axiosErrorWith({ message: 'Tài khoản chưa xác nhận email.' }),
      'Login failed. Please try again.',
    );
    expect(msg).toBe('Login failed. Please try again.');
  });

  it('falls back to the generic default when there is nothing usable and no fallback given', () => {
    changeLanguage('vi');
    const msg = getAuthErrorMessage(axiosErrorWith({}));
    expect(msg).toBe(i18n.t('common.defaultError', { ns: 'toast' }));
  });
});

describe('translateErrorCode', () => {
  it('returns undefined for an unknown code (safe fallback, no crash)', () => {
    changeLanguage('vi');
    expect(translateErrorCode('SOME_CODE_THE_FRONTEND_HAS_NEVER_HEARD_OF')).toBeUndefined();
  });

  it('returns undefined for an empty/missing code', () => {
    expect(translateErrorCode(undefined)).toBeUndefined();
    expect(translateErrorCode('')).toBeUndefined();
  });
});
