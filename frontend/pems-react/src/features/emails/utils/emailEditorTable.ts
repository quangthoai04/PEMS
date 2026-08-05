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
 * what is saved. The markup itself is generated (or accepted) in the shape §17 requires — inline CSS,
 * `border-collapse`, no classes.
 *
 * <b>The cost, stated plainly.</b> Being atomic, its cells are not editable by typing into them. Cell
 * text is edited through the toolbar's table dialog instead. That is a real limitation and the right
 * trade: a table whose contents are awkward to change is recoverable, a table whose columns silently
 * became rows is not.
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
export function buildEmailTable(rows: number, cols: number, cells?: string[][]): string {
  const safeRows = Math.max(1, Math.min(rows, 20));
  const safeCols = Math.max(1, Math.min(cols, 10));

  const body: string[] = [];
  for (let r = 0; r < safeRows; r += 1) {
    const tds: string[] = [];
    for (let c = 0; c < safeCols; c += 1) {
      const text = cells?.[r]?.[c] ?? '';
      const style = r === 0 ? HEADER_STYLE : CELL_STYLE;
      tds.push(`<td style="${style}">${text === '' ? '&nbsp;' : escapeCell(text)}</td>`);
    }
    body.push(`<tr>${tds.join('')}</tr>`);
  }

  return '<table role="presentation" width="100%" cellpadding="0" cellspacing="0"'
    + ' style="border-collapse:collapse;width:100%;margin:16px 0">'
    + `<tbody>${body.join('')}</tbody></table>`;
}

/** The text of each cell, for the dialog that edits one. */
export function readTableCells(tableHtml: string): string[][] {
  if (typeof window === 'undefined' || !window.DOMParser) return [];

  const doc = new window.DOMParser().parseFromString(tableHtml, 'text/html');
  return Array.from(doc.querySelectorAll('tr')).map((tr) =>
    Array.from(tr.querySelectorAll('td, th')).map((td) => (td.textContent ?? '').replace(/ /g, '').trim()));
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

function escapeCell(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
