/**
 * Semantic comparison of two email bodies (V4 §15.2, §15.3).
 *
 * <b>The problem.</b> "Has the operator changed anything?" is asked by every save button on the template
 * screen, and comparing strings answers it wrongly in both directions. A rich-text editor rewrites the
 * document it is handed — `<br>` becomes `<br />`, `style="color:#fff;"` loses its trailing semicolon,
 * attribute order changes, an empty trailing paragraph appears — so a body that was merely LOADED already
 * differs textually from the one in the database. The screen then reports unsaved changes to somebody who
 * has typed nothing, and "Lưu thay đổi" lights up on open.
 *
 * <b>What this does.</b> Reduces both sides to a canonical form and compares that: same words, same
 * structure, same surviving styles → no change, whatever the spelling. It is deliberately NOT a
 * sanitiser and must never be used as one — it exists to answer a question about equality, and the
 * backend remains the only authority on what an email may contain.
 *
 * <b>What it does not collapse.</b> Anything a reader would notice. Text, order, links, images, list
 * structure and the surviving style declarations are all significant; only the notation is normalised.
 */

/**
 * Declarations Quill and the CSS parser reorder or re-spell without changing what is rendered.
 *
 * Exported so callout-preset classification (`emailEditorCalloutPresets.ts`) can compare an observed
 * container style against a canonical preset using the same normalisation this file already uses for
 * dirty-state comparison — never a second, independently-drifting CSS normalizer.
 */
export function canonicalStyle(style: string): string {
  return style
    .split(';')
    .map((d) => d.trim())
    .filter((d) => d.length > 0)
    .map((d) => {
      const at = d.indexOf(':');
      if (at < 0) return d.toLowerCase();
      const property = d.slice(0, at).trim().toLowerCase();
      const declared = d.slice(at + 1).trim().toLowerCase()
        // `rgb(55, 65, 81)` and `rgb(55,65,81)` are the same colour.
        .replace(/\s*,\s*/g, ',')
        // `0.5em` / `.5em`, and `10px` / `10PX`.
        .replace(/\b0+(\.\d)/g, '$1');
      return `${property}:${declared}`;
    })
    // Order of declarations never changes rendering, and editors reorder freely.
    .sort()
    .join(';');
}

/**
 * Attributes that carry no meaning for equality.
 *
 * `target` and `rel` are here because they are not the author's: Quill's link format writes them, and
 * `HtmlSanitizerService` forces `target="_blank" rel="noopener noreferrer"` onto EVERY `<a href>` on the
 * way out. Being applied unconditionally by two different layers, they can never be the difference
 * between two authored bodies — but they are the difference between a body that has been through the
 * editor and the same body straight from the database, which is exactly the false positive this file
 * exists to stop.
 */
const IGNORED_ATTRIBUTES = new Set([
  'class', 'spellcheck', 'contenteditable', 'data-gramm', 'target', 'rel',
]);

/**
 * Removes the editor's own UI furniture and normalises list markup.
 *
 * Quill renders BOTH kinds of list as `<ol>`, telling them apart with `data-list="bullet"` /
 * `data-list="ordered"`, and injects a `<span class="ql-ui">` into every item to draw the marker. So a
 * `<ul>` loaded from the database comes back as an `<ol>` full of spans — textually a large change, and
 * semantically none at all.
 *
 * The bullet/ordered distinction is preserved, because that one IS visible to a reader.
 */
function normalizeEditorArtifacts(body: HTMLElement): void {
  // The marker spans are pure UI. Removed while their class is still present, i.e. before attributes
  // are stripped — afterwards they are indistinguishable from an author's empty <span>.
  for (const ui of Array.from(body.querySelectorAll('span.ql-ui'))) ui.remove();

  for (const list of Array.from(body.querySelectorAll('ul, ol'))) {
    const bulleted = list.tagName === 'UL';

    for (const item of Array.from(list.children)) {
      if (item.tagName !== 'LI') continue;
      if (!item.hasAttribute('data-list')) {
        item.setAttribute('data-list', bulleted ? 'bullet' : 'ordered');
      }
    }

    if (!bulleted) continue;

    // Re-tag <ul> as <ol> so both spellings of the same list compare equal.
    const replacement = list.ownerDocument.createElement('ol');
    for (const a of Array.from(list.attributes)) replacement.setAttribute(a.name, a.value);
    while (list.firstChild) replacement.appendChild(list.firstChild);
    list.replaceWith(replacement);
  }
}

