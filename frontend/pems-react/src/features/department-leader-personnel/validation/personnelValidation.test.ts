import { describe, expect, it } from 'vitest';
import {
  ALLOWED_EMAIL_DOMAINS,
  normalizeEmail,
  validateEmail,
  validatePersonnelForm,
} from './personnelValidation';
import { validateAccountEmail } from '../../account-management/validation/accountIdentityValidation';
import { LOGIN_EMAIL_MESSAGES } from '../../../shared/validation/loginEmailValidation';

/**
 * The login-email contract for the Department Leader personnel modals (spec §4, §17.1).
 *
 * Twin of tests/PEMS.UnitTests/Accounts/Common/AccountIdentityRulesTests.cs — the frontend and the
 * backend must accept and refuse exactly the same addresses with exactly the same wording. If one
 * side changes, this file and its C# twin change together.
 */

const DOMAIN_MESSAGE = 'Chỉ chấp nhận @gmail.com và @fpt.edu.vn.';

describe('ALLOWED_EMAIL_DOMAINS', () => {
  it('is exactly gmail.com and fpt.edu.vn', () => {
    expect([...ALLOWED_EMAIL_DOMAINS]).toEqual(['gmail.com', 'fpt.edu.vn']);
  });

  it('no longer contains fe.edu.vn', () => {
    expect(ALLOWED_EMAIL_DOMAINS as readonly string[]).not.toContain('fe.edu.vn');
  });
});

describe('normalizeEmail', () => {
  it.each([
    ['  User.Name@FPT.EDU.VN  ', 'user.name@fpt.edu.vn'],
    ['USER@GMAIL.COM', 'user@gmail.com'],
    ['   ', ''],
  ])('normalizes %j to %j', (input, expected) => {
    expect(normalizeEmail(input)).toBe(expected);
  });

  it('never rewrites the local-part — a plus is kept so it can be REPORTED, not silently removed', () => {
    expect(normalizeEmail('user+tag@gmail.com')).toBe('user+tag@gmail.com');
  });

  it('never repairs a disallowed domain into an allowed one', () => {
    expect(normalizeEmail('user@fe.edu.vn')).toBe('user@fe.edu.vn');
  });
});

describe('validateEmail — accepted', () => {
  it.each([
    'user@gmail.com',
    'user@fpt.edu.vn',
    'USER@GMAIL.COM',
    'USER@FPT.EDU.VN',
    '  user@gmail.com  ',
    'user.name@fpt.edu.vn',
    'user_name-01@gmail.com',
  ])('accepts %j', (value) => {
    expect(validateEmail(value)).toBeNull();
  });
});

describe('validateEmail — refused', () => {
  it.each([
    // The domain this change removes.
    ['user@fe.edu.vn', DOMAIN_MESSAGE],
    ['USER@FE.EDU.VN', DOMAIN_MESSAGE],
    // Plain outsiders.
    ['user@yahoo.com', DOMAIN_MESSAGE],
    ['user@outlook.com', DOMAIN_MESSAGE],
    // Subdomains — an exact match refuses these; endsWith/includes would not.
    ['user@student.fpt.edu.vn', DOMAIN_MESSAGE],
    ['user@mail.gmail.com', DOMAIN_MESSAGE],
    // Look-alikes: suffixed, prefixed and wrapped.
    ['user@gmail.com.vn', DOMAIN_MESSAGE],
    ['user@fpt.edu.vn.evil.com', DOMAIN_MESSAGE],
    ['user@fake-fpt.edu.vn', DOMAIN_MESSAGE],
    ['user@gmail.com.evil.org', DOMAIN_MESSAGE],
    // Structural rules that survive the domain change.
    ['user+tag@gmail.com', LOGIN_EMAIL_MESSAGES.plusNotAllowed],
    ['user..name@gmail.com', LOGIN_EMAIL_MESSAGES.invalidFormat],
    ['.user@gmail.com', LOGIN_EMAIL_MESSAGES.invalidFormat],
    ['user.@gmail.com', LOGIN_EMAIL_MESSAGES.invalidFormat],
    ['user@@gmail.com', LOGIN_EMAIL_MESSAGES.invalidFormat],
    ['@gmail.com', LOGIN_EMAIL_MESSAGES.invalidFormat],
    ['user gmail.com', LOGIN_EMAIL_MESSAGES.invalidFormat],
    ['', LOGIN_EMAIL_MESSAGES.required],
    ['   ', LOGIN_EMAIL_MESSAGES.required],
  ])('refuses %j with the right message', (value, message) => {
    expect(validateEmail(value)).toBe(message);
  });

  it('refuses a local-part over 64 characters', () => {
    expect(validateEmail(`${'a'.repeat(65)}@gmail.com`)).toBe(LOGIN_EMAIL_MESSAGES.localPartTooLong);
  });

  it('accepts a local-part of exactly 64 characters', () => {
    expect(validateEmail(`${'a'.repeat(64)}@gmail.com`)).toBeNull();
  });

  it('refuses an address over 150 characters before anything else', () => {
    expect(validateEmail(`${'a'.repeat(200)}@gmail.com`)).toBe(LOGIN_EMAIL_MESSAGES.tooLong);
  });

  it('reports the domain, not the format, for a well-formed address at the wrong domain', () => {
    // Regression guard: the message the operator reads must name the actual problem.
    expect(validateEmail('nhansu@fe.edu.vn')).toBe(DOMAIN_MESSAGE);
  });
});

/**
 * The reason the shared module exists. These two validators are reached from different screens and
 * used to hold private copies of the whitelist; the Department Leader copy kept `fe.edu.vn` after
 * account management had dropped it. Any future divergence fails here.
 */
describe('contract: the Department Leader and HO validators agree', () => {
  it.each([
    'user@gmail.com',
    'user@fpt.edu.vn',
    'USER@GMAIL.COM',
    '  user@fpt.edu.vn  ',
    'user@fe.edu.vn',
    'user@yahoo.com',
    'user@student.fpt.edu.vn',
    'user@fpt.edu.vn.evil.com',
    'user+tag@gmail.com',
    'user..name@gmail.com',
    '',
  ])('returns an identical verdict for %j', (value) => {
    expect(validateEmail(value)).toBe(validateAccountEmail(value));
  });
});

describe('validatePersonnelForm', () => {
  const valid = {
    fullName: 'Nguyễn Văn A',
    email: 'user@gmail.com',
    phone: '0912345678',
    gender: 'MALE' as const,
  };

  it('passes a fully valid form', () => {
    expect(validatePersonnelForm(valid)).toEqual({});
  });

  it('reports the email domain without inventing errors on the other fields', () => {
    const errors = validatePersonnelForm({ ...valid, email: 'user@fe.edu.vn' });

    expect(errors.email).toBe(DOMAIN_MESSAGE);
    expect(errors.fullName).toBeUndefined();
    expect(errors.phone).toBeUndefined();
    expect(errors.gender).toBeUndefined();
  });
});
