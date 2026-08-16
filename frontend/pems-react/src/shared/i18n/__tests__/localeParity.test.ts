/**
 * i18n gate — Phase D1 (locale parity).
 *
 * Every VI namespace file must have a matching EN file with the exact same key set, no empty/
 * whitespace-only values, and every namespace file present on disk must be registered in
 * `shared/i18n/config.ts` (an unregistered namespace silently renders the bare key — see the
 * comment on `ns:` in config.ts). Plural-aware: a key ending in `_one`/`_other` is treated as one
 * logical key so i18next's Intl.PluralRules-based resolution doesn't produce false positives.
 */
import { describe, expect, it } from 'vitest';
import config from '../config';

const viModules = import.meta.glob('../locales/vi/*.json', { eager: true }) as Record<string, Record<string, unknown>>;
const enModules = import.meta.glob('../locales/en/*.json', { eager: true }) as Record<string, Record<string, unknown>>;

function namespaceFromPath(path: string): string {
  const match = path.match(/\/([^/]+)\.json$/);
  if (!match) throw new Error(`Cannot derive namespace from path "${path}"`);
  return match[1];
}

const PLURAL_SUFFIXES = ['_zero', '_one', '_two', '_few', '_many', '_other'];

function stripPluralSuffix(key: string): string {
  for (const suffix of PLURAL_SUFFIXES) {
    if (key.endsWith(suffix)) return key.slice(0, -suffix.length);
  }
  return key;
}

/** Flattens a nested JSON object into dot-path keys, collapsing `_one`/`_other` plural siblings
 * into a single logical key (`foo_one` + `foo_other` -> `foo`) so parity comparison isn't fooled
 * by legitimate plural forms. */
function flatten(obj: unknown, prefix = ''): Map<string, unknown> {
  const out = new Map<string, unknown>();
  if (obj === null || typeof obj !== 'object') {
    out.set(prefix, obj);
    return out;
  }
  for (const [rawKey, value] of Object.entries(obj as Record<string, unknown>)) {
    const key = stripPluralSuffix(rawKey);
    const path = prefix ? `${prefix}.${key}` : key;
    if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
      for (const [k, v] of flatten(value, path)) out.set(k, v);
    } else {
      // A plural pair collapses to one entry — keep whichever is non-empty so an empty `_other`
      // fallback doesn't mask a filled `_one` (or vice versa).
      if (!out.has(path) || (typeof out.get(path) === 'string' && (out.get(path) as string).trim() === '')) {
        out.set(path, value);
      }
    }
  }
  return out;
}

const viNamespaces = new Map(Object.entries(viModules).map(([path, mod]) => [namespaceFromPath(path), mod.default ?? mod]));
const enNamespaces = new Map(Object.entries(enModules).map(([path, mod]) => [namespaceFromPath(path), mod.default ?? mod]));

describe('i18n locale parity (Guest/Visitor gate — Phase D1)', () => {
  it('has at least the namespaces already registered in config.ts', () => {
    // A sanity floor, not a ceiling — new namespaces are expected to appear here over time.
    expect(viNamespaces.size).toBeGreaterThan(0);
    expect(enNamespaces.size).toBeGreaterThan(0);
  });

  it('registers every namespace file on disk in shared/i18n/config.ts `ns:` array', () => {
    const configuredResourceNs = new Set(Object.keys((config as any).options?.resources?.vi ?? {}));
    const missing = [...viNamespaces.keys()].filter((ns) => !configuredResourceNs.has(ns));
    expect(missing, `Namespace file(s) exist on disk but are not wired into config.ts resources: ${missing.join(', ')}`).toEqual([]);
  });

  it('has a matching EN file for every VI namespace and vice versa', () => {
    const viOnly = [...viNamespaces.keys()].filter((ns) => !enNamespaces.has(ns));
    const enOnly = [...enNamespaces.keys()].filter((ns) => !viNamespaces.has(ns));
    expect(viOnly, `VI namespace(s) with no EN file: ${viOnly.join(', ')}`).toEqual([]);
    expect(enOnly, `EN namespace(s) with no VI file: ${enOnly.join(', ')}`).toEqual([]);
  });

  for (const [ns, viContent] of viNamespaces) {
    const enContent = enNamespaces.get(ns);
    if (!enContent) continue; // already reported by the "matching EN file" test above

    describe(`namespace "${ns}"`, () => {
      const viFlat = flatten(viContent);
      const enFlat = flatten(enContent);

      it('has identical key sets between VI and EN (plural-aware)', () => {
        const viKeys = [...viFlat.keys()];
        const enKeys = [...enFlat.keys()];
        const missingInEn = viKeys.filter((k) => !enFlat.has(k));
        const missingInVi = enKeys.filter((k) => !viFlat.has(k));
        expect(missingInEn, `Key(s) present in VI but missing in EN for "${ns}": ${missingInEn.join(', ')}`).toEqual([]);
        expect(missingInVi, `Key(s) present in EN but missing in VI for "${ns}": ${missingInVi.join(', ')}`).toEqual([]);
      });

      it('has no empty or whitespace-only string values', () => {
        const emptyInVi = [...viFlat.entries()].filter(([, v]) => typeof v === 'string' && v.trim() === '').map(([k]) => k);
        const emptyInEn = [...enFlat.entries()].filter(([, v]) => typeof v === 'string' && v.trim() === '').map(([k]) => k);
        expect(emptyInVi, `Empty VI value(s) in "${ns}": ${emptyInVi.join(', ')}`).toEqual([]);
        expect(emptyInEn, `Empty EN value(s) in "${ns}": ${emptyInEn.join(', ')}`).toEqual([]);
      });
    });
  }
});
