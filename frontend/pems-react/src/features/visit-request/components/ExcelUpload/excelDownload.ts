import * as XLSX from 'xlsx';
import type { ExcelTranslator, ExcelImportReport } from './excelValidator';

const saveWorkbook = (wb: XLSX.WorkBook, filename: string) => {
  XLSX.writeFile(wb, filename);
};

const makeSheet = (rows: (string | number)[][], colWidths: number[]): XLSX.WorkSheet => {
  const ws = XLSX.utils.aoa_to_sheet(rows);
  ws['!cols'] = colWidths.map((wch) => ({ wch }));
  return ws;
};

/**
 * Headers are written in the language the user is currently browsing in. `excelValidator`
 * matches every column against all of its known aliases, so a template downloaded in one
 * language still uploads correctly in the other.
 */
const headerRow = (t: ExcelTranslator): string[] => [
  t('visitRequest:excel.template.index'),
  t('visitRequest:excel.template.fullName'),
  t('visitRequest:excel.template.jobTitle'),
  t('visitRequest:excel.template.organization'),
  t('visitRequest:excel.template.nationality'),
];

export const downloadVisitorTemplate = (t: ExcelTranslator) => {
  const sample = [
    1,
    t('visitRequest:excel.template.sampleVisitorName'),
    t('visitRequest:excel.template.sampleVisitorJobTitle'),
    t('visitRequest:excel.template.sampleVisitorOrganization'),
    t('visitRequest:excel.template.sampleNationality'),
  ];
  const ws = makeSheet([headerRow(t), sample], [6, 28, 20, 30, 18]);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, t('visitRequest:excel.template.visitorsSheet'));
  saveWorkbook(wb, 'visit-guests-template.xlsx');
};

export const downloadSupportTeamTemplate = (t: ExcelTranslator) => {
  const sample = [
    1,
    t('visitRequest:excel.template.sampleSupportName'),
    t('visitRequest:excel.template.sampleSupportJobTitle'),
    t('visitRequest:excel.template.sampleSupportOrganization'),
    t('visitRequest:excel.template.sampleNationality'),
  ];
  const ws = makeSheet([headerRow(t), sample], [6, 28, 20, 28, 18]);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, t('visitRequest:excel.template.supportSheet'));
  saveWorkbook(wb, 'support-team-template.xlsx');
};

/**
 * The rejected file's report, as a workbook the user can open next to their own sheet
 * (plan §5). A summary block first — so the numbers survive being forwarded to whoever
 * actually owns the list — then one row per problem, keyed by the SAME row number the
 * user sees in Excel.
 */
export const downloadExcelErrorReport = (
  report: ExcelImportReport,
  campusLabel: string,
  t: ExcelTranslator,
) => {
  const L = (key: string) => t(`visitRequestV2:excel.report.${key}`);
  const kindLabel = report.kind === 'visitors'
    ? t('visitRequestV2:card.visitors')
    : t('visitRequestV2:card.supportTeam');

  const summary: (string | number)[][] = [
    [L('title')],
    [],
    [L('fileName'), report.fileName],
    [L('dataKind'), kindLabel],
    [L('campus'), campusLabel],
    [L('checkedAt'), report.checkedAt],
    [L('totalRows'), report.totalRows],
    [L('validRows'), report.validRows],
    [L('errorRows'), report.errorRows],
    [L('duplicateRows'), report.duplicateRows],
    [L('overLimitRows'), report.overLimitRows],
  ];
  if (report.fatalMessage) summary.push([L('fatal'), report.fatalMessage]);

  const detail: (string | number)[][] = [
    [],
    [L('detailHeading')],
    [L('colRow'), L('colColumn'), L('colMessage')],
    ...report.errors.map(e => [e.row, e.column, e.message]),
  ];

  const ws = makeSheet([...summary, ...detail], [22, 26, 60]);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, L('sheetName'));
  // Keep the source file's stem so the report is obviously about THAT upload.
  const stem = report.fileName.replace(/\.[^.]+$/, '') || 'import';
  saveWorkbook(wb, `${stem}-error-report.xlsx`);
};
