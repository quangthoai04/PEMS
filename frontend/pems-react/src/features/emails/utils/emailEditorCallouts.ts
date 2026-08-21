/**
 * Styled callout/panel containers survive the shared rich-text editor (Phase A of the email fidelity
 * plan).
 *
 * <b>The defect.</b> Canonical templates carry structural, styled `<div>` containers — a blue "Cần bạn
 * xác nhận" box, an orange "Lưu ý bảo mật" box, a "Thông tin người gửi" box — each with its own
 * `border`/`padding`/`margin`/`background`/`border-radius`/`color`/`line-height`. `emailEditorFormats.ts`
 * registers `color`/`background` only as INLINE style attributors; there is no format for a BLOCK-level
 * styled container. Quill's clipboard `convert()` therefore drops every one of those declarations on
 * load — measured, not assumed (see the throwaway `emailEditorCallouts.spike.test.ts` this file replaced):
 * a bare `<div style="border:...">` is not `instanceof EmbedBlot`, so Quill's own `matchBlot` in
 * `quill/modules/clipboard.js` contributes nothing for it, and the div's children are simply flattened to
 * the parent level, discarding the wrapper.
 *
 * <b>Why atomic, not a live Container.</b> A genuine Parchment `Container` blot (the shape `list`/
 * `ListContainer` use) was tried first and rejected: making it durable across Quill's Delta model requires
 * a STATIC `formats()` method applying the style as a per-line attribute, a `requiredContainer` auto-wrap
 * on the child blot, and a `checkMerge()` override so two adjacent, differently-styled callouts do not
 * fuse into one div — three interacting, non-trivial Quill-internals mechanisms with real edge-case risk.
 * `emailEditorTable.ts` already tried the analogous "native Quill multi-child format" route for tables and
 * explicitly rejected it in shipped code for measured fidelity-loss reasons, landing on an atomic embed
 * instead — this file follows that same, already-proven precedent.
 *
 * <b>What "atomic" buys, and what it does not cost.</b> One block embed holding the callout's original
 * inner markup VERBATIM — Quill never re-parses or re-formats it, so `<strong>`, `<br>`, a nested
 * `{{actionBlock}}`-turned-node, and a nested variable chip all survive byte-for-byte whenever the operator
 * does not touch the callout at all (the common case, and the plan's core acceptance rule: editing one
 * character elsewhere must not disturb an untouched callout). Recognizing `{{actionBlock}}`/variables
 * NESTED inside a callout is not a property this blot needs to solve on its own: `templateBlocksToNodes` /
 * `variablesToChips` / `nodesToTemplateBlocks` / `chipsToVariables` are plain STRING-level regex passes
 * over the whole document, oblivious to which Quill blot owns a given substring — so running them
 * alongside `calloutsToNodes`/`nodesToCallouts` at the existing load/save boundary (order does not matter,
 * since callout wrapping only touches the outer `<div>` tag, never its inner content) finds and converts
 * nested markers exactly as if they were not nested at all.
 *
 * <b>Why DOM parsing, not regex, for BOTH directions — unlike the table.</b> `emailEditorTable.ts`'s
 * `nodesToTables` (unwrap) uses a non-greedy regex safely because a table CELL never structurally contains
 * another `<div>`. A callout is the opposite case BY DESIGN: it routinely wraps a nested
 * `{{actionBlock}}`-turned-`<div data-template-block>` or a resolved `<div data-system-block="action">` —
 * each of which carries its OWN closing `</div>`. A non-greedy `[\s\S]*?...<\/div>` regex would terminate
 * at the FIRST nested `</div>` it finds, not the callout's own — silently truncating or misplacing content
 * whenever the nesting is not the single trivial shape that happens to make the bug invisible. Both
 * directions here use `DOMParser` (matching `tablesToNodes`' own load-side precedent) so nesting depth
 * never matters.
 *
 * <b>The import that matters.</b> `Quill` from `react-quill-new`, never the top-level `quill` (1.3.7,
 * unused) — the two copies exist side by side and registering against the wrong one compiles, runs, and
 * changes nothing the user sees.
 */
import { Quill } from 'react-quill-new';

export const CALLOUT_BLOT_NAME = 'pemsEmailCallout';
export const CALLOUT_WRAPPER_CLASS = 'pems-email-callout';

/** The attribute the editor-only wrapper carries the original `style=""` value under. */
const CALLOUT_STYLE_ATTR = 'data-pems-callout-style';

let registered = false;

/**
 * Registers the callout blot. Reports rather than throws, for the reason every registration in this
 * directory does: it runs at module scope, and several test files mock `react-quill-new` with a stub
 * carrying no `Quill`.
 */
