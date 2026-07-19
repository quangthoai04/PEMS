/**
 * Shared identity validation for every HO account flow: the "Khởi tạo tài khoản mới" modal, the
 * "Chỉnh sửa thông tin tài khoản" modal and "Thay thế Trưởng phòng IC" (mode CREATE_NEW_USER).
 *
 * This is a UX aid only — the backend (`AccountIdentityRules.cs`) is the source of truth and must
 * reject the same payloads. Keep the two files in sync; the messages below are byte-identical.
 */

export const ACCOUNT_FULL_NAME_MIN_LENGTH = 2;
export const ACCOUNT_FULL_NAME_MAX_LENGTH = 150;
export const ACCOUNT_EMAIL_MAX_LENGTH = 150;
export const ACCOUNT_EMAIL_LOCAL_PART_MAX_LENGTH = 64;
export const REPLACEMENT_REASON_MIN_LENGTH = 10;
export const REPLACEMENT_REASON_MAX_LENGTH = 500;

/** Exact (post-lowercase) domains accepted as a PEMS login email. No subdomains. */
export const ALLOWED_ACCOUNT_EMAIL_DOMAINS = ['gmail.com', 'fpt.edu.vn', 'fe.edu.vn'] as const;

export const ACCOUNT_IDENTITY_MESSAGES = {
  fullNameRequired: 'Vui lòng nhập họ và tên.',
  fullNameTooShort: 'Họ và tên phải có ít nhất 2 ký tự.',
  fullNameTooLong: 'Họ và tên không được vượt quá 150 ký tự.',
  fullNameInvalidChars:
    'Họ và tên chỉ được chứa chữ cái, khoảng trắng, dấu chấm, dấu nháy đơn và dấu gạch nối.',
  emailRequired: 'Vui lòng nhập email.',
  emailFormat: 'Email không đúng định dạng.',
  emailTooLong: 'Email không được vượt quá 150 ký tự.',
  emailLocalPartTooLong: 'Phần tên email trước ký tự @ không được vượt quá 64 ký tự.',
  emailPlusNotAllowed: 'Email dùng để đăng nhập không được chứa dấu cộng (+).',
  emailDomainNotAllowed:
    'Email phải sử dụng một trong các tên miền: @gmail.com, @fpt.edu.vn hoặc @fe.edu.vn.',
  reasonRequired: 'Vui lòng nhập lý do thay thế.',
  reasonTooShort: 'Lý do thay thế phải có ít nhất 10 ký tự.',
  reasonTooLong: 'Lý do thay thế không được vượt quá 500 ký tự.',
  reasonNotMeaningful: 'Lý do thay thế phải chứa nội dung có ý nghĩa.',
} as const;

/** Field-level errors shared by the create / edit modals. */
export type AccountIdentityFieldErrors = {
  fullName?: string;
  email?: string;
};

// Punctuation tolerated inside a person's name (O'Connor, D’Arcy, Jean-Luc, J. Smith).
const NAME_PUNCTUATION = "-'’.";
// Control characters that are NOT whitespace - whitespace is collapsed first, never deleted.
const CONTROL_CHARS_PATTERN = '[\\u0000-\\u0008\\u000B\\u000C\\u000E-\\u001F\\u007F]';
const LETTER_RE = /\p{L}/u;
const NON_SPACING_MARK_RE = /\p{Mn}/u;
const EMAIL_LOCAL_PART_RE = /^[a-z0-9._-]+$/;
const EMAIL_DOMAIN_RE = /^[a-z0-9.-]+$/;

/**
 * Strips control characters, collapses whitespace runs into one space and trims. Casing, diacritics
 * and name punctuation are preserved verbatim.
 */
export function normalizeFullName(value?: string | null): string {
  if (value == null) return '';
  // Whitespace is collapsed FIRST so a tab/newline becomes a separator, not a deletion.
  return value
    .replace(/\s+/g, ' ')
    .replace(new RegExp(CONTROL_CHARS_PATTERN, 'g'), '')
    .trim();
}

/** Trims and lowercases. The local-part is never rewritten. */
export function normalizeAccountEmail(value?: string | null): string {
  if (value == null) return '';
  return value.trim().toLowerCase();
}

/** Trims and collapses whitespace runs for a free-text reason. */
export function normalizeReplacementReason(value?: string | null): string {
  return normalizeFullName(value);
}

/**
 * True when the NORMALIZED name holds only letters, spaces and name punctuation, contains at least
 * one letter, and has no runs of repeated punctuation ("---", "..." → false).
 */
