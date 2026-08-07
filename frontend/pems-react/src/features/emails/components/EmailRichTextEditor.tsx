/**
 * The one rich-text editor for email, shared by TEMPLATE authoring and COMPOSE (V4 §5.1).
 *
 * <b>Why one component.</b> There were two before — the template screen and the send modal each built
 * their own ReactQuill with its own five-button toolbar — and they had already drifted: different
 * formats, different handling of images, and no shared idea of what an email may contain. Two editors
 * means two answers to "can I centre this?", and the one that matters is whichever the recipient's mail
 * client ends up rendering.
 *
 * <b>Why the toolbar is React rather than Quill's.</b> Quill's toolbar module wants a DOM container and
 * gives back opaque `ql-*` buttons; half of what V4 §6.1 asks for — undo, redo, divider, variable,
 * fullscreen, the system action block — are not formats at all, so they need custom handlers regardless.
 * Rendering the toolbar here makes every control a real button with a Vietnamese label a test can find by
 * name, and keeps the icon set consistent with the rest of the product.
 *
 * <b>What it deliberately does not offer.</b> No source view, no arbitrary HTML, no font or size outside
 * the fixed ladders (§6.2, §6.3, §6.4).
 *
 * <b>Tables are edited through a dialog, not in place.</b> Quill 2 models a table as rows of cells it
 * owns, and pasting an email-safe one through it restructures the table (two cells in one row came back
 * as two rows) and strips the inline borders and padding that make it visible in mail. So a table here is
 * one atomic node — which means no caret can go inside it, and its cells are edited in
 * `EmailTableDialog` instead. That dialog is also where the structural operations live, so adding a
 * column is a decision rather than a side effect of where a cursor happened to be.
 *
 * <b>The backend is still the authority.</b> Everything here runs again server-side before anything is
 * hashed, previewed or sent. This layer exists so the sender sees the cleaned result while editing.
 */
import React, {
  forwardRef, useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState,
} from 'react';
// @ts-ignore - react-quill-new ships without bundled types in this project
import ReactQuill, { Quill } from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import {
  Undo2, Redo2, Bold, Italic, Underline, Strikethrough, AlignLeft, AlignCenter, AlignRight,
  List, ListOrdered, IndentDecrease, IndentIncrease, Link2, ImageIcon, Minus, Eraser,
  Maximize2, Minimize2, Braces, MousePointerClick, Baseline, PaintBucket, Table as TableIcon,
  TableProperties,
} from 'lucide-react';
import {
  DIVIDER_BLOT_NAME, EMAIL_EDITOR_FORMATS, EMAIL_FONTS, EMAIL_INDENTS, EMAIL_SIZES,
  registerEmailEditorFormats,
} from '../utils/emailEditorFormats';
import {
  type EmailEditorCapabilities, type EmailEditorMode, capabilitiesFor,
} from '../utils/emailEditorCapabilities';
import {
  SPACE_RUN_WARNING, hasSpaceRun, htmlHasSpaceRun, sanitizePastedFragment,
} from '../utils/emailEditorPaste';
import { fromEditorHtml, toEditorHtml } from '../utils/emailEditorSystemNodes';
import { countSystemActionNodes } from '../utils/systemActionNode';
import {
  VARIABLE_CHIP_CLASS, chipsToVariables, variablesToChips,
} from '../utils/emailEditorVariableChips';
import {
  type EmailTableModel,
  TABLE_BLOT_NAME, TABLE_WRAPPER_CLASS, applyTableEdit, buildEmailTable, nodesToTables,
  parseEmailTable, tablesToNodes,
} from '../utils/emailEditorTable';
import { EmailTableDialog } from './EmailTableDialog';

// Before any editor is constructed: Quill drops what it has no blot for, so a late registration means the
// first document opened loses its action block and its dividers.
registerEmailEditorFormats();

