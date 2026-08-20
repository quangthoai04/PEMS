/**
 * Generates (or, with --check, verifies) `backend/PEMS.Domain/Data/countries.json` from the SAME
 * source `src/shared/utils/countryNames.ts` uses at runtime, so the frontend's country picker and the
 * backend's `CountryName` resolver can never resolve one input to two different answers (Patch 4
 * hardening H4-2).
 *
 * Run after `VI_COUNTRY_NAME_OVERRIDES` / `EXTRA_NAME_ALIASES` change:
 *   npx tsx scripts/generate-country-data.ts
 *
 * `src/test/countryDataParity.test.ts` runs the --check form automatically in every regular test run,
 * so a change to the source overrides that is not followed by a regeneration fails CI rather than
 * silently drifting.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildCountryDataSnapshot } from '../src/shared/utils/countryNames';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUTPUT_PATH = path.resolve(__dirname, '../../../backend/PEMS.Domain/Data/countries.json');

function serialize(): string {
  return JSON.stringify(buildCountryDataSnapshot(), null, 2) + '\n';
}

function main(): void {
  const expected = serialize();
  const check = process.argv.includes('--check');

  if (check) {
    const existing = fs.existsSync(OUTPUT_PATH) ? fs.readFileSync(OUTPUT_PATH, 'utf8') : null;
    if (existing !== expected) {
      console.error(
        `${OUTPUT_PATH} is stale relative to countryNames.ts.\n` +
          'Run: npx tsx scripts/generate-country-data.ts',
      );
      process.exitCode = 1;
      return;
    }
    console.log('countries.json is in sync with countryNames.ts.');
    return;
  }

  fs.writeFileSync(OUTPUT_PATH, expected, 'utf8');
  const count = buildCountryDataSnapshot().countries.length;
  console.log(`Wrote ${count} countries to ${OUTPUT_PATH}`);
}

main();
