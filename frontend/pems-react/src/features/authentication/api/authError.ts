import { AxiosError } from 'axios';
import i18n from '../../../shared/i18n/config';
import { maskSecrets } from '../../../shared/utils/toast';

interface ApiErrorBody {
  message?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
}

/**
 * Backend auth/domain error codes that have a localized message under `errors:api.<CODE>`.
 * Must stay in sync with backend AuthErrorCodes. The codes themselves are technical
 * identifiers and are never translated — only the message shown to the user is.
 */
export const KNOWN_AUTH_ERROR_CODES = [
  'CAMPUS_REQUIRED',
  'CAMPUS_MISMATCH',
  'PORTAL_MISMATCH',
  'INTERNAL_ACCOUNT_NOT_FOUND',
  'PASSWORD_LOGIN_DISABLED',
  'INVALID_CREDENTIALS',
  'ACCOUNT_INACTIVE',
  'ACCOUNT_LOCKED',
  'DEPARTMENT_INACTIVE',
  'SSO_DISABLED',
  'FEID_DISABLED',
  'FEID_NOT_CONFIGURED',
  'FEID_NOT_ELIGIBLE',
  'EXTERNAL_AUTH_FAILED',
  'VISITOR_PROVISION_DISABLED',
  'SESSION_REVOKED',
  'TOKEN_EXPIRED',
  'UNAUTHORIZED',
  'INTERNAL_SERVER_ERROR',

  // Campus Management (UC-82/83/86)
  'CAMPUS_MANAGEMENT_FORBIDDEN',
  'CAMPUS_NOT_FOUND',
  'INVALID_CAMPUS_STATUS',
  'CAMPUS_ACTIVATION_MASTER_DATA_INCOMPLETE',
  'CAMPUS_ACTIVATION_MISSING_IC_DEPARTMENT',
  'CAMPUS_CODE_ALREADY_EXISTS',
  'CAMPUS_NAME_ALREADY_EXISTS',
  'CAMPUS_ADDRESS_ALREADY_EXISTS',
  'CAMPUS_PHONE_ALREADY_EXISTS',
  'CAMPUS_EMAIL_ALREADY_EXISTS',

  // Profile self-service (UC-14 / UC-15)
  'PROFILE_FORBIDDEN_FIELD',
  'PROFILE_FULLNAME_REQUIRED',
  'PROFILE_FULLNAME_TOO_LONG',
  'PROFILE_GENDER_INVALID',
  'PROFILE_PHONE_INVALID',
  'PROFILE_NATIONALITY_TOO_LONG',

  // Public visit request (UC-17)
  'CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT',
  'VISITOR_ACCOUNT_INACTIVE',
] as const;

/** Vietnamese-specific diacritics — used to detect an untranslated backend message. */
const VIETNAMESE_CHARS = /[àáâãèéêìíòóôõùúăđĩũơưạ-ỹ]/i;

/** Localized message for a backend `errorCode`, or undefined when the code is unknown. */
export function translateErrorCode(errorCode?: string): string | undefined {
  if (!errorCode?.trim()) return undefined;
  const key = `errors:api.${errorCode.trim()}`;
  return i18n.exists(key) ? (i18n.t(key) as string) : undefined;
}

/**
 * Extracts a safe, user-facing message from an API error. Prefers the localized message
 * for a known errorCode, then the backend message, then field errors, then a
 * network/generic fallback. Never surfaces stack traces or internal details.
 *
 * In EN mode a raw Vietnamese backend `message` is suppressed in favour of the generic
 * fallback, so an English screen never mixes in Vietnamese (i18n requirements §8.1).
 */
export function getAuthErrorMessage(error: unknown, fallback?: string): string {
  const axiosError = error as AxiosError<ApiErrorBody>;
  const body = axiosError?.response?.data;

  const translated = translateErrorCode(body?.errorCode);
  if (translated) return translated;

  const isEnglish = i18n.language?.startsWith('en');
  const usable = (text?: string): boolean =>
    !!text && !!text.trim() && !(isEnglish && VIETNAMESE_CHARS.test(text));

  if (usable(body?.message)) return maskSecrets(body!.message!);

  if (body?.errors) {
    const first = Object.values(body.errors).flat()[0];
    if (usable(first)) return maskSecrets(first);
  }

  if (axiosError?.code === 'ERR_NETWORK') {
    return i18n.t('common.networkError', { ns: 'toast' }) as string;
  }

  return fallback ?? (i18n.t('common.defaultError', { ns: 'toast' }) as string);
}
