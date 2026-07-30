/**
 * The one frontend definition of what a PEMS **login email** is.
 *
 * Every screen that creates or edits an account's login address validates through this module: HO
 * account management, "Thay thế Trưởng phòng IC", and the Department Leader personnel modals. It
 * exists because the rule had already been copied twice and the copies drifted — the Department
 * Leader modal was still offering `@fe.edu.vn` months after the account-management screen had
 * dropped it, which meant the same address was accepted on one screen and refused by the server on
 * the next.
 *
 * Mirror of the backend `AccountIdentityRules` (Accounts/Common). This is a UX aid only: the server
 * re-validates every payload and is the authority. A value that slips past this file still fails
 * there, and a message shown here that the server disagrees with is a bug in this file — not a
 * permission the user gained.
 */

/** Exact (post-lowercase) domains accepted as a PEMS login email. No subdomains, no look-alikes. */
export const ALLOWED_LOGIN_EMAIL_DOMAINS = ['gmail.com', 'fpt.edu.vn'] as const;

export type AllowedLoginEmailDomain = (typeof ALLOWED_LOGIN_EMAIL_DOMAINS)[number];

export const LOGIN_EMAIL_MAX_LENGTH = 150;
export const LOGIN_EMAIL_LOCAL_PART_MAX_LENGTH = 64;
const LOGIN_EMAIL_DOMAIN_MAX_LENGTH = 253;

/** Byte-identical to the backend's messages — the two sides must never disagree on wording. */
export const LOGIN_EMAIL_MESSAGES = {
  required: 'Vui lòng nhập email.',
  invalidFormat: 'Email không đúng định dạng.',
  tooLong: 'Email không được vượt quá 150 ký tự.',
  localPartTooLong: 'Phần tên email trước ký tự @ không được vượt quá 64 ký tự.',
  plusNotAllowed: 'Email dùng để đăng nhập không được chứa dấu cộng (+).',
  domainNotAllowed: 'Chỉ chấp nhận @gmail.com và @fpt.edu.vn.',
} as const;

const LOCAL_PART_RE = /^[a-z0-9._-]+$/;
const DOMAIN_RE = /^[a-z0-9.-]+$/;

/**
 * True when the value holds a C0/C7F control character. Written against code points rather than a
 * character-class regex so the source file itself never has to contain a literal control character.
 */
function hasControlCharacter(value: string): boolean {
  for (const ch of value) {
    const code = ch.codePointAt(0) ?? 0;
    if (code < 32 || code === 127) return true;
  }
  return false;
}

/**
 * Trims and lowercases. Nothing else: the local-part is never rewritten, a plus is never stripped,
 * and a wrong domain is never "corrected" into an allowed one. Silently repairing an address would
 * provision the account under an address the operator never typed.
 */
export function normalizeLoginEmail(value?: string | null): string {
  if (value == null) return '';
  return value.trim().toLowerCase();
}

/** Splits on the single '@', or null when the address does not have exactly one. */
export function splitLoginEmail(email: string): { local: string; domain: string } | null {
  const at = email.indexOf('@');
  if (at <= 0 || at !== email.lastIndexOf('@')) return null;
  return { local: email.slice(0, at), domain: email.slice(at + 1) };
}

/**
 * Exact domain match against the whitelist — never `endsWith`, never `includes`. A suffix check
 * would accept `fake-fpt.edu.vn`, and a substring check would accept `fpt.edu.vn.evil.com`; both
 * are addresses at a domain the organisation does not control.
 */
export function hasAllowedLoginEmailDomain(email: string): boolean {
  const parts = splitLoginEmail(email);
  return !!parts && (ALLOWED_LOGIN_EMAIL_DOMAINS as readonly string[]).includes(parts.domain);
}

/** Plus addressing is refused outright: one person must map to one login identity. */
export function containsPlusAddressing(email: string): boolean {
  return email.includes('+');
}

/** Structural check of a NORMALIZED address. Domain whitelisting is a separate, later step. */
export function hasValidLoginEmailShape(email: string): boolean {
  if (!email) return false;
  if (/\s/.test(email) || hasControlCharacter(email)) return false;

  const parts = splitLoginEmail(email);
  if (!parts) return false;

  const { local, domain } = parts;
  if (!local || local.length > LOGIN_EMAIL_LOCAL_PART_MAX_LENGTH) return false;
  if (local.startsWith('.') || local.endsWith('.') || local.includes('..')) return false;
  if (!LOCAL_PART_RE.test(local)) return false;

  if (!domain || domain.length > LOGIN_EMAIL_DOMAIN_MAX_LENGTH) return false;
  if (domain.startsWith('.') || domain.endsWith('.') || domain.includes('..')) return false;
  if (!domain.includes('.')) return false;
  return DOMAIN_RE.test(domain);
}

/**
 * Returns the first violated message for the NORMALIZED address, or null when it is acceptable.
 *
 * The order matters and matches the backend exactly (required → too long → plus → local-part too
 * long → shape → domain), so the operator sees the same reason from either side. Uniqueness is
 * absent on purpose: only the server can answer it.
 */
export function validateLoginEmail(value?: string | null): string | null {
  const normalized = normalizeLoginEmail(value);
  if (normalized.length === 0) return LOGIN_EMAIL_MESSAGES.required;
  if (normalized.length > LOGIN_EMAIL_MAX_LENGTH) return LOGIN_EMAIL_MESSAGES.tooLong;
  if (containsPlusAddressing(normalized)) return LOGIN_EMAIL_MESSAGES.plusNotAllowed;

  const parts = splitLoginEmail(normalized);
  if (parts && parts.local.length > LOGIN_EMAIL_LOCAL_PART_MAX_LENGTH) {
    return LOGIN_EMAIL_MESSAGES.localPartTooLong;
  }
  if (!hasValidLoginEmailShape(normalized)) return LOGIN_EMAIL_MESSAGES.invalidFormat;

  return hasAllowedLoginEmailDomain(normalized) ? null : LOGIN_EMAIL_MESSAGES.domainNotAllowed;
}

/**
 * True when `message` is one of the server's login-email refusals. Used to attach a backend
 * rejection to the email field instead of dropping it into a generic toast.
 */
export function isLoginEmailMessage(message: string): boolean {
  return (Object.values(LOGIN_EMAIL_MESSAGES) as readonly string[]).includes(message.trim());
}
