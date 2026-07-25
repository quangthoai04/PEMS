import * as XLSX from 'xlsx';
import type { ExcelValidationError, VisitorEntry, SupportTeamEntry } from '../../types/visitRequest.types';

/** Translator for user-facing Excel messages, supplied by the calling component. */
export type ExcelTranslator = (key: string, options?: Record<string, unknown>) => string;

const ALLOWED_EXTENSIONS = ['xlsx', 'xls'];

export const isAllowedExcelFile = (file: File): boolean => {
  const ext = file.name.split('.').pop()?.toLowerCase();
  return !!ext && ALLOWED_EXTENSIONS.includes(ext);
};

/** Per-campus import cap, mirroring V2_MAX_MEMBERS_PER_CAMPUS and CampusVisitFormDtoValidator.MaxMembers. */
export const EXCEL_MAX_MEMBERS = 200;

export const MAX_EXCEL_FILE_BYTES = 5 * 1024 * 1024;

const readFirstSheet = async (file: File): Promise<string[][] | null> => {
  const buffer = await file.arrayBuffer();
  const wb = XLSX.read(buffer, { type: 'array' });
  const sheet = wb.Sheets[wb.SheetNames[0]];
  if (!sheet) return null;
  return XLSX.utils.sheet_to_json<string[]>(sheet, { header: 1, defval: '' }) as string[][];
};

// ─── Columns ─────────────────────────────────────────────────────────────────
//
// The sheet is parsed entirely in the browser (the result is appended to the form and
// submitted as JSON), so no backend depends on these header strings. Headers may
// therefore be localized — but a file downloaded in one language must still parse when
// uploaded in the other, so every column is matched against ALL of its known aliases
// rather than against the currently active translation.

type ColumnId = 'fullName' | 'jobTitle' | 'organization' | 'nationality';

const REQUIRED_COLUMNS: ColumnId[] = ['fullName', 'jobTitle', 'organization', 'nationality'];

const COLUMN_ALIASES: Record<ColumnId, string[]> = {
  fullName: ['họ và tên', 'full name'],
  jobTitle: ['chức vụ', 'job title', 'position'],
  organization: ['đơn vị công tác', 'organization', 'organisation'],
  nationality: ['quốc tịch', 'nationality'],
};

/**
 * Per-column ceilings. These are NOT chosen here — they mirror, exactly, the Zod schema
 * (`buildPersonSchema`), the FluentValidation rules (`VisitorV2Validator` /
 * `SupportTeamMemberV2Validator`) and the `visit_guest_members` columns. A sheet that
 * imports cleanly must therefore also submit cleanly: a value rejected by the server is
 * rejected here, at the point where the user can still fix the file.
 */
export const EXCEL_COLUMN_MAX_LENGTH: Record<ColumnId, number> = {
  fullName: 150,
  jobTitle: 150,
  organization: 200,
  nationality: 100,
};

/** Leading index column, skipped when present. */
const INDEX_ALIASES = ['stt', 'no.', 'no', '#'];

const normalizeHeader = (value: unknown): string =>
  String(value ?? '').trim().replace(/\s+/g, ' ').toLowerCase();

/** Maps each column id to its position in the (index-stripped) header row. */
const mapColumns = (headerRow: string[]): Partial<Record<ColumnId, number>> => {
  const normalized = headerRow.map(normalizeHeader);
  const result: Partial<Record<ColumnId, number>> = {};
  for (const id of REQUIRED_COLUMNS) {
    const idx = normalized.findIndex((h) => COLUMN_ALIASES[id].includes(h));
    if (idx !== -1) result[id] = idx;
  }
  return result;
};

/** Localized label for a column, used in error messages and templates. */
const columnLabel = (t: ExcelTranslator, id: ColumnId): string =>
  t(`visitRequest:excel.template.${id}`);

interface ParsedSheet {
  rows: string[][];
  colIdx: Partial<Record<ColumnId, number>>;
  dataStartCol: number;
}

