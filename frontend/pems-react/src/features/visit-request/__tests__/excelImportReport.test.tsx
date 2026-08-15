import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, within, act } from '@testing-library/react';
import * as XLSX from 'xlsx';

// `writeFile` reaches for the filesystem, which jsdom has no business doing. Everything else in
// the library stays REAL, so the workbook under assertion is the one the product actually built.
const saved = vi.hoisted(() => [] as Array<{ wb: unknown; name: string }>);
vi.mock('xlsx', async (importOriginal) => {
  const actual = await importOriginal<typeof import('xlsx')>();
  return { ...actual, writeFile: (wb: unknown, name: string) => { saved.push({ wb, name }); } };
});
import i18n from '../../../shared/i18n/config';
import {
  validatePersonExcel,
  canApplyImport,
  EXCEL_MAX_MEMBERS,
  type PersonRow,
} from '../components/ExcelUpload/excelValidator';
import { ExcelImportPanel } from '../components/ExcelUpload/ExcelImportPanel';

/**
 * Plan §22. What was wrong before: the import reported ONE message for both sections, showed
 * only `errors[0]` however many rows were broken, compared duplicates against `[]` instead of
 * the form, silently REPLACED whatever had been typed by hand, ignored the 200-member cap and
 * let a corrupt workbook escape as an unhandled promise rejection.
 */

const HEADER = ['STT', 'Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch'];