export function isValidFullName(value: string): boolean {
  if (!value) return false;

  let hasLetter = false;
  let previousWasPunctuation = false;
  for (const ch of value) {
    if (LETTER_RE.test(ch)) {
      hasLetter = true;
      previousWasPunctuation = false;
      continue;
    }
    // Combining marks (decomposed Vietnamese tone marks) ride along with their letter.
    if (NON_SPACING_MARK_RE.test(ch)) {
      previousWasPunctuation = false;
      continue;
    }
    if (ch === ' ') {
      previousWasPunctuation = false;
      continue;
    }
    if (NAME_PUNCTUATION.includes(ch)) {
      if (previousWasPunctuation) return false;
      previousWasPunctuation = true;
      continue;
    }
    return false; // digits, emoji, <, >, @, / … all rejected
  }

  return hasLetter;
}

function splitEmail(email: string): { local: string; domain: string } | null {
  const at = email.indexOf('@');
  if (at <= 0 || at !== email.lastIndexOf('@')) return null;
  return { local: email.slice(0, at), domain: email.slice(at + 1) };
}

/** Exact domain match — never a suffix check, so subdomains and look-alikes fail. */
export function hasAllowedEmailDomain(email: string): boolean {
  const parts = splitEmail(email);
  return !!parts && (ALLOWED_ACCOUNT_EMAIL_DOMAINS as readonly string[]).includes(parts.domain);
}

export function containsPlusAddressing(email: string): boolean {
  return email.includes('+');
}

/** Structural check of a NORMALIZED email. Domain whitelisting is checked separately. */
function hasValidEmailShape(email: string): boolean {
  if (!email) return false;
  if (/\s/.test(email) || new RegExp(CONTROL_CHARS_PATTERN).test(email)) return false;

  const parts = splitEmail(email);
  if (!parts) return false;

  const { local, domain } = parts;
  if (!local || local.length > ACCOUNT_EMAIL_LOCAL_PART_MAX_LENGTH) return false;
  if (local.startsWith('.') || local.endsWith('.') || local.includes('..')) return false;
  if (!EMAIL_LOCAL_PART_RE.test(local)) return false;

  if (!domain || domain.length > 253) return false;
  if (domain.startsWith('.') || domain.endsWith('.') || domain.includes('..')) return false;
  if (!domain.includes('.')) return false;
  return EMAIL_DOMAIN_RE.test(domain);
}

/** Returns the first violated message for the NORMALIZED name, or null when acceptable. */
export function validateFullName(value?: string | null): string | null {
  const normalized = normalizeFullName(value);
  if (normalized.length === 0) return ACCOUNT_IDENTITY_MESSAGES.fullNameRequired;
  if (normalized.length < ACCOUNT_FULL_NAME_MIN_LENGTH) return ACCOUNT_IDENTITY_MESSAGES.fullNameTooShort;
  if (normalized.length > ACCOUNT_FULL_NAME_MAX_LENGTH) return ACCOUNT_IDENTITY_MESSAGES.fullNameTooLong;
  return isValidFullName(normalized) ? null : ACCOUNT_IDENTITY_MESSAGES.fullNameInvalidChars;
}

/**
 * Returns the first violated message for the NORMALIZED email, or null when acceptable.
 * Uniqueness is not checked here — only the backend can answer that.
 */
export function validateAccountEmail(value?: string | null): string | null {
  const normalized = normalizeAccountEmail(value);
  if (normalized.length === 0) return ACCOUNT_IDENTITY_MESSAGES.emailRequired;
  if (normalized.length > ACCOUNT_EMAIL_MAX_LENGTH) return ACCOUNT_IDENTITY_MESSAGES.emailTooLong;
  if (containsPlusAddressing(normalized)) return ACCOUNT_IDENTITY_MESSAGES.emailPlusNotAllowed;

  const parts = splitEmail(normalized);
  if (parts && parts.local.length > ACCOUNT_EMAIL_LOCAL_PART_MAX_LENGTH) {
    return ACCOUNT_IDENTITY_MESSAGES.emailLocalPartTooLong;
  }
  if (!hasValidEmailShape(normalized)) return ACCOUNT_IDENTITY_MESSAGES.emailFormat;
  return hasAllowedEmailDomain(normalized) ? null : ACCOUNT_IDENTITY_MESSAGES.emailDomainNotAllowed;
}

/** Replacement reason: required, 10–500 chars and not just punctuation. */
export function validateReplacementReason(value?: string | null): string | null {
  const normalized = normalizeReplacementReason(value);
  if (normalized.length === 0) return ACCOUNT_IDENTITY_MESSAGES.reasonRequired;
  if (normalized.length < REPLACEMENT_REASON_MIN_LENGTH) return ACCOUNT_IDENTITY_MESSAGES.reasonTooShort;
  if (normalized.length > REPLACEMENT_REASON_MAX_LENGTH) return ACCOUNT_IDENTITY_MESSAGES.reasonTooLong;
  return /[\p{L}\p{N}]/u.test(normalized) ? null : ACCOUNT_IDENTITY_MESSAGES.reasonNotMeaningful;
}
