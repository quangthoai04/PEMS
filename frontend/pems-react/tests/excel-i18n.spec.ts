/**
 * Excel upload i18n tests.
 *
 * These call the real validator (no browser needed — it only uses `xlsx` and `File`) with
 * real .xlsx fixtures and the real locale JSON, so they cover both the localized messages
 * and the header-alias parsing that keeps a VI-downloaded template uploadable in EN mode
 * and vice versa.
 */
import { test, expect } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import i18next from 'i18next';

import {
  validateVisitorExcel,
  canApplyImport,
  type ExcelTranslator,
} from '../src/features/visit-request/components/ExcelUpload/excelValidator';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const FIXTURES = path.join(HERE, 'fixtures');
const LOCALES = path.join(HERE, '../src/shared/i18n/locales');

// Read the locale files off disk: Node ESM would otherwise demand an import attribute,
// and this also guarantees we assert against exactly what ships.
const loadLocale = (lng: 'vi' | 'en') =>
  JSON.parse(fs.readFileSync(path.join(LOCALES, lng, 'visitRequest.json'), 'utf8'));

/** A translator backed by the real locale files, for the given language. */
async function translatorFor(lng: 'vi' | 'en'): Promise<ExcelTranslator> {
  const instance = i18next.createInstance();
  await instance.init({
    lng,
    fallbackLng: 'vi',
    ns: ['visitRequest'],
    defaultNS: 'visitRequest',
    resources: {
      vi: { visitRequest: loadLocale('vi') },
      en: { visitRequest: loadLocale('en') },
    },
    interpolation: { escapeValue: false },
  });
  return (key, options) => instance.t(key, options as never) as unknown as string;
}

function fileFrom(name: string): File {
  const buf = fs.readFileSync(path.join(FIXTURES, name));
  return new File([buf], name, {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
}

/** Vietnamese-specific diacritics — must never appear in EN-mode output. */
const VIETNAMESE = /[àáâãèéêìíòóôõùúăđĩũơưạ-ỹ]/i;

test.describe('Excel headers parse in either language', () => {
  for (const fixture of ['visitors-vi-valid.xlsx', 'visitors-en-valid.xlsx']) {
    test(`${fixture} parses with no errors`, async () => {
      const t = await translatorFor('en');
      const result = await validateVisitorExcel(fileFrom(fixture), [], t);

      expect(result.errors, `unexpected errors: ${JSON.stringify(result.errors)}`).toHaveLength(0);
      expect(canApplyImport(result)).toBe(true);
      expect(result.data).toHaveLength(1);
      expect(result.data[0].organization).toBe('XYZ');
      expect(result.data[0].nationality).toBe('Vietnam');
    });
  }
});

test.describe('EN mode produces English messages only', () => {
  test('missing required cell (VI-header file)', async () => {
    const t = await translatorFor('en');
    const result = await validateVisitorExcel(fileFrom('visitors-vi-missing-cell.xlsx'), [], t);

    expect(canApplyImport(result)).toBe(false);
    // Row/column are separate report fields (not baked into the message) so the UI table and the
    // downloadable CSV can render them as their own columns -- see ExcelImportPanel/excelDownload.
    expect(result.errors[0]).toMatchObject({ row: 2, column: 'Job Title', message: 'Must not be empty' });
    for (const e of result.errors) expect(e.message).not.toMatch(VIETNAMESE);
  });

  test('missing required cell (EN-header file)', async () => {
    const t = await translatorFor('en');
    const result = await validateVisitorExcel(fileFrom('visitors-en-missing-cell.xlsx'), [], t);

    expect(result.errors[0]).toMatchObject({ row: 2, column: 'Job Title', message: 'Must not be empty' });
  });

  test('missing whole column', async () => {
    const t = await translatorFor('en');
    const result = await validateVisitorExcel(fileFrom('visitors-missing-column.xlsx'), [], t);

    // A whole-file problem (no per-row table to show) is reported as `fatalMessage`, not `errors[]`.
    expect(result.errors).toHaveLength(0);
    expect(result.fatalMessage).toContain('Missing required column: Nationality');
    expect(result.fatalMessage).not.toMatch(VIETNAMESE);
  });

  test('header-only file', async () => {
    const t = await translatorFor('en');
    const result = await validateVisitorExcel(fileFrom('visitors-header-only.xlsx'), [], t);

    expect(result.errors).toHaveLength(0);
    expect(result.fatalMessage).toBe('The file has no data rows (header only, or empty).');
    expect(result.fatalMessage).not.toMatch(VIETNAMESE);
  });

  test('no English error contains the reported Vietnamese words', async () => {
    const t = await translatorFor('en');
    const banned = ['không được', 'Vui lòng', 'thiếu', 'Thiếu', 'Dòng', 'dòng'];

    for (const fixture of [
      'visitors-vi-missing-cell.xlsx',
      'visitors-missing-column.xlsx',
      'visitors-header-only.xlsx',
    ]) {
      const result = await validateVisitorExcel(fileFrom(fixture), [], t);
      // Whole-file failures land in `fatalMessage`, per-row failures in `errors[]` -- scan both, or a
      // fixture that trips the fatal path would leak Vietnamese into EN mode with nothing catching it.
      const text = [result.fatalMessage, ...result.errors.map((e) => e.message)].filter(Boolean).join(' | ');
      for (const word of banned) {
        expect(text, `"${word}" leaked into EN output for ${fixture}`).not.toContain(word);
      }
    }
  });
});

test.describe('VI mode produces Vietnamese messages', () => {
  test('missing required cell', async () => {
    const t = await translatorFor('vi');
    const result = await validateVisitorExcel(fileFrom('visitors-vi-missing-cell.xlsx'), [], t);

    expect(result.errors[0]).toMatchObject({ row: 2, column: 'Chức vụ', message: 'Không được để trống' });
  });

  test('missing whole column', async () => {
    const t = await translatorFor('vi');
    const result = await validateVisitorExcel(fileFrom('visitors-missing-column.xlsx'), [], t);

    expect(result.errors).toHaveLength(0);
    expect(result.fatalMessage).toContain('Thiếu cột bắt buộc: Quốc tịch');
  });

  test('header-only file', async () => {
    const t = await translatorFor('vi');
    const result = await validateVisitorExcel(fileFrom('visitors-header-only.xlsx'), [], t);

    expect(result.errors).toHaveLength(0);
    expect(result.fatalMessage).toBe('File không có dữ liệu (chỉ có header hoặc rỗng).');
  });
});

test.describe('duplicate rows are still skipped', () => {
  test('an existing entry is not added twice', async () => {
    const t = await translatorFor('en');
    const existing = [
      { fullName: 'John Smith', jobTitle: 'Director', organization: 'XYZ', nationality: 'Vietnam' },
    ];
    const result = await validateVisitorExcel(fileFrom('visitors-en-valid.xlsx'), existing, t);

    expect(result.duplicateRows).toBe(1);
    expect(result.data).toHaveLength(0);
  });
});
