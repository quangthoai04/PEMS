import * as XLSX from 'xlsx';
import type {
  ExcelValidationResult,
  SupportTeamExcelValidationResult,
  ExcelValidationError,
  VisitorEntry,
  SupportTeamEntry,
} from '../../types/visitRequest.types';

/** Translator for user-facing Excel messages, supplied by the calling component. */
export type ExcelTranslator = (key: string, options?: Record<string, unknown>) => string;

const ALLOWED_EXTENSIONS = ['xlsx', 'xls'];

export const isAllowedExcelFile = (file: File): boolean => {
  const ext = file.name.split('.').pop()?.toLowerCase();
  return !!ext && ALLOWED_EXTENSIONS.includes(ext);
};

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

const createHash = (fullName: string, jobTitle: string, org: string, nat: string) =>
  `${normalizeStr(fullName)}|${normalizeStr(jobTitle)}|${normalizeStr(org)}|${normalizeStr(nat)}`;

interface RowScan {
  errors: ExcelValidationError[];
  errorRows: Set<number>;
  entries: Record<ColumnId, string>[];
  skippedDuplicates: number;
  totalRows: number;
}

/** Walks the data rows, collecting required-cell errors and de-duplicating entries. */
const scanRows = (
  { rows, colIdx, dataStartCol }: ParsedSheet,
  existingHashes: Set<string>,
  t: ExcelTranslator,
): RowScan => {
  const get = (row: string[], id: ColumnId) =>
    String(row[dataStartCol + (colIdx[id] ?? -1)] ?? '').trim();

  const errors: ExcelValidationError[] = [];
  const errorRows = new Set<number>();
  const entries: Record<ColumnId, string>[] = [];
  let skippedDuplicates = 0;

  rows.slice(1).forEach((row, i) => {
    const rowNum = i + 2;
    if (row.every((c) => String(c).trim() === '')) return;

    for (const id of REQUIRED_COLUMNS) {
      if (!get(row, id)) {
        const column = columnLabel(t, id);
        errors.push({
          row: rowNum,
          column,
          message: t('visitRequest:excel.errors.requiredCell', { row: rowNum, column }),
        });
        errorRows.add(rowNum);
      }
    }

    if (!errorRows.has(rowNum)) {
      const entry = {
        fullName: get(row, 'fullName'),
        jobTitle: get(row, 'jobTitle'),
        organization: get(row, 'organization'),
        nationality: get(row, 'nationality'),
      };
      const hash = createHash(entry.fullName, entry.jobTitle, entry.organization, entry.nationality);
      if (existingHashes.has(hash)) {
        skippedDuplicates++;
      } else {
        existingHashes.add(hash);
        entries.push(entry);
      }
    }
  });

  const totalRows = rows.slice(1).filter((r) => r.some((c) => String(c).trim() !== '')).length;
  return { errors, errorRows, entries, skippedDuplicates, totalRows };
};

const hashesOf = (existing: { fullName: string; jobTitle: string; organization: string; nationality: string }[]) =>
  new Set(existing.map((e) => createHash(e.fullName, e.jobTitle, e.organization, e.nationality)));

// ─── Visitor list ────────────────────────────────────────────────────────────

export const validateVisitorExcel = async (
  file: File,
  existingData: VisitorEntry[] = [],
  t: ExcelTranslator,
): Promise<ExcelValidationResult> => {
  const parsed = parseSheet(await readFirstSheet(file), t);
  if ('error' in parsed) return fail(parsed.error);

  const scan = scanRows(parsed, hashesOf(existingData), t);
  return {
    valid: scan.errors.length === 0,
    totalRows: scan.totalRows,
    errorRows: scan.errorRows.size,
    skippedDuplicates: scan.skippedDuplicates,
    errors: scan.errors,
    data: scan.entries as VisitorEntry[],
  };
};

// ─── Support team list ────────────────────────────────────────────────────────

export const validateSupportTeamExcel = async (
  file: File,
  existingData: SupportTeamEntry[] = [],
  t: ExcelTranslator,
): Promise<SupportTeamExcelValidationResult> => {
  const parsed = parseSheet(await readFirstSheet(file), t);
  if ('error' in parsed) return failSupport(parsed.error);

  const scan = scanRows(parsed, hashesOf(existingData), t);
  return {
    valid: scan.errors.length === 0,
    totalRows: scan.totalRows,
    errorRows: scan.errorRows.size,
    skippedDuplicates: scan.skippedDuplicates,
    errors: scan.errors,
    data: scan.entries as SupportTeamEntry[],
  };
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

const fail = (message: string): ExcelValidationResult => ({
  valid: false, totalRows: 0, errorRows: 0, skippedDuplicates: 0,
  errors: [{ row: 0, column: '', message }],
  data: [],
});

const failSupport = (message: string): SupportTeamExcelValidationResult => ({
  valid: false, totalRows: 0, errorRows: 0, skippedDuplicates: 0,
  errors: [{ row: 0, column: '', message }],
  data: [],
});
