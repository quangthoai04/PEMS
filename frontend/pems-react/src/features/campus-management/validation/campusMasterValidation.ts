/**
 * Shared master-data validation for both HO campus flows: the "Thêm mới campus" modal on the
 * campus list page and the inline edit form on the campus detail page. Create and edit MUST use
 * this module — never a local regex — so one can never accept what the other rejects.
 *
 * This is a UX aid only — the backend (`CampusMasterRules.cs`) is the source of truth and must
 * reject the same payloads. Keep the two files in sync; the messages below are byte-identical.
 */

import { CAMPUS_PROVINCES } from '../constants';

export const CAMPUS_CODE_MIN_LENGTH = 2;
export const CAMPUS_CODE_MAX_LENGTH = 20;
export const CAMPUS_NAME_MIN_LENGTH = 3;
export const CAMPUS_NAME_MAX_LENGTH = 150;
export const CAMPUS_CITY_MAX_LENGTH = 100;
export const CAMPUS_ADDRESS_MIN_LENGTH = 5;
export const CAMPUS_ADDRESS_MAX_LENGTH = 255;
export const CAMPUS_PHONE_MAX_LENGTH = 30;
export const CAMPUS_PHONE_MIN_DIGITS = 8;
export const CAMPUS_PHONE_MAX_DIGITS = 15;
export const CAMPUS_EMAIL_MAX_LENGTH = 150;
export const CAMPUS_EMAIL_LOCAL_PART_MAX_LENGTH = 64;

/** Exact (post-lowercase) domains accepted as a campus contact email. No subdomains, no Gmail. */
export const ALLOWED_CAMPUS_EMAIL_DOMAINS = ['fpt.edu.vn', 'fe.edu.vn'] as const;

export const CAMPUS_MASTER_MESSAGES = {
  codeRequired: 'Vui lòng nhập mã campus.',
  codeTooShort: 'Mã campus phải có ít nhất 2 ký tự.',
  codeTooLong: 'Mã campus không được vượt quá 20 ký tự.',
  codeInvalidChars:
    'Mã campus chỉ được chứa chữ cái không dấu, chữ số, dấu gạch ngang hoặc gạch dưới.',
  codeSeparatorEdge: 'Mã campus không được bắt đầu hoặc kết thúc bằng dấu phân cách.',
  codeConsecutiveSeparator: 'Mã campus không được chứa các dấu phân cách liên tiếp.',
  codeAlreadyExists: 'Mã campus đã tồn tại.',

  nameRequired: 'Vui lòng nhập tên campus.',
  nameTooShort: 'Tên campus phải có ít nhất 3 ký tự.',
  nameTooLong: 'Tên campus không được vượt quá 150 ký tự.',
  nameNotMeaningful: 'Tên campus phải chứa ít nhất một chữ cái.',
  nameInvalidChars: 'Tên campus chứa ký tự không hợp lệ.',
  nameAlreadyExists: 'Tên campus đã tồn tại.',

  cityRequired: 'Vui lòng chọn tỉnh/thành phố.',
  cityNotAllowed: 'Tỉnh/thành phố được chọn không hợp lệ.',

  addressRequired: 'Vui lòng nhập địa chỉ.',
  addressTooShort: 'Địa chỉ phải có ít nhất 5 ký tự.',
  addressTooLong: 'Địa chỉ không được vượt quá 255 ký tự.',
  addressNotMeaningful: 'Địa chỉ phải chứa thông tin có ý nghĩa.',
  addressInvalidChars: 'Địa chỉ chứa ký tự không hợp lệ.',
  addressAlreadyExists: 'Địa chỉ này đã được sử dụng cho campus khác.',

  phoneRequired: 'Vui lòng nhập số điện thoại.',
  phoneDigitCount: 'Số điện thoại phải có từ 8 đến 15 chữ số.',
  phoneFormat: 'Số điện thoại không đúng định dạng.',
  phonePlusPlacement: 'Dấu + chỉ được đặt ở đầu số điện thoại.',
  phoneTooLong: 'Số điện thoại không được vượt quá 30 ký tự.',
  phoneAlreadyExists: 'Số điện thoại này đã được sử dụng cho campus khác.',

  emailRequired: 'Vui lòng nhập email.',
  emailFormat: 'Email không đúng định dạng.',
  emailTooLong: 'Email không được vượt quá 150 ký tự.',
  emailLocalPartTooLong: 'Phần tên email trước ký tự @ không được vượt quá 64 ký tự.',
  emailPlusNotAllowed: 'Email liên hệ campus không được chứa dấu cộng (+).',
  emailDomainNotAllowed: 'Email campus phải sử dụng tên miền @fpt.edu.vn hoặc @fe.edu.vn.',
  emailAlreadyExists: 'Email này đã được sử dụng cho campus khác.',
} as const;

