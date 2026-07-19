import { describe, it, expect } from 'vitest';
import {
  CAMPUS_MASTER_MESSAGES as M,
  hasAllowedCampusEmailDomain,
  isCampusMasterFormDirty,
  normalizeCampusCity,
  normalizeCampusCode,
  normalizeCampusEmail,
  normalizeCampusName,
  normalizeCampusPhoneDisplay,
  normalizeCampusPhoneKey,
  validateCampusAddress,
  validateCampusCity,
  validateCampusCode,
  validateCampusEmail,
  validateCampusMasterForm,
  validateCampusName,
  validateCampusPhone,
} from '../validation/campusMasterValidation';
import type { CampusMasterForm } from '../validation/campusMasterValidation';

/**
 * Mirrors tests/PEMS.UnitTests/Campuses/CampusMasterRulesTests.cs — frontend and backend must
 * accept and reject exactly the same values with the same messages. If one side changes, this
 * file and its C# twin should change together.
 */

// ── §3 Normalization ────────────────────────────────────────────────────────

describe('normalizers', () => {
  it.each([
    ['  hp  ', 'HP'],
    ['fpt-hn', 'FPT-HN'],
    [null, ''],
  ])('normalizeCampusCode(%j) → %j', (input, expected) => {
    expect(normalizeCampusCode(input)).toBe(expected);
  });

  it.each([
    ['  FPT   University   Hải Phòng ', 'FPT University Hải Phòng'],
    ['FPT\tUniversity\nHà Nội', 'FPT University Hà Nội'],
    ['fpt university', 'fpt university'], // casing never rewritten
    [null, ''],
  ])('normalizeCampusName(%j) → %j', (input, expected) => {
    expect(normalizeCampusName(input)).toBe(expected);
  });

  it.each([
    ['  hà nội ', 'Hà Nội'], // mapped onto the canonical spelling
    ['Đà Nẵng', 'Đà Nẵng'],
    ['  Vùng đất lạ  ', 'Vùng đất lạ'], // outside the whitelist: trimmed, not rewritten
  ])('normalizeCampusCity(%j) → %j', (input, expected) => {
    expect(normalizeCampusCity(input)).toBe(expected);
  });

  it.each([
    ['  HP@FPT.EDU.VN ', 'hp@fpt.edu.vn'],
    [null, ''],
  ])('normalizeCampusEmail(%j) → %j', (input, expected) => {
    expect(normalizeCampusEmail(input)).toBe(expected);
  });

  it('keeps the user’s phone separators while collapsing spaces', () => {
    expect(normalizeCampusPhoneDisplay('(024)   7300  5588')).toBe('(024) 7300 5588');
  });

  // §3.6 / §8.5 — every spelling of the same number collapses to one canonical key.
  it.each(['024 7300 5588', '024-7300-5588', '(024) 7300.5588', '+84 24 7300 5588'])(
    'normalizeCampusPhoneKey(%j) → "02473005588"',
    (input) => {
      expect(normalizeCampusPhoneKey(input)).toBe('02473005588');
    },
  );
});

// ── §4 Campus code ──────────────────────────────────────────────────────────

describe('validateCampusCode', () => {
  it.each(['HN', 'HCM', 'HP', 'DN-2', 'FPT_HN', 'CAMPUS01', ' hp '])('accepts %j', (input) => {
    expect(validateCampusCode(input)).toBeNull();
  });

  it.each([
    ['', M.codeRequired],
    ['   ', M.codeRequired],
    ['H', M.codeTooShort],
    ['Hà Nội', M.codeInvalidChars],
    ['H N', M.codeInvalidChars],
    ['HN@01', M.codeInvalidChars],
    ['-HN', M.codeSeparatorEdge],
    ['HN-', M.codeSeparatorEdge],
    ['_HN', M.codeSeparatorEdge],
    ['HN_', M.codeSeparatorEdge],
    ['HN__2', M.codeConsecutiveSeparator],
    ['HN--2', M.codeConsecutiveSeparator],
    ['HN-_2', M.codeConsecutiveSeparator],
  ])('rejects %j with the spec message', (input, expected) => {
    expect(validateCampusCode(input)).toBe(expected);
  });

  it('rejects codes over 20 characters', () => {
    expect(validateCampusCode('A'.repeat(21))).toBe(M.codeTooLong);
  });
});

// ── §5 Campus name ──────────────────────────────────────────────────────────

