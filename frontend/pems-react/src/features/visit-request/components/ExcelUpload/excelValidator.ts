import * as XLSX from 'xlsx';
import type {
  ExcelValidationResult,
  SupportTeamExcelValidationResult,
  ExcelValidationError,
  VisitorEntry,
  SupportTeamEntry,
} from '../../types/visitRequest.types';

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

const mapHeaders = (headerRow: string[], names: string[]): Record<string, number> => {
  const result: Record<string, number> = {};
  names.forEach((name) => {
    const idx = headerRow.indexOf(name);
    if (idx !== -1) result[name] = idx;
  });
  return result;
};

// ─── Visitor list ────────────────────────────────────────────────────────────

const VISITOR_REQUIRED = ['Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch'];
const VISITOR_ALL = ['Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch'];

export const validateVisitorExcel = async (file: File, existingData: VisitorEntry[] = []): Promise<ExcelValidationResult> => {
  const rows = await readFirstSheet(file);

  if (!rows) return fail('File Excel không có dữ liệu.');
  if (rows.length < 2) return fail('File không có dữ liệu (chỉ có header hoặc rỗng).');

  const normalizeStr = (str: string) => String(str || '').trim().replace(/\s+/g, ' ').toLowerCase();
  const createHash = (fullName: string, jobTitle: string, org: string, nat: string) => 
    `${normalizeStr(fullName)}|${normalizeStr(jobTitle)}|${normalizeStr(org)}|${normalizeStr(nat)}`;

  const existingHashes = new Set(
    existingData.map(e => createHash(e.fullName, e.jobTitle, e.organization, e.nationality))
  );

  const headerRow = rows[0].map((h) => String(h).trim());

  // Skip leading STT column if present
  const dataStartCol = headerRow[0].toLowerCase() === 'stt' ? 1 : 0;
  const effectiveHeader = headerRow.slice(dataStartCol);

  const missing = VISITOR_REQUIRED.filter((h) => !effectiveHeader.includes(h));
  if (missing.length > 0)
    return fail(
      `Thiếu cột bắt buộc: ${missing.join(', ')}. File phải có các cột: ${VISITOR_REQUIRED.join(', ')}.`
    );

  const colIdx = mapHeaders(effectiveHeader, VISITOR_ALL);
  const get = (row: string[], col: string) =>
    String(row[dataStartCol + (colIdx[col] ?? -1)] ?? '').trim();

  const errors: ExcelValidationError[] = [];
  const data: VisitorEntry[] = [];
  const errorRows = new Set<number>();
  let skippedDuplicates = 0;

  rows.slice(1).forEach((row, i) => {
    const rowNum = i + 2;
    if (row.every((c) => String(c).trim() === '')) return;

    VISITOR_REQUIRED.forEach((col) => {
      if (!get(row, col)) {
        errors.push({ row: rowNum, column: col, message: `Dòng ${rowNum}: Cột "${col}" không được để trống.` });
        errorRows.add(rowNum);
      }
    });

    if (!errorRows.has(rowNum)) {
      const entry = {
        fullName: get(row, 'Họ và tên'),
        organization: get(row, 'Đơn vị công tác'),
        nationality: get(row, 'Quốc tịch'),
        jobTitle: get(row, 'Chức vụ'),
      };
      const hash = createHash(entry.fullName, entry.jobTitle, entry.organization, entry.nationality);
      if (existingHashes.has(hash)) {
        skippedDuplicates++;
      } else {
        existingHashes.add(hash);
        data.push(entry);
      }
    }
  });

  const totalRows = rows.slice(1).filter((r) => r.some((c) => String(c).trim() !== '')).length;
  return { valid: errors.length === 0, totalRows, errorRows: errorRows.size, skippedDuplicates, errors, data };
};

// ─── Support team list ────────────────────────────────────────────────────────

const SUPPORT_REQUIRED = ['Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch'];

export const validateSupportTeamExcel = async (file: File, existingData: SupportTeamEntry[] = []): Promise<SupportTeamExcelValidationResult> => {
  const rows = await readFirstSheet(file);

  if (!rows) return failSupport('File Excel không có dữ liệu.');
  if (rows.length < 2) return failSupport('File không có dữ liệu (chỉ có header hoặc rỗng).');

  const normalizeStr = (str: string) => String(str || '').trim().replace(/\s+/g, ' ').toLowerCase();
  const createHash = (fullName: string, jobTitle: string, org: string, nat: string) => 
    `${normalizeStr(fullName)}|${normalizeStr(jobTitle)}|${normalizeStr(org)}|${normalizeStr(nat)}`;

  const existingHashes = new Set(
    existingData.map(e => createHash(e.fullName, e.jobTitle, e.organization, e.nationality))
  );

  const headerRow = rows[0].map((h) => String(h).trim());
  const dataStartCol = headerRow[0].toLowerCase() === 'stt' ? 1 : 0;
  const effectiveHeader = headerRow.slice(dataStartCol);

  const missing = SUPPORT_REQUIRED.filter((h) => !effectiveHeader.includes(h));
  if (missing.length > 0)
    return failSupport(
      `Thiếu cột bắt buộc: ${missing.join(', ')}. File phải có các cột: ${SUPPORT_REQUIRED.join(', ')}.`
    );

  const colIdx = mapHeaders(effectiveHeader, SUPPORT_REQUIRED);
  const get = (row: string[], col: string) =>
    String(row[dataStartCol + (colIdx[col] ?? -1)] ?? '').trim();

  const errors: ExcelValidationError[] = [];
  const data: SupportTeamEntry[] = [];
  const errorRows = new Set<number>();
  let skippedDuplicates = 0;

  rows.slice(1).forEach((row, i) => {
    const rowNum = i + 2;
    if (row.every((c) => String(c).trim() === '')) return;

    SUPPORT_REQUIRED.forEach((col) => {
      if (!get(row, col)) {
        errors.push({ row: rowNum, column: col, message: `Dòng ${rowNum}: Cột "${col}" không được để trống.` });
        errorRows.add(rowNum);
      }
    });

    if (!errorRows.has(rowNum)) {
      const entry = {
        fullName: get(row, 'Họ và tên'),
        jobTitle: get(row, 'Chức vụ'),
        organization: get(row, 'Đơn vị công tác'),
        nationality: get(row, 'Quốc tịch'),
      };
      const hash = createHash(entry.fullName, entry.jobTitle, entry.organization, entry.nationality);
      if (existingHashes.has(hash)) {
        skippedDuplicates++;
      } else {
        existingHashes.add(hash);
        data.push(entry);
      }
    }
  });

  const totalRows = rows.slice(1).filter((r) => r.some((c) => String(c).trim() !== '')).length;
  return { valid: errors.length === 0, totalRows, errorRows: errorRows.size, skippedDuplicates, errors, data };
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
