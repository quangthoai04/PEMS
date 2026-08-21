/**
 * The one canonical↔editor conversion pipeline for email bodies — shared by the main
 * `EmailRichTextEditor` instance AND the nested callout-content mini-editor, so the two can never drift
 * into two different conversion orders (email callout frames plan, correction 3).
 *
 * <b>Why order matters.</b> `{{actionBlock}}` must become a protected template-block node BEFORE the
 * generic `{{name}}`-matching variable-chip conversion ever sees it, or it is indistinguishable from an
 * ordinary data variable and becomes deletable/retypeable like one. Every other ordering constraint here
 * (system-node resolution before variable-chip conversion; table/callout wrapping after chip/table-node
 * conversion, so their own nested `{{...}}` markers have already become chips before the wrapper looks at
 * them) mirrors exactly what `EmailRichTextEditor.tsx` composed inline before this extraction — this file
 * changes WHERE that composition lives, not what it does.
 *
 * <b>Why the nested mini-editor needs no second copy.</b> The mini-editor IS another instance of
 * `EmailRichTextEditor`, so it calls these same two functions internally with its own `isTemplate`/
 * `preserveCallouts`. The only additional call site is converting a callout blot's own inner fragment
 * between the blot's (editor) spelling and the mini-editor's (stored) `value` prop — done by calling these
 * same functions directly on that fragment, never a bespoke pipeline.
 */
import { calloutsToNodes, CALLOUT_WRAPPER_CLASS, nodesToCallouts } from './emailEditorCallouts';
import { fromEditorHtml, toEditorHtml } from './emailEditorSystemNodes';
import { nodesToTables, TABLE_WRAPPER_CLASS, tablesToNodes } from './emailEditorTable';
import {
  nodesToTemplateBlocks, TEMPLATE_BLOCK_ATTRIBUTE, templateBlocksToNodes,
} from './emailEditorTemplateBlocks';
import { chipsToVariables, variablesToChips } from './emailEditorVariableChips';

/**
 * True for a block a caret cannot go inside, and therefore cannot go after when it ends a document.
 *
 * <b>Why a bare `<div style="...">` is checked too, not just `CALLOUT_WRAPPER_CLASS`.</b> This function is
 * only ever consulted from {@link dropTrailingBlank}, which runs on the FINAL stored HTML — i.e. AFTER
 * `nodesToCallouts` has already unwrapped a callout embed back into a plain styled div with no class or
 * marker attribute left on it at all. Checking only the wrapper class here would make the check
 * unreachable for every callout (found and fixed by the email callout frames plan's own tests: a callout
 * sitting at the very end of a document accumulated a stray empty `<p></p>` after every Add Frame/Remove
 * Frame/Change Type/Edit-content round trip, since the trailing filler `caretAfterBlock` adds was never
 * recognised as following a "block object" once the callout's own marker was already gone). A bare styled
 * `<div>` has no other legitimate use at the top level of stored content in this system's conventions —
 * ordinary prose is always `<p>`, never a bare `<div>` — so this fallback is safe and specific.
 */
export function isBlockObject(el: Element | null | undefined): boolean {
  if (!el) return false;
  return el.tagName === 'HR'
    || el.tagName === 'TABLE'
    || (el.tagName === 'DIV' && el.hasAttribute('style'))
    || el.classList.contains(TABLE_WRAPPER_CLASS)
    || el.classList.contains(CALLOUT_WRAPPER_CLASS)
    || el.hasAttribute('data-system-block')
    || el.hasAttribute(TEMPLATE_BLOCK_ATTRIBUTE)
    || !!el.querySelector?.(
      `table, hr, [data-system-block], [${TEMPLATE_BLOCK_ATTRIBUTE}], .${CALLOUT_WRAPPER_CLASS}`,
    );
}

/**
 * True for a block with no text in it at all — `<p></p>` and Quill's `<p><br></p>`, and nothing else.
 *
 * Deliberately NOT "looks empty". A paragraph holding a space or a `&nbsp;` is a line the author put
 * there, and Quill keeps it: its delta ends with a character, so the trailing-newline rule below does
 * not apply to it. Treating it as blank here would delete a spacer line from somebody's template.
 */