describe('validateCampusName', () => {
  it.each([
    'FPT University Hà Nội',
    'FPT University Hải Phòng',
    'FPT Campus 2',
    'FPT Education (Hòa Lạc)',
    'FPT Polytechnic - Đà Nẵng',
    'Đại học FPT, cơ sở Hòa Lạc',
    '  FPT   University   Hải Phòng ',
  ])('accepts %j', (input) => {
    expect(validateCampusName(input)).toBeNull();
  });

  it.each([
    ['', M.nameRequired],
    ['A', M.nameTooShort],
    ['12', M.nameTooShort],
    ['123', M.nameNotMeaningful],
    ['...', M.nameNotMeaningful],
    ['<script>alert(1)</script>', M.nameInvalidChars],
    ['😊😊😊', M.nameNotMeaningful],
  ])('rejects %j with the spec message', (input, expected) => {
    expect(validateCampusName(input)).toBe(expected);
  });

  it('rejects names over 150 characters', () => {
    expect(validateCampusName('a'.repeat(151))).toBe(M.nameTooLong);
  });
});

// ── §6 City ─────────────────────────────────────────────────────────────────

describe('validateCampusCity', () => {
  it.each(['Hà Nội', 'TP. Hồ Chí Minh', 'hà nội'])('accepts whitelisted %j', (input) => {
    expect(validateCampusCity(input)).toBeNull();
  });

  it.each([
    ['', M.cityRequired],
    ['   ', M.cityRequired],
    ['Hà Nội City', M.cityNotAllowed],
    ['Bắc Giang', M.cityNotAllowed], // merged away in 2025
    ['<script>', M.cityNotAllowed],
  ])('rejects %j', (input, expected) => {
    expect(validateCampusCity(input)).toBe(expected);
  });
});

// ── §7 Address ──────────────────────────────────────────────────────────────

describe('validateCampusAddress', () => {
  it.each([
    'Khu Giáo dục và Đào tạo, Khu Công nghệ cao Hòa Lạc, Hà Nội',
    'Lô E2a-7, Đường D1, Khu Công nghệ cao, TP. Hồ Chí Minh',
    '25 Nguyễn Văn Linh, Hải Châu, Đà Nẵng',
    'Km 29 Đại lộ Thăng Long, Thạch Thất, Hà Nội',
    '25 Nguyễn Văn Linh,\nĐà Nẵng', // newline collapses to a space
  ])('accepts %j', (input) => {
    expect(validateCampusAddress(input)).toBeNull();
  });

  it.each([
    ['', M.addressRequired],
    ['25sđ', M.addressTooShort],
    ['12345', M.addressNotMeaningful],
    ['.....', M.addressNotMeaningful],
    ['<script>', M.addressInvalidChars],
  ])('rejects %j with the spec message', (input, expected) => {
    expect(validateCampusAddress(input)).toBe(expected);
  });

  it('rejects addresses over 255 characters', () => {
    expect(validateCampusAddress('a'.repeat(256))).toBe(M.addressTooLong);
  });
});

// ── §8 Phone ────────────────────────────────────────────────────────────────

describe('validateCampusPhone', () => {
  it.each(['024 7300 5588', '024-7300-5588', '(024) 7300 5588', '+84 24 7300 5588', '0918271611'])(
    'accepts %j',
    (input) => {
      expect(validateCampusPhone(input)).toBeNull();
    },
  );

  it.each([
    ['', M.phoneRequired],
    ['1234567', M.phoneDigitCount], // 7 digits
    ['0123456789012345', M.phoneDigitCount], // 16 digits
    ['024ABC5588', M.phoneFormat],
    ['024 7300 5588 ext 123', M.phoneFormat],
    ['84+2473005588', M.phonePlusPlacement],
    ['++84 24 7300 5588', M.phonePlusPlacement],
    ['+1 202 555 0173', M.phoneFormat], // not a VN number
    ['1900 1234', M.phoneFormat], // no leading 0 / +84
  ])('rejects %j with the spec message', (input, expected) => {
    expect(validateCampusPhone(input)).toBe(expected);
  });

  it('rejects display values over 30 characters', () => {
    expect(validateCampusPhone('0'.repeat(31))).toBe(M.phoneTooLong);
  });
});

// ── §9 Email ────────────────────────────────────────────────────────────────

