/**
 * "Xuất thống kê PDF" for a Department Leader — the logistics cost summary they print and sign.
 *
 * Built as DOM nodes; see `printDocument.ts` for why nothing here is assembled as an HTML string.
 */
import { el, officialHeader, row, section, table } from './printDocument';

export interface DeptInvoiceStatsLine {
  title: string;
  delegationName: string;
  /** Already formatted for display by the caller. */
  usageDate: string;
  quantity: number;
  totalExpense: number;
  noExpense: boolean;
}

export interface DeptInvoiceStatsModel {
  departmentName: string;
  periodFrom: string;
  periodTo: string;
  lines: DeptInvoiceStatsLine[];
  grandTotal: number;
}

export const DEPT_INVOICE_STATS_CSS = `
  body { font-family: 'Segoe UI', Arial, sans-serif; color: #0f172a; padding: 28px; }
  .top { display: flex; justify-content: space-between; border-bottom: 1px solid #cbd5e1; padding-bottom: 12px; margin-bottom: 22px; font-size: 12px; }
  .org { font-weight: 800; text-transform: uppercase; font-size: 13px; }
  .org-sub { color: #64748b; font-weight: 600; }
  .rep { font-weight: 800; font-size: 11px; }
  .rep-accent { color: #f37021; }
  h2 { text-align: center; text-transform: uppercase; margin: 4px 0 2px; font-size: 20px; }
  .sub { text-align: center; font-size: 14px; margin-bottom: 20px; }
  table { width: 100%; border-collapse: collapse; font-size: 13px; }
  th, td { border: 1px solid #475569; padding: 6px 8px; }
  th { background: #f1f5f9; }
  .total td { font-weight: 700; background: #fff7ed; }
  .sign { display: flex; justify-content: space-between; margin-top: 44px; text-align: center; font-size: 14px; }
  .sign div { width: 48%; }
  .sign .hint { font-size: 11px; color: #64748b; }
`;

const money = (v: number) => v.toLocaleString('vi-VN');

export function deptInvoiceStatsTitle(departmentName: string): string {
  return `Thống kê chi phí hậu cần — ${departmentName}`;
}

export function buildDeptInvoiceStatsDocument(model: DeptInvoiceStatsModel): HTMLElement {
  const head = section('thead', [row([
    { text: 'STT', header: true, align: 'center' },
    { text: 'Hạng mục', header: true },
    { text: 'Đoàn khách', header: true },
    { text: 'Ngày', header: true },
    { text: 'SL', header: true, align: 'center' },
    { text: 'Số tiền (₫)', header: true, align: 'right' },
  ])]);

  const body = section('tbody', model.lines.map((line, index) => row([
    { text: String(index + 1), align: 'center' },
    { text: line.title },
    { text: line.delegationName },
    { text: line.usageDate, align: 'center' },
    { text: String(line.quantity), align: 'center' },
    line.noExpense
      ? { text: 'Không có chi phí', align: 'right', italic: true, color: '#64748b' }
      : { text: money(line.totalExpense), align: 'right' },
  ])));

  const foot = section('tfoot', [row([
    { text: 'Tổng số tiền', colSpan: 5, align: 'right' },
    { text: money(model.grandTotal), align: 'right' },
  ], { className: 'total' })]);

  return el('div', {}, [
    officialHeader('TRƯỜNG ĐẠI HỌC FPT', `${model.departmentName} · Hệ thống PEMS`),
    el('h2', { text: 'THỐNG KÊ CHI PHÍ HẬU CẦN TIẾP KHÁCH' }),
    el('p', { className: 'sub' }, [
      el('span', { text: 'Phòng ban: ' }),
      el('b', { text: model.departmentName }),
      el('span', { text: ' · Kỳ: ' }),
      el('b', { text: `${model.periodFrom} – ${model.periodTo}` }),
      el('span', { text: ` · ${model.lines.length} đơn đã hoàn thành` }),
    ]),
    table(head, body, foot),
    el('div', { className: 'sign' }, [
      el('div', {}, [
        el('b', { text: 'ĐẠI DIỆN PHÒNG BAN' }),
        el('div', { text: '(Ký, ghi rõ họ tên)', className: 'hint' }),
      ]),
      el('div', {}, [
        el('b', { text: 'ĐẠI DIỆN VĂN PHÒNG IC' }),
        el('div', { text: '(Ký, ghi rõ họ tên)', className: 'hint' }),
      ]),
    ]),
  ]);
}
