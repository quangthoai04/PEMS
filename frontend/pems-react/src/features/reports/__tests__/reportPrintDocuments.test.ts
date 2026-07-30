/**
 * G6.6 — the two statistics print surfaces must never execute what the server sent.
 *
 * Both printers used to interpolate API values into an HTML string handed to `document.write`. They were
 * safe only because every interpolation happened to sit in text position, where the local `esc()` covering
 * `& < >` is enough — a property no test held and no reader could see from the call site. These tests hold
 * the replacement: the document is built from DOM nodes, so a hostile value can only ever become text.
 *
 * The printed document is produced in a real iframe, which is what `window.open` hands the code in the
 * browser — asserting on the builder's return value alone would miss anything the render step did.
 */
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { renderPrintDocument, PRINT_SKELETON } from '../print/printDocument';
import {
  DEPT_INVOICE_STATS_CSS,
  buildDeptInvoiceStatsDocument,
  deptInvoiceStatsTitle,
} from '../print/deptInvoiceStatsDocument';
import {
  STAFF_LEADER_EXPENSE_CSS,
  buildStaffLeaderExpenseDocument,
  staffLeaderExpenseTitle,
} from '../print/staffLeaderExpenseDocument';

/** The payloads a compromised or careless server row could carry into a printed report. */
const HOSTILE = {
  tableBreakout: '</td><script>alert(1)</script>',
  imageHandler: '<img src=x onerror=alert(1)>',
  attributeBreakout: '" onmouseover="alert(1)" data-x="',
  singleQuoteBreakout: "' onfocus='alert(1)",
  javascriptUrl: 'javascript:alert(document.cookie)',
  validMarkup: '<b>đậm</b> và <a href="https://fpt.edu.vn">liên kết</a>',
  vietnamese: 'Đoàn Đại học Cần Thơ — phòng họp số 3 (đợt 1) · giá 1.500.000₫',
};

const ALL_HOSTILE = Object.values(HOSTILE);

let frame: HTMLIFrameElement;

/** A print target backed by a real document, the way `window.open` provides one. */
const target = () => ({
  document: frame.contentDocument as Document,
  focus: () => {},
  print: () => {},
});

beforeEach(() => {
  frame = document.createElement('iframe');
  document.body.appendChild(frame);
});

afterEach(() => {
  frame.remove();
});

/** Everything a printed document must not contain, whatever the server sent. */
function expectInert(doc: Document) {
  expect(doc.querySelectorAll('script')).toHaveLength(0);
  expect(doc.querySelectorAll('iframe, object, embed, form, style[onload]')).toHaveLength(0);

  for (const element of Array.from(doc.querySelectorAll('*'))) {
    for (const attr of Array.from(element.attributes)) {
      expect(attr.name.toLowerCase().startsWith('on')).toBe(false);
      if (['href', 'src', 'action', 'formaction'].includes(attr.name.toLowerCase())) {
        expect(attr.value.replace(/\s+/g, '').toLowerCase()).not.toMatch(/^(javascript|vbscript|data:text\/html)/);
      }
    }
  }

  // No anchors at all on these two surfaces: neither report has a link, so a `javascript:` value has
  // nowhere to land even before the scheme check above.
  expect(doc.querySelectorAll('a')).toHaveLength(0);
}

/** Every hostile string must have survived as literal text, not been parsed away or executed. */
function expectRenderedAsText(doc: Document, values: string[]) {
  const text = doc.body.textContent ?? '';
  for (const value of values) expect(text).toContain(value);
}

describe('Department invoice statistics print document', () => {
  const model = {
    departmentName: HOSTILE.attributeBreakout,
    periodFrom: '01/07/2026',
    periodTo: '31/07/2026',
    grandTotal: 1500000,
    lines: [
      {
        title: HOSTILE.tableBreakout,
        delegationName: HOSTILE.imageHandler,
        usageDate: '15/07/2026',
        quantity: 2,
        totalExpense: 1500000,
        noExpense: false,
      },
      {
        title: HOSTILE.javascriptUrl,
        delegationName: HOSTILE.validMarkup,
        usageDate: '16/07/2026',
        quantity: 1,
        totalExpense: 0,
        noExpense: true,
      },
      {
        title: HOSTILE.vietnamese,
        delegationName: HOSTILE.singleQuoteBreakout,
        usageDate: '17/07/2026',
        quantity: 3,
        totalExpense: 0,
        noExpense: false,
      },
    ],
  };

  it('executes nothing from the server data', () => {
    renderPrintDocument(target(), {
      title: deptInvoiceStatsTitle(model.departmentName),
      css: DEPT_INVOICE_STATS_CSS,
      root: buildDeptInvoiceStatsDocument(model),
    });

    expectInert(frame.contentDocument as Document);
  });

  it('prints every hostile value as text, and Vietnamese unchanged', () => {
    renderPrintDocument(target(), {
      title: deptInvoiceStatsTitle(model.departmentName),
      css: DEPT_INVOICE_STATS_CSS,
      root: buildDeptInvoiceStatsDocument(model),
    });

    const doc = frame.contentDocument as Document;
    expectRenderedAsText(doc, ALL_HOSTILE);
    // Valid markup is shown, not applied: no <b> element came from the data.
    expect(doc.querySelector('td b')).toBeNull();
  });

  it('keeps the title a title, even when the value tries to close the tag', () => {
    renderPrintDocument(target(), {
      title: deptInvoiceStatsTitle('</title><script>alert(1)</script>'),
      css: DEPT_INVOICE_STATS_CSS,
      root: buildDeptInvoiceStatsDocument(model),
    });

    const doc = frame.contentDocument as Document;
    expect(doc.title).toContain('</title><script>alert(1)</script>');
    expect(doc.querySelectorAll('script')).toHaveLength(0);
  });

  it('still renders the numbers and the layout it is supposed to', () => {
    renderPrintDocument(target(), {
      title: deptInvoiceStatsTitle(model.departmentName),
      css: DEPT_INVOICE_STATS_CSS,
      root: buildDeptInvoiceStatsDocument(model),
    });

    const doc = frame.contentDocument as Document;
    expect(doc.querySelectorAll('tbody tr')).toHaveLength(3);
    expect(doc.body.textContent).toContain('THỐNG KÊ CHI PHÍ HẬU CẦN TIẾP KHÁCH');
    expect(doc.querySelector('tfoot')?.textContent).toContain('1.500.000');
    expect(doc.body.textContent).toContain('Không có chi phí');
  });
});

