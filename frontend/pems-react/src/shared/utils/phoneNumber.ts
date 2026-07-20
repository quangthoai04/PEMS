import { parsePhoneNumberFromString } from 'libphonenumber-js';

/**
 * The single phone rule for the app, mirroring backend `PEMS.Shared.PhoneNumber` exactly so the two
 * sides can never disagree about what a phone number is:
 *   • national Vietnamese form, e.g. "0912345678" (parsed with default region VN);
 *   • full international form, e.g. "+84912345678".
 * Letters, too-short/too-long values, numbers the metadata says cannot exist, and extensions are
 * all rejected. The stored form is always E.164, so one person is never saved under two spellings.
 */
const DEFAULT_REGION = 'VN' as const;

/** E.164 for a valid number, or null when the input is not a usable phone number. */
export function normalizePhone(input: string | null | undefined): string | null {
  if (!input || !input.trim()) return null;
  const trimmed = input.trim();
  try {
    // A leading '+' carries its own country code; anything else is treated as Vietnamese.
    const parsed = trimmed.startsWith('+')
      ? parsePhoneNumberFromString(trimmed)
      : parsePhoneNumberFromString(trimmed, DEFAULT_REGION);
    if (!parsed || !parsed.isValid()) return null;
    if (parsed.ext) return null; // an extension has no place in a stored contact number
    return parsed.number; // E.164
  } catch {
    return null;
  }
}

export function isValidPhone(input: string | null | undefined): boolean {
  return normalizePhone(input) !== null;
}
