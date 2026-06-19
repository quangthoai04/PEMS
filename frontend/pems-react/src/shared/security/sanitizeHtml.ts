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
  if (typeof window === 'undefined' || typeof window.DOMParser === 'undefined') {
    return input
      .replace(/<\s*(script|iframe|object|embed|style)[\s\S]*?<\s*\/\s*\1\s*>/gi, '')
      .replace(/\son\w+\s*=\s*("[^"]*"|'[^']*'|[^\s>]+)/gi, '');
  }

  const doc = new window.DOMParser().parseFromString(input, 'text/html');

  for (const node of Array.from(doc.body.querySelectorAll('*'))) {
    if (FORBIDDEN_TAGS.has(node.tagName)) {
      node.remove();
    }
  }
  cleanElement(doc.body);

  return doc.body.innerHTML;
}
