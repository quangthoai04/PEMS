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
import { isSameEmailHtml } from '../utils/emailHtmlCanonicalizer';
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
import {
  TEMPLATE_BLOCK_ATTRIBUTE, TEMPLATE_BLOCK_BLOT_NAME, TEMPLATE_BLOCK_CLASS,
  countTemplateBlocks, nodesToTemplateBlocks, templateBlockLabel, templateBlocksToNodes,
} from '../utils/emailEditorTemplateBlocks';
import { EmailTableDialog } from './EmailTableDialog';

// Before any editor is constructed: Quill drops what it has no blot for, so a late registration means the
// first document opened loses its action block and its dividers.
registerEmailEditorFormats();

/*
 * A note on the ORDER of declarations inside a style attribute, because sorting them looked obvious.
 *
 * There used to be a `sortStyleDeclarations` here, applied to everything on its way to storage, written
 * to stop a ping-pong: Quill re-spells a style attribute when it parses one, that re-spelling became the
 * next value, and the two notations traded places forever. That loop is closed at its source now —
 * `handleChange` ignores anything Quill did not attribute to a person (see its `source` note), so an
 * echo never becomes a value at all.
 *
 * Sorting on top of that was not free, and the price was measured on quill 2.0.3: a sorted spelling
 * never equals what Quill renders, react-quill-new compares those two on EVERY render, and a lasting
 * difference makes it re-run `setContents`. So any element carrying two declarations — every formatted
 * span, in every shipped template — rebuilt the whole document on every render of the screen around it,
 * and Quill's selection went with it. Applying three formats to one selection therefore lost the third:
 * the size landed, the font landed, and by the time the colour was picked the selection had collapsed to
 * a cursor at index 0. The colour was silently dropped, on a heading the operator was watching.
 *
 * What is stored now is Quill's own spelling, unaltered. Equality is answered by
 * `canonicalizeEmailHtml`, which sorts declarations for COMPARISON only — where reordering is free,
 * because nothing is written back.
 */

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
  /**
   * Fired the moment this editor becomes the thing the operator is writing in.
   *
   * <b>The defect it closes.</b> A host screen with a second place to write — the template screen's
   * subject line — has to know which of the two an inserted variable belongs to, and it learns about the
   * subject from that input's own focus and select events. The body had no equivalent: this editor keeps
   * its caret to itself (rightly — one copy of that rule, not two), so the screen went on believing the
   * subject was still the target long after the operator had clicked into the body, and every variable
   * they picked jumped up into the subject line.
   *
   * Deliberately carries no position. Where inside the body a variable lands is this component's business
   * and stays here; all the host is told is WHICH field is live.
   */
  onEditorActivated?: () => void;
  /**
   * The system blocks this TEMPLATE may carry, from its contract — never a list held in the editor.
   *
   * Each becomes a protected object in the document and `{{name}}` in what is stored (§5). Absent or
   * empty hides the button entirely, which is the correct answer for a template whose send path attaches
   * no block: offering one would let an operator save a placeholder the renderer then refuses.
   *
   * Ignored in COMPOSE, where the action area is a position node inside already-rendered content rather
   * than a placeholder — see `emailEditorTemplateBlocks.ts` for why those are two different things.
   */
  systemBlocks?: { name: string; label?: string }[];
  disabled?: boolean;
  minHeight?: number;
  'data-testid'?: string;
}

/** True for a block a caret cannot go inside, and therefore cannot go after when it ends a document. */
function isBlockObject(el: Element | null | undefined): boolean {
  if (!el) return false;
  return el.tagName === 'HR'
    || el.tagName === 'TABLE'
    || el.classList.contains(TABLE_WRAPPER_CLASS)
    || el.hasAttribute('data-system-block')
    || el.hasAttribute(TEMPLATE_BLOCK_ATTRIBUTE)
    || !!el.querySelector?.(`table, hr, [data-system-block], [${TEMPLATE_BLOCK_ATTRIBUTE}]`);
}

/**
 * True for a block with no text in it at all — `<p></p>` and Quill's `<p><br></p>`, and nothing else.
 *
 * Deliberately NOT "looks empty". A paragraph holding a space or a `&nbsp;` is a line the author put
 * there, and Quill keeps it: its delta ends with a character, so the trailing-newline rule below does
 * not apply to it. Treating it as blank here would delete a spacer line from somebody's template.
 */
