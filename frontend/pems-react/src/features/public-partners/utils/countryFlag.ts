/**
 * Maps a partner's `country` text (Vietnamese, as stored in `partners.country`) to an ISO-3166
 * alpha-2 code, for rendering a real flag image via the `flag-icons` CSS library (`fi fi-<code>`).
 * No ISO code is stored in the DB, so this is a best-effort name -> code lookup backed by
 * `i18n-iso-countries` (already a project dependency, used by CountrySelect.tsx); a small alias
 * table covers common Vietnamese variants that library doesn't recognize (e.g. "Hoa Kỳ", "Anh").
 */

import countries from 'i18n-iso-countries';
import viLocale from 'i18n-iso-countries/langs/vi.json';

countries.registerLocale(viLocale);

/** Vietnamese country-name variants not recognized by i18n-iso-countries' "vi" locale. */
const COUNTRY_ALIASES: Record<string, string> = {
  'hoa kỳ': 'US',
  'mỹ': 'US',
  'anh': 'GB',
  'vương quốc anh': 'GB',
  'nga': 'RU',
  'hàn quốc': 'KR',
  'triều tiên': 'KP',
  'lào': 'LA',
};

function normalize(name: string): string {
  return name.trim().toLowerCase();
}

/** Returns the ISO-3166 alpha-2 code for a country name, or null if unrecognized. */
export function getCountryIsoCode(countryName: string): string | null {
  const key = normalize(countryName);
  if (COUNTRY_ALIASES[key]) return COUNTRY_ALIASES[key];

  const code = countries.getAlpha2Code(countryName, 'vi');
  return code ?? null;
}