/** Shared parse + header validation. Returns a localized message on failure. */
const parseSheet = (
  rows: string[][] | null,
  t: ExcelTranslator,
): { error: string } | ParsedSheet => {
  if (!rows) return { error: t('visitRequest:excel.errors.noData') };
  if (rows.length < 2) return { error: t('visitRequest:excel.errors.headerOnly') };

  const headerRow = rows[0].map((h) => String(h).trim());
  const dataStartCol = INDEX_ALIASES.includes(normalizeHeader(headerRow[0])) ? 1 : 0;
  const effectiveHeader = headerRow.slice(dataStartCol);

  const colIdx = mapColumns(effectiveHeader);
  const missing = REQUIRED_COLUMNS.filter((id) => colIdx[id] === undefined);
  if (missing.length > 0) {
    return {
      error: t('visitRequest:excel.errors.missingColumns', {
        missing: missing.map((id) => columnLabel(t, id)).join(', '),
        required: REQUIRED_COLUMNS.map((id) => columnLabel(t, id)).join(', '),
      }),
    };
  }

  return { rows, colIdx, dataStartCol };
};

const normalizeStr = (str: string) => String(str || '').trim().replace(/\s+/g, ' ').toLowerCase();

/**
 * Identity key for de-duplication (plan §6.4): the four business columns after trim,
 * whitespace collapse and case folding. Two rows that differ only in spacing or casing
 * are the same person, and importing both would silently create a duplicate guest.
 */
const createHash = (fullName: string, jobTitle: string, org: string, nat: string) =>
  `${normalizeStr(fullName)}|${normalizeStr(jobTitle)}|${normalizeStr(org)}|${normalizeStr(nat)}`;

export type PersonRow = Record<ColumnId, string>;

/** What one import attempt did, or refused to do. Shared by both sections. */
export interface ExcelImportReport {
  fileName: string;
  kind: 'visitors' | 'supportTeam';
  /** Vietnam wall-clock "dd/MM/yyyy HH:mm" of the check, for the downloadable report. */
  checkedAt: string;
  totalRows: number;
  validRows: number;
  errorRows: number;
  duplicateRows: number;
  /** Rows that would push the list past EXCEL_MAX_MEMBERS. Non-zero blocks the import. */
  overLimitRows: number;
  /** How many more people this campus can still take. */
  remainingSlots: number;
  /** Size of the list AFTER a successful import (unchanged when the import is refused). */
  resultingCount: number;
  errors: ExcelValidationError[];
  /** The rows to append. EMPTY whenever the import is refused. */
  data: PersonRow[];
  /**
   * Set when the whole file was rejected before any row was read (wrong type, too big,
   * unreadable, missing columns, header only). Distinct from row errors: there is no
   * per-row table to show.
   */
  fatalMessage?: string;
}

const isImportable = (report: ExcelImportReport): boolean =>
  !report.fatalMessage && report.errorRows === 0 && report.overLimitRows === 0 && report.validRows > 0;

/** True when the form may be changed with this report's rows. */
export const canApplyImport = isImportable;

interface ScanOptions {
  existing: PersonRow[];
  t: ExcelTranslator;
}

/**
 * Walks the data rows, collecting EVERY cell error (never just the first), skipping
 * duplicates and stopping at the member cap. Duplicates are not errors — they are
 * reported and skipped; over-limit rows ARE blocking, because silently dropping the
 * tail of someone's list is worse than refusing the file.
 */
const scanRows = (
  { rows, colIdx, dataStartCol }: ParsedSheet,
  { existing, t }: ScanOptions,
) => {
  const get = (row: string[], id: ColumnId) =>
    String(row[dataStartCol + (colIdx[id] ?? -1)] ?? '').trim();

  const errors: ExcelValidationError[] = [];
  const errorRows = new Set<number>();
  const entries: PersonRow[] = [];
  // Seeded with what the form ALREADY holds, so a person typed by hand is recognised as
  // a duplicate of the same person in the sheet (plan §6.2).
  const seen = new Set(existing.map((e) => createHash(e.fullName, e.jobTitle, e.organization, e.nationality)));
  let duplicateRows = 0;

  const dataRows = rows.slice(1).filter((r) => r.some((c) => String(c).trim() !== ''));

  rows.slice(1).forEach((row, i) => {
    const rowNum = i + 2; // 1-based, header occupies row 1
    if (row.every((c) => String(c).trim() === '')) return;

    for (const id of REQUIRED_COLUMNS) {
      const value = get(row, id);
      const column = columnLabel(t, id);
      if (!value) {
        errors.push({ row: rowNum, column, message: t('visitRequest:excel.errors.cellRequired') });
        errorRows.add(rowNum);
        continue;
      }
      const max = EXCEL_COLUMN_MAX_LENGTH[id];
      if (value.length > max) {
        errors.push({
          row: rowNum,
          column,
          message: t('visitRequest:excel.errors.cellMaxLength', { max, length: value.length }),
        });
        errorRows.add(rowNum);
      }
    }

    if (errorRows.has(rowNum)) return;

    const entry: PersonRow = {
      fullName: get(row, 'fullName'),
      jobTitle: get(row, 'jobTitle'),
      organization: get(row, 'organization'),
      nationality: get(row, 'nationality'),
    };
    const hash = createHash(entry.fullName, entry.jobTitle, entry.organization, entry.nationality);
    if (seen.has(hash)) {
      duplicateRows++;
      return;
    }
    seen.add(hash);
    entries.push(entry);
  });

  const remainingSlots = Math.max(0, EXCEL_MAX_MEMBERS - existing.length);
  const overLimitRows = Math.max(0, entries.length - remainingSlots);

  return {
    errors,
    errorRows: errorRows.size,
    entries,
    duplicateRows,
    totalRows: dataRows.length,
    remainingSlots,
    overLimitRows,
  };
};

