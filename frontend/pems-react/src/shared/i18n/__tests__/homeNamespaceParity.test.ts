/**
 * Key parity giữa vi/home.json và en/home.json.
 *
 * Thiếu một key phía EN sẽ khiến UI rơi về fallbackLng 'vi' và hiển thị tiếng Việt trong chế độ
 * tiếng Anh — lỗi im lặng đúng loại mà task này đang sửa. Test phải FAIL khi thiếu key, nên ở đây
 * cố tình không dùng defaultValue để che.
 */

import { describe, expect, it } from 'vitest';
import viHome from '../locales/vi/home.json';
import enHome from '../locales/en/home.json';

type Tree = { [key: string]: string | Tree };

/** Trả về map path -> 'leaf' | 'object' cho toàn bộ cây. */
function flatten(tree: Tree, prefix = ''): Map<string, 'leaf' | 'object'> {
  const out = new Map<string, 'leaf' | 'object'>();
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'string') {
      out.set(path, 'leaf');
    } else {
      out.set(path, 'object');
      for (const [childPath, kind] of flatten(value, path)) {
        out.set(childPath, kind);
      }
    }
  }
  return out;
}

const vi = flatten(viHome as unknown as Tree);
const en = flatten(enHome as unknown as Tree);

function leaves(tree: Tree, prefix = ''): [string, string][] {
  return Array.from(flatten(tree, prefix))
    .filter(([, kind]) => kind === 'leaf')
    .map(([path]) => [path, path.split('.').reduce<unknown>((node, seg) => (node as Tree)[seg], tree) as string]);
}

describe('home namespace translation parity', () => {
  it('has no key present in vi but missing in en', () => {
    const missing = Array.from(vi.keys()).filter((path) => !en.has(path));
    expect(missing).toEqual([]);
  });

  it('has no key present in en but missing in vi', () => {
    const missing = Array.from(en.keys()).filter((path) => !vi.has(path));
    expect(missing).toEqual([]);
  });

  it('has no object/leaf shape mismatch', () => {
    const mismatched = Array.from(vi.entries())
      .filter(([path, kind]) => en.has(path) && en.get(path) !== kind)
      .map(([path]) => path);
    expect(mismatched).toEqual([]);
  });

  it.each([
    ['vi', viHome],
    ['en', enHome],
  ])('has no empty string in %s', (_lng, tree) => {
    const empty = leaves(tree as unknown as Tree)
      .filter(([, value]) => value.trim().length === 0)
      .map(([path]) => path);
    expect(empty).toEqual([]);
  });

  it('covers all seven internal role buckets in quickAccess and guide', () => {
    const quickAccessBuckets = ['ADMIN', 'HO', 'STAFF_LEADER', 'STAFF', 'DEPT_LEADER', 'DEPT_STAFF', 'STUDENT'];
    const roleLabelKeys = ['ADMIN', 'HO', 'STAFF_LEADER', 'STAFF', 'DEPARTMENT_LEADER', 'DEPARTMENT_STAFF', 'STUDENT'];

    for (const table of [vi, en]) {
      for (const bucket of quickAccessBuckets) {
        expect(table.get(`internal.quickAccess.${bucket}`)).toBe('object');
        expect(table.get(`internal.guide.roles.${bucket}`)).toBe('object');
      }
      for (const label of roleLabelKeys) {
        expect(table.get(`internal.roleLabels.${label}`)).toBe('leaf');
      }
    }
  });

  it('gives every quickAccess card both a label and a description', () => {
    for (const [path, kind] of vi) {
      if (kind !== 'object') continue;
      // internal.quickAccess.<BUCKET>.<card>
      if (!/^internal\.quickAccess\.[A-Z_]+\.[a-zA-Z]+$/.test(path)) continue;
      for (const table of [vi, en]) {
        expect(table.get(`${path}.label`)).toBe('leaf');
        expect(table.get(`${path}.description`)).toBe('leaf');
      }
    }
  });
});
