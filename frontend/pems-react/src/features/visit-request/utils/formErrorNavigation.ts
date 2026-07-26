/**
 * Turning "the form is invalid" into "here is the field" (plan §19).
 *
 * A v2 request is a long form inside a scrolling modal, with each campus in its own collapsible
 * card. Telling the user to "fill in all required fields" and leaving them to scroll for the red
 * box is the difference between a five-second fix and a minute of hunting — and on a collapsed
 * card the box is not even on screen to be found.
 */

/** Counts LEAF errors in a react-hook-form error tree (each `{ message }` node is one field). */
export function countFieldErrors(node: unknown): number {
  if (!node || typeof node !== 'object') return 0;
  const record = node as Record<string, unknown>;
  if ('message' in record && typeof record.message === 'string') return 1;
  return Object.values(record).reduce<number>((acc, v) => acc + countFieldErrors(v), 0);
}

const FOCUSABLE = [
  'input:not([type="hidden"]):not([disabled])',
  'textarea:not([disabled])',
  'select:not([disabled])',
].join(', ');

/**
 * Focuses the first control inside the first field marked invalid, and brings it into view.
 * Returns the element it landed on, or null when every candidate is inside a collapsed card.
 *
 * "Collapsed" is detected through `.hidden` rather than layout, because the campus cards hide their
 * body with exactly that class — and layout-based visibility checks (`offsetParent`) report nothing
 * useful in jsdom, so a test could not prove this works.
 */
export function focusFirstInvalidField(root: ParentNode = document): HTMLElement | null {
  const containers = Array.from(root.querySelectorAll<HTMLElement>('[data-field-error="true"]'));
  for (const container of containers) {
    if (container.closest('.hidden, [hidden]')) continue;
    const control = container.querySelector<HTMLElement>(FOCUSABLE);
    if (!control || control.closest('.hidden, [hidden]')) continue;
    control.focus();
    if (typeof control.scrollIntoView === 'function') {
      control.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
    return control;
  }
  return null;
}
