/**
 * Variable chips and email-safe tables, through a REAL Quill (V4 §8.1, §7.3, §17).
 *
 * These do not mock `react-quill-new`. Every defect this branch has caught — the action node being
 * dropped, alignment emitted as an unusable class, Quill restructuring a table — was invisible to a
 * mocked editor and obvious to a real one.
 */
import { beforeAll, describe, expect, it } from 'vitest';
import {
  VARIABLE_CHIP_CLASS, chipNames, chipsToVariables, registerVariableChipBlot, variablesToChips,
} from '../utils/emailEditorVariableChips';
import {
  TABLE_MAX_COLS, TABLE_MAX_ROWS,
  addTableColumn, addTableRow, applyTableEdit, buildEmailTable, nodesToTables, parseEmailTable,
  readTableCells, registerEmailTableBlot, removeTableColumn, removeTableRow, tablesToNodes,
} from '../utils/emailEditorTable';
import { registerEmailEditorFormats } from '../utils/emailEditorFormats';

/* eslint-disable @typescript-eslint/no-explicit-any */
let Quill: any;

const LABELS: Record<string, string> = {
  senderName: 'Họ tên người gửi',
  delegationName: 'Tên đoàn',
};
const labelOf = (n: string) => LABELS[n];

/** Loads content the way the editor does, and returns what the editor made of it. */
function roundTrip(stored: string): string {
  const host = document.body.appendChild(document.createElement('div'));
  const q = new Quill(host, { modules: { toolbar: false } });
  q.clipboard.dangerouslyPasteHTML(tablesToNodes(variablesToChips(stored, labelOf)));
  return nodesToTables(chipsToVariables(q.root.innerHTML));
}

beforeAll(async () => {
  Quill = (await import('react-quill-new')).Quill;
  registerEmailEditorFormats();
  registerVariableChipBlot();
  registerEmailTableBlot();
});

describe('registration', () => {
  it('registers both blots against the real editor', () => {
    expect(registerVariableChipBlot()).toBe(true);
    expect(registerEmailTableBlot()).toBe(true);
    expect(Quill.import('formats/pemsVariable')).toBeTruthy();
    expect(Quill.import('formats/pemsEmailTable')).toBeTruthy();
  });
});

// ── §8.1 variable chips ─────────────────────────────────────────────────────

describe('variable chips', () => {
  it('shows the friendly label but serialises the placeholder', () => {
    const editorHtml = variablesToChips('<p>Kính gửi {{senderName}},</p>', labelOf);

    expect(editorHtml).toContain('Họ tên người gửi');
    expect(editorHtml).toContain('data-variable="senderName"');
    expect(chipsToVariables(editorHtml)).toBe('<p>Kính gửi {{senderName}},</p>');
  });

  it('survives a real editor unchanged', () => {
    expect(roundTrip('<p>Kính gửi {{senderName}} của {{delegationName}}.</p>'))
      .toBe('<p>Kính gửi {{senderName}} của {{delegationName}}.</p>');
  });

  it('labels an undeclared variable with its own name rather than dropping it', () => {
    const html = variablesToChips('<p>{{somethingNew}}</p>', labelOf);

    expect(html).toContain('data-variable="somethingNew"');
    expect(chipsToVariables(html)).toBe('<p>{{somethingNew}}</p>');
  });

  it('is atomic: the label never survives back into stored content', () => {
    const stored = roundTrip('<p>{{senderName}}</p>');

    expect(stored).not.toContain('Họ tên người gửi');
    expect(stored).not.toContain(VARIABLE_CHIP_CLASS);
    expect(stored).toBe('<p>{{senderName}}</p>');
  });

  it('reports the variables in use, in order, including repeats', () => {
    const html = variablesToChips('<p>{{senderName}} {{delegationName}} {{senderName}}</p>', labelOf);

    expect(chipNames(html)).toEqual(['senderName', 'delegationName', 'senderName']);
  });

  it('leaves content with no variables untouched in both directions', () => {
    expect(variablesToChips('<p>không có biến</p>', labelOf)).toBe('<p>không có biến</p>');
    expect(chipsToVariables('<p>không có biến</p>')).toBe('<p>không có biến</p>');
  });

  it('is idempotent, so a double conversion cannot nest chips', () => {
    const once = variablesToChips('<p>{{senderName}}</p>', labelOf);
    expect(variablesToChips(once, labelOf)).toBe(once);
  });
});

