import type { PersonnelGender } from '../types/departmentLeaderPersonnel.types';
import {
  ALLOWED_LOGIN_EMAIL_DOMAINS,
  LOGIN_EMAIL_LOCAL_PART_MAX_LENGTH,
  LOGIN_EMAIL_MAX_LENGTH,
  normalizeLoginEmail,
  validateLoginEmail,
} from '../../../shared/validation/loginEmailValidation';

/**
 * Client-side mirror of the backend personnel rules (`AccountIdentityRules`,
 * `DepartmentPersonnelPhoneRules`, `DepartmentPersonnelGenders`).
 *
 * This exists ONLY to give the operator immediate feedback while typing. The backend re-validates
 * every field on the normalized value and is the authority — a payload that slips past this file
 * still fails there, and a message shown here that the server disagrees with is a bug in this file,
 * not a permission the user gained.
 *
 * The login-email rule is NOT restated here. It is imported from
 * `shared/validation/loginEmailValidation`, which the HO account-management screen uses too: this
 * file used to carry its own copy of the whitelist, and that copy kept `@fe.edu.vn` long after the
 * organisation had stopped accepting it — the modal promised an address the server then refused.
 */

export const FULL_NAME_MIN_LENGTH = 2;
export const FULL_NAME_MAX_LENGTH = 150;
export const EMAIL_MAX_LENGTH = LOGIN_EMAIL_MAX_LENGTH;
export const EMAIL_LOCAL_PART_MAX_LENGTH = LOGIN_EMAIL_LOCAL_PART_MAX_LENGTH;
export const PHONE_MAX_LENGTH = 30;
export const PHONE_MIN_DIGITS = 8;
export const PHONE_MAX_DIGITS = 15;

/**
 * Re-exported from the shared module rather than declared, so this screen cannot drift away from
 * the rest of the product again.
 */
export const ALLOWED_EMAIL_DOMAINS = ALLOWED_LOGIN_EMAIL_DOMAINS;

/** Collapses whitespace runs and trims — same normalization the backend applies before validating. */
export function normalizeFullName(value: string): string {
  return value.replace(/\s+/g, ' ').trim();
}

/** Trims and lowercases. The local-part is never otherwise rewritten. */
export function normalizeEmail(value: string): string {
  return normalizeLoginEmail(value);
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

/**
 * The login-email rule, in full, from the shared module: required, ≤150 chars, no plus addressing,
 * local-part ≤64 with no leading/trailing/doubled dot, and an EXACT match on `gmail.com` or
 * `fpt.edu.vn`. Identical in the create and the edit modal, and identical in every account status —
 * a status decides what an email change costs, never which addresses are legal.
 */
export function validateEmail(rawValue: string): string | null {
  return validateLoginEmail(rawValue);
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