/**
 * Puts every `style` attribute's declarations in one fixed order.
 *
 * <b>The defect this closes.</b> Quill assigns an element's declarations one property at a time as it
 * rebuilds the DOM from a Delta, and which property lands first is not stable across parses of the SAME
 * content — one pass gives `"font-size:12px;color:#647"`, the next gives `"color:#647;font-size:12px"`.
 * That is invisible on screen, but this editor is CONTROLLED: the stored html this produces becomes the
 * `value` prop handed back to Quill, which parses it again and can re-order it again, to a THIRD spelling
 * that differs from the second exactly as much as the second differed from the first. A pair of
 * declarations whose order keeps changing never reaches a spelling that equals the one before it, so
 * ReactQuill's own "did the value actually change" check never once answers no — it re-set the document
 * and fired another `onChange` on every tick, which pinned a CPU core the moment an editor opened on any
 * template whose style carried two declarations (every shipped template's does), before the operator
 * could do anything at all, including opening devtools to see why.
 *
 * <b>Why sorting, not `canonicalizeEmailHtml`.</b> That comparison is deliberately lossy — it also
 * collapses runs of whitespace, because two spellings of insignificant markup formatting should read as
 * "no edit". Applied to what actually gets STORED, that same collapse would silently rewrite a run of
 * spaces an author typed on purpose into one space, which is precisely what V4 §7.4 refuses to do
 * anywhere in this product. This touches only the order of declarations inside `style=""` — never a
 * property, a value, or a character of text — which is safe to apply unconditionally: two declarations in
 * either order paint the same pixels.
 */
function sortStyleDeclarations(html: string): string {
  if (!html || typeof window === 'undefined' || !window.DOMParser) return html;

  const doc = new window.DOMParser().parseFromString(`<body>${html}</body>`, 'text/html');
  for (const el of Array.from(doc.body.querySelectorAll('[style]'))) {
    const declarations = (el.getAttribute('style') ?? '')
      .split(';')
      .map((d) => d.trim())
      .filter((d) => d.length > 0);
    const sorted = [...declarations].sort();
    const next = sorted.join(';');
    if (next !== declarations.join(';')) el.setAttribute('style', next);
  }
  return doc.body.innerHTML;
}

/** A variable the author may insert, as offered by the picker (§8.1). */
export interface EmailEditorVariable {
  /** The raw name, without braces — e.g. `senderName`. */
  name: string;
  /** What a person calls it — e.g. "Họ tên người gửi". */
  label: string;
  /** Optional grouping, e.g. "Thông tin người gửi". */
  group?: string;
}

export interface EmailRichTextEditorProps {
  mode: EmailEditorMode;
  /** Canonical HTML. The system action node is stored in its backend spelling, not the editor's. */
  value: string;
  onChange: (html: string) => void;
  /** Overrides the mode's defaults where a flow genuinely differs. */
  capabilities?: Partial<EmailEditorCapabilities>;
  variables?: EmailEditorVariable[];
  placeholder?: string;
  /** Uploads an image and returns the URL to embed. Without it, the image button is hidden. */
  onUploadImage?: (file: File) => Promise<string>;
  onNotice?: (message: string) => void;
  disabled?: boolean;
  minHeight?: number;
  'data-testid'?: string;
}

/** A toolbar button. Kept tiny and unstyled-by-default so the group markup below stays readable. */
function TB({
  onClick, title, active, disabled, children,
}: {
  onClick: () => void; title: string; active?: boolean; disabled?: boolean; children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      title={title}
      aria-label={title}
      aria-pressed={active ? true : undefined}
      disabled={disabled}
      onMouseDown={(e) => e.preventDefault()}   // keep the selection while the button takes focus
      onClick={onClick}
      className={`inline-flex h-8 w-8 items-center justify-center rounded-lg border text-gray-600 outline-none transition
        ${active
        ? 'border-[#004c91] bg-[#004c91]/10 text-[#004c91]'
        : 'border-transparent hover:border-gray-200 hover:bg-gray-50'}
        disabled:opacity-40 disabled:hover:bg-transparent`}
    >
      {children}
    </button>
  );
}

const SEP = <span className="mx-1 h-5 w-px shrink-0 bg-gray-200" aria-hidden="true" />;

/**
 * What a host screen may ask the editor to do from outside it.
 *
 * Exists for one real case: the template screen has its own variable sidebar, listing each variable with
 * the description from the template's contract. That list is worth more than the compact picker in the
 * toolbar, so the screen keeps it — and needs a way to say "put this one where the caret was".
 */
export interface EmailRichTextEditorHandle {
  /** Inserts a variable chip at the last known caret, replacing a selection if there was one. */
  insertVariable: (variable: EmailEditorVariable) => void;
  /** True when a live editor is attached — false under a mocked one, where the host must fall back. */
  isReady: () => boolean;
}