describe('validateCampusEmail', () => {
  it.each(['hn@fpt.edu.vn', 'campus.hp@fpt.edu.vn', 'contact.qn@fe.edu.vn', '  HP@FPT.EDU.VN '])(
    'accepts %j',
    (input) => {
      expect(validateCampusEmail(input)).toBeNull();
    },
  );

  it.each([
    ['', M.emailRequired],
    ['abc@gmail.com', M.emailDomainNotAllowed],
    ['abc@yahoo.com', M.emailDomainNotAllowed],
    ['abc@student.fpt.edu.vn', M.emailDomainNotAllowed],
    ['abc@fpt.edu.vn.fake.com', M.emailDomainNotAllowed],
    ['abc@fakefpt.edu.vn', M.emailDomainNotAllowed],
    ['abc+test@fpt.edu.vn', M.emailPlusNotAllowed],
    ['abc..def@fpt.edu.vn', M.emailFormat],
    ['.abc@fpt.edu.vn', M.emailFormat],
    ['abc.@fpt.edu.vn', M.emailFormat],
    ['abc@@fpt.edu.vn', M.emailFormat],
    ['abc fpt@fpt.edu.vn', M.emailFormat],
  ])('rejects %j with the spec message', (input, expected) => {
    expect(validateCampusEmail(input)).toBe(expected);
  });

  it('rejects a local-part over 64 characters', () => {
    expect(validateCampusEmail(`${'a'.repeat(65)}@fpt.edu.vn`)).toBe(M.emailLocalPartTooLong);
  });

  it('rejects addresses over 150 characters', () => {
    expect(validateCampusEmail(`${'a'.repeat(140)}@fpt.edu.vn`)).toBe(M.emailTooLong);
  });

  // §9.5 — the domain must be matched exactly, never with includes/endsWith.
  it('matches the domain exactly, not as a suffix', () => {
    expect(hasAllowedCampusEmailDomain('hn@fpt.edu.vn')).toBe(true);
    expect(hasAllowedCampusEmailDomain('hn@sub.fpt.edu.vn')).toBe(false);
    expect(hasAllowedCampusEmailDomain('hn@xfpt.edu.vn')).toBe(false);
    expect(hasAllowedCampusEmailDomain('hn@fpt.edu.vn.evil.com')).toBe(false);
  });
});

// ── Form-level helpers ──────────────────────────────────────────────────────

const validForm: CampusMasterForm = {
  campusCode: 'HN',
  name: 'FPT University Hà Nội',
  city: 'Hà Nội',
  address: 'Km 29 Đại lộ Thăng Long, Thạch Thất, Hà Nội',
  phone: '024 7300 5588',
  email: 'hn@fpt.edu.vn',
};

describe('validateCampusMasterForm', () => {
  it('reports no error for a well-formed form', () => {
    expect(validateCampusMasterForm(validForm)).toEqual({});
  });

  it('reports every offending field at once', () => {
    const errors = validateCampusMasterForm({
      ...validForm,
      campusCode: '-HN',
      email: 'abc@gmail.com',
      phone: '1234567',
    });

    expect(errors).toEqual({
      campusCode: M.codeSeparatorEdge,
      phone: M.phoneDigitCount,
      email: M.emailDomainNotAllowed,
    });
  });
});

// §12.2 / AC-06 — dirty is decided on normalized values.
describe('isCampusMasterFormDirty', () => {
  it('is false when nothing changed', () => {
    expect(isCampusMasterFormDirty(validForm, validForm)).toBe(false);
  });

  it.each([
    ['lowercased code', { campusCode: 'hn' }],
    ['extra spaces in the name', { name: '  FPT   University   Hà Nội ' }],
    ['differently cased city', { city: 'hà nội' }],
    ['extra spaces in the address', { address: 'Km 29  Đại lộ Thăng Long, Thạch Thất, Hà Nội ' }],
    ['the same phone written internationally', { phone: '+84 24 7300 5588' }],
    ['an uppercased email', { email: 'HN@FPT.EDU.VN' }],
  ])('is false for %s', (_label, patch) => {
    expect(isCampusMasterFormDirty({ ...validForm, ...patch }, validForm)).toBe(false);
  });

  it.each([
    ['a different code', { campusCode: 'HN2' }],
    ['a different name', { name: 'FPT University Hà Nội 2' }],
    ['a different city', { city: 'Đà Nẵng' }],
    ['a different address', { address: '25 Nguyễn Văn Linh, Hải Châu, Đà Nẵng' }],
    ['a different phone', { phone: '024 7300 5589' }],
    ['a different email', { email: 'hn2@fpt.edu.vn' }],
  ])('is true for %s', (_label, patch) => {
    expect(isCampusMasterFormDirty({ ...validForm, ...patch }, validForm)).toBe(true);
  });
});