export type CampusMasterForm = {
  campusCode: string;
  name: string;
  city: string;
  address: string;
  phone: string;
  email: string;
};

export type CampusMasterFieldErrors = Partial<Record<keyof CampusMasterForm, string>>;

/** Separators allowed inside a campus code, but never doubled or at either end. */
const CODE_SEPARATORS = '-_';
/** Punctuation tolerated in a campus name: "FPT Education (Hòa Lạc)", "D'Or & Co.". */
const NAME_PUNCTUATION = "-.'’&(),";
/** Punctuation tolerated in a street address: "Lô E2a-7, Đường D1", "#12/3". */
const ADDRESS_PUNCTUATION = ",.-/()'’#";
/** Characters a phone number may be written with. */
const PHONE_PUNCTUATION = '+ ().-';

// Control characters that are NOT whitespace — whitespace is collapsed first, never deleted.
const CONTROL_CHARS_PATTERN = '[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]';
const LETTER_RE = /\p{L}/u;
const NON_SPACING_MARK_RE = /\p{Mn}/u;
const DIGIT_RE = /[0-9]/;
const CODE_CHAR_RE = /[A-Z0-9]/;
const EMAIL_LOCAL_PART_RE = /^[a-z0-9._-]+$/;
const EMAIL_DOMAIN_RE = /^[a-z0-9.-]+$/;

/** Drops control characters, collapses every whitespace run into one space, then trims. */
function collapseWhitespace(value?: string | null): string {
  if (value == null) return '';
  // Whitespace is collapsed FIRST so a tab/newline becomes a separator, not a deletion.
  return value.replace(/\s+/g, ' ').replace(new RegExp(CONTROL_CHARS_PATTERN, 'g'), '').trim();
}

// ────────────────────────────── Normalizers (spec §3) ──────────────────────────────

/** Trim + uppercase. Separators are never rewritten: "fpt-hn" → "FPT-HN". */
export function normalizeCampusCode(value?: string | null): string {
  return (value ?? '').trim().toUpperCase();
}

/** Trim + collapse spaces. Casing and Vietnamese diacritics are preserved verbatim. */
export function normalizeCampusName(value?: string | null): string {
  return collapseWhitespace(value);
}

/**
 * Trim + collapse, then map onto the canonical province spelling. Values outside the whitelist
 * come back trimmed but unchanged, so a legacy city stays comparable to what the backend stored.
 */
export function normalizeCampusCity(value?: string | null): string {
  const trimmed = collapseWhitespace(value);
  const canonical = CAMPUS_PROVINCES.find((p) => p.toLowerCase() === trimmed.toLowerCase());
  return canonical ?? trimmed;
}

/** Trim + collapse spaces + drop control characters; newlines become spaces. */
export function normalizeCampusAddress(value?: string | null): string {
  return collapseWhitespace(value);
}

/** Trim + collapse spaces, keeping the user's separators: "(024)   7300  5588" → "(024) 7300 5588". */
export function normalizeCampusPhoneDisplay(value?: string | null): string {
  return collapseWhitespace(value);
}

/**
 * Canonical key for duplicate comparison: drop spaces, dots, hyphens and parentheses, then fold
 * "+84" onto the domestic "0". "024 7300 5588", "024-7300-5588", "(024) 7300.5588" and
 * "+84 24 7300 5588" all collapse to "02473005588".
 */
export function normalizeCampusPhoneKey(value?: string | null): string {
  const compact = (value ?? '').replace(/[\s.\-()]/g, '');
  // Only the "+84" form is folded; a bare leading "84" is ambiguous and rejected by the prefix rule.
  return compact.startsWith('+84') ? `0${compact.slice(3)}` : compact;
}

/** Trim + lowercase. The local-part is never rewritten. */
export function normalizeCampusEmail(value?: string | null): string {
  return (value ?? '').trim().toLowerCase();
}

/** Applies every field normalizer — the shape actually sent to the API. */
export function normalizeCampusMasterForm(form: CampusMasterForm): CampusMasterForm {
  return {
    campusCode: normalizeCampusCode(form.campusCode),
    name: normalizeCampusName(form.name),
    city: normalizeCampusCity(form.city),
    address: normalizeCampusAddress(form.address),
    phone: normalizeCampusPhoneDisplay(form.phone),
    email: normalizeCampusEmail(form.email),
  };
}

// ────────────────────────────── Predicates ──────────────────────────────

const isCodeSeparator = (ch: string) => CODE_SEPARATORS.includes(ch);

/** Only A–Z, 0–9, '-' and '_' — diacritics and spaces are excluded by construction. */
export function hasCodeCharsOnly(value: string): boolean {
  return value.length > 0 && [...value].every((ch) => CODE_CHAR_RE.test(ch) || isCodeSeparator(ch));
}