// ── §7.3 / §17 email-safe tables ────────────────────────────────────────────

const STORED_TABLE = '<table role="presentation" width="100%" cellpadding="0" cellspacing="0"'
  + ' style="border-collapse:collapse;width:100%;margin:16px 0"><tbody>'
  + '<tr><td style="border:1px solid #dbe4ee;padding:8px 10px;vertical-align:top">Hạng mục</td>'
  + '<td style="border:1px solid #dbe4ee;padding:8px 10px;vertical-align:top">Số lượng</td></tr>'
  + '</tbody></table>';

describe('email-safe tables', () => {
  it('keeps the inline CSS an email needs, which Quill\'s own model strips', () => {
    const out = roundTrip(`<p>trên</p>${STORED_TABLE}<p>dưới</p>`);

    expect(out).toContain('border-collapse:collapse');
    expect(out).toContain('border:1px solid #dbe4ee');
    expect(out).toContain('padding:8px 10px');
    expect(out).toContain('role="presentation"');
  });

  it('keeps the geometry: two cells in one row stay two cells in one row', () => {
    const out = roundTrip(STORED_TABLE);

    // The failure this guards against produced <tr><td>A</td></tr><tr><td>B</td></tr>.
    expect((out.match(/<tr>/g) ?? []).length).toBe(1);
    expect((out.match(/<td/g) ?? []).length).toBe(2);
    expect(out).not.toContain('data-row');
  });

  it('round-trips a stored table byte for byte', () => {
    expect(roundTrip(STORED_TABLE)).toBe(STORED_TABLE);
  });

  it('keeps the text either side of it, in order', () => {
    const out = roundTrip(`<p>trên</p>${STORED_TABLE}<p>dưới</p>`);

    expect(out.indexOf('trên')).toBeLessThan(out.indexOf('<table'));
    expect(out.indexOf('<table')).toBeLessThan(out.indexOf('dưới'));
  });

  it('builds a table an email can render', () => {
    const html = buildEmailTable(2, 2, [['Hạng mục', 'Số lượng'], ['Ghế', '20']]);

    expect(html).toContain('role="presentation"');
    expect(html).toContain('border-collapse:collapse');
    expect(html).not.toContain('class=');   // §17 — no stylesheet exists in mail
    expect(html).toContain('Hạng mục');
    expect(html).toContain('20');
  });

  it('reads its cells back, so the dialog can edit them', () => {
    expect(readTableCells(buildEmailTable(2, 2, [['A', 'B'], ['C', 'D']])))
      .toEqual([['A', 'B'], ['C', 'D']]);
  });

  it('escapes cell text rather than letting it become markup', () => {
    const html = buildEmailTable(1, 1, [['<script>alert(1)</script>']]);

    expect(html).not.toContain('<script>');
    expect(html).toContain('&lt;script&gt;');
  });

  it('leaves content with no table untouched in both directions', () => {
    expect(tablesToNodes('<p>không có bảng</p>')).toBe('<p>không có bảng</p>');
    expect(nodesToTables('<p>không có bảng</p>')).toBe('<p>không có bảng</p>');
  });
});

// ── the table model the dialog edits ────────────────────────────────────────

/**
 * `applyTableEdit` on its own, without a dialog or an editor in the way.
 *
 * The case that matters most here is deleting a row that is not the last one. The first implementation
 * resized the table to the model's row COUNT, which removes from the end — so deleting the header row
 * dropped the final row's markup and then left the header text in place. The table came back with the
 * row the author had just deleted and without one they had not touched.
 */