describe('Staff Leader expense statistics print document', () => {
  const model = {
    campusName: HOSTILE.imageHandler,
    periodFrom: '01/07/2026',
    periodTo: '31/07/2026',
    grandTotal: 2400000,
    byType: [{ label: HOSTILE.tableBreakout, count: 2, total: 2400000 }],
    byDepartment: [{ label: HOSTILE.attributeBreakout, count: 1, total: 900000 }],
    visits: [{
      delegationName: HOSTILE.validMarkup,
      requestCode: HOSTILE.singleQuoteBreakout,
      visitDate: '15/07/2026',
      totalExpense: 2400000,
      reports: [
        {
          source: HOSTILE.vietnamese,
          noExpense: false,
          title: 'Đơn yêu cầu',
          note: HOSTILE.javascriptUrl,
          items: [{
            itemName: HOSTILE.tableBreakout,
            typeLabel: 'Hạng mục yêu cầu',
            source: HOSTILE.vietnamese,
            quantity: 2,
            unitPrice: 1200000,
            totalAmount: 2400000,
          }],
        },
        {
          source: 'Host',
          noExpense: true,
          title: HOSTILE.imageHandler,
          note: null,
          items: [],
        },
      ],
    }],
  };

  it('executes nothing from the server data', () => {
    renderPrintDocument(target(), {
      title: staffLeaderExpenseTitle(model.campusName),
      css: STAFF_LEADER_EXPENSE_CSS,
      root: buildStaffLeaderExpenseDocument(model),
    });

    expectInert(frame.contentDocument as Document);
  });

  it('prints every hostile value as text, and Vietnamese unchanged', () => {
    renderPrintDocument(target(), {
      title: staffLeaderExpenseTitle(model.campusName),
      css: STAFF_LEADER_EXPENSE_CSS,
      root: buildStaffLeaderExpenseDocument(model),
    });

    const doc = frame.contentDocument as Document;
    expectRenderedAsText(doc, ALL_HOSTILE);
    expect(doc.querySelector('td b')).toBeNull();
    expect(doc.querySelector('td img')).toBeNull();
  });

  it('still renders all three parts with their totals', () => {
    renderPrintDocument(target(), {
      title: staffLeaderExpenseTitle(model.campusName),
      css: STAFF_LEADER_EXPENSE_CSS,
      root: buildStaffLeaderExpenseDocument(model),
    });

    const doc = frame.contentDocument as Document;
    const headings = Array.from(doc.querySelectorAll('h3')).map(h => h.textContent);
    expect(headings).toEqual([
      'Phần 1 · Chi phí theo từng đoàn',
      'Phần 2 · Thống kê theo loại chi phí',
      'Phần 3 · Chi phí phải thanh toán cho từng phòng ban',
    ]);
    expect(doc.querySelector('tfoot')?.textContent).toContain('2.400.000');
    expect(doc.body.textContent).toContain('Không có chi phí');
  });

  it('shows the empty-state rows rather than an empty table', () => {
    renderPrintDocument(target(), {
      title: staffLeaderExpenseTitle('FPTU Hà Nội'),
      css: STAFF_LEADER_EXPENSE_CSS,
      root: buildStaffLeaderExpenseDocument({ ...model, byType: [], byDepartment: [] }),
    });

    const text = (frame.contentDocument as Document).body.textContent ?? '';
    expect(text).toContain('Không có hạng mục nào');
    expect(text).toContain('Không có phòng ban nào kê khai chi phí');
  });
});

describe('print skeleton', () => {
  it('carries no interpolation of any kind', () => {
    expect(PRINT_SKELETON).not.toMatch(/\$\{|\+/);
    expect(PRINT_SKELETON).toBe(
      '<!doctype html><html><head><meta charset="utf-8"></head><body></body></html>');
  });
});
