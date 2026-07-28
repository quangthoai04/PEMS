import type { PersonnelGender } from '../types/departmentLeaderPersonnel.types';

/**
 * Client-side mirror of the backend personnel rules (`AccountIdentityRules`,
 * `DepartmentPersonnelPhoneRules`, `DepartmentPersonnelGenders`).
 *
 * This exists ONLY to give the operator immediate feedback while typing. The backend re-validates
 * every field on the normalized value and is the authority — a payload that slips past this file
 * still fails there, and a message shown here that the server disagrees with is a bug in this file,
 * not a permission the user gained.
 */

export const FULL_NAME_MIN_LENGTH = 2;
export const FULL_NAME_MAX_LENGTH = 150;
export const EMAIL_MAX_LENGTH = 150;
export const EMAIL_LOCAL_PART_MAX_LENGTH = 64;
export const PHONE_MAX_LENGTH = 30;
export const PHONE_MIN_DIGITS = 8;
export const PHONE_MAX_DIGITS = 15;

/** Exact domains accepted as a PEMS login email. No subdomains. */
export const ALLOWED_EMAIL_DOMAINS = ['gmail.com', 'fpt.edu.vn', 'fe.edu.vn'] as const;

/** Collapses whitespace runs and trims — same normalization the backend applies before validating. */
export function normalizeFullName(value: string): string {
  return value.replace(/\s+/g, ' ').trim();
}

/** Trims and lowercases. The local-part is never otherwise rewritten. */
export function normalizeEmail(value: string): string {
  return value.trim().toLowerCase();
}

export function normalizePhone(value: string): string {
  return value.replace(/\s+/g, ' ').trim();
}

export function validateFullName(rawValue: string): string | null {
  const value = normalizeFullName(rawValue);
  if (value.length === 0) return 'Vui lòng nhập họ và tên.';
  if (value.length < FULL_NAME_MIN_LENGTH) return 'Họ và tên phải có ít nhất 2 ký tự.';
  if (value.length > FULL_NAME_MAX_LENGTH) return 'Họ và tên không được vượt quá 150 ký tự.';

  // Letters (incl. Vietnamese), spaces and name punctuation only; at least one letter; no doubled
  // punctuation ("--", "..").
  if (!/\p{L}/u.test(value)) {
    return 'Họ và tên chỉ được chứa chữ cái, khoảng trắng, dấu chấm, dấu nháy đơn và dấu gạch nối.';
  }
  if (!/^[\p{L}\p{M}\s\-'’.]+$/u.test(value) || /[-'’.]{2}/u.test(value)) {
    return 'Họ và tên chỉ được chứa chữ cái, khoảng trắng, dấu chấm, dấu nháy đơn và dấu gạch nối.';
  }
  return null;
}

export function validateEmail(rawValue: string): string | null {
  const value = normalizeEmail(rawValue);
  if (value.length === 0) return 'Vui lòng nhập email.';
  if (value.length > EMAIL_MAX_LENGTH) return 'Email không được vượt quá 150 ký tự.';
  if (value.includes('+')) return 'Email dùng để đăng nhập không được chứa dấu cộng (+).';

  const at = value.indexOf('@');
  if (at <= 0 || at !== value.lastIndexOf('@')) return 'Email không đúng định dạng.';

  const local = value.slice(0, at);
  const domain = value.slice(at + 1);

  if (local.length > EMAIL_LOCAL_PART_MAX_LENGTH) {
    return 'Phần tên email trước ký tự @ không được vượt quá 64 ký tự.';
  }
  if (local.startsWith('.') || local.endsWith('.') || local.includes('..')) {
    return 'Email không đúng định dạng.';
  }
  if (!/^[a-z0-9._-]+$/.test(local)) return 'Email không đúng định dạng.';
  if (!/^[a-z0-9.-]+$/.test(domain) || !domain.includes('.')) return 'Email không đúng định dạng.';

  // Exact match, never endsWith — "abc@fake-fpt.edu.vn" and "abc@fpt.edu.vn.evil.com" must fail.
  if (!ALLOWED_EMAIL_DOMAINS.includes(domain as (typeof ALLOWED_EMAIL_DOMAINS)[number])) {
    return 'Email phải sử dụng một trong các tên miền: @gmail.com, @fpt.edu.vn hoặc @fe.edu.vn.';
  }
  return null;
}

export function validatePhone(rawValue: string): string | null {
  const value = normalizePhone(rawValue);
  if (value.length === 0) return 'Vui lòng nhập số điện thoại.';
  if (value.length > PHONE_MAX_LENGTH) return 'Số điện thoại không được vượt quá 30 ký tự.';
  if (!/^[0-9+ .\-()]+$/.test(value)) return 'Số điện thoại không đúng định dạng.';

  const plusIndex = value.indexOf('+');
  if (plusIndex > 0 || value.lastIndexOf('+') !== plusIndex) {
    return 'Dấu + chỉ được đặt ở đầu số điện thoại.';
  }

  const digits = (value.match(/\d/g) ?? []).length;
  if (digits < PHONE_MIN_DIGITS || digits > PHONE_MAX_DIGITS) {
    return 'Số điện thoại phải có từ 8 đến 15 chữ số.';
  }
  return null;
}

export function validateGender(value: string | null | undefined): string | null {
  return value === 'MALE' || value === 'FEMALE' || value === 'OTHER'
    ? null
    : 'Vui lòng chọn giới tính.';
}

export interface PersonnelFormValues {
  fullName: string;
  email: string;
  phone: string;
  gender: PersonnelGender | '';
}

export type PersonnelFormErrors = Partial<Record<keyof PersonnelFormValues, string>>;

/** Validates the whole form. An empty object means every field is acceptable. */
export function validatePersonnelForm(values: PersonnelFormValues): PersonnelFormErrors {
  const errors: PersonnelFormErrors = {};

  const fullName = validateFullName(values.fullName);
  if (fullName) errors.fullName = fullName;

  const email = validateEmail(values.email);
  if (email) errors.email = email;

  const phone = validatePhone(values.phone);
  if (phone) errors.phone = phone;

  const gender = validateGender(values.gender);
  if (gender) errors.gender = gender;

  return errors;
}

export function hasErrors(errors: PersonnelFormErrors): boolean {
  return Object.keys(errors).length > 0;
}