export function isBlankBlock(el: Element | null | undefined): boolean {
  if (!el || !/^(P|DIV)$/.test(el.tagName)) return false;
  if (el.classList.contains(CALLOUT_WRAPPER_CLASS)) return false;

  // The canonical system-action node (`<div data-system-block="action"></div>`) carries the marker
  // attribute on ITSELF, not on a child — unlike a variable chip or an image, which are always nested
  // inside a surrounding block. A descendant-only check (`el.querySelector(...)`) therefore missed it: the
  // node has no text content, so it was wrongly treated as an empty spacer paragraph and DELETED whenever
  // it ended up as the last element of a fragment (found via the email callout frames plan's own tests —
  // an action block that ends a callout's own content, with no expiry sentence after it inside the frame,
  // vanished entirely on save). `el.matches` covers the element itself; the descendant selector below
  // still covers a genuinely nested marker (an image, a chip, a table cell holding one).
  const markerSelector = 'img, table, hr, [data-system-block], [data-variable]';
  if (el.matches(markerSelector) || el.querySelector(markerSelector)) return false;

  return (el.textContent ?? '').replace(/[﻿​]/g, '') === '';
}

/**
 * Drops one trailing blank block — the one Quill is going to drop anyway.
 *
 * See `EmailRichTextEditor.tsx`'s original doc comment (unchanged by this extraction) for the full
 * measured rationale: `clipboard.convert` eats exactly one trailing newline on load, so failing to match
 * that here makes react-quill-new believe the document always differs from what Quill is holding and
 * rebuild it on every render.
 */
export function dropTrailingBlank(html: string, onlyAfterObject = false): string {
  if (!html || typeof window === 'undefined' || !window.DOMParser) return html;

  const doc = new window.DOMParser().parseFromString(`<body>${html}</body>`, 'text/html');
  const last = doc.body.lastElementChild;
  if (!isBlankBlock(last)) return html;
  if (onlyAfterObject && !isBlockObject(last!.previousElementSibling)) return html;

  last!.remove();
  return doc.body.innerHTML;
}

export interface StoredToEditorOptions {
  isTemplate: boolean;
  labelOf: (name: string) => string | undefined;
  /** Whether a top-level styled `<div>` becomes an atomic callout embed. `false` inside a callout's own
   * inner mini-editor and inner-fragment conversions, so a nested callout can never be created. */
  preserveCallouts: boolean;
}

/** Canonical (stored) HTML → the shape Quill's registered blots recognise. */
export function storedToEditorHtml(stored: string, opts: StoredToEditorOptions): string {
  const { isTemplate, labelOf, preserveCallouts } = opts;

  const withSystemNode = toEditorHtml(stored);
  const withTemplateBlocks = isTemplate ? templateBlocksToNodes(withSystemNode) : withSystemNode;
  const withChips = variablesToChips(withTemplateBlocks, labelOf);
  const withTables = tablesToNodes(withChips);
  const withCallouts = preserveCallouts ? calloutsToNodes(withTables) : withTables;

  return dropTrailingBlank(withCallouts);
}

export interface EditorToStoredOptions {
  isTemplate: boolean;
  /** Mirrors `StoredToEditorOptions.preserveCallouts` — see there for why this must be `false` for a
   * callout's own inner fragment. */
  preserveCallouts: boolean;
  /** Passed straight through to the final `dropTrailingBlank` call — see its own doc comment. */
  onlyTrailingAfterObject?: boolean;
}

/** Editor HTML → canonical (stored) HTML — the inverse of {@link storedToEditorHtml}. */
export function editorHtmlToStored(html: string, opts: EditorToStoredOptions): string {
  const { isTemplate, preserveCallouts, onlyTrailingAfterObject } = opts;

  const withoutCallouts = preserveCallouts ? nodesToCallouts(html) : html;
  const withoutChips = chipsToVariables(withoutCallouts);
  const canonical = isTemplate ? nodesToTemplateBlocks(withoutChips) : withoutChips;
  const withoutTables = nodesToTables(canonical);

  return dropTrailingBlank(fromEditorHtml(withoutTables), onlyTrailingAfterObject ?? false);
}
