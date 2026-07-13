/**
 * Maps a partner's `country` text (Vietnamese, as stored in `partners.country`) to an ISO-3166
 * alpha-2 code, for rendering a real flag image via the `flag-icons` CSS library (`fi fi-<code>`).
 * No ISO code is stored in the DB; the actual name -> code logic (vi + en + alias table) lives
 * in the shared `countryNames.ts` so flags, the globe and the partner form country picker all
 * agree on what counts as the same country.
 */

import { countryNameToAlpha2 } from '../../../shared/utils/countryNames';

/** Returns the ISO-3166 alpha-2 code for a country name, or null if unrecognized. */
export function getCountryIsoCode(countryName: string): string | null {
  return countryNameToAlpha2(countryName);
}
