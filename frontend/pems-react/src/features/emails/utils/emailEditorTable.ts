/**
 * Email-safe tables, kept out of Quill's table model on purpose (V4 §7.3, §16.4, §17).
 *
 * <b>Why not Quill's own.</b> Quill 2 owns a table as rows of cells it manages. Measured on 2.0.3,
 * pasting a table an email can actually use through it:
 *
 *     in   <table role="presentation" style="border-collapse:collapse">
 *            <tbody><tr><td style="border:1px solid #dbe4ee;padding:8px">A</td>
 *                       <td style="border:1px solid #dbe4ee;padding:8px">B</td></tr></tbody></table>
 *     out  <table><tbody><tr><td data-row="1">A</td><td data-row="1">B</td></tr></tbody></table>
 *
 * Every declaration that makes a table VISIBLE in mail — the collapse, the borders, the padding — is
 * gone, and `role="presentation"` with it. Worse, attempts to preserve them by subclassing its cell blot
 * corrupted the geometry: two cells in one row came back as two rows. A table editor that silently
 * restructures a table is not a feature.
 *
 * <b>What this does instead.</b> One atomic block embed holding the table's markup verbatim. Quill treats
 * it as a single indivisible object, so it cannot reflow, restyle or re-row anything: what was loaded is
 * what is saved. Editing happens through {@link parseEmailTable} / {@link applyTableEdit}, which the
 * table dialog drives.
 *
 * <b>Why editing PATCHES rather than regenerates.</b> `applyTableEdit` mutates the original markup and
 * serialises that, instead of building a fresh table from the model. Regenerating would rewrite every
 * cell, every style and every attribute of a table the author only meant to add one row to — and since
 * dirty-state is decided by comparing HTML, it would report a document as edited for opening a dialog
 * and pressing Save. Patching makes an untouched table come back byte-identical, so "changed" keeps
 * meaning changed.
 */
import { Quill } from 'react-quill-new';

export const TABLE_BLOT_NAME = 'pemsEmailTable';
export const TABLE_WRAPPER_CLASS = 'pems-email-table';

/** Matches a rendered table node, wrapper and all. */
const TABLE_NODE = new RegExp(
  '<div\\b[^>]*\\bdata-email-table\\s*=\\s*"1"[^>]*>([\\s\\S]*?)</div\\s*>',
  'gi',
);

/** The cell border/padding every generated table uses — §17's inline-CSS requirement. */
const CELL_STYLE = 'border:1px solid #dbe4ee;padding:8px 10px;vertical-align:top';
const HEADER_STYLE = `${CELL_STYLE};background:#f8fafc;font-weight:600;text-align:left`;

/**
 * Safe bounds. Eight columns is already tight inside the 560px shell every PEMS mail renders in; beyond
 * that Outlook stops honouring the widths and the columns crush to their longest syllable, which is the
 * failure mode §7.3 introduced tables to avoid in the first place.
 */
export const TABLE_MAX_ROWS = 20;
export const TABLE_MAX_COLS = 8;
export const TABLE_MIN_ROWS = 1;
export const TABLE_MIN_COLS = 1;

/** How the table sits in the message. Emitted as the `align` attribute AND a margin, for Outlook. */
export type EmailTableAlign = 'left' | 'center' | 'right';

/** §6.3's fixed ladder, applied to width: a preset, never a number somebody typed. */
export const TABLE_WIDTH_PRESETS = ['100%', '75%', '50%', 'auto'] as const;
export type EmailTableWidth = typeof TABLE_WIDTH_PRESETS[number];

/**
 * One cell.
 *
 * `text` is what the dialog edits — plain text, `\n` for a line break, `{{name}}` for a variable. `html`
 * is what the cell held when it was read. Keeping both is what lets an untouched cell be written back
 * exactly as it was, including inline markup this model has no way to express: a cell whose text the
 * author did not change is not rewritten, so nothing is lost by opening the dialog.
 */
export interface EmailTableCell {
  text: string;
  /** The cell's original inner HTML, or `undefined` for a cell that did not exist before. */
  html?: string;
  /**
   * Where this cell sat in the table that was read, as `row:col`. Absent on a cell the author added.
   *
   * It is what makes a deletion land on the right markup. Matching by POSITION instead — resizing the
   * table to the model's row count — removes from the end, so deleting the first row deleted the last
   * row's cells and then wrote the survivors' text over what was left. The visible result was a table
   * that lost the wrong line and kept the one the author had just removed.
   */
  key?: string;
}

export interface EmailTableModel {
  rows: EmailTableCell[][];
  /** True when the first row is `<th>` — a heading, not data. */
  headerRow: boolean;
  align: EmailTableAlign;
  width: EmailTableWidth;
}