const nowStamp = (): string => {
  const fmt = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', hour12: false,
  });
  const p: Record<string, string> = {};
  for (const part of fmt.formatToParts(new Date())) p[part.type] = part.value;
  if (p.hour === '24') p.hour = '00';
  return `${p.day}/${p.month}/${p.year} ${p.hour}:${p.minute}`;
};

const fatalReport = (
  file: File,
  kind: 'visitors' | 'supportTeam',
  existingCount: number,
  message: string,
): ExcelImportReport => ({
  fileName: file.name,
  kind,
  checkedAt: nowStamp(),
  totalRows: 0,
  validRows: 0,
  errorRows: 0,
  duplicateRows: 0,
  overLimitRows: 0,
  remainingSlots: Math.max(0, EXCEL_MAX_MEMBERS - existingCount),
  resultingCount: existingCount,
  errors: [],
  data: [],
  fatalMessage: message,
});

/**
 * Validates one sheet against the CURRENT contents of the section it will be appended to.
 *
 * Every failure mode is returned as a report rather than thrown: a corrupt workbook used
 * to escape as an unhandled promise rejection from `XLSX.read`, which left the button
 * spinning and told the user nothing.
 */
export const validatePersonExcel = async (
  file: File,
  kind: 'visitors' | 'supportTeam',
  existing: PersonRow[],
  t: ExcelTranslator,
): Promise<ExcelImportReport> => {
  if (!isAllowedExcelFile(file)) {
    return fatalReport(file, kind, existing.length, t('visitRequest:excel.errors.invalidFileType'));
  }
  if (file.size > MAX_EXCEL_FILE_BYTES) {
    return fatalReport(file, kind, existing.length, t('visitRequestV2:excel.tooLarge', { maxMb: 5 }));
  }

  let parsed: { error: string } | ParsedSheet;
  try {
    parsed = parseSheet(await readFirstSheet(file), t);
  } catch {
    return fatalReport(file, kind, existing.length, t('visitRequestV2:excel.parseFailed'));
  }
  if ('error' in parsed) return fatalReport(file, kind, existing.length, parsed.error);

  const scan = scanRows(parsed, { existing, t });
  if (scan.totalRows === 0) {
    return fatalReport(file, kind, existing.length, t('visitRequest:excel.errors.headerOnly'));
  }

  const blocked = scan.errorRows > 0 || scan.overLimitRows > 0;
  return {
    fileName: file.name,
    kind,
    checkedAt: nowStamp(),
    totalRows: scan.totalRows,
    validRows: scan.entries.length,
    errorRows: scan.errorRows,
    duplicateRows: scan.duplicateRows,
    overLimitRows: scan.overLimitRows,
    remainingSlots: scan.remainingSlots,
    resultingCount: blocked ? existing.length : existing.length + scan.entries.length,
    errors: scan.errors,
    // A blocked file changes nothing: the caller has no rows to apply (plan §4.4).
    data: blocked ? [] : scan.entries,
  };
};

/** Convenience wrappers so call sites read as the section they belong to. */
export const validateVisitorExcel = (file: File, existing: VisitorEntry[], t: ExcelTranslator) =>
  validatePersonExcel(file, 'visitors', existing as PersonRow[], t);

export const validateSupportTeamExcel = (file: File, existing: SupportTeamEntry[], t: ExcelTranslator) =>
  validatePersonExcel(file, 'supportTeam', existing as PersonRow[], t);