export function registerEmailCalloutBlot(): boolean {
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

  class EmailCallout extends BlockEmbed {
    static blotName = CALLOUT_BLOT_NAME;
    static tagName = 'div';
    static className = CALLOUT_WRAPPER_CLASS;

    static create(value: { style: string; html: string }): HTMLElement {
      const node: HTMLElement = super.create(value);
      node.setAttribute(CALLOUT_STYLE_ATTR, value?.style ?? '');
      // Also mirrored onto the real `style` attribute — cosmetic only, so the operator SEES the callout's
      // actual border/background/padding while editing rather than a plain unstyled box. `create()` is
      // the only place this is ever set from; nothing re-derives `style` from it afterward, and the
      // round trip's source of truth stays the data attribute above.
      node.setAttribute('style', value?.style ?? '');
      // Not editable, still selectable/movable — the same contract every other system node here uses.
      node.setAttribute('contenteditable', 'false');
      node.innerHTML = value?.html ?? '';
      return node;
    }

    /** The callout's own style plus its markup exactly as it stands — the lossless round trip. */
    static value(node: HTMLElement): { style: string; html: string } {
      return { style: node.getAttribute(CALLOUT_STYLE_ATTR) ?? '', html: node.innerHTML };
    }
  }

  q.register(EmailCallout, true);
  /* eslint-enable @typescript-eslint/no-explicit-any */
  registered = true;
  return true;
}

/**
 * Stored content → editor content: every top-level styled `<div style="...">` becomes the atomic node.
 *
 * Only OUTERMOST styled divs are wrapped — one already inside another styled div (genuinely nested
 * callouts do not occur in any shipped template today, but the guard costs nothing) is left as part of the
 * outer one's opaque inner markup rather than double-wrapped.
 */
export function calloutsToNodes(html: string | null | undefined): string {
  if (!html) return '';
  if (typeof window === 'undefined' || !window.DOMParser) return html;

  const doc = new window.DOMParser().parseFromString(html, 'text/html');

  const candidates = Array.from(doc.body.querySelectorAll('div[style]'));
  for (const div of candidates) {
    // Skip a div whose nearest styled-div ancestor is itself a candidate — that one wraps this one.
    const ancestorStyled = div.parentElement?.closest('div[style]');
    if (ancestorStyled && candidates.includes(ancestorStyled as HTMLDivElement)) continue;
    // Already converted on an earlier pass — do not re-wrap.
    if (div.closest(`.${CALLOUT_WRAPPER_CLASS}`)) continue;

    const style = div.getAttribute('style') ?? '';
    const inner = div.innerHTML;

    const wrapper = doc.createElement('div');
    wrapper.className = CALLOUT_WRAPPER_CLASS;
    wrapper.setAttribute(CALLOUT_STYLE_ATTR, style);
    wrapper.setAttribute('contenteditable', 'false');
    wrapper.innerHTML = inner;
    div.replaceWith(wrapper);
  }

  return doc.body.innerHTML;
}

/**
 * Editor content → stored content: the node unwraps back to a plain `<div style="...">`, editor-only
 * attributes removed.
 *
 * DOM-based, not regex — see the file doc-comment for why a callout's nested content (routinely another
 * `<div>`, unlike a table cell) makes a non-greedy regex unsafe here.
 */
export function nodesToCallouts(html: string | null | undefined): string {
  if (!html) return '';
  if (typeof window === 'undefined' || !window.DOMParser) return html;

  const doc = new window.DOMParser().parseFromString(html, 'text/html');

  for (const node of Array.from(doc.body.querySelectorAll(`.${CALLOUT_WRAPPER_CLASS}`))) {
    const style = node.getAttribute(CALLOUT_STYLE_ATTR) ?? '';
    const plain = doc.createElement('div');
    plain.setAttribute('style', style);
    plain.innerHTML = node.innerHTML;
    node.replaceWith(plain);
  }

  return doc.body.innerHTML;
}

/** True when `html` contains at least one callout node — used by tests and the dynamic template audit. */
export function hasCalloutNode(html: string | null | undefined): boolean {
  if (!html || typeof window === 'undefined' || !window.DOMParser) return false;
  return new window.DOMParser().parseFromString(html, 'text/html')
    .body.querySelector(`.${CALLOUT_WRAPPER_CLASS}`) !== null;
}

/** How many top-level styled divs `html` (in STORED, not editor, spelling) would be wrapped as callouts. */
export function countStyledContainers(html: string | null | undefined): number {
  if (!html || typeof window === 'undefined' || !window.DOMParser) return 0;
  const doc = new window.DOMParser().parseFromString(html, 'text/html');
  const candidates = Array.from(doc.body.querySelectorAll('div[style]'));
  return candidates.filter((div) => {
    const ancestorStyled = div.parentElement?.closest('div[style]');
    return !(ancestorStyled && candidates.includes(ancestorStyled as HTMLDivElement));
  }).length;
}