function isBlankBlock(el: Element | null | undefined): boolean {
  if (!el || !/^(P|DIV)$/.test(el.tagName)) return false;
  if (el.querySelector('img, table, hr, [data-system-block], [data-variable]')) return false;
  return (el.textContent ?? '').replace(/[﻿​]/g, '') === '';
}

/**
 * Drops one trailing blank block — the one Quill is going to drop anyway.
 *
 * <b>The defect this closes, which is a performance one.</b> `clipboard.convert` deletes exactly one
 * trailing newline when it parses a document, so a body stored as `…</table><p></p>` arrives in the
 * editor WITHOUT that paragraph. react-quill-new then compares the value it was given against what the
 * editor actually holds — on every render — and re-runs `setContents` whenever they differ. A difference
 * the parse itself creates can never be reconciled, so that comparison answers "different" forever: the
 * whole document is rebuilt on every keystroke, every DOM node is replaced under whatever was holding
 * one, and the caret goes with it. Measured on quill 2.0.3 with a body ending in a table and an empty
 * paragraph — a shape a person makes by pressing Enter after a table and saving.
 *
 * `onlyAfterObject` is for the OTHER direction. On the way IN this matches Quill's own rule exactly, so
 * what is handed over is what will be held. On the way OUT only the blank line that follows a table, a
 * divider or the action block is removed — the one `caretAfterBlock` adds so the author has somewhere to
 * type — because a blank line an author left at the end of ordinary prose is theirs while they are still
 * editing, and taking it out from under them mid-session is an edit nobody asked for.
 */
function dropTrailingBlank(html: string, onlyAfterObject = false): string {
  if (!html || typeof window === 'undefined' || !window.DOMParser) return html;

  const doc = new window.DOMParser().parseFromString(`<body>${html}</body>`, 'text/html');
  const last = doc.body.lastElementChild;
  if (!isBlankBlock(last)) return html;
  if (onlyAfterObject && !isBlockObject(last!.previousElementSibling)) return html;

  last!.remove();
  return doc.body.innerHTML;
}

/*
 * A note on the line AFTER a table, because the obvious repair is a trap.
 *
 * A table, a divider and the action block are each one indivisible block, so a document that ENDS with
 * one has no position after it — the last line IS the object. `caretAfterBlock` adds a paragraph as part
 * of the insert so the caret has somewhere to land.
 *
 * What is deliberately NOT done is keeping that paragraph there permanently by writing it into the html
 * handed to Quill. The parse eats one trailing newline (see `dropTrailingBlank`), so a single filler
 * paragraph never appears at all; writing TWO does leave one, and buys the rebuild-on-every-render
 * failure described above in exchange. An empty line that survives a reload is not worth that — and an
 * author who writes something under the table keeps their paragraph anyway, because it is then not blank.
 */

/**
 * The table wrapper living at `index`, or null if that position holds something else.
 *
 * Resolved from the DOCUMENT rather than remembered from the insert, because the element Quill renders
 * for an embed is not the one the caller passed it — the value is markup, and the node is built from it.
 */
