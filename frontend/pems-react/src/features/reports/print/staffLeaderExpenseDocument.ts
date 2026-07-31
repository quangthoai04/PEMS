/**
 * "Xuất thống kê PDF" for a Staff Leader — the three-part campus cost summary.
 *
 * Built as DOM nodes; see `printDocument.ts` for why nothing here is assembled as an HTML string.
 */
import { el, officialHeader, row, section, table } from './printDocument';

export interface ExpenseItemLine {
  itemName: string;
  typeLabel: string;
  source: string;
  quantity: number;
  unitPrice: number;
  totalAmount: number;
}

export interface ExpenseReportBlock {
  source: string;
  noExpense: boolean;
  /** Only meaningful when `noExpense`; the request the empty declaration belongs to. */
  title: string;
  note: string | null;
  items: ExpenseItemLine[];
}

export interface ExpenseVisitBlock {
  delegationName: string;
  requestCode: string;
  /** Already formatted for display by the caller. */
  visitDate: string;
  totalExpense: number;
  reports: ExpenseReportBlock[];
}

export interface ExpenseTotalRow {
  label: string;
  count: number;
  total: number;
}

export interface StaffLeaderExpenseModel {
  campusName: string;
  periodFrom: string;
  periodTo: string;
  visits: ExpenseVisitBlock[];
  grandTotal: number;
  byType: ExpenseTotalRow[];
  byDepartment: ExpenseTotalRow[];
}

export const STAFF_LEADER_EXPENSE_CSS = `
  body { font-family: 'Segoe UI', Arial, sans-serif; color: #0f172a; padding: 28px; }
  .top { display: flex; justify-content: space-between; border-bottom: 1px solid #cbd5e1; padding-bottom: 12px; margin-bottom: 22px; font-size: 12px; }
  .org { font-weight: 800; text-transform: uppercase; font-size: 13px; }
  .org-sub { color: #64748b; font-weight: 600; }
  .rep { font-weight: 800; font-size: 11px; }
  .rep-accent { color: #f37021; }
  h2 { text-align: center; text-transform: uppercase; margin: 4px 0 2px; font-size: 20px; }
  h3 { font-size: 14px; text-transform: uppercase; color: #004c91; margin: 22px 0 8px; }
  .sub { text-align: center; font-size: 14px; margin-bottom: 20px; }
  table { width: 100%; border-collapse: collapse; font-size: 12px; }
  th, td { border: 1px solid #475569; padding: 5px 7px; }
  th { background: #f1f5f9; }
  .total td { font-weight: 700; background: #fff7ed; }
  .visit td { background: #eef2f7; font-weight: 700; }
`;

const money = (v: number) => v.toLocaleString('vi-VN');

export function staffLeaderExpenseTitle(campusName: string): string {
  return `Thống kê chi phí tiếp khách — ${campusName}`;
}

function visitRows(visit: ExpenseVisitBlock, index: number): HTMLTableRowElement[] {
  const rows: HTMLTableRowElement[] = [
    row([
      { text: `${index + 1}. ${visit.delegationName} (${visit.requestCode}) — ${visit.visitDate}`, colSpan: 5 },
      { text: money(visit.totalExpense), align: 'right' },
    ], { className: 'visit' }),
  ];

  for (const report of visit.reports) {
    if (report.noExpense) {
      rows.push(row([
        { text: `${report.title} — ${report.source}: Không có chi phí`, colSpan: 5, color: '#64748b', italic: true },
        { text: '0', align: 'right' },
      ]));
      continue;
    }

    for (const item of report.items) {
      rows.push(row([
        { text: item.itemName },
        { text: item.typeLabel },
        { text: item.source },
        { text: String(Math.floor(item.quantity || 0)), align: 'right' },
        { text: money(item.unitPrice), align: 'right' },
        { text: money(item.totalAmount), align: 'right' },
      ]));
    }

    if (report.note) {
      rows.push(row([
        { text: `Ghi chú (${report.source}): ${report.note}`, colSpan: 6, color: '#64748b', italic: true },
      ]));
    }
  }

  return rows;
}

function totalsTable(rows: ExpenseTotalRow[], firstHeader: string, emptyText: string): HTMLTableElement {
  const head = section('thead', [row([
    { text: firstHeader, header: true },
    { text: 'Số hạng mục', header: true, align: 'right' },
    { text: 'Tổng tiền (₫)', header: true, align: 'right' },
  ])]);

  const body = section('tbody', rows.length === 0
    ? [row([{ text: emptyText, colSpan: 3, align: 'center', color: '#64748b' }])]
    : rows.map(r => row([
      { text: r.label },
      { text: String(r.count), align: 'right' },
      { text: money(r.total), align: 'right' },
    ])));

  return table(head, body);
}

export function buildStaffLeaderExpenseDocument(model: StaffLeaderExpenseModel): HTMLElement {
  const detailHead = section('thead', [row([
    { text: 'Hạng mục', header: true },
    { text: 'Loại', header: true },
    { text: 'Bên kê khai', header: true },
    { text: 'SL', header: true, align: 'right' },
    { text: 'Đơn giá (₫)', header: true, align: 'right' },
    { text: 'Thành tiền (₫)', header: true, align: 'right' },
  ])]);

  const detailBody = section('tbody', model.visits.flatMap((visit, index) => visitRows(visit, index)));

  const detailFoot = section('tfoot', [row([
    { text: 'Tổng chi phí các đoàn', colSpan: 5, align: 'right' },
    { text: money(model.grandTotal), align: 'right' },
  ], { className: 'total' })]);

  return el('div', {}, [
    officialHeader(`TRƯỜNG ĐẠI HỌC FPT — ${model.campusName}`, 'Văn phòng IC · Hệ thống PEMS'),
    el('h2', { text: 'BẢNG THỐNG KÊ CHI PHÍ TIẾP KHÁCH' }),
    el('p', { className: 'sub' }, [
      el('span', { text: 'Kỳ: ' }),
      el('b', { text: `${model.periodFrom} – ${model.periodTo}` }),
      el('span', { text: ` · ${model.visits.length} đoàn` }),
    ]),

    el('h3', { text: 'Phần 1 · Chi phí theo từng đoàn' }),
    table(detailHead, detailBody, detailFoot),

    el('h3', { text: 'Phần 2 · Thống kê theo loại chi phí' }),
    totalsTable(model.byType, 'Loại', 'Không có hạng mục nào'),

    el('h3', { text: 'Phần 3 · Chi phí phải thanh toán cho từng phòng ban' }),
    totalsTable(model.byDepartment, 'Phòng ban', 'Không có phòng ban nào kê khai chi phí'),
  ]);
}
