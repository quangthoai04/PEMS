/**
 * Dependency-free HTML sanitizer for rich-text we render via
 * `dangerouslySetInnerHTML` (e.g. News article body).
 *
 * Why hand-rolled: the project currently has no `dompurify` dependency and the
 * build must stay offline-safe. This uses the browser DOMParser + a strict
 * allow-list, which neutralises the XSS vectors that matter here:
 *   - <script>, <iframe>, <object>, <embed>, inline event handlers (onerror/onload)
 *   - javascript:/vbscript:/data:text/html URLs in href/src
 *
 * TODO (recommended upgrade): once `dompurify` can be installed, replace the
 * internals with `DOMPurify.sanitize(...)` - keep this function signature.
 */

// Elements that are always stripped (with their subtree).
const FORBIDDEN_TAGS = new Set([
  'SCRIPT', 'IFRAME', 'OBJECT', 'EMBED', 'LINK', 'META', 'BASE', 'FORM',
  'INPUT', 'BUTTON', 'TEXTAREA', 'SELECT', 'STYLE', 'SVG', 'MATH',
  'TEMPLATE', 'NOSCRIPT', 'FRAME', 'FRAMESET', 'APPLET',
]);

// Attribute values (for href/src) whose scheme is unsafe.
const UNSAFE_URL = /^(javascript|vbscript|data:text\/html|data:application)/i;
const URL_ATTRS = ['href', 'src', 'xlink:href', 'srcset', 'action', 'formaction'];

// Whitespace/control chars stripped before the scheme test (defeats "java\tscript:").
const WHITESPACE = /\s+/g;

// Invisible Unicode characters that Quill or Vietnamese IMEs (e.g. Unikey) can
// silently insert mid-word. Browsers treat these as line-break opportunities,
// causing Vietnamese syllables to split across lines even with word-break: normal.
//   U+200B  ZERO WIDTH SPACE         — explicit break opportunity
//   U+200C  ZERO WIDTH NON-JOINER    — may create break opportunity
//   U+200D  ZERO WIDTH JOINER        — safe to strip from plain text
//   U+FEFF  ZERO WIDTH NO-BREAK SPACE / BOM — stray BOM from Quill internals
//   U+00AD  SOFT HYPHEN              — causes hyphen-break even with hyphens:none
const INVISIBLE_CHARS = /[​‌‍﻿­]/g;

function cleanElement(el: Element): void {
  // Remove inline event handlers and unsafe URL attributes.
  for (const attr of Array.from(el.attributes)) {
    const name = attr.name.toLowerCase();
    if (name.startsWith('on')) {
      el.removeAttribute(attr.name);
      continue;
    }
    if (URL_ATTRS.includes(name)) {
      const value = attr.value.replace(WHITESPACE, '');
      if (UNSAFE_URL.test(value)) {
        el.removeAttribute(attr.name);
      }
    }
  }

  // Recurse, removing forbidden descendants.
  for (const child of Array.from(el.children)) {
    if (FORBIDDEN_TAGS.has(child.tagName)) {
      child.remove();
    } else {
      cleanElement(child);
    }
  }
}

/**
 * Returns a sanitized HTML string safe to pass to `dangerouslySetInnerHTML`.
 * When DOMParser is unavailable (non-browser/SSR) it falls back to a textual
 * strip of the most dangerous constructs.
 */
export function sanitizeHtml(input: string | null | undefined): string {
  if (!input) return '';

  // Quill replaces every regular space with &nbsp; (U+00A0) in its HTML output.
  // &nbsp; is a non-breaking space — browsers cannot wrap lines at it regardless of CSS.
  // This turns each paragraph into one unbreakable block, forcing overflow-wrap to cut
  // mid-syllable. Convert back to regular spaces before any further processing.
  // Also strip invisible break characters (ZWSP etc.) that IMEs may insert mid-syllable.
  const cleaned = input
    .replace(/&nbsp;/gi, ' ')
    .replace(/ /g, ' ')
    .replace(INVISIBLE_CHARS, '');

  if (typeof window === 'undefined' || typeof window.DOMParser === 'undefined') {
    return cleaned
      .replace(/<\s*(script|iframe|object|embed|style)[\s\S]*?<\s*\/\s*\1\s*>/gi, '')
      .replace(/\son\w+\s*=\s*("[^"]*"|'[^']*'|[^\s>]+)/gi, '');
  }

  const doc = new window.DOMParser().parseFromString(cleaned, 'text/html');

  for (const node of Array.from(doc.body.querySelectorAll('*'))) {
    if (FORBIDDEN_TAGS.has(node.tagName)) {
      node.remove();
    }
  }
  cleanElement(doc.body);

  return doc.body.innerHTML;
}