function canonicalizeElement(el: Element): void {
  const attrs = Array.from(el.attributes)
    .filter((a) => !IGNORED_ATTRIBUTES.has(a.name.toLowerCase()));

  for (const a of Array.from(el.attributes)) el.removeAttribute(a.name);

  // Re-add in a stable order, so attribute order cannot make two identical elements compare unequal.
  for (const a of attrs.sort((x, y) => x.name.localeCompare(y.name))) {
    if (a.name.toLowerCase() === 'style') {
      const style = canonicalStyle(a.value);
      if (style) el.setAttribute('style', style);
      continue;
    }
    el.setAttribute(a.name, a.value.trim());
  }
}

/** True for a block that holds nothing a reader would see. Editors add and drop these freely. */
function isEmptyBlock(el: Element): boolean {
  if (!/^(P|DIV)$/.test(el.tagName)) return false;
  // A block carrying an embed (the action node, an image) is never empty, whatever its text says.
  if (el.querySelector('img, hr, [data-system-block]')) return false;
  if (el.hasAttribute('data-system-block')) return false;

  const text = (el.textContent ?? '')
    .replace(/ /g, ' ')
    .replace(/​/g, '')
    .trim();

  return text.length === 0 && el.querySelectorAll('*:not(br)').length === 0;
}

/**
 * Reduces a body to the form used for comparison.
 *
 * Exported so a test can show WHAT two bodies were reduced to when they unexpectedly differ — a boolean
 * alone makes a failing dirty-state test almost impossible to diagnose.
 */
export function canonicalizeEmailHtml(html: string | null | undefined): string {
  if (!html) return '';

  if (typeof window === 'undefined' || typeof window.DOMParser === 'undefined') {
    // No DOM (SSR, a worker): fall back to whitespace normalisation, which is weaker but never wrong in
    // the direction that matters — it may report a change that is not one, never hide one that is.
    return html.replace(/\s+/g, ' ').trim();
  }

  const doc = new window.DOMParser().parseFromString(html, 'text/html');
  const body = doc.body;

  // Before attributes are stripped: both steps below need the classes that are about to be removed.
  normalizeEditorArtifacts(body);

  for (const el of Array.from(body.querySelectorAll('*'))) canonicalizeElement(el);

  // Trailing empty blocks: Quill keeps one at the end of every document, the database usually does not.
  let last = body.lastElementChild;
  while (last && isEmptyBlock(last)) {
    last.remove();
    last = body.lastElementChild;
  }

  return body.innerHTML
    // <br>, <br/>, <br /> are one thing.
    .replace(/<br\s*\/?>/gi, '<br>')
    // Non-breaking spaces are what Quill writes for an ordinary space; a reader cannot tell them apart.
    .replace(/&nbsp;| /g, ' ')
    // Zero-width characters an IME or the editor may insert mid-word.
    .replace(/[​‌‍﻿­]/g, '')
    // Whitespace BETWEEN tags is never rendered.
    .replace(/>\s+</g, '><')
    .replace(/\s{2,}/g, ' ')
    .trim();
}

/**
 * True when two bodies say the same thing.
 *
 * This is the question a save button should ask. `a !== b` is the question it used to ask, and the answer
 * was wrong every time a document had merely passed through the editor.
 */
export function isSameEmailHtml(a: string | null | undefined, b: string | null | undefined): boolean {
  return canonicalizeEmailHtml(a) === canonicalizeEmailHtml(b);
}

/** True when the body has genuinely been edited away from its original. The inverse, named for readers. */
export function isEmailHtmlDirty(original: string | null | undefined, current: string | null | undefined): boolean {
  return !isSameEmailHtml(original, current);
}