export function hasConsecutiveCodeSeparators(value: string): boolean {
  for (let i = 1; i < value.length; i += 1) {
    if (isCodeSeparator(value[i]) && isCodeSeparator(value[i - 1])) return true;
  }
  return false;
}

/** True when the value holds at least one letter (rejects "123", "..."). */
export function isMeaningfulText(value: string): boolean {
  return LETTER_RE.test(value);
}

/**
 * True when every character is a Unicode letter, a combining mark (decomposed Vietnamese tone
 * marks), a digit, a space, or one of `punctuation`. HTML tags and emoji fail here.
 */
function hasAllowedCharsOnly(value: string, punctuation: string): boolean {
  return [...value].every(
    (ch) =>
      LETTER_RE.test(ch) ||
      DIGIT_RE.test(ch) ||
      ch === ' ' ||
      NON_SPACING_MARK_RE.test(ch) ||
      punctuation.includes(ch),
  );
}

export const hasNameCharsOnly = (value: string) => hasAllowedCharsOnly(value, NAME_PUNCTUATION);
export const hasAddressCharsOnly = (value: string) => hasAllowedCharsOnly(value, ADDRESS_PUNCTUATION);

export function isAllowedCampusCity(value: string): boolean {
  return CAMPUS_PROVINCES.some((p) => p.toLowerCase() === value.toLowerCase());
}

/** Digits, '+', spaces, parentheses, '.' and '-' only — letters and extensions fail. */
export function hasPhoneCharsOnly(value: string): boolean {
  return value.length > 0 && [...value].every((ch) => DIGIT_RE.test(ch) || PHONE_PUNCTUATION.includes(ch));
}

/** At most one '+', and only as the very first character. */
export function hasValidPlusPlacement(value: string): boolean {
  const first = value.indexOf('+');
  return first < 0 || (first === 0 && value.lastIndexOf('+') === 0);
}

/** True when the canonical key is a Vietnamese number: domestic "0…" or the folded "+84…". */
export function hasVietnamesePrefix(value: string): boolean {
  return normalizeCampusPhoneKey(value).startsWith('0');
}

export function countDigits(value: string): number {
  return (value.match(/\d/g) ?? []).length;
}

function splitEmail(email: string): { local: string; domain: string } | null {
  const at = email.indexOf('@');
  if (at <= 0 || at !== email.lastIndexOf('@')) return null;
  return { local: email.slice(0, at), domain: email.slice(at + 1) };
}

/** Exact domain match — never a suffix check, so subdomains and look-alikes fail. */
export function hasAllowedCampusEmailDomain(email: string): boolean {
  const parts = splitEmail(email);
  return !!parts && (ALLOWED_CAMPUS_EMAIL_DOMAINS as readonly string[]).includes(parts.domain);
}

/** Structural check of a NORMALIZED email. Domain whitelisting is checked separately. */
function hasValidEmailShape(email: string): boolean {
  if (!email) return false;
  if (/\s/.test(email) || new RegExp(CONTROL_CHARS_PATTERN).test(email)) return false;

  const parts = splitEmail(email);
  if (!parts) return false;

  const { local, domain } = parts;
  if (!local || local.length > CAMPUS_EMAIL_LOCAL_PART_MAX_LENGTH) return false;
  if (local.startsWith('.') || local.endsWith('.') || local.includes('..')) return false;
  if (!EMAIL_LOCAL_PART_RE.test(local)) return false;

  if (!domain || domain.length > 253) return false;
  if (domain.startsWith('.') || domain.endsWith('.') || domain.includes('..')) return false;
  if (!domain.includes('.')) return false;
  return EMAIL_DOMAIN_RE.test(domain);
}

// ────────────────────────────── Field validators (spec §15) ──────────────────────────────

/** Returns the first violated message for the NORMALIZED value, or null when acceptable. */
export function validateCampusCode(value?: string | null): string | null {
  const M = CAMPUS_MASTER_MESSAGES;
  const normalized = normalizeCampusCode(value);
  if (normalized.length === 0) return M.codeRequired;
  if (normalized.length < CAMPUS_CODE_MIN_LENGTH) return M.codeTooShort;
  if (normalized.length > CAMPUS_CODE_MAX_LENGTH) return M.codeTooLong;
  if (!hasCodeCharsOnly(normalized)) return M.codeInvalidChars;
  if (isCodeSeparator(normalized[0]) || isCodeSeparator(normalized[normalized.length - 1])) {
    return M.codeSeparatorEdge;
  }
  return hasConsecutiveCodeSeparators(normalized) ? M.codeConsecutiveSeparator : null;
}