let registered = false;

/**
 * Registers the table blot. Reports rather than throws, for the reason the other registrations do: it
 * runs at module scope, and a mocked `react-quill-new` has no `Quill` to register against.
 */
export function registerEmailTableBlot(): boolean {
  if (registered) return true;

  /* eslint-disable @typescript-eslint/no-explicit-any */
  let q: any;
  try {
    q = Quill as any;
  } catch {
    return false;
  }
  if (!q || typeof q.import !== 'function' || typeof q.register !== 'function') return false;

  const BlockEmbed: any = q.import('blots/block/embed');
  if (!BlockEmbed) return false;

  class EmailTable extends BlockEmbed {
    static blotName = TABLE_BLOT_NAME;
    static tagName = 'div';
    static className = TABLE_WRAPPER_CLASS;

    static create(value: string): HTMLElement {
      const node: HTMLElement = super.create(value);
      node.setAttribute('data-email-table', '1');
      // The wrapper is not editable; the table inside it is markup the editor must not reflow.
      node.setAttribute('contenteditable', 'false');
      node.innerHTML = value ?? '';
      return node;
    }

    /** The table markup exactly as it stands — this is what makes the round trip lossless. */
    static value(node: HTMLElement): string {
      return node.innerHTML;
    }
  }

  q.register(EmailTable, true);
  /* eslint-enable @typescript-eslint/no-explicit-any */
  registered = true;
  return true;
}

/** Builds an email-safe table: inline CSS only, collapsed borders, presentation role. */
export function buildEmailTable(
  rows: number,
  cols: number,
  cells?: string[][],
  options?: { headerRow?: boolean; align?: EmailTableAlign; width?: EmailTableWidth },
): string {
  const safeRows = clamp(rows, TABLE_MIN_ROWS, TABLE_MAX_ROWS);
  const safeCols = clamp(cols, TABLE_MIN_COLS, TABLE_MAX_COLS);
  const headerRow = options?.headerRow ?? true;
  const align = options?.align ?? 'left';
  const width = options?.width ?? '100%';

  const body: string[] = [];
  for (let r = 0; r < safeRows; r += 1) {
    const isHeader = headerRow && r === 0;
    const tag = isHeader ? 'th' : 'td';
    const style = isHeader ? HEADER_STYLE : CELL_STYLE;

    const cellsHtml: string[] = [];
    for (let c = 0; c < safeCols; c += 1) {
      const text = cells?.[r]?.[c] ?? '';
      cellsHtml.push(`<${tag} style="${style}">${text === '' ? '&nbsp;' : cellTextToHtml(text)}</${tag}>`);
    }
    body.push(`<tr>${cellsHtml.join('')}</tr>`);
  }

  const widthAttr = width === 'auto' ? '' : ` width="${width}"`;
  const alignAttr = align === 'left' ? '' : ` align="${align}"`;
  const style = `border-collapse:collapse;${width === 'auto' ? '' : `width:${width};`}margin:${marginFor(align)}`;

  return `<table role="presentation"${widthAttr} cellpadding="0" cellspacing="0"${alignAttr}`
    + ` style="${style}">`
    + `<tbody>${body.join('')}</tbody></table>`;
}

/** The text of each cell, for the dialog that edits one. */
export function readTableCells(tableHtml: string): string[][] {
  return parseEmailTable(tableHtml)?.rows.map((row) => row.map((cell) => cell.text)) ?? [];
}

/**
 * Reads a table into the model the dialog edits, or `null` when it cannot be edited safely.
 *
 * Returns `null` for a table containing another table. Nested tables are refused rather than flattened:
 * the row/column model here has no way to represent one, so every operation the dialog offers — add a
 * column, drop a row — would be applied to a shape the author is not looking at.
 */
export function parseEmailTable(tableHtml: string | null | undefined): EmailTableModel | null {
  const table = firstTable(tableHtml);
  if (!table) return null;
  if (table.querySelector('table')) return null;    // nested — see above

  const rows = Array.from(table.querySelectorAll('tr')).map((tr, r) =>
    Array.from(tr.querySelectorAll('th, td')).map((cell, c) => ({
      text: cellHtmlToText(cell.innerHTML),
      html: cell.innerHTML,
      key: `${r}:${c}`,
    })));

  if (rows.length === 0 || rows[0].length === 0) return null;

  const firstRow = table.querySelector('tr');
  const headerRow = !!firstRow
    && firstRow.querySelectorAll('th').length > 0
    && firstRow.querySelectorAll('td').length === 0;

  return { rows, headerRow, align: readAlign(table), width: readWidth(table) };
}