/* eslint-disable-next-line @typescript-eslint/no-explicit-any */
function tableNodeAt(q: any, index: number): HTMLElement | null {
  const [blot] = q.getLine?.(index) ?? [];
  const node: unknown = blot?.domNode;
  return node instanceof HTMLElement && node.classList.contains(TABLE_WRAPPER_CLASS) ? node : null;
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
  onUploadImage, onNotice, onEditorActivated, systemBlocks,
  disabled, minHeight = 240, 'data-testid': testId,
}: EmailRichTextEditorProps, ref) => {
  const caps = useMemo(
    () => ({ ...capabilitiesFor(mode), ...(capabilities ?? {}) }),
    [mode, capabilities],
  );

  const quillRef = useRef<any>(null);

  /**
   * Kept stable by the CONTENT of the variable list, not by the identity of the array.
   *
   * A host that builds `variables` inline hands over a new array on every render — which is ordinary
   * React — and that would make every derived conversion a new function, which in turn defeats the
   * document-reuse rule in `shown` below and reloads the editor on every render of the screen. The
   * mapping only changes when a name or a label changes, so that is what this depends on.
   */
  const variablesRef = useRef(variables);
  variablesRef.current = variables;

  const labelKey = useMemo(
    () => (variables ?? []).map((v) => `${v.name} ${v.label}`).join(''),
    [variables],
  );

  const labelOf = useCallback(
    (name: string) => variablesRef.current?.find((v) => v.name === name)?.label,
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [labelKey],
  );
  const [fullscreen, setFullscreen] = useState(false);
  const [active, setActive] = useState<Record<string, any>>({});
  const [showVariables, setShowVariables] = useState(false);
  const [showBlocks, setShowBlocks] = useState(false);

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

  /**
   * Applies `fn` to the live editor ON THE SELECTION THE OPERATOR MADE, then refreshes the toolbar.
   *
   * <b>The defect this closes.</b> Applying two formats in a row to one selection worked; the THIRD did
   * nothing. Measured on quill 2.0.3: after a format, the host stores the new html, which re-renders a
   * controlled editor, and Quill's own selection comes back COLLAPSED — `{index: 0, length: 0}`. Every
   * later format then lands on a cursor instead of on the words, so the operator selects a heading, sets
   * the size, sets the font, picks a colour, and the colour is silently dropped.
   *
   * `q.focus()` alone cannot fix it: Quill restores the range IT last saw, which is the collapsed one.
   * The remembered range is the operator's, so it is restored explicitly before the format is applied,
   * `'silent'` because moving a caret back to where somebody put it is not an edit to report.
   */
  const withEditor = useCallback((fn: (q: any) => void) => {
    const q = editor();
    if (!q) return;

    q.focus();

    const wanted = lastRange.current;
    if (wanted) {
      const limit = Math.max(0, q.getLength() - 1);
      const index = Math.min(wanted.index, limit);
      const length = Math.min(wanted.length, Math.max(0, limit - index));
      const live = q.getSelection();
      if (!live || live.index !== index || live.length !== length) {
        q.setSelection(index, length, 'silent');
      }
    }

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

  /**
   * Leaves the caret on a line the author can actually write on after inserting a block object.
   *
   * A table, a divider and the action block are each one indivisible block. Inserted at the END of the
   * document, there is no position after them — the last line IS the object — so an author who put a
   * table at the bottom of a template could not type a closing sentence and reported the editor as
   * stuck. It is not stuck; there is simply nowhere left to put a cursor.
   *
   * The paragraph is added only when there is nothing after the object already, and it is not made
   * permanent — see the note above `tableNodeAt` for the two measurements that rule that out.
   */
  const caretAfterBlock = useCallback((q: any, index: number) => {
    if (index + 1 >= q.getLength()) q.insertText(q.getLength(), '\n', 'user');
    q.setSelection(index + 1, 0);
    lastRange.current = { index: index + 1, length: 0 };
  }, []);

  const insertDivider = useCallback(() => {
    withEditor((q) => {
      const range = q.getSelection(true);
      const index = range ? range.index : q.getLength();
      q.insertEmbed(index, DIVIDER_BLOT_NAME, true, 'user');
      caretAfterBlock(q, index);
    });
  }, [withEditor, caretAfterBlock]);

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
      caretAfterBlock(q, index);
    });
  }, [value, withEditor, onNotice, caretAfterBlock]);

  /**
   * Puts a system block into a TEMPLATE — as `{{name}}`, which is what the renderer substitutes.
   *
   * Not the same operation as `insertActionBlock` above, and deliberately not merged with it: that one
   * writes the COMPOSE position node into content that has already been rendered. Writing that node into
   * a template stores a `<div>` the renderer never looks at, so the buttons the template promises are
   * simply absent from the message — which is what this screen used to do.
   */
  const insertTemplateBlock = useCallback((name: string) => {
    setShowBlocks(false);

    // One of each: the backend builds a block once, and a second placeholder would either duplicate a
    // one-time action or repeat a whole table of setup data under the first.
    if (countTemplateBlocks(value, name) >= 1) {
      onNotice?.(`Mẫu này đã có ${templateBlockLabel(name)}. Mỗi khối chỉ được đặt một lần.`);
      return;
    }

    withEditor((q) => {
      const at = lastRange.current;
      const index = at ? at.index : q.getLength();
      q.insertEmbed(index, TEMPLATE_BLOCK_BLOT_NAME, { name, label: templateBlockLabel(name) }, 'user');
      caretAfterBlock(q, index);
    });
  }, [value, withEditor, onNotice, caretAfterBlock]);

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
  // `el` is the wrapper element the dialog was opened on and `at` the position it was at: the index used
  // to write the replacement is resolved from the ELEMENT at apply time — so an edit elsewhere while the
  // dialog is open cannot land it on the wrong block — and falls back to the position only when the
  // element itself has been replaced by a rebuild.
  const [tableEdit, setTableEdit] = useState<
  { el: HTMLElement | null; at: number | null; original: string; model: EmailTableModel } | null>(null);

  /** The table the caret/click is on, if any — what enables "Chỉnh sửa bảng". */
  const [selectedTable, setSelectedTable] = useState<HTMLElement | null>(null);

  /**
   * WHERE the selected table is, as a document index, kept beside WHICH element it is.
   *
   * A DOM element is not a durable handle here. This editor is controlled: an edit anywhere re-feeds the
   * document through `setContents`, which rebuilds every node — so the element this state was holding is
   * detached a moment after any change, and "Chỉnh sửa bảng" was left pointing at markup no longer in the
   * document. The index survives that rebuild, so the selection can be re-resolved against whatever the
   * editor now holds; the element is still kept because it is what gets painted and what enables the
   * button.
   */
  const selectedTableIndex = useRef<number | null>(null);

  /** Selects a table, remembering both the element and the position it was found at. */
  const selectTable = useCallback((el: HTMLElement | null) => {
    setSelectedTable(el);
    /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
    const blot = el ? (Quill as any)?.find?.(el) : null;
    const q = editor();
    selectedTableIndex.current = blot && q ? q.getIndex(blot) : null;
  }, [editor]);

  /**
   * The selected table AS IT STANDS NOW — the element if it is still in the document, otherwise the one
   * that took its place at the same position.
   *
   * Read at the moment it is needed rather than kept in state on every render. Re-resolving into state
   * would be a `setState` in an effect that has to run after every render (a rebuild does not change
   * `value`), and each rebuild produces a new element, so the two would chase each other.
   */
  const liveSelectedTable = useCallback((): HTMLElement | null => {
    if (selectedTable?.isConnected) return selectedTable;

    const q = editor();
    const at = selectedTableIndex.current;
    return q && at !== null ? tableNodeAt(q, at) : null;
  }, [selectedTable, editor]);

  const openTableDialog = useCallback((el: HTMLElement | null, original: string) => {
    const model = parseEmailTable(original);
    if (!model) {
      // Refused rather than flattened — see `parseEmailTable`. The author keeps a table this dialog
      // cannot express, instead of getting back a rearranged one it can.
      onNotice?.('Không mở được trình chỉnh sửa cho bảng này (bảng lồng nhau hoặc cấu trúc không đọc được).');
      return;
    }
    /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
    const blot = el ? (Quill as any)?.find?.(el) : null;
    const q = editor();
    setTableEdit({ el, at: blot && q ? q.getIndex(blot) : null, original, model });
  }, [onNotice, editor]);

  const insertTable = useCallback(() => {
    // A starting shape rather than a size prompt: rows and columns are added in the dialog, where the
    // author can see what they are adding to.
    openTableDialog(null, buildEmailTable(3, 2));
  }, [openTableDialog]);

  const editSelectedTable = useCallback(() => {
    if (!selectedTable) return;

    // Never the element held in state without checking: a controlled Quill replaces every node when it
    // reloads the document, so by the time this runs the selected element is routinely detached — and
    // editing a detached one resolves an index in a document it is no longer part of, which is how a
    // replacement lands on some other block. The position is what survives.
    const live = liveSelectedTable();
    if (!live) {
      selectTable(null);
      onNotice?.('Bảng đã thay đổi. Vui lòng chọn lại bảng cần chỉnh sửa.');
      return;
    }

    openTableDialog(live, live.innerHTML);
  }, [selectedTable, liveSelectedTable, openTableDialog, onNotice, selectTable]);

  /**
   * Paints the selected table, and only that one.
   *
   * Written as an attribute on the wrapper — which `nodesToTables` strips on the way to stored content,
   * so it cannot reach the database, the preview or a recipient. It is set from an effect rather than at
   * click time so that a table which stops being selected (because another was clicked, or because the
   * node was replaced) is always cleaned up by the same code that marks one.
   */
  useEffect(() => {
    const root: HTMLElement | undefined = editor()?.root;
    if (!root || typeof root.querySelectorAll !== 'function') return;

    const live = liveSelectedTable();
    for (const el of Array.from(root.querySelectorAll(`.${TABLE_WRAPPER_CLASS}[data-selected]`))) {
      if (el !== live) el.removeAttribute('data-selected');
    }
    live?.setAttribute('data-selected', 'true');
  }, [selectedTable, liveSelectedTable, editor, value]);

  const applyTable = useCallback((model: EmailTableModel) => {
    const target = tableEdit;
    setTableEdit(null);
    if (!target) return;

    const html = applyTableEdit(target.original, model);
    const q = editor();
    if (!q) return;

    // What goes INTO the document is editor spelling: a variable inside a cell is a chip here, exactly
    // as it is everywhere else in the body, and `chipsToVariables` turns it back into a placeholder on
    // the way to storage. Inserting the stored spelling instead would leave the braces showing inside
    // the table until the next reload — the one place in the document where a variable looked raw.
    const editorTableHtml = variablesToChips(html, labelOf);

    if (!target.el) {
      const at = lastRange.current;
      const index = at ? at.index : q.getLength();
      q.insertEmbed(index, TABLE_BLOT_NAME, editorTableHtml, 'user');
      // A caret on a line the author can actually type on, and the new table left selected so
      // "Chỉnh sửa bảng" opens the one they have just made rather than nothing at all.
      caretAfterBlock(q, index);
      selectTable(tableNodeAt(q, index));
      return;
    }

    // Nothing actually changed — leave the document alone. Replacing the embed with an identical one
    // would still register as an edit, and the screen would offer to save a table nobody touched.
    if (html === target.original) return;

    // Resolved at APPLY time, not remembered from when the dialog opened, so an edit made elsewhere in
    // the document while the dialog was open cannot land the replacement on the wrong block. The element
    // is only trusted while it is still in the document — see `liveSelectedTable`.
    /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
    const blot = target.el.isConnected ? (Quill as any)?.find?.(target.el) : null;
    const index = blot ? q.getIndex(blot) : target.at;

    if (index === null || !tableNodeAt(q, index)) {
      // The table this dialog was opened on is not there any more (a reload, an undo, someone else's
      // change). Said out loud rather than dropped: the author spent time in that dialog and is entitled
      // to know the edit did not land.
      onNotice?.('Bảng đã thay đổi trong lúc chỉnh sửa. Vui lòng mở lại bảng và áp dụng lần nữa.');
      return;
    }

    q.deleteText(index, 1, 'user');
    q.insertEmbed(index, TABLE_BLOT_NAME, editorTableHtml, 'user');
    caretAfterBlock(q, index);
    // The element that was selected has just been replaced, so the selection follows the REPLACEMENT
    // rather than being dropped: an author who adds a row usually adds a second one straight after, and
    // clearing it made them click the table again between every edit.
    selectTable(tableNodeAt(q, index));
  }, [tableEdit, editor, onNotice, caretAfterBlock, selectTable, labelOf]);

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

    const onClick = (e: Event) => selectTable(wrapperOf(e));
    const onDouble = (e: Event) => {
      const el = wrapperOf(e);
      if (!el || disabled) return;
      selectTable(el);
      openTableDialog(el, el.innerHTML);
    };

    node.addEventListener('click', onClick);
    node.addEventListener('dblclick', onDouble);
    return () => {
      node.removeEventListener('click', onClick);
      node.removeEventListener('dblclick', onDouble);
    };
  }, [editor, openTableDialog, disabled, selectTable]);

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

  /**
   * In TEMPLATE mode a `{{systemBlock}}` is an object, not a variable — and the ORDER below is what makes
   * that true: `variablesToChips` matches any `{{name}}`, so a block reaching it first becomes a data
   * chip labelled "actionBlock" and stops being recognisable as something the backend builds.
   */
  const isTemplate = mode === 'TEMPLATE';

  const toEditor = useCallback(
    (stored: string) => dropTrailingBlank(
      tablesToNodes(variablesToChips(
        isTemplate ? templateBlocksToNodes(toEditorHtml(stored)) : toEditorHtml(stored),
        labelOf,
      )),
    ),
    [labelOf, isTemplate],
  );

  const fromEditor = useCallback(
    (html: string) => {
      const withoutChips = chipsToVariables(html);
      const canonical = isTemplate ? nodesToTemplateBlocks(withoutChips) : withoutChips;
      return dropTrailingBlank(
        fromEditorHtml(nodesToTables(canonical)),
        true,
      );
    },
    [isTemplate],
  );

  /**
   * The document currently on screen, in QUILL's spelling, for the stored value it stands for.
   *
   * <b>Why a controlled Quill needs this.</b> react-quill-new compares the value it is handed against
   * what the editor holds, on EVERY render, and re-runs `setContents` when the two differ. They always
   * differ: Quill writes an ordinary space as `&nbsp;`, wraps a variable chip in guard characters and an
   * inner span, and re-spells inline styles — none of which belongs in stored content, so what is stored
   * is deliberately not what Quill rendered. The comparison therefore answered "different" forever, and
   * the whole document was rebuilt on every render of the screen around it: the caret was discarded
   * mid-edit, live DOM nodes were replaced under whatever held one, and a large body was re-parsed on
   * every keystroke in the SUBJECT field.
   *
   * <b>The rule.</b> When the value coming back is the same document the editor already shows, hand back
   * the spelling the editor itself produced — byte-identical to what it is holding, so nothing is
   * reloaded. When it is genuinely a different document (a restore, a language switch, a fresh template),
   * hand over the converted value and let it load. Sameness is decided in STORED space, where the two
   * spellings are supposed to agree, not in the editor's.
   */
  const shown = useRef<{ value: string; editorHtml: string; convert: typeof toEditor } | null>(null);

  const resolveEditorHtml = (stored: string): string => {
    const cached = shown.current;
    if (cached && cached.value === stored && cached.convert === toEditor) return cached.editorHtml;

    const converted = toEditor(stored);
    shown.current = { value: stored, editorHtml: converted, convert: toEditor };
    return converted;
  };

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
    if (source !== 'user') {
      // An echo still carries information worth keeping: it is Quill's OWN rendering of the value that
      // was just handed over, so it is by definition what "showing that value" looks like. Recorded
      // against that value, it stops the next render offering a different spelling of the same document
      // and reloading everything — see `shown`.
      //
      // Recorded whatever it says, including where Quill's rendering differs from what was given to it:
      // a block-level style becomes a span, a colour becomes `rgb()`, a trailing blank line disappears.
      // Handing the original back again would only produce the same rendering a second time, so the
      // difference is not a reason to reload — it is what loading that document produces.
      const cached = shown.current;
      if (cached) cached.editorHtml = html;
      return;
    }

    const canonical = fromEditor(html);
    // The same recording for a real edit, where it is free: `html` IS what the editor holds, and
    // `canonical` is exactly the value about to come back as a prop.
    shown.current = { value: canonical, editorHtml: html, convert: toEditor };

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
  }, [onChange, fromEditor, toEditor, onNotice]);

  const editorHtml = resolveEditorHtml(value);

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

        {/*
          COMPOSE inserts the position node; TEMPLATE inserts a placeholder the renderer substitutes.
          One button, two operations, because they are two representations of the same idea — see
          `emailEditorTemplateBlocks.ts`.

          In TEMPLATE the list comes from the contract: a template whose send path attaches no block
          offers no button at all, so an operator cannot save a placeholder the renderer would refuse.
        */}
        {caps.allowSystemBlockInsert && !isTemplate && (
          <TB onClick={insertActionBlock} title="Chèn khối nút phản hồi" disabled={disabled}>
            <MousePointerClick className="h-4 w-4" />
          </TB>
        )}

        {caps.allowSystemBlockInsert && isTemplate && (systemBlocks?.length ?? 0) > 0 && (
          <div className="relative">
            <TB
              onClick={() => {
                if (systemBlocks!.length === 1) insertTemplateBlock(systemBlocks![0].name);
                else setShowBlocks((s) => !s);
              }}
              title="Chèn khối hệ thống"
              active={showBlocks}
              disabled={disabled}
            >
              <MousePointerClick className="h-4 w-4" />
            </TB>
            {showBlocks && (
              <div className="absolute right-0 z-20 mt-1 w-64 rounded-xl border border-gray-200 bg-white p-1 shadow-xl">
                {systemBlocks!.map((b) => (
                  <button
                    key={b.name}
                    type="button"
                    onClick={() => insertTemplateBlock(b.name)}
                    className="block w-full rounded-lg px-2.5 py-1.5 text-left text-xs text-gray-700 hover:bg-gray-50"
                  >
                    <span className="font-semibold">{b.label ?? templateBlockLabel(b.name)}</span>
                    <span className="ml-1 text-gray-400">{`{{${b.name}}}`}</span>
                  </button>
                ))}
              </div>
            )}
          </div>
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
          }
          /*
            The LABEL is unselectable; the chip element around it is not.

            Those two guard characters Quill hides at each end of an inline embed are the caret positions
            immediately before and after the object — the only places a caret can go between two adjacent
            variables. A user-select of none on the whole chip takes those positions with it, and the wall
            this fix removes comes straight back.
          */
          .pems-email-editor .ql-editor .${VARIABLE_CHIP_CLASS} > span[contenteditable="false"] {
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
          /*
            Which table "Chỉnh sửa bảng" would open, said on the table rather than only in a disabled
            button. The attribute is editor furniture: it is set on the wrapper, and the wrapper is
            removed on the way to stored content, so it can never be saved, previewed or sent.
          */
          .pems-email-editor .ql-editor .${TABLE_WRAPPER_CLASS}[data-selected="true"] {
            border: 1px solid #004c91;
            box-shadow: 0 0 0 3px rgba(0, 76, 145, 0.12);
          }
          .pems-email-editor .ql-editor .${TABLE_WRAPPER_CLASS} table { width: 100%; }
          /* A TEMPLATE system block: an object the author places, whose contents the backend builds. */
          .pems-email-editor .ql-editor .${TEMPLATE_BLOCK_CLASS} {
            margin: 16px 0; padding: 12px 14px;
            border: 1px dashed #f59e0b; border-radius: 10px; background: #fffbeb;
            color: #92400e; font-size: 12px; font-weight: 600; text-align: center;
            cursor: move; user-select: none;
          }
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
            onChangeSelection={(
              range: { index: number; length: number } | null,
              source?: string,
            ) => {
              // Ignore null: that is the blur a toolbar click causes, and storing it would erase the
              // position the insert is about to need, one event before it needs it.
              //
              // Ignore `'api'` for the same reason, one step further on. A controlled re-render reloads
              // the document and Quill reports a selection of its own — usually collapsed at 0 — which
              // is not where the operator put the caret. Recording that would overwrite the range the
              // next toolbar click is about to restore, and the format would land on a cursor instead
              // of on the words they had selected. Only a person's own selection is remembered here;
              // the places that move the caret deliberately (`insertVariable`, `caretAfterBlock`) set
              // this ref themselves.
              //
              // <b>Returning EARLY on those two, rather than falling through to a toolbar refresh, is
              // the whole of this handler's focus behaviour.</b> `quill.getFormat()` called with no
              // arguments defaults its index to `getSelection(true)` — and that `true` is a FOCUS flag:
              // it runs `quill.focus()`, which calls `root.focus()` and restores the last body range
              // whenever the editor does not already hold focus. Quill reports a null range from a
              // document-level `selectionchange` listener deferred by a timer (core/selection.js), so
              // the event lands about a millisecond AFTER the click that moved focus away — and the
              // refresh then tore focus back out of whatever the operator had just clicked. That is one
              // defect with two faces: a caret placed in the subject line jumped back into the body a
              // frame later, and every cell of the table dialog refused to take a keystroke for the
              // same reason. Neither was a dialog bug or an input bug; both were this line.
              if (!range || source === 'api') return;

              lastRange.current = { index: range.index, length: range.length };
              // A caret inside this editor IS the signal that the body is what the operator is
              // writing in — one signal for both facts, so the two cannot disagree. The `onFocus`
              // below is a second route to the same statement, not the primary one: a click that
              // lands in the document reports a range whether or not focus was elsewhere before.
              onEditorActivated?.();

              // The range is passed EXPLICITLY, never left to default. It is the same range Quill has
              // just reported, so the formats read are identical — but naming it keeps `getSelection`
              // out of the call, and with it the focus grab and the `scrollSelectionIntoView()` that
              // rides along on every caret move.
              setActive(editor()?.getFormat(range.index, range.length) ?? {});
            }}
            onFocus={() => onEditorActivated?.()}
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