export function validateCampusName(value?: string | null): string | null {
  const M = CAMPUS_MASTER_MESSAGES;
  const normalized = normalizeCampusName(value);
  if (normalized.length === 0) return M.nameRequired;
  if (normalized.length < CAMPUS_NAME_MIN_LENGTH) return M.nameTooShort;
  if (normalized.length > CAMPUS_NAME_MAX_LENGTH) return M.nameTooLong;
  if (!isMeaningfulText(normalized)) return M.nameNotMeaningful;
  return hasNameCharsOnly(normalized) ? null : M.nameInvalidChars;
}

export function validateCampusCity(value?: string | null): string | null {
  const M = CAMPUS_MASTER_MESSAGES;
  const normalized = normalizeCampusCity(value);
  if (normalized.length === 0) return M.cityRequired;
  return isAllowedCampusCity(normalized) ? null : M.cityNotAllowed;
}

export function validateCampusAddress(value?: string | null): string | null {
  const M = CAMPUS_MASTER_MESSAGES;
  const normalized = normalizeCampusAddress(value);
  if (normalized.length === 0) return M.addressRequired;
  if (normalized.length < CAMPUS_ADDRESS_MIN_LENGTH) return M.addressTooShort;
  if (normalized.length > CAMPUS_ADDRESS_MAX_LENGTH) return M.addressTooLong;
  if (!isMeaningfulText(normalized)) return M.addressNotMeaningful;
  return hasAddressCharsOnly(normalized) ? null : M.addressInvalidChars;
}

export function validateCampusPhone(value?: string | null): string | null {
  const M = CAMPUS_MASTER_MESSAGES;
  const normalized = normalizeCampusPhoneDisplay(value);
  if (normalized.length === 0) return M.phoneRequired;
  if (normalized.length > CAMPUS_PHONE_MAX_LENGTH) return M.phoneTooLong;
  if (!hasPhoneCharsOnly(normalized)) return M.phoneFormat;
  // Checked before the digit count so "84+2473005588" reports the specific '+' message.
  if (!hasValidPlusPlacement(normalized)) return M.phonePlusPlacement;

  const digits = countDigits(normalized);
  if (digits < CAMPUS_PHONE_MIN_DIGITS || digits > CAMPUS_PHONE_MAX_DIGITS) return M.phoneDigitCount;
  return hasVietnamesePrefix(normalized) ? null : M.phoneFormat;
}

export function validateCampusEmail(value?: string | null): string | null {
  const M = CAMPUS_MASTER_MESSAGES;
  const normalized = normalizeCampusEmail(value);
  if (normalized.length === 0) return M.emailRequired;
  if (normalized.length > CAMPUS_EMAIL_MAX_LENGTH) return M.emailTooLong;
  if (normalized.includes('+')) return M.emailPlusNotAllowed;

  const parts = splitEmail(normalized);
  if (parts && parts.local.length > CAMPUS_EMAIL_LOCAL_PART_MAX_LENGTH) return M.emailLocalPartTooLong;
  if (!hasValidEmailShape(normalized)) return M.emailFormat;
  return hasAllowedCampusEmailDomain(normalized) ? null : M.emailDomainNotAllowed;
}

/** Per-field validator lookup, so a form can validate a single field on blur. */
export const CAMPUS_FIELD_VALIDATORS: Record<
  keyof CampusMasterForm,
  (value?: string | null) => string | null
> = {
  campusCode: validateCampusCode,
  name: validateCampusName,
  city: validateCampusCity,
  address: validateCampusAddress,
  phone: validateCampusPhone,
  email: validateCampusEmail,
};

/** Validates every field; an empty object means the form may be submitted. */
export function validateCampusMasterForm(form: CampusMasterForm): CampusMasterFieldErrors {
  const errors: CampusMasterFieldErrors = {};
  (Object.keys(CAMPUS_FIELD_VALIDATORS) as (keyof CampusMasterForm)[]).forEach((field) => {
    const message = CAMPUS_FIELD_VALIDATORS[field](form[field]);
    if (message) errors[field] = message;
  });
  return errors;
}

export function isCampusMasterFormValid(form: CampusMasterForm): boolean {
  return Object.keys(validateCampusMasterForm(form)).length === 0;
}

/**
 * Per-field dirty check on NORMALIZED values (spec §12.2): re-typing the same city in a different
 * case, or adding stray spaces, is NOT a change. Phone compares by canonical key so "+84 24 7300
 * 5588" does not look like an edit of "024 7300 5588".
 */
export function isCampusMasterFormDirty(form: CampusMasterForm, baseline: CampusMasterForm): boolean {
  const a = normalizeCampusMasterForm(form);
  const b = normalizeCampusMasterForm(baseline);
  return (
    a.campusCode !== b.campusCode ||
    a.name !== b.name ||
    a.city.toLowerCase() !== b.city.toLowerCase() ||
    a.address !== b.address ||
    normalizeCampusPhoneKey(a.phone) !== normalizeCampusPhoneKey(b.phone) ||
    a.email !== b.email
  );
}
