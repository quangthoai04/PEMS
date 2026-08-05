/**
 * Variables as chips (V4 §8.1): a friendly label on screen, `{{variableName}}` on the wire.
 *
 * <b>Why a blot and not text.</b> `{{senderName}}` typed as characters is editable as characters: an
 * operator can delete one brace, correct the spelling to `{{senderNames}}`, or paste half of it. Each of
 * those produces a template that renders wrongly, and the failure surfaces at SEND time in front of a
 * recipient rather than at edit time in front of the person who caused it. An atomic embed can only be
 * inserted or deleted whole — there is no half of it to leave behind.
 *
 * <b>Why the label lives in the DOM.</b> `data-label` rather than a lookup at serialisation time, so the
 * conversion back to `{{name}}` needs no registry and cannot be broken by a variable list that arrives
 * late, or by a template whose contract has since changed. The name is the truth; the label is decoration
 * carried alongside it.
 *
 * <b>The import that matters.</b> `Quill` from `react-quill-new`, never from `quill` — two copies exist
 * and registering against the top-level 1.3.7 does nothing at all to the editor the user sees.
 */
import { Quill } from 'react-quill-new';

export const VARIABLE_BLOT_NAME = 'pemsVariable';
export const VARIABLE_CHIP_CLASS = 'pems-variable-chip';

/** What a chip carries: the placeholder name, and what a person calls it. */
export interface VariableChipValue {
  name: string;
  label: string;
}

/**
 * Fallback matcher for environments with no DOM. Deliberately NOT the primary route.
 *
 * Quill's inline embed wraps its content in zero-width guards and its own inner `<span>`, so a rendered
 * chip is not the flat element this was written from — a non-greedy match closes on Quill's inner tag and
 * leaves `﻿</span>` behind in the saved template. The DOM path below has no such problem because it
 * removes the whole ELEMENT rather than a span of characters.
 */
const CHIP = new RegExp(
  '<span\\b[^>]*\\bdata-variable\\s*=\\s*"([^"]+)"[^>]*>[\\s\\S]*?</span\\s*>',
  'gi',
);

/** Zero-width guards Quill inserts around an inline embed. */
const ZERO_WIDTH = /[﻿​]/g;

/** Matches a `{{name}}` placeholder in stored content. */
const PLACEHOLDER = /\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}/g;

let registered = false;

/**
 * Registers the chip blot. Reports rather than throws — it runs at module scope, and several test files
 * mock `react-quill-new` with a stub carrying no `Quill`; reading a missing named export throws on the
 * BINDING, so the guard wraps the access rather than testing the value.
 */
export function registerVariableChipBlot(): boolean {
  if (registered) return true;

  /* eslint-disable @typescript-eslint/no-explicit-any */
  let q: any;
  try {
    q = Quill as any;
  } catch {
    return false;
  }
  if (!q || typeof q.import !== 'function' || typeof q.register !== 'function') return false;

  const Embed: any = q.import('blots/embed');
  if (!Embed) return false;

  class VariableChip extends Embed {
    static blotName = VARIABLE_BLOT_NAME;
    static tagName = 'span';
    static className = VARIABLE_CHIP_CLASS;

    static create(value: VariableChipValue | string): HTMLElement {
      const node: HTMLElement = super.create(value);
      const name = typeof value === 'string' ? value : value?.name ?? '';
      const label = typeof value === 'string' ? value : value?.label ?? name;

      node.setAttribute('data-variable', name);
      node.setAttribute('data-label', label);
      // Not editable: the point of the chip is that there is no half of it to leave behind.
      node.setAttribute('contenteditable', 'false');
      node.textContent = label;
      return node;
    }

    static value(node: HTMLElement): VariableChipValue {
      const name = node.getAttribute('data-variable') ?? '';
      return { name, label: node.getAttribute('data-label') ?? name };
    }
  }

  q.register(VariableChip, true);
  /* eslint-enable @typescript-eslint/no-explicit-any */
  registered = true;
  return true;
}

/**
 * Stored content → editor content: every `{{name}}` becomes a chip.
 *
 * `labelOf` supplies the human name. A variable the current template does not declare still becomes a
 * chip, labelled with its own name: showing it as raw braces would invite an operator to "fix" a
 * placeholder that is merely unfamiliar, and deleting it would silently drop content.
 */
export function variablesToChips(
  html: string | null | undefined,
  labelOf: (name: string) => string | undefined,
): string {
  if (!html) return '';

  PLACEHOLDER.lastIndex = 0;
  return html.replace(PLACEHOLDER, (whole, name: string) => {
    const label = labelOf(name) ?? name;
    return `<span class="${VARIABLE_CHIP_CLASS}" data-variable="${escapeAttr(name)}"`
      + ` data-label="${escapeAttr(label)}" contenteditable="false">${escapeText(label)}</span>`;
  });
}

/**
 * Editor content → stored content: every chip becomes `{{name}}` again.
 *
 * This is what makes the label safe to show. The label is never stored and never sent; only the name the
 * renderer substitutes survives, so changing what a variable is CALLED cannot change what it renders.
 */
export function chipsToVariables(html: string | null | undefined): string {
  if (!html) return '';

  if (typeof window === 'undefined' || !window.DOMParser) {
    CHIP.lastIndex = 0;
    return html.replace(CHIP, (whole: string, name: string) => `{{${name}}}`).replace(ZERO_WIDTH, '');
  }

  const doc = new window.DOMParser().parseFromString(html, 'text/html');

  // Replacing the ELEMENT takes Quill's guards and inner span with it. A character-span replacement
  // cannot: the chip Quill renders is not the flat element `variablesToChips` wrote.
  for (const chip of Array.from(doc.body.querySelectorAll('[data-variable]'))) {
    const name = chip.getAttribute('data-variable') ?? '';
    chip.replaceWith(doc.createTextNode(`{{${name}}}`));
  }

  return doc.body.innerHTML.replace(ZERO_WIDTH, '');
}

/** Every variable name currently present as a chip, in document order, including repeats. */
export function chipNames(html: string | null | undefined): string[] {
  if (!html) return [];

  if (typeof window === 'undefined' || !window.DOMParser) {
    CHIP.lastIndex = 0;
    return Array.from(html.matchAll(CHIP)).map((m) => m[1]);
  }

  return Array.from(
    new window.DOMParser().parseFromString(html, 'text/html').body.querySelectorAll('[data-variable]'),
  ).map((el) => el.getAttribute('data-variable') ?? '');
}

function escapeAttr(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');
}

function escapeText(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
