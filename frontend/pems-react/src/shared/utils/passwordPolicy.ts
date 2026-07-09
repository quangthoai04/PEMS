// Frontend mirror of the backend password policy (PasswordPolicy.cs).
import i18n from '../i18n/config';

/**
 * Human-readable policy text. Resolved at call time (not module load) so it follows the
 * active language. Used both as helper text under the field and as the validation error.
 */
export function getPasswordRequirements(): string {
  return i18n.t('passwordPolicy', { ns: 'validation' }) as string;
}

export function isStrongPassword(password: string): boolean {
  return (
    password.length >= 8 &&
    /[A-Z]/.test(password) &&
    /[a-z]/.test(password) &&
    /[0-9]/.test(password) &&
    /[^A-Za-z0-9]/.test(password)
  );
}
