/**
 * Print windows built from the DOM, not from strings.
 *
 * The two statistics printers used to interpolate API values into an HTML string and hand it to
 * `document.write`. They were safe only by a property nobody could see from the call site — every
 * interpolation happened to sit in text position, where a local `esc()` covering `& < >` is enough. One
 * value moved into an attribute, or one `esc()` forgotten on a new column, and the document being printed
 * would execute whatever the server sent. Nothing tested that property and nothing announced it.
 *
 * So no server value reaches an HTML parser at all now. Structure is created with `createElement`, text is
 * assigned with `textContent`, and layout is set through DOM style properties with values written here as
 * literals. `document.write` survives only to give the popup a doctype, and what it writes is a constant
 * with nothing interpolated into it.
 */

/** Static skeleton — deliberately contains no interpolation of any kind. */
export const PRINT_SKELETON = '<!doctype html><html><head><meta charset="utf-8"></head><body></body></html>';

/** The minimal surface of `window` a print target must offer; keeps the callers testable. */
export interface PrintTargetWindow {
  document: Document;
  focus: () => void;
  print: () => void;
}

export interface PrintDocumentContent {
  /** Goes to `document.title` as a property — never parsed as markup. */
  title: string;
  /** Static stylesheet text. Must not contain values that came from the server. */
  css: string;
  /** The body content, already built as DOM nodes. */
  root: HTMLElement;
}

/**
 * Fills an opened blank window with the given content and starts the print dialog.
 * `importNode` re-owns the nodes in the popup's document; the tree is never serialised back to a string.
 */
export function renderPrintDocument(win: PrintTargetWindow, content: PrintDocumentContent): void {
  const doc = win.document;
  doc.open();
  doc.write(PRINT_SKELETON);
  doc.close();

  doc.title = content.title;

  const style = doc.createElement('style');
  style.textContent = content.css;
  doc.head.appendChild(style);

  doc.body.appendChild(doc.importNode(content.root, true));
}

// ── DOM helpers ──────────────────────────────────────────────────────────────
//
// Text always goes through `textContent`, so a value containing `<script>` becomes those characters on
// the page rather than an element. `style` takes CSS declarations written at the call site, never data.

export interface ElementOptions {
  className?: string;
  /** Literal CSS declarations, e.g. `{ textAlign: 'right' }`. */
  style?: Partial<CSSStyleDeclaration>;
  /** Assigned with textContent. Any string is safe here. */
  text?: string;
  colSpan?: number;
}

export function el<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  options: ElementOptions = {},
  children: (Node | null | undefined)[] = [],
): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (options.className) node.className = options.className;
  if (options.style) Object.assign(node.style, options.style);
  if (options.text !== undefined) node.textContent = options.text;
  if (options.colSpan !== undefined) (node as HTMLTableCellElement).colSpan = options.colSpan;
  for (const child of children) if (child) node.appendChild(child);
  return node;
}

/** A table row from plain cell descriptions. */
export interface CellSpec {
  text: string;
  align?: 'left' | 'center' | 'right';
  colSpan?: number;
  className?: string;
  /** Renders a `<th>` instead of a `<td>`. */
  header?: boolean;
  italic?: boolean;
  bold?: boolean;
  color?: string;
}

export function row(cells: CellSpec[], options: ElementOptions = {}): HTMLTableRowElement {
  return el('tr', options, cells.map(cell => el(cell.header ? 'th' : 'td', {
    text: cell.text,
    className: cell.className,
    colSpan: cell.colSpan,
    style: {
      ...(cell.align ? { textAlign: cell.align } : {}),
      ...(cell.italic ? { fontStyle: 'italic' } : {}),
      ...(cell.bold ? { fontWeight: '700' } : {}),
      ...(cell.color ? { color: cell.color } : {}),
    },
  })));
}

export function table(
  head: HTMLTableSectionElement | null,
  body: HTMLTableSectionElement,
  foot?: HTMLTableSectionElement | null,
): HTMLTableElement {
  return el('table', {}, [head, body, foot]);
}

export function section(tag: 'thead' | 'tbody' | 'tfoot', rows: HTMLTableRowElement[]): HTMLTableSectionElement {
  return el(tag, {}, rows);
}

/** The republic header both printers carry, identical in each. */
export function officialHeader(leftTitle: string, leftSubtitle: string): HTMLElement {
  return el('div', { className: 'top' }, [
    el('div', {}, [
      el('div', { text: leftTitle, className: 'org' }),
      el('div', { text: leftSubtitle, className: 'org-sub' }),
    ]),
    el('div', { style: { textAlign: 'right' } }, [
      el('div', { text: 'CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM', className: 'rep' }),
      el('div', { text: 'Độc lập - Tự do - Hạnh phúc', className: 'rep rep-accent' }),
    ]),
  ]);
}
