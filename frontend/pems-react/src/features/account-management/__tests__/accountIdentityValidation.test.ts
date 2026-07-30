import { describe, it, expect } from 'vitest';
import {
  ACCOUNT_EMAIL_LOCAL_PART_MAX_LENGTH,
  ACCOUNT_FULL_NAME_MAX_LENGTH,
  ACCOUNT_IDENTITY_MESSAGES as M,
  hasAllowedEmailDomain,
  normalizeAccountEmail,
  normalizeFullName,
  validateAccountEmail,
  validateFullName,
  validateReplacementReason,
} from '../validation/accountIdentityValidation';

/**
 * Mirrors tests/PEMS.UnitTests/Accounts/Common/AccountIdentityRulesTests.cs — frontend and backend
 * must accept and reject exactly the same values with the same messages. If one side changes, this
 * file and its C# twin should change together.
 */
describe('normalizeFullName', () => {
  it.each([
    ['  Nguyễn   Văn   An  ', 'Nguyễn Văn An'],
    ['Trần\tMinh\nAnh', 'Trần Minh Anh'],
    ["O'Connor", "O'Connor"],
    ['Jean-Luc Picard', 'Jean-Luc Picard'],
    ['nguyễn văn an', 'nguyễn văn an'],
    [null, ''],
    ['   ', ''],
  ])('normalizes %j to %j', (input, expected) => {
    expect(normalizeFullName(input)).toBe(expected);
  });
});

describe('normalizeAccountEmail', () => {
  it.each([
    ['  User.Name@FPT.EDU.VN  ', 'user.name@fpt.edu.vn'],
    ['  USER@FPT.EDU.VN ', 'user@fpt.edu.vn'],
    [null, ''],
  ])('normalizes %j to %j', (input, expected) => {
    expect(normalizeAccountEmail(input)).toBe(expected);
  });
});

describe('validateFullName', () => {
  it.each([
    'Nguyễn Văn An',
    'An',
    'Li',
    'Jean-Luc Picard',
    "O'Connor",
    'D’Arcy',
    'J. Smith',
    'Đỗ Thị Hồng',
    '  Nguyễn   Văn   An  ',
  ])('accepts %j', (value) => {
    expect(validateFullName(value)).toBeNull();
  });

  it.each([
    ['', M.fullNameRequired],
    ['   ', M.fullNameRequired],
    ['A', M.fullNameTooShort],
    ['Nguyễn Văn 123', M.fullNameInvalidChars],
    ['<script>alert(1)</script>', M.fullNameInvalidChars],
    ['😊 Nguyễn Văn An', M.fullNameInvalidChars],
    ['...', M.fullNameInvalidChars],
    ['---', M.fullNameInvalidChars],
    ['@@@', M.fullNameInvalidChars],
  ])('rejects %j', (value, expected) => {
    expect(validateFullName(value)).toBe(expected);
  });

  it('rejects more than 150 characters and accepts exactly 150', () => {
    expect(validateFullName('a'.repeat(ACCOUNT_FULL_NAME_MAX_LENGTH + 1))).toBe(M.fullNameTooLong);
    expect(validateFullName('a'.repeat(ACCOUNT_FULL_NAME_MAX_LENGTH))).toBeNull();
  });
});

describe('validateAccountEmail', () => {
  it.each([
    'user@gmail.com',
    'user.name@gmail.com',
    'user_name-2@gmail.com',
    'user@fpt.edu.vn',
    'USER@FPT.EDU.VN',
    '  User@Gmail.Com  ',
  ])('accepts %j', (value) => {
    expect(validateAccountEmail(value)).toBeNull();
  });

  it.each([
    ['', M.emailRequired],
    ['abc', M.emailFormat],
    ['abc@', M.emailFormat],
    ['@gmail.com', M.emailFormat],
    ['abc..def@gmail.com', M.emailFormat],
    ['.abc@gmail.com', M.emailFormat],
    ['abc.@gmail.com', M.emailFormat],
    ['abc @gmail.com', M.emailFormat],
    ['Nguyen Van A <abc@gmail.com>', M.emailFormat],
    ['abc+test@gmail.com', M.emailPlusNotAllowed],
    ['abc@yahoo.com', M.emailDomainNotAllowed],
    ['abc@outlook.com', M.emailDomainNotAllowed],
    ['abc@fpt.com.vn', M.emailDomainNotAllowed],
    ['abc@student.fpt.edu.vn', M.emailDomainNotAllowed],
    ['abc@gmail.com.example.com', M.emailDomainNotAllowed],
    ['abc@fakefpt.edu.vn', M.emailDomainNotAllowed],
    // Dropped from the allowlist — HO accounts now log in with @gmail.com or @fpt.edu.vn only.
    ['abc@fe.edu.vn', M.emailDomainNotAllowed],
    ['abc@edu.vn', M.emailDomainNotAllowed],
    ['abc@sub.gmail.com', M.emailDomainNotAllowed],
    ['abc@gmail.com.vn', M.emailDomainNotAllowed],
    ['abc@fpt.edu.vn.evil.com', M.emailDomainNotAllowed],
  ])('rejects %j', (value, expected) => {
    expect(validateAccountEmail(value)).toBe(expected);
  });

  it('states both allowed domains, and only those, in the message', () => {
    expect(M.emailDomainNotAllowed).toBe('Chỉ chấp nhận @gmail.com và @fpt.edu.vn.');
    expect(M.emailDomainNotAllowed).not.toContain('fe.edu.vn');
  });

  it('enforces the 64-character local-part limit', () => {
    const tooLong = `${'a'.repeat(ACCOUNT_EMAIL_LOCAL_PART_MAX_LENGTH + 1)}@gmail.com`;
    expect(validateAccountEmail(tooLong)).toBe(M.emailLocalPartTooLong);
    expect(validateAccountEmail(`${'a'.repeat(ACCOUNT_EMAIL_LOCAL_PART_MAX_LENGTH)}@gmail.com`)).toBeNull();
  });

  it('enforces the 150-character total limit', () => {
    expect(validateAccountEmail(`${'a'.repeat(64)}.${'b'.repeat(90)}@gmail.com`)).toBe(M.emailTooLong);
  });

  it('matches the domain exactly, never as a suffix', () => {
    expect(hasAllowedEmailDomain('a@fpt.edu.vn')).toBe(true);
    expect(hasAllowedEmailDomain('a@sub.fpt.edu.vn')).toBe(false);
    expect(hasAllowedEmailDomain('a@fpt.edu.vn.evil.com')).toBe(false);
  });
});

describe('validateReplacementReason', () => {
  it('accepts a meaningful reason', () => {
    expect(
      validateReplacementReason('Điều chuyển nhân sự phụ trách Phòng Hợp tác Quốc tế từ tháng 8/2026.'),
    ).toBeNull();
  });

  it.each([
    ['', M.reasonRequired],
    ['   ', M.reasonRequired],
    ['abc', M.reasonTooShort],
    ['-----', M.reasonTooShort],
    ['..............', M.reasonNotMeaningful],
    ['--- --- --- ---', M.reasonNotMeaningful],
    ['a'.repeat(501), M.reasonTooLong],
  ])('rejects %j', (value, expected) => {
    expect(validateReplacementReason(value)).toBe(expected);
  });
});