export const EmailRichTextEditor = forwardRef<EmailRichTextEditorHandle, EmailRichTextEditorProps>(({
  mode, value, onChange, capabilities, variables, placeholder,
  onUploadImage, onNotice, disabled, minHeight = 240, 'data-testid': testId,
}: EmailRichTextEditorProps, ref) => {
  const caps = useMemo(
    () => ({ ...capabilitiesFor(mode), ...(capabilities ?? {}) }),
    [mode, capabilities],
  );

  const quillRef = useRef<any>(null);
  const [fullscreen, setFullscreen] = useState(false);
  const [active, setActive] = useState<Record<string, any>>({});
  const [showVariables, setShowVariables] = useState(false);

  /**
   * The last caret the editor actually had.
   *
   * Clicking anything outside the editor — a toolbar button, a chip in the host's sidebar — blurs it, and
   * by the time the handler runs `getSelection()` reports null. Asking for it with the force flag would
   * FOCUS the editor and answer 0, which silently moves the insert to the top of the document. So the
   * position is remembered while it is still true, and a null range is ignored rather than stored.
   */
  const lastRange = useRef<{ index: number; length: number } | null>(null);

  /**
   * Whether this editing session has already been told that runs of spaces do not align anything.
   *
   * One warning, not one per keystroke — and shared with the paste handler, which would otherwise
   * toast the same sentence twice for a single paste.
   */
  const warnedSpaceRun = useRef(false);

  const editor = useCallback(() => quillRef.current?.getEditor?.(), []);

  /** Applies `fn` to the live editor, then refreshes the toolbar's active state. */
  const withEditor = useCallback((fn: (q: any) => void) => {
    const q = editor();
    if (!q) return;
    q.focus();
    fn(q);
    setActive(q.getFormat() ?? {});
  }, [editor]);

  const toggle = useCallback((format: string) => {
    withEditor((q) => q.format(format, !q.getFormat()[format], 'user'));
  }, [withEditor]);

  const setFormat = useCallback((format: string, formatValue: unknown) => {
    withEditor((q) => q.format(format, formatValue, 'user'));
  }, [withEditor]);

  // ── Indent, as a ladder over margin-left rather than Quill's class-based levels ──
  const stepIndent = useCallback((direction: 1 | -1) => {
    withEditor((q) => {
      const current: string | undefined = q.getFormat().indent;
      const at = current ? EMAIL_INDENTS.indexOf(current as typeof EMAIL_INDENTS[number]) : -1;
      const next = at + direction;
      q.format('indent', next < 0 ? false : EMAIL_INDENTS[Math.min(next, EMAIL_INDENTS.length - 1)], 'user');
    });
  }, [withEditor]);

  // ── Link ──
  const insertLink = useCallback(() => {
    const q = editor();
    if (!q) return;
    const existing = q.getFormat().link;
    // eslint-disable-next-line no-alert
    const url = window.prompt('Địa chỉ liên kết:', typeof existing === 'string' ? existing : 'https://');
    if (url === null) return;

    if (url.trim() === '') {
      withEditor((qq) => qq.format('link', false, 'user'));
      return;
    }
    // §14.5 — a link may only be one of these. Checked here so the sender is told, and again on the
    // backend because this check is advice, not enforcement.
    if (!/^(?:https?:|mailto:|tel:)/i.test(url.trim())) {
      onNotice?.('Liên kết chỉ được dùng http, https, mailto hoặc tel.');
      return;
    }
    withEditor((qq) => qq.format('link', url.trim(), 'user'));
  }, [editor, withEditor, onNotice]);

  // ── Image ──
  const insertImage = useCallback(() => {
    if (!onUploadImage) return;
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/*';
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) return;
      try {
        const url = await onUploadImage(file);
        const q = editor();
        if (!q) return;
        const range = q.getSelection(true);
        const index = range ? range.index : q.getLength();
        q.insertEmbed(index, 'image', url, 'user');
        q.setSelection(index + 1, 0);
      } catch (err: any) {
        onNotice?.(err?.message ?? 'Không tải được ảnh.');
      }
    };
    input.click();
  }, [onUploadImage, editor, onNotice]);

  const insertDivider = useCallback(() => {
    withEditor((q) => {
      const range = q.getSelection(true);
      const index = range ? range.index : q.getLength();
      q.insertEmbed(index, DIVIDER_BLOT_NAME, true, 'user');
      q.setSelection(index + 1, 0);
    });
  }, [withEditor]);

  const insertActionBlock = useCallback(() => {
    // §9.5 — one action area, never two: both would be minted from the same one-time token, so the first
    // click would answer for both and the second would report an expired link to somebody blameless.
    if (countSystemActionNodes(value) >= 1) {
      onNotice?.('Mỗi email chỉ được có một khối nút phản hồi.');
      return;
    }
    withEditor((q) => {
      const range = q.getSelection(true);
      const index = range ? range.index : q.getLength();
      q.insertEmbed(index, 'pemsSystemActionBlock', 'action', 'user');
      q.setSelection(index + 1, 0);
    });
  }, [value, withEditor, onNotice]);

  /**
   * Inserts a variable as an atomic CHIP, not as the characters `{{name}}`.
   *
   * Typed characters are editable as characters: an operator can delete one brace, correct the spelling
   * to something the renderer does not know, or paste half of it — and each of those surfaces at SEND
   * time, in front of a recipient. A chip can only be inserted or deleted whole.
   */
  const insertVariable = useCallback((v: EmailEditorVariable) => {
    setShowVariables(false);
    const q = editor();
    if (!q) return;

    // The remembered caret, not a forced one — see `lastRange`. A selected range is REPLACED, which is
    // what every other editor does with a paste.
    const at = lastRange.current ?? { index: 0, length: 0 };
    const index = Math.min(at.index, Math.max(0, q.getLength() - 1));
    if (at.length > 0) q.deleteText(index, at.length, 'user');

    q.insertEmbed(index, 'pemsVariable', { name: v.name, label: v.label }, 'user');
    q.setSelection(index + 1, 0);
    lastRange.current = { index: index + 1, length: 0 };
    setActive(q.getFormat() ?? {});
  }, [editor]);

  useImperativeHandle(ref, () => ({
    insertVariable,
    isReady: () => !!editor(),
  }), [insertVariable, editor]);

  // ── Table (§7.3) ──
  //
  // The node is atomic, so a caret cannot go inside it and the cells have to be edited somewhere else.
  // `target.el` is the live wrapper element rather than a document index: the index is resolved from it
  // at APPLY time, so an edit elsewhere in the document while the dialog is open cannot land the
  // replacement on the wrong block.
  const [tableEdit, setTableEdit] = useState<
  { el: HTMLElement | null; original: string; model: EmailTableModel } | null>(null);

  /** The table the caret/click is on, if any — what enables "Chỉnh sửa bảng". */
  const [selectedTable, setSelectedTable] = useState<HTMLElement | null>(null);

  const openTableDialog = useCallback((el: HTMLElement | null, original: string) => {
    const model = parseEmailTable(original);
    if (!model) {
      // Refused rather than flattened — see `parseEmailTable`. The author keeps a table this dialog
      // cannot express, instead of getting back a rearranged one it can.
      onNotice?.('Không mở được trình chỉnh sửa cho bảng này (bảng lồng nhau hoặc cấu trúc không đọc được).');
      return;
    }
    setTableEdit({ el, original, model });
  }, [onNotice]);

  const insertTable = useCallback(() => {
    // A starting shape rather than a size prompt: rows and columns are added in the dialog, where the
    // author can see what they are adding to.
    openTableDialog(null, buildEmailTable(3, 2));
  }, [openTableDialog]);

  const editSelectedTable = useCallback(() => {
    if (!selectedTable) return;
    openTableDialog(selectedTable, selectedTable.innerHTML);
  }, [selectedTable, openTableDialog]);

  const applyTable = useCallback((model: EmailTableModel) => {
    const target = tableEdit;
    setTableEdit(null);
    if (!target) return;

    const html = applyTableEdit(target.original, model);
    const q = editor();
    if (!q) return;

    if (!target.el) {
      const at = lastRange.current;
      const index = at ? at.index : q.getLength();
      q.insertEmbed(index, TABLE_BLOT_NAME, html, 'user');
      q.setSelection(index + 1, 0);
      return;
    }

    // Nothing actually changed — leave the document alone. Replacing the embed with an identical one
    // would still register as an edit, and the screen would offer to save a table nobody touched.
    if (html === target.original) return;

    /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
    const blot = (Quill as any)?.find?.(target.el);
    if (!blot) {
      // The element was replaced under the dialog (a reload, an undo). Said out loud rather than
      // dropped: the author spent time in that dialog and is entitled to know the edit did not land.
      onNotice?.('Bảng đã thay đổi trong lúc chỉnh sửa. Vui lòng mở lại bảng và áp dụng lần nữa.');
      return;
    }

    const index = q.getIndex(blot);
    q.deleteText(index, 1, 'user');
    q.insertEmbed(index, TABLE_BLOT_NAME, html, 'user');
    q.setSelection(index + 1, 0);
    setSelectedTable(null);
  }, [tableEdit, editor, onNotice]);

  // Clicking a table selects it; double-clicking opens the editor, which is what every other table
  // control in the product does and therefore what an author will try first.
  useEffect(() => {
    const q = editor();
    const node: HTMLElement | undefined = q?.root;
    if (!node || typeof node.addEventListener !== 'function') return undefined;

    const wrapperOf = (e: Event): HTMLElement | null => {
      const t = e.target as HTMLElement | null;
      return t?.closest?.(`.${TABLE_WRAPPER_CLASS}`) ?? null;
    };

    const onClick = (e: Event) => setSelectedTable(wrapperOf(e));
    const onDouble = (e: Event) => {
      const el = wrapperOf(e);
      if (!el || disabled) return;
      setSelectedTable(el);
      openTableDialog(el, el.innerHTML);
    };

    node.addEventListener('click', onClick);
    node.addEventListener('dblclick', onDouble);
    return () => {
      node.removeEventListener('click', onClick);
      node.removeEventListener('dblclick', onDouble);
    };
  }, [editor, openTableDialog, disabled]);

  const clearFormatting = useCallback(() => {
    withEditor((q) => {
      const range = q.getSelection(true);
      if (range && range.length > 0) q.removeFormat(range.index, range.length, 'user');
    });
  }, [withEditor]);

  // ── Paste (§14.8) ──
  // Registered on the Quill root rather than as a clipboard matcher so it runs on the whole fragment
  // once, which is where `position`/hidden-content rules are cheapest to apply correctly.
  useEffect(() => {
    const q = editor();
    // `q.root` is Quill's contenteditable element. Checked rather than assumed: a stand-in editor (the
    // mocked one several screens' tests use) answers `getEditor()` with an object that has no root, and
    // an unguarded addEventListener there takes the whole screen down — the editor is not the only thing
    // on it, and a paste refinement is no reason for a template screen to fail to render.
    const node: HTMLElement | undefined = q?.root;
    if (!q || !node || typeof node.addEventListener !== 'function') return undefined;

    const onPaste = (e: ClipboardEvent) => {
      const html = e.clipboardData?.getData('text/html');
      if (!html) return;    // plain text needs no cleaning; let Quill handle it

      e.preventDefault();
      const doc = new DOMParser().parseFromString(html, 'text/html');
      sanitizePastedFragment(doc.body);

      const range = q.getSelection(true);
      const index = range ? range.index : q.getLength();
      if (range && range.length > 0) q.deleteText(range.index, range.length, 'user');
      q.clipboard.dangerouslyPasteHTML(index, doc.body.innerHTML, 'user');

      // Checked on the pasted fragment specifically, so a paste is warned about even when the document
      // already carried a run. The shared flag stops handleChange repeating it for the same paste.
      if (hasSpaceRun(doc.body.textContent ?? '') && !warnedSpaceRun.current) {
        warnedSpaceRun.current = true;
        onNotice?.(SPACE_RUN_WARNING);
      }
    };

    node.addEventListener('paste', onPaste);
    return () => node.removeEventListener('paste', onPaste);
  }, [editor, onNotice]);

  const modules = useMemo(() => ({
    toolbar: false,
    history: { delay: 700, maxStack: 200, userOnly: true },
    clipboard: { matchVisual: false },
  }), []);

  /**
   * The two conversions that keep ONE spelling of a document in the host's hands.
   *
   * Stored form is what the renderer, the hash, the final preview and the send all read: `{{name}}`
   * placeholders, bare `<table>`s, and the canonical action node. Editor form is what Quill's blots
   * recognise: chips carrying a label, tables wrapped in their atomic node, the action node classed so
   * Parchment matches it. Neither side ever sees the other's spelling.
   */
  const labelOf = useCallback(
    (name: string) => variables?.find((v) => v.name === name)?.label,
    [variables],
  );

  const toEditor = useCallback(
    (stored: string) => tablesToNodes(variablesToChips(toEditorHtml(stored), labelOf)),
    [labelOf],
  );

  const fromEditor = useCallback(
    (html: string) => sortStyleDeclarations(fromEditorHtml(nodesToTables(chipsToVariables(html)))),
    [],
  );

  /**
   * V4 §7.4 — a run of spaces is not a layout tool, however it got there.
   *
   * The warning used to fire on paste only, so holding the space bar to line two columns up was
   * silently accepted — and HTML collapses those runs, so the sender's tidy column arrives as one
   * ragged line. Typing is in fact the likelier way to do it: pasting from Word brings tables, while
   * a person building a column by hand reaches for the space bar.
   *
   * Warned, not rewritten. Deleting characters out from under someone mid-sentence is worse than the
   * problem — and `&nbsp;` would be worse still, since it "works" in the composer and then refuses to
   * wrap on a phone.
   *
   * The warning is not the enforcement. Saving a template and asking for the final preview are both
   * refused while a run is present, in the screen and again on the server, because a warning that can be
   * scrolled past is a warning that ships. What this does is tell the author at the moment they make the
   * run, rather than at the moment they press the button.
   */
  const handleChange = useCallback((html: string, _delta: unknown, source?: string) => {
    // `source` is Quill's own word for where this change came from: `'user'` for a keystroke or a
    // paste, `'api'` for a `setContents()` call this component made itself, `'silent'` for one that
    // must not even reach a listener. Only `'user'` is an edit.
    //
    // Every render of a CONTROLLED Quill re-feeds it `editorHtml` via `setContents()`, and Quill answers
    // that with a 'text-change' of its own — react-quill-new's wrapper does not distinguish that echo
    // from typing, so it reached this handler exactly like a keystroke would. The echo is never
    // byte-identical to what was fed in — Quill has its own spelling for a colour, a self-closing tag, a
    // typed space — so `onChange` ran again with the reparsed spelling, which became the next `value`,
    // which Quill reparsed into a THIRD spelling on the next render, and so on: two notations for the
    // same content trading places forever. That pinned a CPU core the instant a template with a
    // multi-declaration inline style was opened — every shipped template has one — before the operator
    // could do anything, including opening devtools to see why.
    //
    // Filtering on `source` closes it at the one point that cannot mistake an edit for an echo: Quill
    // itself marks the difference at the moment the change happens. A guard built from comparing HTML
    // strings instead (tried first) had to approximate "no real edit", and the closest available
    // approximation — the dirty-check's own comparison — also collapses runs of whitespace, which
    // silently broke the one warning that exists to make a run of spaces impossible to save (V4 §7.4).
    // This has no such tradeoff: an echo is never `'user'`, and every keystroke always is.
    if (source !== 'user') return;

    const canonical = fromEditor(html);

    // Once per editing session, not once per keystroke: a warning that reappears on every space after
    // the third is noise, and the sender has already been told.
    if (typeof document !== 'undefined' && onNotice) {
      // Asked of the DOCUMENT, not of its flattened text: `textContent` joins the end of one paragraph
      // to the start of the next, so formatted markup reports runs that were never in one place.
      if (htmlHasSpaceRun(html)) {
        if (!warnedSpaceRun.current) {
          warnedSpaceRun.current = true;
          onNotice(SPACE_RUN_WARNING);
        }
      } else {
        // Re-armed once the runs are gone, so a second attempt later is warned about again.
        warnedSpaceRun.current = false;
      }
    }

    onChange(canonical);
  }, [onChange, fromEditor, onNotice]);

  const editorHtml = useMemo(() => toEditor(value), [value, toEditor]);

  const groupClass = 'flex flex-wrap items-center gap-0.5';
  const selectClass = 'h-8 rounded-lg border border-gray-200 bg-white px-1.5 text-xs text-gray-700 outline-none focus:border-[#004c91]';

  return (
    <div
      data-testid={testId}
      className={fullscreen
        ? 'fixed inset-0 z-[200] flex flex-col bg-white p-4'
        : 'rounded-xl border border-gray-200 bg-white'}
    >
      {/* ── Toolbar (§6.1) ── */}
      <div className={`${groupClass} gap-y-1 border-b border-gray-100 px-2 py-1.5`} role="toolbar" aria-label="Định dạng nội dung email">
        <TB onClick={() => withEditor((q) => q.history.undo())} title="Hoàn tác" disabled={disabled}><Undo2 className="h-4 w-4" /></TB>
        <TB onClick={() => withEditor((q) => q.history.redo())} title="Làm lại" disabled={disabled}><Redo2 className="h-4 w-4" /></TB>
        {SEP}

        <select
          className={selectClass}
          title="Phông chữ"
          aria-label="Phông chữ"
          value={typeof active.font === 'string' ? active.font : ''}
          disabled={disabled}
          onChange={(e) => setFormat('font', e.target.value || false)}
        >
          <option value="">Phông chữ</option>
          {EMAIL_FONTS.map((f) => <option key={f} value={f}>{f}</option>)}
        </select>

        <select
          className={selectClass}
          title="Cỡ chữ"
          aria-label="Cỡ chữ"
          value={typeof active.size === 'string' ? active.size : ''}
          disabled={disabled}
          onChange={(e) => setFormat('size', e.target.value || false)}
        >
          <option value="">Cỡ chữ</option>
          {EMAIL_SIZES.map((s) => <option key={s} value={s}>{s.replace('px', '')}</option>)}
        </select>
        {SEP}

        <TB onClick={() => toggle('bold')} title="Đậm" active={!!active.bold} disabled={disabled}><Bold className="h-4 w-4" /></TB>
        <TB onClick={() => toggle('italic')} title="Nghiêng" active={!!active.italic} disabled={disabled}><Italic className="h-4 w-4" /></TB>
        <TB onClick={() => toggle('underline')} title="Gạch chân" active={!!active.underline} disabled={disabled}><Underline className="h-4 w-4" /></TB>
        <TB onClick={() => toggle('strike')} title="Gạch ngang" active={!!active.strike} disabled={disabled}><Strikethrough className="h-4 w-4" /></TB>
        {SEP}

        <label className="inline-flex h-8 w-8 cursor-pointer items-center justify-center rounded-lg border border-transparent text-gray-600 hover:border-gray-200 hover:bg-gray-50" title="Màu chữ">
          <Baseline className="h-4 w-4" />
          <input
            type="color"
            aria-label="Màu chữ"
            className="sr-only"
            disabled={disabled}
            onChange={(e) => setFormat('color', e.target.value)}
          />
        </label>
        <label className="inline-flex h-8 w-8 cursor-pointer items-center justify-center rounded-lg border border-transparent text-gray-600 hover:border-gray-200 hover:bg-gray-50" title="Màu nền">
          <PaintBucket className="h-4 w-4" />
          <input
            type="color"
            aria-label="Màu nền"
            className="sr-only"
            disabled={disabled}
            onChange={(e) => setFormat('background', e.target.value)}
          />
        </label>
        {SEP}

        <TB onClick={() => setFormat('align', false)} title="Căn trái" active={!active.align} disabled={disabled}><AlignLeft className="h-4 w-4" /></TB>
        <TB onClick={() => setFormat('align', 'center')} title="Căn giữa" active={active.align === 'center'} disabled={disabled}><AlignCenter className="h-4 w-4" /></TB>
        <TB onClick={() => setFormat('align', 'right')} title="Căn phải" active={active.align === 'right'} disabled={disabled}><AlignRight className="h-4 w-4" /></TB>
        {SEP}

        <TB onClick={() => setFormat('list', active.list === 'ordered' ? false : 'ordered')} title="Danh sách đánh số" active={active.list === 'ordered'} disabled={disabled}><ListOrdered className="h-4 w-4" /></TB>
        <TB onClick={() => setFormat('list', active.list === 'bullet' ? false : 'bullet')} title="Danh sách gạch đầu dòng" active={active.list === 'bullet'} disabled={disabled}><List className="h-4 w-4" /></TB>
        <TB onClick={() => stepIndent(-1)} title="Giảm thụt lề" disabled={disabled}><IndentDecrease className="h-4 w-4" /></TB>
        <TB onClick={() => stepIndent(1)} title="Tăng thụt lề" disabled={disabled}><IndentIncrease className="h-4 w-4" /></TB>
        {SEP}

        {caps.allowLinks && (
          <TB onClick={insertLink} title="Chèn liên kết" active={!!active.link} disabled={disabled}><Link2 className="h-4 w-4" /></TB>
        )}
        {caps.allowImages && onUploadImage && (
          <TB onClick={insertImage} title="Chèn ảnh" disabled={disabled}><ImageIcon className="h-4 w-4" /></TB>
        )}
        <TB onClick={insertTable} title="Chèn bảng" disabled={disabled}><TableIcon className="h-4 w-4" /></TB>
        <TB
          onClick={editSelectedTable}
          title="Chỉnh sửa bảng"
          disabled={disabled || !selectedTable}
        >
          <TableProperties className="h-4 w-4" />
        </TB>
        <TB onClick={insertDivider} title="Chèn đường kẻ ngang" disabled={disabled}><Minus className="h-4 w-4" /></TB>
        <TB onClick={clearFormatting} title="Xóa định dạng" disabled={disabled}><Eraser className="h-4 w-4" /></TB>

        {caps.allowSystemBlockInsert && (
          <TB onClick={insertActionBlock} title="Chèn khối nút phản hồi" disabled={disabled}>
            <MousePointerClick className="h-4 w-4" />
          </TB>
        )}

        {caps.allowVariables && variables && variables.length > 0 && (
          <div className="relative">
            <TB onClick={() => setShowVariables((s) => !s)} title="Chèn biến" active={showVariables} disabled={disabled}>
              <Braces className="h-4 w-4" />
            </TB>
            {showVariables && (
              <div className="absolute right-0 z-20 mt-1 max-h-64 w-64 overflow-y-auto rounded-xl border border-gray-200 bg-white p-1 shadow-xl">
                {variables.map((v) => (
                  <button
                    key={v.name}
                    type="button"
                    onClick={() => insertVariable(v)}
                    className="block w-full rounded-lg px-2.5 py-1.5 text-left text-xs text-gray-700 hover:bg-gray-50"
                  >
                    <span className="font-semibold">{v.label}</span>
                    <span className="ml-1 text-gray-400">{`{{${v.name}}}`}</span>
                  </button>
                ))}
              </div>
            )}
          </div>
        )}

        <div className="ml-auto">
          <TB onClick={() => setFullscreen((f) => !f)} title={fullscreen ? 'Thoát toàn màn hình' : 'Toàn màn hình'} active={fullscreen}>
            {fullscreen ? <Minimize2 className="h-4 w-4" /> : <Maximize2 className="h-4 w-4" />}
          </TB>
        </div>
      </div>

      <div className={fullscreen ? 'min-h-0 flex-1 overflow-y-auto' : ''}>
        <style>{`
          .pems-email-editor .ql-editor { min-height: ${fullscreen ? 60 : minHeight}px; font-size: 14px; }
          .pems-email-editor .ql-container { border: none; font-family: Arial, sans-serif; }
          .pems-email-editor .ql-editor img { max-width: 560px; height: auto; display: block; margin: 16px auto; }
          .pems-email-editor .ql-editor hr { border: none; border-top: 1px solid #e2e8f0; margin: 20px 0; }
          /* A variable reads as an object, not as text somebody is invited to correct. */
          .pems-email-editor .ql-editor .${VARIABLE_CHIP_CLASS} {
            display: inline-block;
            padding: 1px 8px;
            margin: 0 1px;
            border-radius: 999px;
            background: #e0edff;
            color: #0f3d67;
            font-size: 12px;
            font-weight: 600;
            white-space: nowrap;
            user-select: none;
          }
          /* The table is atomic here: what was loaded is what is saved, so it must not look editable. */
          .pems-email-editor .ql-editor .${TABLE_WRAPPER_CLASS} {
            margin: 16px 0;
            padding: 4px;
            border: 1px dashed #cbd5e1;
            border-radius: 8px;
            user-select: none;
          }
          .pems-email-editor .ql-editor .${TABLE_WRAPPER_CLASS} table { width: 100%; }
          .pems-email-editor .ql-editor .pems-system-action-block {
            margin: 16px 0; padding: 12px 14px;
            border: 1px dashed #f59e0b; border-radius: 10px; background: #fffbeb;
            color: #92400e; font-size: 12px; font-weight: 600; text-align: center;
            cursor: move; user-select: none;
          }
        `}</style>
        <div className="pems-email-editor">
          <ReactQuill
            ref={quillRef}
            theme="snow"
            readOnly={disabled}
            value={editorHtml}
            onChange={handleChange}
            onChangeSelection={(range: { index: number; length: number } | null) => {
              // Ignore null: that is the blur a toolbar click causes, and storing it would erase the
              // position the insert is about to need, one event before it needs it.
              if (range) lastRange.current = { index: range.index, length: range.length };
              setActive(editor()?.getFormat() ?? {});
            }}
            placeholder={placeholder ?? 'Soạn nội dung email...'}
            modules={modules}
            formats={EMAIL_EDITOR_FORMATS}
          />
        </div>
      </div>

      {tableEdit && (
        <EmailTableDialog
          initial={tableEdit.model}
          variables={caps.allowVariables ? variables : undefined}
          onCancel={() => setTableEdit(null)}
          onApply={applyTable}
        />
      )}
    </div>
  );
});

EmailRichTextEditor.displayName = 'EmailRichTextEditor';
