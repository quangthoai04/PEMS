import * as XLSX from 'xlsx';
import type { ExcelTranslator } from './excelValidator';

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