/**
 * Applies the model back onto the ORIGINAL markup and returns the result.
 *
 * <b>Reuses the original elements rather than generating new ones.</b> Each model cell carries the
 * `key` of the cell it was read from, so a cell whose text is unchanged is written back as the very
 * element it came from — its inline style, its colspan, its inline `<strong>`, all intact. Only cells
 * the author actually retyped are rewritten, and only from escaped text.
 *
 * <b>Returns the input unchanged when nothing changed.</b> The comparison is on the MODEL, not on the
 * markup, because reparsing normalises the HTML on its own (a `<table>` with no `<tbody>` acquires
 * one). Comparing strings would have reported every such table as edited the first time anybody opened
 * the dialog on it and pressed Apply.
 */
export function applyTableEdit(originalHtml: string, model: EmailTableModel): string {
  const original = parseEmailTable(originalHtml);
  if (original && isSameTableModel(original, model)) return originalHtml;

  const table = firstTable(originalHtml);
  if (!table) return originalHtml;

  const wantRows = clamp(model.rows.length, TABLE_MIN_ROWS, TABLE_MAX_ROWS);
  const wantCols = clamp(model.rows[0]?.length ?? 1, TABLE_MIN_COLS, TABLE_MAX_COLS);

  rebuildRows(table, model, wantRows, wantCols);
  setHeaderRow(table, model.headerRow);
  setAlign(table, model.align);
  setWidth(table, model.width);

  return table.outerHTML;
}

/** True when two models describe the same table — same geometry, same text, same presentation. */
export function isSameTableModel(a: EmailTableModel, b: EmailTableModel): boolean {
  if (a.headerRow !== b.headerRow || a.align !== b.align || a.width !== b.width) return false;
  if (a.rows.length !== b.rows.length) return false;

  return a.rows.every((row, r) => {
    const other = b.rows[r];
    return other?.length === row.length
      && row.every((cell, c) => cell.text === other[c]?.text && cell.key === other[c]?.key);
  });
}

/** A model with one more row, cloned blank. Returns the input when already at the ceiling. */
export function addTableRow(model: EmailTableModel): EmailTableModel {
  if (model.rows.length >= TABLE_MAX_ROWS) return model;
  const cols = model.rows[0]?.length ?? 1;
  return { ...model, rows: [...model.rows, Array.from({ length: cols }, () => ({ text: '' }))] };
}

export function removeTableRow(model: EmailTableModel, index: number): EmailTableModel {
  if (model.rows.length <= TABLE_MIN_ROWS) return model;
  if (index < 0 || index >= model.rows.length) return model;
  return { ...model, rows: model.rows.filter((_, i) => i !== index) };
}

export function addTableColumn(model: EmailTableModel): EmailTableModel {
  if ((model.rows[0]?.length ?? 0) >= TABLE_MAX_COLS) return model;
  return { ...model, rows: model.rows.map((row) => [...row, { text: '' }]) };
}

export function removeTableColumn(model: EmailTableModel, index: number): EmailTableModel {
  const cols = model.rows[0]?.length ?? 0;
  if (cols <= TABLE_MIN_COLS) return model;
  if (index < 0 || index >= cols) return model;
  return { ...model, rows: model.rows.map((row) => row.filter((_, i) => i !== index)) };
}

/**
 * Stored content → editor content: a bare `<table>` becomes the atomic node.
 *
 * Parchment matches a blot by tag AND class, so the wrapper has to exist before Quill parses — the same
 * reason the action node needs `toEditorHtml`.
 */
export function tablesToNodes(html: string | null | undefined): string {
  if (!html) return '';
  if (typeof window === 'undefined' || !window.DOMParser) return html;

  const doc = new window.DOMParser().parseFromString(html, 'text/html');

  for (const table of Array.from(doc.body.querySelectorAll('table'))) {
    // A table already inside a node is left alone — this runs on content that may be half-converted.
    if (table.closest(`.${TABLE_WRAPPER_CLASS}`)) continue;

    const wrapper = doc.createElement('div');
    wrapper.className = TABLE_WRAPPER_CLASS;
    wrapper.setAttribute('data-email-table', '1');
    wrapper.setAttribute('contenteditable', 'false');
    table.replaceWith(wrapper);
    wrapper.appendChild(table);
  }

  return doc.body.innerHTML;
}

/** Editor content → stored content: the node unwraps back to a bare `<table>`. */
export function nodesToTables(html: string | null | undefined): string {
  if (!html) return '';
  TABLE_NODE.lastIndex = 0;
  return html.replace(TABLE_NODE, (whole: string, inner: string) => inner);
}