const makeFile = (rows: (string | number)[][], name = 'danh-sach-khach.xlsx'): File => {
  const ws = XLSX.utils.aoa_to_sheet(rows);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Sheet1');
  const buf = XLSX.write(wb, { type: 'array', bookType: 'xlsx' }) as ArrayBuffer;
  return new File([buf], name, {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
};

const person = (n: number, over: Partial<PersonRow> = {}): (string | number)[] => [
  n,
  over.fullName ?? `Khách ${n}`,
  over.jobTitle ?? 'Giảng viên',
  over.organization ?? 'ĐH Đối Tác',
  over.nationality ?? 'Việt Nam',
];

const row = (p: Partial<PersonRow>): PersonRow => ({
  fullName: '', jobTitle: '', organization: '', nationality: '', ...p,
});

const t = (key: string, options?: Record<string, unknown>) => i18n.t(key, options) as string;

describe('Excel import — the report (plan §22)', () => {
  beforeEach(async () => { await i18n.changeLanguage('en'); });

  it('counts total, valid and duplicate rows, and says what the list will hold', async () => {
    const file = makeFile([
      HEADER,
      person(1),
      person(2, { fullName: 'Khách 2' }),
      person(3, { fullName: 'khách  1' }), // same person as row 2 after normalising
    ]);

    const report = await validatePersonExcel(file, 'visitors', [], t);

    expect(report.totalRows).toBe(3);
    expect(report.validRows).toBe(2);
    expect(report.errorRows).toBe(0);
    expect(report.duplicateRows).toBe(1);
    expect(report.resultingCount).toBe(2);
    expect(canApplyImport(report)).toBe(true);
  });

  it('reports EVERY broken cell, not only the first one', async () => {
    const file = makeFile([
      HEADER,
      person(1, { fullName: '' }),
      person(2, { organization: '' }),
      person(3, { nationality: '' }),
      person(4, { jobTitle: '' }),
    ]);

    const report = await validatePersonExcel(file, 'visitors', [], t);

    expect(report.errorRows).toBe(4);
    expect(report.errors).toHaveLength(4);
    expect(report.errors.map(e => e.row)).toEqual([2, 3, 4, 5]);
    // A file with errors changes nothing, so there is nothing to apply.
    expect(report.data).toEqual([]);
    expect(canApplyImport(report)).toBe(false);
  });

  it('rejects a value longer than the column allows, naming the limit', async () => {
    const file = makeFile([
      HEADER,
      person(1, { nationality: 'x'.repeat(101) }),
      person(2, { organization: 'y'.repeat(201) }),
      person(3, { fullName: 'z'.repeat(151) }),
    ]);

    const report = await validatePersonExcel(file, 'visitors', [], t);

    expect(report.errorRows).toBe(3);
    expect(report.errors[0].message).toContain('100');
    expect(report.errors[1].message).toContain('200');
    expect(report.errors[2].message).toContain('150');
  });

  it('treats a person already on the form as a duplicate, not as a new guest', async () => {
    // The old call passed `[]` as the existing list, so re-uploading the same sheet after
    // typing one of its people in by hand produced that person twice.
    const existing = [row({ fullName: 'Khách 1', jobTitle: 'Giảng viên', organization: 'ĐH Đối Tác', nationality: 'Việt Nam' })];
    const file = makeFile([HEADER, person(1), person(2)]);

    const report = await validatePersonExcel(file, 'visitors', existing, t);

    expect(report.duplicateRows).toBe(1);
    expect(report.validRows).toBe(1);
    expect(report.data[0].fullName).toBe('Khách 2');
    expect(report.resultingCount).toBe(2);
  });

  it('refuses the file rather than silently truncating past the member cap', async () => {
    const existing = Array.from({ length: EXCEL_MAX_MEMBERS - 2 }, (_, i) =>
      row({ fullName: `Có sẵn ${i}`, jobTitle: 'GV', organization: 'Org', nationality: 'VN' }));
    const file = makeFile([HEADER, person(1), person(2), person(3), person(4), person(5)]);

    const report = await validatePersonExcel(file, 'visitors', existing, t);

    expect(report.remainingSlots).toBe(2);
    expect(report.overLimitRows).toBe(3);
    expect(canApplyImport(report)).toBe(false);
    expect(report.data).toEqual([]);
    // Nothing was applied, so the list is still exactly what it was.
    expect(report.resultingCount).toBe(EXCEL_MAX_MEMBERS - 2);
  });

  it('returns a report instead of throwing when the workbook cannot be read', async () => {
    const corrupt = new File([new Uint8Array([1, 2, 3, 4, 5])], 'hong.xlsx');
    const report = await validatePersonExcel(corrupt, 'visitors', [], t);

    expect(report.fatalMessage).toBeTruthy();
    expect(canApplyImport(report)).toBe(false);
  });

  it('rejects a file whose columns do not match, and one with only a header', async () => {
    const wrongColumns = await validatePersonExcel(
      makeFile([['A', 'B'], ['1', '2']]), 'visitors', [], t);
    expect(wrongColumns.fatalMessage).toContain('Missing required column');

    const headerOnly = await validatePersonExcel(makeFile([HEADER]), 'visitors', [], t);
    expect(headerOnly.fatalMessage).toBeTruthy();
  });

  it('rejects a file that is not a spreadsheet at all', async () => {
    const notExcel = new File(['hello'], 'notes.txt', { type: 'text/plain' });
    const report = await validatePersonExcel(notExcel, 'visitors', [], t);
    expect(report.fatalMessage).toBeTruthy();
    expect(report.totalRows).toBe(0);
  });

  it('keeps the guest and support reports apart', async () => {
    const guests = await validatePersonExcel(makeFile([HEADER, person(1)]), 'visitors', [], t);
    const support = await validatePersonExcel(
      makeFile([HEADER, person(9)], 'ho-tro.xlsx'), 'supportTeam', [], t);

    expect(guests.kind).toBe('visitors');
    expect(guests.fileName).toBe('danh-sach-khach.xlsx');
    expect(support.kind).toBe('supportTeam');
    expect(support.fileName).toBe('ho-tro.xlsx');
  });
});

describe('Excel import — the panel (plan §22)', () => {
  const noop = () => {};

  beforeEach(async () => { await i18n.changeLanguage('en'); });

  it('announces which file is being checked', () => {
    render(
      <ExcelImportPanel
        testId="p" kind="visitors" campusLabel="Hòa Lạc" onChooseAnother={noop} onDismiss={noop}
        state={{ loadingFileName: 'danh-sach-khach.xlsx' }}
      />,
    );
    expect(screen.getByTestId('p-loading')).toHaveTextContent('danh-sach-khach.xlsx');
  });

  it('shows the success figures the plan asks for', async () => {
    const file = makeFile([HEADER, person(1), person(2), person(3, { fullName: 'Khách 1' })]);
    const report = await validatePersonExcel(file, 'visitors', [], t);

    render(
      <ExcelImportPanel
        testId="p" kind="visitors" campusLabel="Hòa Lạc" onChooseAnother={noop} onDismiss={noop} state={{ report }}
      />,
    );

    const panel = screen.getByTestId('p-success');
    expect(panel).toHaveTextContent('danh-sach-khach.xlsx');
    expect(panel).toHaveTextContent('3');   // total rows
    expect(panel).toHaveTextContent('2');   // imported
    expect(panel).toHaveTextContent('1');   // duplicates skipped
    expect(screen.queryByTestId('p-error')).toBeNull();
  });

  it('lists every faulty row in a table with its column and reason', async () => {
    const file = makeFile([
      HEADER, person(1, { fullName: '' }), person(2, { jobTitle: '' }), person(3, { nationality: '' }),
    ]);
    const report = await validatePersonExcel(file, 'visitors', [], t);

    render(
      <ExcelImportPanel
        testId="p" kind="visitors" campusLabel="Hòa Lạc" onChooseAnother={noop} onDismiss={noop} state={{ report }}
      />,
    );

    const table = screen.getByTestId('p-error-table');
    const bodyRows = within(table).getAllByRole('row').slice(1); // drop the header row
    expect(bodyRows).toHaveLength(3);
    expect(bodyRows[0]).toHaveTextContent('2');
    expect(bodyRows[2]).toHaveTextContent('4');
    // And the way out is offered, not just the bad news.
    expect(screen.getByTestId('p-download')).toBeInTheDocument();
    expect(screen.getByTestId('p-retry')).toBeInTheDocument();
  });

  it('says plainly that the form was not touched', async () => {
    const report = await validatePersonExcel(
      makeFile([HEADER, person(1, { fullName: '' })]), 'visitors', [], t);

    render(
      <ExcelImportPanel
        testId="p" kind="visitors" campusLabel="Hòa Lạc" onChooseAnother={noop} onDismiss={noop} state={{ report }}
      />,
    );
    expect(screen.getByTestId('p-error')).toHaveTextContent(/form is unchanged/i);
  });

  it('renders the report in Vietnamese too', async () => {
    await act(async () => { await i18n.changeLanguage('vi'); });
    const report = await validatePersonExcel(makeFile([HEADER, person(1)]), 'visitors', [], t);

    render(
      <ExcelImportPanel
        testId="p" kind="visitors" campusLabel="Hòa Lạc" onChooseAnother={noop} onDismiss={noop} state={{ report }}
      />,
    );
    expect(screen.getByTestId('p-success')).toHaveTextContent('danh sách khách');
    await act(async () => { await i18n.changeLanguage('en'); });
  });

  it('offers no download when the whole file was rejected before any row was read', async () => {
    const report = await validatePersonExcel(
      new File([new Uint8Array([9, 9, 9])], 'hong.xlsx'), 'visitors', [], t);

    render(
      <ExcelImportPanel
        testId="p" kind="visitors" campusLabel="Hòa Lạc" onChooseAnother={noop} onDismiss={noop} state={{ report }}
      />,
    );
    // There is no row table to export — only a reason and a way to pick another file.
    expect(screen.queryByTestId('p-error-table')).toBeNull();
    expect(screen.queryByTestId('p-download')).toBeNull();
    expect(screen.getByTestId('p-retry')).toBeInTheDocument();
  });
});

describe('Excel import — downloadable report (plan §5)', () => {
  it('writes a workbook carrying the summary and every problem row', async () => {
    saved.length = 0;
    const { downloadExcelErrorReport } = await import('../components/ExcelUpload/excelDownload');
    const report = await validatePersonExcel(
      makeFile([HEADER, person(1, { fullName: '' }), person(2, { jobTitle: '' })]), 'visitors', [], t);

    downloadExcelErrorReport(report, 'Hòa Lạc', t);

    expect(saved).toHaveLength(1);
    expect(saved[0].name).toBe('danh-sach-khach-error-report.xlsx');
    const wb = saved[0].wb as XLSX.WorkBook;
    const sheet = wb.Sheets[wb.SheetNames[0]];
    const flat = XLSX.utils.sheet_to_json<string[]>(sheet, { header: 1, defval: '' }) as string[][];
    const text = flat.map(r => r.join('|')).join('\n');

    expect(text).toContain('danh-sach-khach.xlsx'); // file name
    expect(text).toContain('Hòa Lạc');              // campus
    expect(flat.some(r => String(r[0]) === '2')).toBe(true); // the first faulty row
    expect(flat.some(r => String(r[0]) === '3')).toBe(true); // and the second
  });
});
