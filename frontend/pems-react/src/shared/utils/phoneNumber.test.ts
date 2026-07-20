import { describe, expect, it } from 'vitest';
import { isValidPhone, normalizePhone } from './phoneNumber';

/**
 * Mirrors tests/PEMS.UnitTests/Common/PhoneNumberTests.cs case for case. The two implementations
 * must agree exactly: any divergence is a value the UI accepts and the API rejects (or worse, the
 * reverse), which is how the original bypass appeared.
 */
describe('phoneNumber', () => {
  it.each(['0912345678', '+84912345678', ' 0912345678 ', '+1 202 555 0134'])(
    'accepts %s', input => expect(isValidPhone(input)).toBe(true));

  it.each([null, undefined, '', '   ', '123', '090abc123', '09999999999999999', '+84000000000'])(
    'rejects %s', input => expect(isValidPhone(input)).toBe(false));

  it.each([
    ['0912345678', '+84912345678'],
    ['+84912345678', '+84912345678'],
    [' 0912345678 ', '+84912345678'],
    ['091 234 5678', '+84912345678'],
  ])('normalizes %s to %s', (input, expected) => expect(normalizePhone(input)).toBe(expected));

  it('stores the same number identically however it was typed', () => {
    expect(normalizePhone('0912345678')).toBe(normalizePhone('+84912345678'));
  });

  it('returns null rather than a guess for an invalid number', () => {
    expect(normalizePhone('090abc123')).toBeNull();
  });
});