// ── internals ───────────────────────────────────────────────────────────────

function clamp(value: number, min: number, max: number): number {
  if (!Number.isFinite(value)) return min;
  return Math.max(min, Math.min(Math.trunc(value), max));
}

function firstTable(html: string | null | undefined): HTMLTableElement | null {
  if (!html) return null;
  if (typeof window === 'undefined' || !window.DOMParser) return null;

  const doc = new window.DOMParser().parseFromString(html, 'text/html');
  return doc.body.querySelector('table');
}

function marginFor(align: EmailTableAlign): string {
  if (align === 'center') return '16px auto';
  if (align === 'right') return '16px 0 16px auto';
  return '16px 0';
}

/**
 * Cell HTML → the plain text the dialog edits. `<br>` survives as a newline; everything else that is
 * markup is read for its text. The original HTML is kept alongside (see {@link EmailTableCell}), so this
 * lossy direction is only ever used to SHOW the cell, never to rewrite one nobody edited.
 */
function cellHtmlToText(html: string): string {
  if (typeof window === 'undefined' || !window.DOMParser) return html;

  const doc = new window.DOMParser().parseFromString(
    html.replace(/<br\s*\/?>/gi, '\n'), 'text/html');

  // A variable that reached here as a rendered chip goes back to its placeholder, so the dialog shows
  // {{name}} whether the cell came from stored content or from the live editor.
  for (const chip of Array.from(doc.body.querySelectorAll('[data-variable]'))) {
    chip.replaceWith(doc.createTextNode(`{{${chip.getAttribute('data-variable')}}}`));
  }

  //   is the &nbsp; a blank cell holds; ﻿ is the guard Quill wraps an inline embed in.
  return (doc.body.textContent ?? '').replace(/ /g, ' ').replace(/﻿/g, '').trim();
}

/** The dialog's text → cell HTML: escaped, with newlines as `<br>`. Placeholders pass through. */
function cellTextToHtml(text: string): string {
  const escaped = text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
  return escaped.replace(/\n/g, '<br>');
}

/**
 * Rewrites the table's rows from the model, reusing every original element the model still refers to.
 *
 * The rows are rebuilt rather than resized in place because the model may have dropped any row or any
 * column, not only the last one — and it is the `key` on each cell, not its position, that says which
 * original markup it stands for.
 */
function rebuildRows(
  table: HTMLTableElement, model: EmailTableModel, rows: number, cols: number,
): void {
  const originalRows = Array.from(table.querySelectorAll('tr'));
  const cellAt = (key: string | undefined): Element | undefined => {
    if (!key) return undefined;
    const [r, c] = key.split(':').map(Number);
    return originalRows[r]?.querySelectorAll('th, td')[c];
  };

  const container = originalRows[0]?.parentElement ?? table;
  const built: HTMLTableRowElement[] = [];

  for (let r = 0; r < rows; r += 1) {
    const modelRow = model.rows[r] ?? [];

    // The row's own attributes come from wherever its cells came from; a row the author added inherits
    // the shell of the last original row so a striped or coloured table stays consistent.
    const sourceRow = originalRows[sourceRowIndex(modelRow) ?? originalRows.length - 1];
    const tr = (sourceRow?.cloneNode(false) as HTMLTableRowElement | undefined)
      ?? table.ownerDocument.createElement('tr');
    while (tr.firstChild) tr.removeChild(tr.firstChild);

    for (let c = 0; c < cols; c += 1) {
      const wanted = modelRow[c];
      const source = cellAt(wanted?.key)
        // A new column copies the cell to its left, so it arrives with the same borders and padding.
        ?? cellAt(modelRow[c - 1]?.key)
        ?? cellAt(model.rows.flat().find((x) => x.key)?.key);

      const cell = source
        ? (source.cloneNode(false) as HTMLElement)
        : table.ownerDocument.createElement(r === 0 && model.headerRow ? 'th' : 'td');

      if (!source) cell.setAttribute('style', r === 0 && model.headerRow ? HEADER_STYLE : CELL_STYLE);

      const text = wanted?.text ?? '';
      const unchanged = wanted?.html !== undefined && text === cellHtmlToText(wanted.html);

      // An untouched cell is written back as it was — see EmailTableCell.html. Only a cell somebody
      // retyped is re-rendered, and then only from escaped text.
      if (unchanged) cell.innerHTML = wanted!.html!;
      else cell.innerHTML = text.trim() === '' ? '&nbsp;' : cellTextToHtml(text);

      tr.appendChild(cell);
    }

    built.push(tr);
  }

  for (const tr of originalRows) tr.remove();
  for (const tr of built) container.appendChild(tr);
}

