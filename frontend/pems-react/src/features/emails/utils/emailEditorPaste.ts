/**
 * Paste cleanup (V4 §14.8) and the whitespace rule (V4 §7.1, §7.4).
 *
 * <b>What Quill already does.</b> Measured on 2.0.3, pasting Word/Gmail markup through its clipboard
 * already drops `position:absolute`, `MsoNormal`, `<o:p>` and unsupported font declarations. So this is
 * not a re-implementation — it handles the cases Quill keeps and email cannot.
 *
 * <b>The backend remains the authority.</b> Everything here runs again, server-side, in
 * `HtmlSanitizerService` before anything is hashed, previewed or sent. This layer exists so the sender
 * sees the cleaned result while they are still editing, rather than approving one document and delivering
 * another — not because the browser is trusted.
 */

/** V4 §14.4 — declarations that position content out of the flow, which email cannot render anyway. */
const FORBIDDEN_CSS = /(?:^|;)\s*(?:position|z-index|transform|animation|transition|filter|clip-path|behavior|expression)\s*:[^;]*/gi;

/** V4 §14.7 — declarations whose only purpose is to make content unreadable. */
const HIDING_CSS = new RegExp(
  '(?:^|;)\\s*(?:'
  + 'display\\s*:\\s*none'
  + '|visibility\\s*:\\s*(?:hidden|collapse)'
  + '|opacity\\s*:\\s*0*(?:\\.0+)?(?=\\s*(?:;|$))'
  + '|font-size\\s*:\\s*0(?:\\.0+)?(?:px|pt|em|rem|%)?(?=\\s*(?:;|$))'
  + '|text-indent\\s*:\\s*-\\d'
  + ')[^;]*',
  'gi',
);

/** Elements that must never survive a paste, with their subtree. */
const FORBIDDEN_TAGS = new Set([
  'SCRIPT', 'IFRAME', 'OBJECT', 'EMBED', 'FORM', 'INPUT', 'BUTTON', 'TEXTAREA', 'SELECT',
  'STYLE', 'LINK', 'META', 'BASE', 'SVG', 'CANVAS', 'VIDEO', 'AUDIO', 'NOSCRIPT', 'APPLET',
]);

/** V4 §14.5 — the only schemes a link may use. */
const SAFE_SCHEME = /^(?:https?:|mailto:|tel:|cid:|#|\/)/i;

/**
 * Strips the declarations email cannot carry from one inline style, leaving the rest intact.
 * Returns null when nothing usable remains, so the caller can drop the attribute instead of writing
 * `style=""`.
 */
export function cleanInlineStyle(style: string | null): string | null {
  if (!style) return null;

  const cleaned = style
    .replace(FORBIDDEN_CSS, '')
    .replace(HIDING_CSS, '')
    .replace(/;{2,}/g, ';')
    .trim()
    .replace(/^;+|;+$/g, '')
    .trim();

  return cleaned.length > 0 ? cleaned : null;
}

/**
 * Cleans a pasted fragment in place.
 *
 * Deliberately preserves the things §14.8 says to keep — basic formatting, simple tables, valid links —
 * and removes only what cannot be rendered or cannot be trusted. A paste that lost the sender's bold and
 * their bullet list would be obeyed by nobody: they would paste it again into a plain field and send that.
 */
export function sanitizePastedFragment(root: Element): void {
  for (const el of Array.from(root.querySelectorAll('*'))) {
    if (FORBIDDEN_TAGS.has(el.tagName)) {
      el.remove();
      continue;
    }

    for (const attr of Array.from(el.attributes)) {
      const name = attr.name.toLowerCase();

      // Inline event handlers, in any spelling.
      if (name.startsWith('on')) {
        el.removeAttribute(attr.name);
        continue;
      }

      if (name === 'style') {
        const cleaned = cleanInlineStyle(attr.value);
        if (cleaned) el.setAttribute('style', cleaned);
        else el.removeAttribute('style');
        continue;
      }

      if (name === 'href' || name === 'src') {
        const value = attr.value.replace(/\s+/g, '');
        // A `data:` image is the one data URL worth keeping — it is how a pasted screenshot arrives.
        const isInlineImage = name === 'src' && /^data:image\//i.test(value);
        if (!isInlineImage && !SAFE_SCHEME.test(value)) el.removeAttribute(attr.name);
        continue;
      }

      // Word and Google Docs carry their own bookkeeping; none of it means anything in mail.
      if (name === 'class' || name === 'id' || name.startsWith('data-') || name.startsWith('aria-')) {
        // The system-block marker is the one data- attribute that carries meaning here.
        if (name !== 'data-system-block') el.removeAttribute(attr.name);
      }
    }
  }
}

/**
 * V4 §7.1 / §7.4 — runs of spaces are not a layout tool.
 *
 * HTML collapses consecutive spaces, so text a sender lined up by holding the space bar arrives
 * un-aligned. The rule is to normalise rather than convert to `&nbsp;`: a run of non-breaking spaces
 * would "work" in the composer and then refuse to wrap on a phone, turning a tidy column into a line
 * the reader has to scroll sideways to finish.
 *
 * Returns the text unchanged when there is nothing to collapse, so callers can compare by identity.
 */
export function normalizeSpaceRuns(text: string): string {
  return text.replace(/ {2,}/g, ' ');
}

/** True when the text contains a run of spaces somebody is probably using to line something up. */
export function hasSpaceRun(text: string): boolean {
  return / {3,}/.test(text);
}

/** The warning shown when it does. Wording from V4 §7.4. */
export const SPACE_RUN_WARNING =
  'Dấu cách liên tiếp không được dùng để căn chỉnh trong email. '
  + 'Vui lòng dùng căn lề, thụt lề hoặc bảng.';