describe('the table model', () => {
  const THREE_ROWS = '<table style="border-collapse:collapse"><tbody>'
    + '<tr><td style="border:1px solid #dbe4ee">một</td></tr>'
    + '<tr><td style="border:1px solid #dbe4ee">hai</td></tr>'
    + '<tr><td style="border:1px solid #dbe4ee">ba</td></tr>'
    + '</tbody></table>';

  const model = (html: string) => parseEmailTable(html)!;

  it('deletes the row that was asked for, not the last one', () => {
    const out = applyTableEdit(THREE_ROWS, removeTableRow(model(THREE_ROWS), 0));

    expect(out).not.toContain('một');
    expect(out).toContain('hai');
    expect(out).toContain('ba');
  });

  it('deletes the column that was asked for', () => {
    const html = buildEmailTable(1, 3, [['A', 'B', 'C']], { headerRow: false });
    const out = applyTableEdit(html, removeTableColumn(model(html), 0));

    expect(out).not.toContain('>A<');
    expect(out).toContain('>B<');
    expect(out).toContain('>C<');
  });

  it('returns the original markup untouched when the model did not change', () => {
    expect(applyTableEdit(THREE_ROWS, model(THREE_ROWS))).toBe(THREE_ROWS);
  });

  it('keeps inline markup in a cell nobody retyped', () => {
    const html = '<table><tbody><tr><td><strong>đậm</strong></td><td>thường</td></tr></tbody></table>';
    const edited = model(html);
    edited.rows[0][1] = { ...edited.rows[0][1], text: 'đã sửa' };

    const out = applyTableEdit(html, edited);

    expect(out).toContain('<strong>đậm</strong>');
    expect(out).toContain('đã sửa');
  });

  it('escapes text an author typed rather than letting it become markup', () => {
    const html = buildEmailTable(1, 1, [['x']], { headerRow: false });
    const edited = model(html);
    edited.rows[0][0] = { ...edited.rows[0][0], text: '<script>alert(1)</script>' };

    const out = applyTableEdit(html, edited);

    expect(out).not.toContain('<script>');
    expect(out).toContain('&lt;script&gt;');
  });

  it('refuses a nested table instead of flattening it', () => {
    expect(parseEmailTable(
      '<table><tbody><tr><td><table><tbody><tr><td>trong</td></tr></tbody></table></td></tr></tbody></table>',
    )).toBeNull();
  });

  it('stops adding at the safe bounds', () => {
    let m = model(THREE_ROWS);
    for (let i = 0; i < TABLE_MAX_ROWS + 5; i += 1) m = addTableRow(m);
    expect(m.rows.length).toBe(TABLE_MAX_ROWS);

    for (let i = 0; i < TABLE_MAX_COLS + 5; i += 1) m = addTableColumn(m);
    expect(m.rows[0].length).toBe(TABLE_MAX_COLS);
  });

  it('will not empty the table', () => {
    const one = model('<table><tbody><tr><td>một</td></tr></tbody></table>');

    expect(removeTableRow(one, 0).rows.length).toBe(1);
    expect(removeTableColumn(one, 0).rows[0].length).toBe(1);
  });

  it('reads alignment and width back out of the markup it wrote', () => {
    const html = buildEmailTable(1, 1, [['x']], { align: 'center', width: '50%' });
    const m = model(html);

    expect(m.align).toBe('center');
    expect(m.width).toBe('50%');
  });

  it('survives a line break in a cell in both directions', () => {
    const html = '<table><tbody><tr><td>dòng một<br>dòng hai</td></tr></tbody></table>';
    const m = model(html);

    expect(m.rows[0][0].text).toBe('dòng một\ndòng hai');
    expect(applyTableEdit(html, m)).toBe(html);
  });
});

// ── the two together, which is how §16.4 will use them ──────────────────────

describe('a variable inside a table cell', () => {
  it('survives both conversions and the editor', () => {
    const stored = '<table role="presentation" style="border-collapse:collapse"><tbody>'
      + '<tr><td style="border:1px solid #dbe4ee">Đoàn</td>'
      + '<td style="border:1px solid #dbe4ee">{{delegationName}}</td></tr></tbody></table>';

    expect(roundTrip(stored)).toBe(stored);
  });
});
