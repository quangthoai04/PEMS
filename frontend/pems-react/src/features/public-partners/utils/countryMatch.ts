import type { PublicPartnerCountry } from '../types/publicPartners.types';

/**
 * Normalizes a country string for tolerant matching: strips Vietnamese diacritics, lowercases,
 * trims, and collapses internal whitespace. "  Việt Nam " and "viet nam" both normalize to
 * "viet nam".
 */
function normalizeCountryText(input: string): string {
  return input
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '') // strip combining diacritical marks (e.g. Vietnamese tone marks)
    .replace(/[đĐ]/g, 'd') // Vietnamese "đ"/"Đ" doesn't decompose via NFD
    .trim()
    .toLowerCase()
    .replace(/\s+/g, ' ');
}

/**
 * Matches an arbitrary country string (e.g. whatever GlobeComponent's pin-click emits) against the
 * real country list from the backend (`/public/partners/countries`), so the filter is never called
 * with a value that doesn't exist in `partners.country`. Tries, in order: exact match on `value`,
 * exact match on `label`, then a diacritics/case/whitespace-tolerant normalized match. Returns the
 * canonical `value` to filter by, or null if nothing in the real country list matches.
 */
export function findMatchingCountryValue(
  input: string | null | undefined,
  countries: PublicPartnerCountry[],
): string | null {
  if (!input || !input.trim()) return null;

  const exact = countries.find((c) => c.value === input);
  if (exact) return exact.value;

  const byLabel = countries.find((c) => c.label === input);
  if (byLabel) return byLabel.value;

  const normalizedInput = normalizeCountryText(input);
  const normalizedMatch = countries.find(
    (c) => normalizeCountryText(c.value) === normalizedInput || normalizeCountryText(c.label) === normalizedInput,
  );
  return normalizedMatch ? normalizedMatch.value : null;
}
