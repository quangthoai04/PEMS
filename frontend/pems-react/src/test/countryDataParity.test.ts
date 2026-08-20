/**
 * Patch 4 hardening H4-2 — FE/BE country-data parity guard.
 *
 * `backend/PEMS.Domain/Data/countries.json` is a GENERATED snapshot of this module's own
 * `buildCountryDataSnapshot()` (see `scripts/generate-country-data.ts`). Nothing forces a developer to
 * remember to regenerate it after touching `VI_COUNTRY_NAME_OVERRIDES` / `EXTRA_NAME_ALIASES` — so this
 * test does it for them: it rebuilds the expected snapshot from the live source and fails loudly if the
 * committed backend file has drifted, rather than letting the frontend's country picker and the
 * backend's `CountryName` resolver quietly disagree about which alpha-2 a name resolves to.
 */
import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import { buildCountryDataSnapshot } from '../shared/utils/countryNames';

const BACKEND_COUNTRIES_JSON = path.resolve(
  __dirname,
  '../../../../backend/PEMS.Domain/Data/countries.json',
);

describe('backend/frontend country data parity (H4-2)', () => {
  it('countries.json exists and is valid JSON', () => {
    expect(fs.existsSync(BACKEND_COUNTRIES_JSON)).toBe(true);
    expect(() => JSON.parse(fs.readFileSync(BACKEND_COUNTRIES_JSON, 'utf8'))).not.toThrow();
  });

  it('is byte-for-byte in sync with countryNames.ts (run `npm run gen:country-data` if this fails)', () => {
    const expected = JSON.stringify(buildCountryDataSnapshot(), null, 2) + '\n';
    const actual = fs.readFileSync(BACKEND_COUNTRIES_JSON, 'utf8');
    expect(actual).toBe(expected);
  });

  it('every alpha-2 code carries the same Vietnamese short name on both sides', () => {
    const expected = buildCountryDataSnapshot();
    const actual = JSON.parse(fs.readFileSync(BACKEND_COUNTRIES_JSON, 'utf8')) as typeof expected;
    const actualByCode = new Map(actual.countries.map((c) => [c.alpha2, c]));
    for (const country of expected.countries) {
      const found = actualByCode.get(country.alpha2);
      expect(found, `backend countries.json is missing alpha-2 code ${country.alpha2}`).toBeDefined();
      expect(found?.vi).toBe(country.vi);
    }
  });

  it('every alias the frontend resolves is present in the backend alias list for the same country', () => {
    const expected = buildCountryDataSnapshot();
    const actual = JSON.parse(fs.readFileSync(BACKEND_COUNTRIES_JSON, 'utf8')) as typeof expected;
    const actualAliasSets = new Map(actual.countries.map((c) => [c.alpha2, new Set(c.aliases)]));
    for (const country of expected.countries) {
      const backendAliases = actualAliasSets.get(country.alpha2) ?? new Set<string>();
      for (const alias of country.aliases) {
        expect(
          backendAliases.has(alias),
          `alias "${alias}" resolves to ${country.alpha2} (${country.vi}) on the frontend but is missing from the backend's countries.json`,
        ).toBe(true);
      }
    }
  });

  it('sanity-checks a few real values from the Patch 4 decision and the P4.0 audit', () => {
    const snapshot = buildCountryDataSnapshot();
    const byAlias = new Map<string, string>();
    for (const c of snapshot.countries) for (const alias of c.aliases) byAlias.set(alias, c.vi);

    // The decision's own worked example: "Hàn Quốc" / "South Korea" / "KR" all resolve to "Hàn Quốc".
    expect(byAlias.get('hàn quốc')).toBe('Hàn Quốc');
    expect(byAlias.get('south korea')).toBe('Hàn Quốc');
    expect(byAlias.get('kr')).toBe('Hàn Quốc');

    // The UAE alias-duplication case the Staff-Leader filter audit flagged.
    expect(byAlias.get('uae')).toBe(byAlias.get('ae'));
  });
});