/** The original row a model row came from, or `undefined` when the author added it. */
function sourceRowIndex(row: EmailTableCell[]): number | undefined {
  for (const cell of row) {
    if (!cell.key) continue;
    const r = Number(cell.key.split(':')[0]);
    if (Number.isFinite(r)) return r;
  }
  return undefined;
}

function setHeaderRow(table: HTMLTableElement, headerRow: boolean): void {
  const first = table.querySelector('tr');
  if (!first) return;

  for (const cell of Array.from(first.querySelectorAll('th, td'))) {
    const wantTag = headerRow ? 'th' : 'td';
    if (cell.tagName.toLowerCase() === wantTag) continue;
    retagCell(cell, wantTag, headerRow ? HEADER_STYLE : CELL_STYLE);
  }
}

/**
 * Swaps a cell between `th` and `td`, keeping its attributes.
 *
 * The style is only replaced when the cell had none, or had exactly the default for the tag it is
 * leaving. A cell somebody styled by hand keeps that styling across the switch — turning the header row
 * off is a change of ROLE, and it should not also throw away a colour the author chose.
 */
function retagCell(cell: Element, tag: string, defaultStyle: string): void {
  const doc = cell.ownerDocument;
  const replacement = doc.createElement(tag);

  for (const attr of Array.from(cell.attributes)) {
    replacement.setAttribute(attr.name, attr.value);
  }
  replacement.innerHTML = cell.innerHTML;

  const current = cell.getAttribute('style');
  if (!current || current === CELL_STYLE || current === HEADER_STYLE) {
    replacement.setAttribute('style', defaultStyle);
  }

  cell.replaceWith(replacement);
}

function readAlign(table: HTMLTableElement): EmailTableAlign {
  const attr = (table.getAttribute('align') ?? '').toLowerCase();
  if (attr === 'center' || attr === 'right' || attr === 'left') return attr;

  const margin = (table.getAttribute('style') ?? '').toLowerCase();
  const m = /margin\s*:\s*([^;]+)/.exec(margin);
  if (m) {
    const parts = m[1].trim().split(/\s+/);
    // margin: <t> <r> <b> <l>  |  <t> <rl>  — "auto" on both sides centres, on the left only pushes right.
    const left = parts.length >= 4 ? parts[3] : parts[1];
    const right = parts.length >= 2 ? parts[1] : undefined;
    if (left === 'auto' && right === 'auto') return 'center';
    if (left === 'auto') return 'right';
  }
  return 'left';
}

function readWidth(table: HTMLTableElement): EmailTableWidth {
  const attr = (table.getAttribute('width') ?? '').trim();
  if (isPreset(attr)) return attr;

  const style = (table.getAttribute('style') ?? '').toLowerCase();
  const m = /(?:^|;)\s*width\s*:\s*([^;]+)/.exec(style);
  const value = m?.[1]?.trim();
  if (value && isPreset(value)) return value;

  return 'auto';
}

function isPreset(value: string): value is EmailTableWidth {
  return (TABLE_WIDTH_PRESETS as readonly string[]).includes(value);
}

function setAlign(table: HTMLTableElement, align: EmailTableAlign): void {
  if (readAlign(table) === align) return;

  if (align === 'left') table.removeAttribute('align');
  else table.setAttribute('align', align);

  setStyleDeclaration(table, 'margin', marginFor(align));
}

function setWidth(table: HTMLTableElement, width: EmailTableWidth): void {
  if (readWidth(table) === width) return;

  if (width === 'auto') {
    table.removeAttribute('width');
    setStyleDeclaration(table, 'width', null);
    return;
  }
  table.setAttribute('width', width);
  setStyleDeclaration(table, 'width', width);
}

/**
 * Sets (or removes) one declaration in an inline style, leaving the others in their original order.
 *
 * Written by hand rather than through `element.style` because that reparses and re-spells the whole
 * attribute — `border-collapse:collapse` comes back as `border-collapse: collapse`, and every table in
 * the document would then report itself as changed the first time anybody re-aligned one.
 */
function setStyleDeclaration(el: Element, property: string, value: string | null): void {
  const style = el.getAttribute('style') ?? '';
  const parts = style.split(';').map((s) => s.trim()).filter(Boolean);
  const kept = parts.filter((p) => p.split(':')[0].trim().toLowerCase() !== property);

  if (value !== null) kept.push(`${property}:${value}`);

  if (kept.length === 0) el.removeAttribute('style');
  else el.setAttribute('style', kept.join(';'));
}
